import { gridColumns, normalizedPoster } from "./model.js";

const STORAGE_KEY = "desktop-up.a0-demo.v1";
const POSTER_SURFACE_GUTTER = 10;
const POSTER_CORNER_RADIUS = 16;
const fallbackPoster = normalizedPoster({
  id: "night-ops",
  title: "NIGHT OPS",
  subtitle: "TACTICAL ARCHIVE / 01",
  theme: "nocturne",
  accent: "#72e4ff",
  opacity: 94,
  density: 1,
  width: 490,
  height: 392,
  collapsed: false,
  locked: true,
  items: [
    ["AFTERLIGHT", "AL", "violet"],
    ["GHOST SIGNAL", "GS", "cyan"],
    ["IRON VEIL", "IV", "red"],
    ["LAST ORBIT", "LO", "amber"],
    ["SABLE RUN", "SR", "blue"],
    ["HOLLOW CROWN", "HC", "green"]
  ].map(([title, mark, hue], index) => ({ id: `${mark}-${index}`, title, mark, hue }))
});

const surface = document.querySelector("#poster-surface");
const toast = document.querySelector("#poster-toast");
let poster = loadPoster();
let toastTimer = null;
let manuallyPaused = false;
let automaticallyPaused = false;
let nativeSurfaceSyncTimer = null;

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function loadSnapshot() {
  try {
    const snapshot = JSON.parse(localStorage.getItem(STORAGE_KEY));
    return snapshot && Array.isArray(snapshot.posters) ? snapshot : null;
  } catch {
    return null;
  }
}

function loadPoster() {
  const snapshot = loadSnapshot();
  return normalizedPoster(snapshot?.posters?.[0] ?? fallbackPoster);
}

function persistPoster(patch) {
  const snapshot = loadSnapshot() ?? {
    selectedPosterId: fallbackPoster.id,
    posters: [fallbackPoster]
  };
  const next = normalizedPoster({ ...poster, ...patch });
  snapshot.posters = snapshot.posters.length
    ? snapshot.posters.map((entry, index) => index === 0 ? next : entry)
    : [next];
  localStorage.setItem(STORAGE_KEY, JSON.stringify(snapshot));
  poster = next;
  render();
  syncNativeSurface();
}

function showToast(message) {
  clearTimeout(toastTimer);
  toast.textContent = message;
  toast.classList.add("is-visible");
  toastTimer = setTimeout(() => toast.classList.remove("is-visible"), 2200);
}

function render() {
  const columns = gridColumns(poster.width, poster.density);
  const games = poster.items.map((item, index) => `
    <button class="game-tile hue-${escapeHtml(item.hue)}" type="button" data-action="launch"
      data-item-index="${index}" title="双击启动 ${escapeHtml(item.title)}">
      <span class="game-cover"><i></i><strong>${escapeHtml(item.mark)}</strong><small>0${index + 1}</small></span>
      <span class="game-name">${escapeHtml(item.title)}</span>
    </button>
  `).join("");

  surface.innerHTML = `
    <article class="poster theme-${poster.theme}${poster.collapsed ? " is-collapsed" : ""}${poster.locked ? " is-locked" : ""}"
      style="--poster-accent:${poster.accent};--poster-opacity:${poster.opacity / 100};--grid-columns:${columns}">
      <div class="poster-atmosphere"><i></i><i></i><i></i></div>
      <header class="poster-header" data-native-drag>
        <div class="poster-kicker"><span>DESKTOP COLLECTION</span><i></i><small>01</small></div>
        <div class="poster-actions">
          <button class="poster-lock" type="button" data-action="lock" aria-label="${poster.locked ? "解锁" : "锁定"}海报位置" aria-pressed="${poster.locked}"><span></span></button>
          <button class="poster-collapse" type="button" data-action="collapse" aria-label="${poster.collapsed ? "展开" : "收起"}海报"><span></span></button>
        </div>
      </header>
      <div class="poster-copy">
        <span>${escapeHtml(poster.subtitle)}</span>
        <h2>${escapeHtml(poster.title)}</h2>
        <p>${poster.items.length.toString().padStart(2, "0")} TITLES <i></i> READY</p>
      </div>
      <div class="poster-content"><div class="game-grid">${games}</div></div>
      <footer class="poster-footer">
        <span>LOCAL LIBRARY</span>
        <span class="poster-bars"><i></i><i></i><i></i><i></i></span>
        <span>${poster.locked ? "POSITION LOCKED" : "DRAG TO MOVE"}</span>
      </footer>
    </article>
  `;
}

