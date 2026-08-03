import { gridColumns, normalizedPoster } from "./model.js";

// ── Constants ──────────────────────────────────────────────────────────
const STORAGE_KEY = "desktop-up.a0-demo.v1";
const POSTER_SURFACE_GUTTER = 0;
const POSTER_CORNER_RADIUS = 18;
const COLLAPSED_TITLE_HEIGHT = 60;
const SAFETY_MARGIN_TOP = 12;
const SAFETY_MARGIN_BOTTOM = 12;

// ── State ──────────────────────────────────────────────────────────────
const surface = document.querySelector("#poster-surface");
const toast = document.querySelector("#poster-toast");

let poster = loadPoster();
let presentationMode = poster.collapsed ? "collapsed" : "expanded"; // §4.1
let interactionMode = "using";                                        // §4.2  (using | editing)
let toastTimer = null;
let manuallyPaused = false;
let automaticallyPaused = false;
let nativeSurfaceSyncTimer = null;

const fallbackPoster = normalizedPoster({
  id: "night-ops", title: "NIGHT OPS", subtitle: "TACTICAL ARCHIVE / 01",
  theme: "nocturne", accent: "#72e4ff", opacity: 94, density: 1,
  width: 420, height: 1368, collapsed: false, locked: true,
  items: [
    ["AFTERLIGHT", "AL", "violet"], ["GHOST SIGNAL", "GS", "cyan"],
    ["IRON VEIL", "IV", "red"], ["LAST ORBIT", "LO", "amber"],
    ["SABLE RUN", "SR", "blue"], ["HOLLOW CROWN", "HC", "green"]
  ].map(([title, mark, hue], index) => ({ id: `${mark}-${index}`, title, mark, hue }))
});

// ── Helpers ────────────────────────────────────────────────────────────
function escapeHtml(value) {
  return String(value).replaceAll("&", "&amp;").replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#039;");
}

function loadSnapshot() {
  try {
    const snap = JSON.parse(localStorage.getItem(STORAGE_KEY));
    return snap && Array.isArray(snap.posters) ? snap : null;
  } catch { return null; }
}

function loadPoster() {
  const snap = loadSnapshot();
  return normalizedPoster(snap?.posters?.[0] ?? fallbackPoster);
}

function computeWorkAreaPosterSize() {
  const sw = window.screen?.availWidth ?? screen.width;
  const sh = window.screen?.availHeight ?? screen.height;
  const dpr = Math.max(1, window.devicePixelRatio || 1);
  const pw = Math.max(280, Math.round(sw * dpr * 0.22));
  const ph = sh * dpr - SAFETY_MARGIN_TOP - SAFETY_MARGIN_BOTTOM;
  return {
    logicalWidth: Math.round(pw / dpr),
    logicalHeight: Math.round(ph / dpr),
    deviceScaleFactor: dpr
  };
}

function persistPoster(patch) {
  const snap = loadSnapshot() ?? { selectedPosterId: fallbackPoster.id, posters: [fallbackPoster] };
  const next = normalizedPoster({ ...poster, ...patch });
  snap.posters = snap.posters.length
    ? snap.posters.map((e, i) => i === 0 ? next : e) : [next];
  localStorage.setItem(STORAGE_KEY, JSON.stringify(snap));
  poster = next;
  // Keep presentationMode in sync with persisted collapsed state
  if (poster.collapsed) presentationMode = "collapsed";
  else presentationMode = "expanded";
  render();
  syncNativeSurface();
}

function showToast(message) {
  clearTimeout(toastTimer);
  toast.textContent = message;
  toast.classList.add("is-visible");
  toastTimer = setTimeout(() => toast.classList.remove("is-visible"), 2200);
}

