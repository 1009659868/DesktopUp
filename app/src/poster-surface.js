import { gridColumns, normalizedPoster } from "./model.js";

const STORAGE_KEY = "desktop-up.a0-demo.v1";
const POSTER_SURFACE_GUTTER = 0;
const POSTER_CORNER_RADIUS = 18;
const fallbackPoster = normalizedPoster({
  id: "night-ops",
  title: "NIGHT OPS",
  subtitle: "TACTICAL ARCHIVE / 01",
  theme: "nocturne",
  accent: "#72e4ff",
  opacity: 94,
  density: 1,
  width: 420,
  height: 1368,
  collapsed: false,
  locked: false,
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
let poster = loadPoster();
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

function computeWorkAreaPosterSize() {
  const sw = window.screen?.availWidth ?? screen.width;
  const sh = window.screen?.availHeight ?? screen.height;
  const pw = Math.max(280, Math.round(sw * 0.22));
  const ph = sh - 24; // 12px top + 12px bottom safety margin
  return { logicalWidth: pw, logicalHeight: ph };
}

// On first load, override the fallback default with work-area-aware size.
// poster.width / poster.height from storage or fallback are the source of
// truth for the JS render; the native window is driven from those values
// via syncNativeSurface → resize_poster_surface.
(function applyInitialPosterSize() {
  const { logicalWidth, logicalHeight } = computeWorkAreaPosterSize();
  poster.width = logicalWidth;
  poster.height = logicalHeight;
})();

async function syncNativeSurface() {
  const deviceScaleFactor = Math.max(1, window.devicePixelRatio || 1);
  // Use poster DOM dimensions, not work-area size, so the native window
  // exactly matches the rendered poster content.
  if (poster.collapsed) {
    // Collapsed: only the 60px header is visible.
    await invokeNative("resize_poster_surface", {
      collapsed: true,
      width: poster.width,
      height: poster.height,
      surfaceGutter: POSTER_SURFACE_GUTTER,
      deviceScaleFactor
    });
  } else {
    await invokeNative("resize_poster_surface", {
      collapsed: false,
      width: poster.width,
      height: poster.height,
      surfaceGutter: POSTER_SURFACE_GUTTER,
      deviceScaleFactor
    });
  }
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
  }
  if (action === "collapse") {
    persistPoster({ collapsed: !poster.collapsed });
  }
});

surface.addEventListener("pointerdown", (event) => {
  if (!event.target.closest("[data-native-drag]") || event.target.closest("button")) return;
  if (poster.locked) return;
  event.preventDefault();
  invokeNative("start_poster_drag");
});

surface.addEventListener("dblclick", (event) => {
  const tile = event.target.closest('[data-action="launch"]');
  if (!tile) return;
  const item = poster.items[Number(tile.dataset.itemIndex)];
  if (item) { /* demo: no-op, launch not implemented yet */ }
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

// ── Frame-rate telemetry (C-DHF-06) ───────────────────────────────────
let frameCount = 0;
let fpsLastSample = performance.now();
function sampleFrame() {
  frameCount++;
  const now = performance.now();
  const elapsed = now - fpsLastSample;
  if (elapsed >= 2000) {
    const fps = Math.round((frameCount / elapsed) * 1000);
    invokeNative("report_poster_fps", { fps });
    frameCount = 0;
    fpsLastSample = now;
  }
  requestAnimationFrame(sampleFrame);
}
requestAnimationFrame(sampleFrame);
