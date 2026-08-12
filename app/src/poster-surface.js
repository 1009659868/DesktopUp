import {
  buildPosterMarkup
} from "./poster-core.js";
import {
  POSTER_STATE_UPDATED_EVENT,
  PosterStateClient,
  createEphemeralPosterRuntimeSnapshot
} from "./poster-state-store.js";
import {
  applyPosterAction,
  buildHostSurfaceSyncArgs,
  createRuntimeState,
  isRuntimeSuspended,
  measuredInteractionRegion,
  runtimeMode,
  posterInteractionMode,
  selectedPoster
} from "./poster-runtime.js";
import { isReportingFps } from "./performance-monitor.js";
import { hostPresentationCssVariables } from "./host-presentation.js";
const ACTIVE_ANIMATION_FRAME_INTERVAL_MS = 1000 / 30;
const POSTER_SURFACE_GUTTER = 10;
const POSTER_CORNER_RADIUS = 16;
const POSTER_COLLAPSED_HEIGHT = 116;

const surface = document.querySelector("#poster-surface");
const toast = document.querySelector("#poster-toast");
const posterState = new PosterStateClient({ invoke: nativeInvoke(), storage: localStorage });
let runtime = createRuntimeState(createEphemeralPosterRuntimeSnapshot());
let toastTimer = null;
let launchClickTimer = null;
let nativeSurfaceSyncTimer = null;
let hostSurfaceRevision = 0;
let nativeSurfaceReady = false;
let fpsRafId = null;
let frameCount = 0;
let fpsTimerId = null;
let fpsLastSample = performance.now();
let lastReportedFps = null;

function nativeInvoke() {
  return window.__TAURI__?.core?.invoke;
}

function runtimeFromPosterState() {
  return createRuntimeState(posterState.runtimeSnapshot());
}