// ── Render ─────────────────────────────────────────────────────────────
function render() {
  const columns = gridColumns(poster.width, poster.density);
  const isCollapsed = presentationMode === "collapsed";

  const gameCards = poster.items.map((item, index) => `
    <button class="game-tile hue-${escapeHtml(item.hue)}" type="button"
      data-action="launch" data-item-index="${index}"
      title="${interactionMode === "editing" ? "拖拽排序 · 双击启动" : "单击启动"} ${escapeHtml(item.title)}">
      <span class="game-cover"><i></i><strong>${escapeHtml(item.mark)}</strong><small>0${index + 1}</small></span>
      <span class="game-name">${escapeHtml(item.title)}</span>
    </button>
  `).join("");

  const itemCount = poster.items.length;

  const classList = [
    "poster",
    `theme-${poster.theme}`,
    isCollapsed && "is-collapsed",
    poster.locked && "is-locked",
    interactionMode === "editing" && "is-editing"
  ].filter(Boolean).join(" ");

  surface.innerHTML = `
    <article class="${classList}"
      style="--poster-accent:${poster.accent};--poster-opacity:${poster.opacity / 100};--grid-columns:${columns}">
      <div class="poster-atmosphere"><i></i><i></i><i></i></div>

      <!-- ── Title bar (§6.1: 60px collapsed, normal expanded) ── -->
      <header class="poster-header" data-native-drag>
        <div class="poster-header-left">
          <span class="poster-header-icon"></span>
          <span class="poster-header-title">${escapeHtml(poster.title)}</span>
          <span class="poster-header-count">${itemCount.toString().padStart(2, "0")} GAMES</span>
        </div>

        <div class="poster-header-right">
          ${isCollapsed ? `
            <button class="poster-btn poster-expand-btn" type="button" data-action="expand"
              aria-label="展开海报" title="展开海报"><span></span></button>
          ` : `
            <button class="poster-btn poster-collapse-btn" type="button" data-action="collapse"
              aria-label="收起海报" title="收起海报"><span></span></button>
            ${interactionMode === "editing" ? `
              <button class="poster-btn poster-lock-btn" type="button" data-action="lock"
                aria-label="${poster.locked ? "解锁" : "锁定"}海报" aria-pressed="${poster.locked}"
                title="${poster.locked ? "已锁定 · 点击解锁" : "解锁状态 · 点击锁定"}">
                <i class="poster-lock-icon">${poster.locked ? "◈" : "◇"}</i>
              </button>
            ` : poster.locked ? `
              <span class="poster-lock-indicator" title="已锁定">◈</span>
            ` : ""}
          `}
        </div>
      </header>

      <!-- ── Expanded-only content (§5.1: COLLAPSED hides these) ── -->
      ${isCollapsed ? "" : `
        <div class="poster-copy">
          <span>${escapeHtml(poster.subtitle)}</span>
          <h2>${escapeHtml(poster.title)}</h2>
          <p>${itemCount.toString().padStart(2, "0")} TITLES <i></i> READY</p>
        </div>
        <div class="poster-content"><div class="game-grid">${gameCards}</div></div>
        <footer class="poster-footer">
          <span>LOCAL LIBRARY</span>
          <span class="poster-bars"><i></i><i></i><i></i><i></i></span>
          <span>${poster.locked ? "POSITION LOCKED" : interactionMode === "editing" ? "DRAG TO MOVE" : ""}</span>
        </footer>
      `}

      <!-- ── Editing-mode overlays (§6.2) ── -->
      ${interactionMode === "editing" && !isCollapsed ? `
        <div class="poster-resize-handle" title="拖动调整宽度"></div>
        <div class="poster-anchor poster-anchor-tl"></div>
        <div class="poster-anchor poster-anchor-tr"></div>
        <div class="poster-anchor poster-anchor-bl"></div>
        <div class="poster-anchor poster-anchor-br"></div>
      ` : ""}
    </article>
  `;
}

// ── Native IPC ─────────────────────────────────────────────────────────
async function invokeNative(command, args = {}) {
  const invoke = window.__TAURI__?.core?.invoke;
  if (!invoke) return;
  try { return await invoke(command, args); } catch { showToast("原生窗口操作暂不可用"); }
}

async function syncNativeSurface() {
  const { logicalWidth, logicalHeight, deviceScaleFactor } = computeWorkAreaPosterSize();
  const isCollapsed = presentationMode === "collapsed";
  await invokeNative("resize_poster_surface", {
    collapsed: isCollapsed,
    width: logicalWidth,
    height: isCollapsed ? COLLAPSED_TITLE_HEIGHT : logicalHeight,
    surfaceGutter: POSTER_SURFACE_GUTTER,
    deviceScaleFactor
  });
  await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));
  const el = surface.querySelector(".poster");
  if (!el) return;
  const diag = await invokeNative("set_poster_interaction_region", {
    x: el.offsetLeft, y: el.offsetTop,
    width: el.offsetWidth, height: el.offsetHeight,
    cornerRadius: POSTER_CORNER_RADIUS, deviceScaleFactor
  });
  if (diag?.mode) document.body.dataset.desktopHostMode = diag.mode;
}

