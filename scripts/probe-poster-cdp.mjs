const port = Number(process.argv[2] ?? 9222);
const action = process.argv[3] ?? "inspect";

async function targets() {
  const response = await fetch(`http://127.0.0.1:${port}/json/list`);
  if (!response.ok) throw new Error(`CDP target list failed with ${response.status}`);
  return response.json();
}

function connect(url) {
  return new Promise((resolve, reject) => {
    const socket = new WebSocket(url);
    socket.addEventListener("open", () => resolve(socket), { once: true });
    socket.addEventListener("error", () => reject(new Error("CDP WebSocket failed")), {
      once: true
    });
  });
}

function cdpClient(socket) {
  let nextId = 0;
  const pending = new Map();
  socket.addEventListener("message", (event) => {
    const message = JSON.parse(event.data);
    const waiter = pending.get(message.id);
    if (!waiter) return;
    pending.delete(message.id);
    if (message.error) waiter.reject(new Error(message.error.message));
    else waiter.resolve(message.result);
  });
  return (method, params = {}) => new Promise((resolve, reject) => {
    const id = ++nextId;
    pending.set(id, { resolve, reject });
    socket.send(JSON.stringify({ id, method, params }));
  });
}

async function evaluate(send, expression, awaitPromise = false) {
  const response = await send("Runtime.evaluate", {
    expression,
    awaitPromise,
    returnByValue: true
  });
  if (response.exceptionDetails) {
    throw new Error(response.exceptionDetails.text ?? "CDP evaluation failed");
  }
  return response.result?.value;
}

const inspectExpression = `(() => {
  const surface = document.querySelector("#poster-surface");
  const poster = surface?.querySelector(".poster");
  const rect = (element) => {
    if (!element) return null;
    const value = element.getBoundingClientRect();
    return { x: value.x, y: value.y, width: value.width, height: value.height };
  };
  return {
    collapsed: poster?.classList.contains("is-collapsed") ?? null,
    targetDisplayId: document.body.dataset.targetDisplayId ?? null,
    responsiveScale: document.body.dataset.responsiveScale ?? null,
    hostLogicalWidth: surface?.style.getPropertyValue("--host-logical-width") ?? null,
    hostLogicalHeight: surface?.style.getPropertyValue("--host-logical-height") ?? null,
    hostVisualScale: surface?.style.getPropertyValue("--host-visual-scale") ?? null,
    viewport: { width: innerWidth, height: innerHeight, devicePixelRatio },
    surfaceRect: rect(surface),
    posterRect: rect(poster)
  };
})()`;

const posterTarget = (await targets()).find((target) => target.url.includes("poster.html"));
if (!posterTarget?.webSocketDebuggerUrl) {
  throw new Error("poster.html CDP target was not found");
}

const socket = await connect(posterTarget.webSocketDebuggerUrl);
const send = cdpClient(socket);
await send("Runtime.enable");

if (action === "toggle-collapse") {
  await evaluate(send, `document.querySelector('[data-action="collapse"]')?.click()`);
  await new Promise((resolve) => setTimeout(resolve, 800));
} else if (action !== "inspect") {
  throw new Error(`unsupported action ${action}`);
}

const presentation = await evaluate(send, inspectExpression);
const runtime = await evaluate(
  send,
  `window.__TAURI__.core.invoke("runtime_state").catch((error) => ({ probeError: String(error) }))`,
  true
);
socket.close();

process.stdout.write(`${JSON.stringify({ presentation, runtime }, null, 2)}\n`);
