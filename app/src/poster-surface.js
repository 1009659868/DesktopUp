import { gridColumns, normalizedPoster } from "./model.js";
import {
  interpolate,
  injectTemplateCSSVariables,
  switchTemplateCSSClass,
  applyUserSlotOverrides,
  SCHEMA_CACHE
} from "./template/engine.js";

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
let presentationMode = poster.collapsed ? "collapsed" : "expanded";
let interactionMode = "using";
let toastTimer = null;
let manuallyPaused = false;
let automaticallyPaused = false;
let nativeSurfaceSyncTimer = null;
let currentTemplateKey = "neon-hud";
let templateCSSStyleElement = null;

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

// ── Render (template-driven) ───────────────────────────────────────────
function render() {
  const isCollapsed = presentationMode === "collapsed";
  const itemCount = poster.items.length;
  const columns = gridColumns(poster.width, poster.density);

  // ── Load and render template HTML ──
  const bundle = SCHEMA_CACHE.get(currentTemplateKey);
  let templateHTML = bundle
    ? bundle.html
    : '<div class="poster-template-missing">模板加载中…</div>';

  // Apply depth-3 user slot overrides (if any)
  templateHTML = applyUserSlotOverrides(templateHTML, {});

  // Interpolate poster data variables
  const templateData = {
    title: poster.title,
    item_count: itemCount,
    collapsed_state: presentationMode,
    theme: poster.theme,
    locked: poster.locked ? "locked" : "unlocked"
  };
  templateHTML = interpolate(templateHTML, templateData);

  // Game cards — injected into the template's #poster-icon-container
  const gameCards = poster.items.map((item, index) => `
    <button class="game-tile hue-${escapeHtml(item.hue)}" type="button"
      data-action="launch" data-item-index="${index}"
      title="${interactionMode === "editing" ? "拖拽排序 · 双击启动" : "单击启动"} ${escapeHtml(item.title)}">
      <span class="game-cover"><i></i><strong>${escapeHtml(item.mark)}</strong><small>0${index + 1}</small></span>
      <span class="game-name">${escapeHtml(item.title)}</span>
    </button>
  `).join("");

  // Inject game cards into the icon container in the template HTML
  templateHTML = templateHTML.replace(
    /<div id="poster-icon-container"><\/div>/,
    `<div id="poster-icon-container">${gameCards}</div>`
  );

  // Build the poster article wrapper
  const classList = [
    "poster",
    isCollapsed && "is-collapsed",
    poster.locked && "is-locked",
    interactionMode === "editing" && "is-editing"
  ].filter(Boolean).join(" ");

  surface.innerHTML = `
    <article class="${classList}" style="--grid-columns:${columns}">
      ${templateHTML}

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

  // ── Inject template CSS (hot-switch safe) ──
  if (bundle) {
    if (templateCSSStyleElement) templateCSSStyleElement.remove();
    templateCSSStyleElement = document.createElement("style");
    templateCSSStyleElement.id = "template-css";
    templateCSSStyleElement.textContent = bundle.css;
    document.head.appendChild(templateCSSStyleElement);

    // Inject default CSS variables from params-schema
    const posterElement = surface.querySelector(".poster");
    if (posterElement) {
      const defaults = {};
      if (Array.isArray(bundle.params_schema)) {
        for (const group of bundle.params_schema) {
          for (const param of (group.params || [])) {
            defaults[param.key] = param.default;
          }
        }
      }
      injectTemplateCSSVariables(posterElement, currentTemplateKey, defaults);
    }
  }
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

// When running in Tauri, try loading templates from the Rust side first.
// In browser/dev mode, templates are served as static files from app/templates.
async function preloadTemplateBundle(key) {
  try {
    const invoke = window.__TAURI__?.core?.invoke;
    if (invoke) {
      const json = await invoke("get_template_bundle", { templateKey: key });
      const bundle = JSON.parse(json);
      SCHEMA_CACHE.set(key, bundle);
      return;
    }
  } catch {
    // Fall back to HTTP fetch below
  }

  try {
    const { loadTemplateBundle } = await import("./template/engine.js");
    await loadTemplateBundle(key);
  } catch {
    console.warn(`template "${key}" unavailable — using fallback render`);
  }
}

async function initTemplates() {
  await preloadTemplateBundle("neon-hud");
  if (!SCHEMA_CACHE.has(currentTemplateKey)) {
    currentTemplateKey = "neon-hud";
    await preloadTemplateBundle("neon-hud");
  }
}

initTemplates().then(() => {
  currentTemplateKey = poster.theme === "cinema" ? "cinema-lightbox" : "neon-hud";
  const bundle = SCHEMA_CACHE.get(currentTemplateKey);
  if (bundle) {
    // Switch template CSS class on existing poster container
    switchTemplateCSSClass(surface, null, currentTemplateKey);
  }
  render();
  syncNativeSurface();
  connectNativeControls().catch(() => {});
});