function scheduleNativeSurfaceSync() {
  window.clearTimeout(nativeSurfaceSyncTimer);
  nativeSurfaceSyncTimer = window.setTimeout(syncNativeSurface, 120);
}

// ── Pause state ────────────────────────────────────────────────────────
function applyRuntimePauseState(snapshot = {}) {
  manuallyPaused = Boolean(snapshot.manuallyPaused);
  automaticallyPaused = Boolean(snapshot.automaticallyPaused);
  surface.classList.toggle("is-runtime-paused",
    manuallyPaused || automaticallyPaused || document.hidden);
  const reasons = snapshot.runtimeVisibility?.pauseReasons ?? snapshot.pauseReasons;
  document.body.dataset.runtimePauseReason = Array.isArray(reasons) ? reasons.join(",") : "";
}

// ── Event handlers ─────────────────────────────────────────────────────
surface.addEventListener("click", (event) => {
  const action = event.target.closest("[data-action]")?.dataset.action;
  if (!action) return;

  switch (action) {
    case "lock": {
      const newLocked = !poster.locked;
      persistPoster({ locked: newLocked });
      showToast(newLocked ? "海报位置已锁定" : "海报位置已解锁");
      break;
    }
    case "collapse": {
      presentationMode = "collapsed";
      persistPoster({ collapsed: true });
      showToast("海报已收起");
      break;
    }
    case "expand": {
      presentationMode = "expanded";
      persistPoster({ collapsed: false });
      showToast("海报已展开");
      break;
    }
  }
});

surface.addEventListener("pointerdown", (event) => {
  // Title-bar drag (§5.1)
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

// ── Resize handle drag (§5.2: EDITING + EXPANDED right-edge drag) ──
surface.addEventListener("pointerdown", (event) => {
  if (!event.target.closest(".poster-resize-handle")) return;
  if (poster.locked) return;
  event.preventDefault();

  const startX = event.clientX;
  const startWidth = poster.width;
  const onMove = (e) => {
    const delta = e.clientX - startX;
    const newWidth = Math.max(240, Math.min(1200, startWidth + delta));
    // Live preview: update CSS variable on the poster element
    const el = surface.querySelector(".poster");
    if (el) el.style.width = `${newWidth}px`;
  };
  const onUp = (e) => {
    window.removeEventListener("pointermove", onMove);
    window.removeEventListener("pointerup", onUp);
    const delta = e.clientX - startX;
    const newWidth = Math.max(240, Math.min(1200, startWidth + delta));
    persistPoster({ width: newWidth });
  };
  window.addEventListener("pointermove", onMove);
  window.addEventListener("pointerup", onUp);
});

// ── Window events ──────────────────────────────────────────────────────
window.addEventListener("storage", (event) => {
  if (event.key !== STORAGE_KEY) return;
  poster = loadPoster();
  presentationMode = poster.collapsed ? "collapsed" : "expanded";
  render();
  syncNativeSurface();
});

window.addEventListener("resize", scheduleNativeSurfaceSync);

document.addEventListener("visibilitychange", () => {
  applyRuntimePauseState({
    manuallyPaused, automaticallyPaused,
    pauseReasons: document.body.dataset.runtimePauseReason?.split(",").filter(Boolean) ?? []
  });
});

// ── Native control bridge ──────────────────────────────────────────────
async function connectNativeControls() {
  const listen = window.__TAURI__?.event?.listen;
  if (!listen) return;

  const snapshot = await invokeNative("runtime_state");
  if (snapshot) {
    applyRuntimePauseState(snapshot);
    // Sync interactionMode from native edit_mode flag
    if (snapshot.editMode) interactionMode = "editing";
    else interactionMode = "using";
    render();
  }

  await listen("runtime-pause-state", (event) => {
    applyRuntimePauseState(event.payload ?? {});
  });

  // Listen for edit-mode toggles from tray/control-center
  await listen("runtime-control", (event) => {
    const payload = event.payload ?? "";
    if (payload === "toggle-edit") {
      interactionMode = interactionMode === "editing" ? "using" : "editing";
      render();
      scheduleNativeSurfaceSync();
    } else if (payload === "toggle-lock-all") {
      const newLocked = !poster.locked;
      persistPoster({ locked: newLocked });
      showToast(newLocked ? "全部海报已锁定" : "全部海报已解锁");
    }
  });
}

// ── Init ───────────────────────────────────────────────────────────────
render();
syncNativeSurface();
connectNativeControls().catch(() => {});