async function loadRuntime() {
  const result = await posterState.bootstrap();
  document.body.dataset.posterStateStatus = result.status;
  if (result.status !== "ready") {
    throw new Error(result.message ?? "poster state bootstrap rejected");
  }
  return runtimeFromPosterState();
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
  void posterState.saveRuntimeState(runtime).then((result) => {
    if (result?.status === "rejected") {
      document.body.dataset.posterStateStatus = "rejected";
      showToast("海报状态保存失败，原状态已保留");
    } else if (result?.status === "stored") {
      document.body.dataset.posterStateStatus = "ready";
    }
  });
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

function logicalRegionForElement(element, posterElement, visualRect, regionId, cornerRadius = 0) {
  return measuredInteractionRegion({
    regionId,
    cornerRadius,
    surfaceRect: surface.getBoundingClientRect(),
    surfaceSize: { width: surface.offsetWidth, height: surface.offsetHeight },
    posterRect: posterElement.getBoundingClientRect(),
    elementRect: element.getBoundingClientRect(),
    visualRect
  });
}

function collectSinglePosterInteractionRegions(posterElement, posterId, visualRect) {
  const regions = [];
  const header = posterElement.querySelector("[data-native-drag]");
  if (header) {
    const headerRegion = logicalRegionForElement(
      header,
      posterElement,
      visualRect,
      `poster-${posterId}-header`,
      POSTER_CORNER_RADIUS
    );
    if (headerRegion) regions.push(headerRegion);
  }
  posterElement.querySelectorAll('[data-action="launch"]').forEach((tile, index) => {
    const tileRegion = logicalRegionForElement(
      tile,
      posterElement,
      visualRect,
      `poster-${posterId}-item-${index}`,
      6
    );
    if (tileRegion) regions.push(tileRegion);
  });
  return regions;
}

function applyHostPresentation(hostSurface) {
  if (!hostSurface || hostSurface.revision <= 0) return;
  const variables = hostPresentationCssVariables(hostSurface);
  for (const [name, value] of Object.entries(variables)) {
    surface.style.setProperty(name, value);
  }
  document.body.dataset.targetDisplayId = hostSurface.targetDisplayId ?? "unknown";
  document.body.dataset.responsiveScale = String(hostSurface.responsiveScale ?? 1);
}

async function revealInitializedNativeSurface(revision) {
  if (nativeSurfaceReady || !Number.isSafeInteger(revision) || revision <= 0) return;
  await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  const ready = await invokeNative("mark_poster_surface_ready", { revision });
  if (ready) {
    nativeSurfaceReady = true;
    document.body.dataset.hostSurfaceReady = "true";
  }
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
  try {
    const deviceScaleFactor = Math.max(1, window.devicePixelRatio || 1);
    await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
    const posterElement = surface.querySelector(".poster");
    if (!posterElement) return;
    hostSurfaceRevision += 1;
    const poster = currentPoster();
    const visualRect = {
      x: posterElement.offsetLeft,
      y: posterElement.offsetTop,
      width: poster.width,
      height: poster.collapsed ? POSTER_COLLAPSED_HEIGHT : poster.height
    };
    const interactionRegions = collectSinglePosterInteractionRegions(
      posterElement,
      poster.id,
      visualRect
    );
    const args = buildHostSurfaceSyncArgs(poster, visualRect, {
      revision: hostSurfaceRevision,
      deviceScaleFactor,
      surfaceGutter: POSTER_SURFACE_GUTTER,
      collapsedHeight: POSTER_COLLAPSED_HEIGHT,
      cornerRadius: POSTER_CORNER_RADIUS,
      interactionRegions,
      placementIntent: { kind: "preserve" }
    });
    const result = await invokeNative("sync_host_surface", args);
    applyHostPresentation(result?.hostSurface);
    await revealInitializedNativeSurface(result?.hostSurface?.revision);
    const diagnostics = result?.desktopHost;
    if (diagnostics?.mode) {
      document.body.dataset.desktopHostMode = diagnostics.mode;
    }
  } catch {
    showToast("海报窗口同步失败");
  }
}

function scheduleNativeSurfaceSync() {
  window.clearTimeout(nativeSurfaceSyncTimer);
  nativeSurfaceSyncTimer = window.setTimeout(syncNativeSurface, 120);
}

function applyRuntimePresentation() {
  surface.classList.toggle("is-runtime-paused", isRuntimeSuspended(runtime, document.hidden));
  const pauseState = runtimeMode(runtime, document.hidden);
  surface.classList.toggle("is-performance-static", pauseState === "activeStatic");
  surface.classList.toggle("is-performance-animated", pauseState === "activeAnimated");
  surface.classList.toggle("is-performance-paused", pauseState === "paused");
  surface.classList.toggle("is-performance-stopped", pauseState === "stopped");
  document.body.dataset.runtimePauseReason = runtime.pauseReasons.join(",");
  document.body.dataset.runtimeMode = pauseState;
  document.body.dataset.performanceMode = runtime.performanceMode;
  syncFpsSampling();
}

function applyRuntimePauseState(snapshot = {}) {
  if (Number.isSafeInteger(snapshot?.hostSurface?.revision)) {
    hostSurfaceRevision = Math.max(hostSurfaceRevision, snapshot.hostSurface.revision);
  }
  runtime = applyPosterAction(runtime, { type: "applyRuntimeSnapshot", snapshot });
  applyHostPresentation(runtime.hostSurface);
  applyRuntimePresentation();
}

function reportPosterFps(fps) {
  if (lastReportedFps === fps) return;
  lastReportedFps = fps;
  invokeNative("report_poster_fps", { fps });
}

function shouldSampleFps() {
  const mode = runtimeMode(runtime, document.hidden);
  return isReportingFps(mode, isRuntimeSuspended(runtime, document.hidden), document.hidden);
}

function stopFpsSampling() {
  if (fpsTimerId != null) {
    window.clearTimeout(fpsTimerId);
    fpsTimerId = null;
  }
  if (fpsRafId != null) {
    cancelAnimationFrame(fpsRafId);
    fpsRafId = null;
  }
  frameCount = 0;
  reportPosterFps(0);
}

function sampleFrame(now) {
function scheduleNextFpsFrame() {
  fpsTimerId = window.setTimeout(() => {
    fpsTimerId = null;
    if (!shouldSampleFps()) {
      stopFpsSampling();
      return;
    }
    fpsRafId = requestAnimationFrame(sampleFrame);
  }, ACTIVE_ANIMATION_FRAME_INTERVAL_MS);
}

  if (!shouldSampleFps()) {
    stopFpsSampling();
    return;
  }
  fpsRafId = null;
  frameCount += 1;
  const elapsed = now - fpsLastSample;
  if (elapsed >= 2000) {
    reportPosterFps(Math.round((frameCount / elapsed) * 1000));
    frameCount = 0;
    fpsLastSample = now;
  }
  scheduleNextFpsFrame();
}

function syncFpsSampling() {
  if (shouldSampleFps()) {
    if (fpsRafId == null && fpsTimerId == null) {
      fpsLastSample = performance.now();
      frameCount = 0;
      scheduleNextFpsFrame();
    }
  } else {
    stopFpsSampling();
  }
}

surface.addEventListener("click", (event) => {
  const action = event.target.closest("[data-action]")?.dataset.action;
  const poster = currentPoster();
  if (action === "lock") {
    const nextLocked = !poster.locked;
    persistPoster({ locked: nextLocked });
    showToast(nextLocked ? "海报位置已锁定" : "海报位置已解锁，可拖动标题栏");
    return;
  }
  if (action === "collapse") {
    persistPoster({ collapsed: !poster.collapsed });
    return;
  }
  if (action === "launch") {
    const tile = event.target.closest('[data-action="launch"]');
    const item = poster.items[Number(tile?.dataset.itemIndex)];
    if (!item) return;
    window.clearTimeout(launchClickTimer);
    if (posterInteractionMode(runtime, poster) === "editing") {
      if (event.detail === 1) showToast(`${item.title} 已选中，可继续排序或编辑`);
    } else if (event.detail === 1) {
      launchClickTimer = window.setTimeout(() => {
        showToast(`Demo：准备启动 ${item.title}`);
      }, 400);
    }
  }
});

surface.addEventListener("contextmenu", (event) => {
  event.preventDefault();
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
  window.clearTimeout(launchClickTimer);
  const item = currentPoster().items[Number(tile.dataset.itemIndex)];
  if (item) showToast(`Demo：准备启动 ${item.title}，并标记为最近活跃`);
});

window.addEventListener("resize", scheduleNativeSurfaceSync);

document.addEventListener("visibilitychange", () => {
  applyRuntimePresentation();
});

async function connectNativeControls() {
  const listen = window.__TAURI__?.event?.listen;
  if (!listen) return;
  const snapshot = await invokeNative("runtime_state");
  if (snapshot) applyRuntimePauseState(snapshot);
  await listen("runtime-state-changed", (event) => {
    applyRuntimePauseState(event.payload ?? {});
  });
  await listen(POSTER_STATE_UPDATED_EVENT, (event) => {
    if (!posterState.acceptSnapshot(event.payload)) return;
    const next = runtimeFromPosterState();
    runtime = applyPosterAction(next, { type: "applyRuntimeSnapshot", snapshot: runtime });
    document.body.dataset.posterStateStatus = "ready";
    render();
    syncNativeSurface();
  });
}

async function bootstrap() {
  try {
    runtime = await loadRuntime();
  } catch {
    runtime = createRuntimeState(createEphemeralPosterRuntimeSnapshot());
    document.body.dataset.posterStateStatus = "rejected";
    showToast("正式海报状态不可用；当前仅显示未持久化的安全预览");
  }
  render();
  await connectNativeControls().catch(() => {});
  syncNativeSurface();
  syncFpsSampling();
}

bootstrap();
