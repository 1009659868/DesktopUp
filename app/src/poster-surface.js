import { normalizedPoster } from "./model.js";
import {
  STORAGE_KEY,
  buildPosterMarkup,
  createDefaultSnapshot,
  readFirstPoster,
  readPosterSnapshot,
  replaceFirstPoster,
  writePosterSnapshot
} from "./poster-core.js";
import {
  applyPosterAction,
  buildInteractionRegionArgs,
  buildNativeResizeArgs,
  createRuntimeState,
  isRuntimeSuspended,
  selectedPoster
} from "./poster-runtime.js";
import { isReportingFps } from "./performance-monitor.js";
const POSTER_SURFACE_GUTTER = 10;
const POSTER_CORNER_RADIUS = 16;
const fallbackPoster = normalizedPoster(createDefaultSnapshot().posters[0]);

const surface = document.querySelector("#poster-surface");
const toast = document.querySelector("#poster-toast");
let runtime = loadRuntime();
let toastTimer = null;
let manuallyPaused = false;
let automaticallyPaused = false;
let nativeSurfaceSyncTimer = null;
let fpsRafId = null;
let frameCount = 0;
let fpsLastSample = performance.now();
let lastReportedFps = null;

function loadSnapshot() {
  return readPosterSnapshot(localStorage);
}

function loadRuntime() {
  const first = readFirstPoster(loadSnapshot(), fallbackPoster);
  return createRuntimeState({ selectedPosterId: first.id, posters: [first] });
}

function currentPoster() {
  return selectedPoster(runtime);
}

function persistPoster(patch) {
  runtime = applyPosterAction(runtime, {
    type: "patchPoster",
    posterId: currentPoster().id,
    patch
  });
  const next = currentPoster();
  const snapshot = loadSnapshot() ?? {
    selectedPosterId: fallbackPoster.id,
    posters: [fallbackPoster]
  };
  writePosterSnapshot(localStorage, replaceFirstPoster(snapshot, next));
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
  surface.innerHTML = buildPosterMarkup(currentPoster(), { surface: "native" });
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
  await invokeNative("resize_poster_surface", buildNativeResizeArgs(currentPoster(), {
    deviceScaleFactor,
    surfaceGutter: POSTER_SURFACE_GUTTER
  }));
  await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  const posterElement = surface.querySelector(".poster");
  if (!posterElement) return;
  const diagnostics = await invokeNative("set_poster_interaction_region", buildInteractionRegionArgs({
    x: posterElement.offsetLeft,
    y: posterElement.offsetTop,
    width: posterElement.offsetWidth,
    height: posterElement.offsetHeight
  }, {
    deviceScaleFactor,
    cornerRadius: POSTER_CORNER_RADIUS
  }));
  if (diagnostics?.mode) {
    document.body.dataset.desktopHostMode = diagnostics.mode;
  }
}

function scheduleNativeSurfaceSync() {
  window.clearTimeout(nativeSurfaceSyncTimer);
  nativeSurfaceSyncTimer = window.setTimeout(syncNativeSurface, 120);
}

function applyRuntimePauseState(snapshot = {}) {
  runtime = applyPosterAction(runtime, { type: "applyRuntimeSnapshot", snapshot });
  manuallyPaused = runtime.manuallyPaused;
  automaticallyPaused = runtime.automaticallyPaused;
  surface.classList.toggle("is-runtime-paused", isRuntimeSuspended(runtime, document.hidden));
  const pauseState = runtime.performanceMode === "auto"
    ? (isRuntimeSuspended(runtime, document.hidden) ? "paused" : "activeStatic")
    : runtime.performanceMode;
  surface.classList.toggle("is-performance-static", pauseState === "activeStatic");
  surface.classList.toggle("is-performance-animated", pauseState === "activeAnimated");
  surface.classList.toggle("is-performance-paused", pauseState === "paused");
  surface.classList.toggle("is-performance-stopped", pauseState === "stopped");
  document.body.dataset.runtimePauseReason = runtime.pauseReasons.join(",");
  document.body.dataset.performanceMode = runtime.performanceMode;
  syncFpsSampling();
}

function reportPosterFps(fps) {
  if (lastReportedFps === fps) return;
  lastReportedFps = fps;
  invokeNative("report_poster_fps", { fps });
}

function shouldSampleFps() {
  const mode = document.body.dataset.performanceMode || "auto";
  return isReportingFps(mode, isRuntimeSuspended(runtime, document.hidden), document.hidden);
}

function stopFpsSampling() {
  if (fpsRafId != null) {
    cancelAnimationFrame(fpsRafId);
    fpsRafId = null;
  }
  frameCount = 0;
  reportPosterFps(0);
}

function sampleFrame(now) {
  if (!shouldSampleFps()) {
    stopFpsSampling();
    return;
  }
  frameCount += 1;
  const elapsed = now - fpsLastSample;
  if (elapsed >= 2000) {
    reportPosterFps(Math.round((frameCount / elapsed) * 1000));
    frameCount = 0;
    fpsLastSample = now;
  }
  fpsRafId = requestAnimationFrame(sampleFrame);
}

function syncFpsSampling() {
  if (shouldSampleFps()) {
    if (fpsRafId == null) {
      fpsLastSample = performance.now();
      frameCount = 0;
      fpsRafId = requestAnimationFrame(sampleFrame);
    }
  } else {
    stopFpsSampling();
  }
}

surface.addEventListener("click", (event) => {
  const action = event.target.closest("[data-action]")?.dataset.action;
  const poster = currentPoster();
  if (action === "lock") {
    persistPoster({ locked: !poster.locked });
    showToast(poster.locked ? "海报位置已锁定" : "海报位置已解锁，可拖动标题栏");
  }
  if (action === "collapse") {
    persistPoster({ collapsed: !poster.collapsed });
  }
});

surface.addEventListener("pointerdown", (event) => {
  if (!event.target.closest("[data-native-drag]") || event.target.closest("button")) return;
  const poster = currentPoster();
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
  const item = currentPoster().items[Number(tile.dataset.itemIndex)];
  if (item) showToast(`Demo：准备启动 ${item.title}`);
});

window.addEventListener("storage", (event) => {
  if (event.key !== STORAGE_KEY) return;
  runtime = loadRuntime();
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
syncFpsSampling();
connectNativeControls().catch(() => {});