async function invokeNative(command, args = {}) {
  const invoke = window.__TAURI__?.core?.invoke;
  if (!invoke) return;
  try {
    return await invoke(command, args);
  } catch {
    showToast("原生窗口操作暂不可用");
  }
}

async function syncNativeSurface() {
  const deviceScaleFactor = Math.max(1, window.devicePixelRatio || 1);
  await invokeNative("resize_poster_surface", {
    collapsed: poster.collapsed,
    width: poster.width,
    height: poster.height,
    surfaceGutter: POSTER_SURFACE_GUTTER,
    deviceScaleFactor
  });
  await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  const posterElement = surface.querySelector(".poster");
  if (!posterElement) return;
  const diagnostics = await invokeNative("set_poster_interaction_region", {
    x: posterElement.offsetLeft,
    y: posterElement.offsetTop,
    width: posterElement.offsetWidth,
    height: posterElement.offsetHeight,
    cornerRadius: POSTER_CORNER_RADIUS,
    deviceScaleFactor
  });
  if (diagnostics?.mode) {
    document.body.dataset.desktopHostMode = diagnostics.mode;
  }
}

function scheduleNativeSurfaceSync() {
  window.clearTimeout(nativeSurfaceSyncTimer);
  nativeSurfaceSyncTimer = window.setTimeout(syncNativeSurface, 120);
}

function applyRuntimePauseState(snapshot = {}) {
  manuallyPaused = Boolean(snapshot.manuallyPaused);
  automaticallyPaused = Boolean(snapshot.automaticallyPaused);
  surface.classList.toggle(
    "is-runtime-paused",
    manuallyPaused || automaticallyPaused || document.hidden
  );
  const pauseReasons = snapshot.runtimeVisibility?.pauseReasons ?? snapshot.pauseReasons;
  document.body.dataset.runtimePauseReason = Array.isArray(pauseReasons)
    ? pauseReasons.join(",")
    : "";
}

surface.addEventListener("click", (event) => {
  const action = event.target.closest("[data-action]")?.dataset.action;
  if (action === "lock") {
    persistPoster({ locked: !poster.locked });
    showToast(poster.locked ? "海报位置已锁定" : "海报位置已解锁，可拖动标题栏");
  }
  if (action === "collapse") {
    persistPoster({ collapsed: !poster.collapsed });
    showToast(poster.collapsed ? "海报已收起" : "海报已展开");
  }
});

surface.addEventListener("pointerdown", (event) => {
  if (!event.target.closest("[data-native-drag]") || event.target.closest("button")) return;
  if (poster.locked) {
    showToast("位置已锁定，点击右上角锁按钮后再拖动");
    return;
  }
  event.preventDefault();
  invokeNative("start_poster_drag");
});

surface.addEventListener("dblclick", (event) => {
  const tile = event.target.closest('[data-action="launch"]');
  if (!tile) return;
  const item = poster.items[Number(tile.dataset.itemIndex)];
  if (item) showToast(`Demo：准备启动 ${item.title}`);
});

window.addEventListener("storage", (event) => {
  if (event.key !== STORAGE_KEY) return;
  poster = loadPoster();
  render();
  syncNativeSurface();
});

window.addEventListener("resize", scheduleNativeSurfaceSync);

document.addEventListener("visibilitychange", () => {
  applyRuntimePauseState({
    manuallyPaused,
    automaticallyPaused,
    pauseReasons: document.body.dataset.runtimePauseReason?.split(",").filter(Boolean) ?? []
  });
});

async function connectNativeControls() {
  const listen = window.__TAURI__?.event?.listen;
  if (!listen) return;
  const snapshot = await invokeNative("runtime_state");
  if (snapshot) applyRuntimePauseState(snapshot);
  await listen("runtime-pause-state", (event) => {
    applyRuntimePauseState(event.payload ?? {});
  });
}

render();
syncNativeSurface();
connectNativeControls().catch(() => {});
