import {
  DENSITY_LABELS,
  initials
} from "./model.js";
import {
  containPosterInWorkArea,
  movePosterFromEditorDelta,
  projectPosterToEditor,
  resizePosterFromEditorDelta
} from "./poster-editor-geometry.js";
import {
  buildPosterMarkup
} from "./poster-core.js";
import {
  LEGACY_DEFAULT_CATALOG,
  toLegacyDemoItem
} from "./poster-legacy-bridge.js";
import {
  POSTER_STATE_UPDATED_EVENT,
  PosterStateClient,
  createEphemeralPosterRuntimeSnapshot
} from "./poster-state-store.js";
import {
  applyPosterAction,
  buildRuntimeSyncArgs,
  createRuntimeState,
  isRuntimeSuspended,
  runtimeMode,
  posterInteractionMode,
  selectedPoster as runtimeSelectedPoster
} from "./poster-runtime.js";
import {
  AUTOSTART_STATE_EVENT,
  autostartOutcomeMessage,
  deriveAutostartControl
} from "./autostart-control.js";
import {
  buildPerformanceSample,
  formatCpuMs,
  formatMemoryBytes,
  normalizePerformanceMode,
  performanceModeLabel
} from "./performance-monitor.js";

const app = document.querySelector("#app");
const stage = document.querySelector("#desktop-stage");
const posterLayer = document.querySelector("#poster-layer");
const editToggle = document.querySelector("#edit-toggle");
const pauseToggle = document.querySelector("#pause-toggle");
const runtimeChip = document.querySelector("#runtime-chip");
const perfChip = document.querySelector("#perf-chip");
const perfMemory = document.querySelector("#perf-memory");
const perfFps = document.querySelector("#perf-fps");
const perfProcesses = document.querySelector("#perf-processes");
const perfCpu = document.querySelector("#perf-cpu");
const perfState = document.querySelector("#perf-state");
const performanceModeList = document.querySelector("#performance-mode-list");
const perfCollect = document.querySelector("#perf-collect");
const toast = document.querySelector("#toast");
const accentControl = document.querySelector("#accent-control");
const opacityControl = document.querySelector("#opacity-control");
const densityControl = document.querySelector("#density-control");
const autoCollapseControl = document.querySelector("#auto-collapse");
const autostartControl = document.querySelector("#autostart-control");
const autostartStatus = document.querySelector("#autostart-status");

const posterState = new PosterStateClient({ invoke: nativeInvoke(), storage: localStorage });
let state = createRuntimeState(createEphemeralPosterRuntimeSnapshot());
let gesture = null;
let toastTimer = null;
let launchClickTimer = null;
const PERFORMANCE_SAMPLE_INTERVAL_MS = 2000;
const PERFORMANCE_SAMPLE_COUNT = 6;
const PERFORMANCE_MAIN_PRIVATE_WORKING_SET_BUDGET_BYTES = 50 * 1024 * 1024;
const PERFORMANCE_TREE_PRIVATE_WORKING_SET_BUDGET_BYTES = 256 * 1024 * 1024;
let performanceCollecting = false;
let autostartSnapshot = null;
let autostartPending = false;

function nativeInvoke() {
  return window.__TAURI__?.core?.invoke;
}

function applyAutostartSnapshot(snapshot = autostartSnapshot) {
  if (snapshot) autostartSnapshot = snapshot;
  const control = deriveAutostartControl(autostartSnapshot, autostartPending);
  autostartControl.checked = control.checked;
  autostartControl.disabled = control.disabled;
  autostartStatus.textContent = control.status;
  autostartControl.closest(".switch-row")?.setAttribute("data-autostart-state", control.state);
}

async function requestAutostartChange(enabled) {
  const invoke = nativeInvoke();
  if (!invoke || autostartPending) {
    applyAutostartSnapshot();
    return;
  }
  autostartPending = true;
  applyAutostartSnapshot();
  try {
    const result = await invoke("set_autostart_enabled", { enabled });
    applyAutostartSnapshot(result?.after ?? autostartSnapshot);
    showToast(autostartOutcomeMessage(result));
  } catch {
    try {
      applyAutostartSnapshot(await invoke("autostart_diagnostics"));
    } catch {
      applyAutostartSnapshot();
    }
    showToast("开机自启更改失败，界面已恢复为原生状态");
  } finally {
    autostartPending = false;
    applyAutostartSnapshot();
  }
}

async function loadState() {
  const result = await posterState.bootstrap();
  document.body.dataset.posterStateStatus = result.status;
  if (result.status !== "ready") {
    throw new Error(result.message ?? "poster state bootstrap rejected");
  }
  return createRuntimeState(posterState.runtimeSnapshot());
}

function saveState() {
  void posterState.saveRuntimeState(state).then((result) => {
    if (result?.status === "rejected") {
      document.body.dataset.posterStateStatus = "rejected";
      showToast("海报状态保存失败，原状态已保留");
    } else if (result?.status === "stored") {
      document.body.dataset.posterStateStatus = "ready";
    }
  });
}

function selectedPoster() {
  return runtimeSelectedPoster(state);
}

function editorGeometryContext() {
  const bounds = stage.getBoundingClientRect();
  const primaryWorkArea = posterState.primaryWorkArea ?? {
    width: Number(globalThis.screen?.availWidth) || bounds.width,
    height: Number(globalThis.screen?.availHeight) || bounds.height
  };
  return {
    stage: { width: bounds.width, height: bounds.height },
    workArea: {
      width: primaryWorkArea.width,
      height: primaryWorkArea.height
    }
  };
}

function applyEditorPosterProjection() {
  const context = editorGeometryContext();
  for (const poster of state.posters) {
    const posterElement = posterLayer.querySelector(`[data-poster-id="${poster.id}"]`);
    if (!posterElement) continue;
    const projection = projectPosterToEditor(poster, context);
    posterElement.style.setProperty("--poster-x", `${projection.x}px`);
    posterElement.style.setProperty("--poster-y", `${projection.y}px`);
    posterElement.style.setProperty("--poster-scale", String(projection.scale));
  }
}

function renderRuntimeControls() {
  app.classList.toggle("is-editing", state.editMode);
  app.classList.toggle("is-paused", state.manuallyPaused || isRuntimeSuspended(state, document.hidden));
  const pauseState = runtimeMode(state, document.hidden);
  app.classList.toggle("is-performance-static", pauseState === "activeStatic");
  app.classList.toggle("is-performance-animated", pauseState === "activeAnimated");
  app.classList.toggle("is-performance-paused", pauseState === "paused");
  app.classList.toggle("is-performance-stopped", pauseState === "stopped");
  editToggle.setAttribute("aria-pressed", String(state.editMode));
  editToggle.querySelector("span:last-child").textContent = state.editMode ? "完成编辑" : "进入编辑";
  pauseToggle.setAttribute("aria-pressed", String(state.manuallyPaused));
  renderRuntime();
}

function render() {
  renderRuntimeControls();
  renderPosters();
  renderInspector();
  syncNativeRuntimeState();
}

function syncNativeRuntimeState() {
  const invoke = window.__TAURI__?.core?.invoke;
  if (!invoke) return;
  invoke("update_runtime_state", buildRuntimeSyncArgs(state)).catch(() => {});
}

function renderPosters() {
  posterLayer.innerHTML = state.posters.map((poster, posterIndex) =>
    buildPosterMarkup(poster, {
      posterId: poster.id,
      posterIndex,
      isSelected: poster.id === state.selectedPosterId,
      surface: "demo"
    })
  ).join("");
  applyEditorPosterProjection();
}

function renderInspector() {
  const poster = selectedPoster();
  if (!poster) return;
  const posterIndex = state.posters.indexOf(poster);
  document.querySelector("#selection-index").textContent = String(posterIndex + 1).padStart(2, "0");
  document.querySelector("#selected-title").textContent = poster.title;
  document.querySelector("#selected-count").textContent = `${poster.items.length} 个游戏 · ${poster.collapsed ? "已收起" : "已展开"} · ${poster.locked ? "位置已锁定" : "位置可编辑"}`;
  const miniCover = document.querySelector("#mini-cover");
  miniCover.className = `mini-cover theme-${poster.theme}`;
  miniCover.style.setProperty("--poster-accent", poster.accent);
  miniCover.querySelector("span").textContent = initials(poster.title);
  accentControl.value = poster.accent;
  opacityControl.value = String(poster.opacity);
  densityControl.value = String(poster.density);
  autoCollapseControl.checked = poster.autoCollapse;
  document.querySelector("#accent-output").textContent = poster.accent.toUpperCase();
  document.querySelector("#opacity-output").textContent = `${poster.opacity}%`;
  document.querySelector("#density-output").textContent = DENSITY_LABELS[poster.density];
  document.querySelectorAll(".theme-option").forEach((button) => {
    button.classList.toggle("is-active", button.dataset.theme === poster.theme);
  });
}

function renderRuntime() {
  const runtimeSuspended = isRuntimeSuspended(state, document.hidden);
  const isPaused = state.manuallyPaused || runtimeSuspended;
  const automaticPauseReasons = state.pauseReasons;
  runtimeChip.classList.toggle("is-paused", isPaused);
  runtimeChip.querySelector("strong").textContent = isPaused ? "PAUSED" : "STATIC";
  runtimeChip.querySelector("small").textContent = state.manuallyPaused
    ? "手动暂停 · 0 FPS"
    : runtimeSuspended
    ? automaticPauseReasons.includes("displayOff")
      ? "显示器关闭 · 0 FPS"
      : automaticPauseReasons.includes("sessionLocked")
        ? "系统锁定 · 0 FPS"
        : automaticPauseReasons.includes("fullscreenForeground")
          ? "全屏覆盖 · 0 FPS"
          : automaticPauseReasons.includes("maximizedForeground")
            ? "最大化覆盖 · 0 FPS"
            : "窗口不可见 · 0 FPS"
    : "按需绘制";
}

function renderPerformance(snapshot = {}) {
  const memory = formatMemoryBytes(snapshot.privateWorkingSetBytes);
  const fps = snapshot.posterFps != null && snapshot.posterFps > 0
    ? `${Math.round(snapshot.posterFps)}`
    : "--";
  if (perfChip) {
    perfChip.querySelector("strong").textContent = memory;
    perfChip.querySelector("small").textContent = `FPS ${fps}`;
    perfChip.classList.toggle(
      "is-good",
      snapshot.privateWorkingSetBytes != null
        && snapshot.privateWorkingSetBytes <= PERFORMANCE_MAIN_PRIVATE_WORKING_SET_BUDGET_BYTES
        && snapshot.processTreePrivateWorkingSetBytes != null
        && snapshot.processTreePrivateWorkingSetBytes
          <= PERFORMANCE_TREE_PRIVATE_WORKING_SET_BUDGET_BYTES
        && snapshot.processTreeMeasurementComplete === true
    );
  }
  if (perfMemory) perfMemory.textContent = memory;
  if (perfFps) perfFps.textContent = fps;
  if (perfProcesses) perfProcesses.textContent = String(snapshot.processTreeProcessCount ?? "--");
  if (perfCpu) perfCpu.textContent = formatCpuMs(snapshot.cpuMs);
  if (perfState) perfState.textContent = performanceModeLabel(snapshot.pauseState ?? state.performanceMode);

  const pauseState = snapshot.pauseState ?? state.performanceMode;
  app.classList.toggle("is-performance-static", pauseState === "activeStatic");
  app.classList.toggle("is-performance-animated", pauseState === "activeAnimated");
  app.classList.toggle("is-performance-paused", pauseState === "paused");
  app.classList.toggle("is-performance-stopped", pauseState === "stopped");
}

async function pollPerformance() {
  const invoke = window.__TAURI__?.core?.invoke;
  if (!invoke) return;
  try {
    renderPerformance(await invoke("get_performance_snapshot"));
  } catch {
    /* backend snapshot not ready yet */
  }
}

async function collectPerformanceBaseline() {
  const invoke = window.__TAURI__?.core?.invoke;
  if (!invoke) {
    showToast("性能采样仅在 Tauri 环境可用");
    return;
  }
  if (performanceCollecting) return;
  performanceCollecting = true;
  perfCollect.disabled = true;
  perfCollect.textContent = "采集中...";
  const samples = [];
  try {
    for (let index = 0; index < PERFORMANCE_SAMPLE_COUNT; index += 1) {
      const snapshot = await invoke("get_performance_snapshot");
      samples.push(buildPerformanceSample(snapshot, state.performanceMode));
      renderPerformance(snapshot);
      if (index < PERFORMANCE_SAMPLE_COUNT - 1) {
        await new Promise((resolve) => setTimeout(resolve, PERFORMANCE_SAMPLE_INTERVAL_MS));
      }
    }
    const gate = await invoke("save_performance_baseline", { samples });
    showToast(gate?.passed ? "性能门通过，基线已保存" : "性能门未通过，已保存诊断证据");
  } catch {
    showToast("性能采样失败");
  } finally {
    performanceCollecting = false;
    perfCollect.disabled = false;
    perfCollect.textContent = "采集当前状态 10s";
  }
}

function applyPerformanceMode(mode) {
  state = applyPosterAction(state, {
    type: "setPerformanceMode",
    value: normalizePerformanceMode(mode)
  });
  render();
}

function applyNativePauseSnapshot(snapshot = {}) {
  state = applyPosterAction(state, { type: "applyRuntimeSnapshot", snapshot });
  renderRuntimeControls();
  void pollPerformance();
}

function updatePoster(id, patch) {
  state = applyPosterAction(state, { type: "patchPoster", posterId: id, patch });
  saveState();
  render();
}

function showToast(message) {
  clearTimeout(toastTimer);
  toast.textContent = message;
  toast.classList.add("is-visible");
  toastTimer = setTimeout(() => toast.classList.remove("is-visible"), 2600);
}

function selectPoster(id) {
  if (!state.posters.some((poster) => poster.id === id)) return;
  if (state.selectedPosterId === id) return;
  state = applyPosterAction(state, { type: "selectPoster", posterId: id });
  saveState();
  render();
}

posterLayer.addEventListener("click", (event) => {
  const posterElement = event.target.closest("[data-poster-id]");
  if (!posterElement) return;
  const id = posterElement.dataset.posterId;
  selectPoster(id);

  const action = event.target.closest("[data-action]")?.dataset.action;
  if (action === "launch") {
    const poster = state.posters.find((entry) => entry.id === id);
    const tile = event.target.closest('[data-action="launch"]');
    const item = poster?.items[Number(tile?.dataset.itemIndex)];
    if (!item) return;
    window.clearTimeout(launchClickTimer);
    if (posterInteractionMode(state, poster) === "editing") {
      if (event.detail === 1) showToast(`${item.title} 已选中，可继续排序或编辑`);
    } else if (event.detail === 1) {
      launchClickTimer = window.setTimeout(() => {
        showToast(`Demo：准备启动 ${item.title}`);
      }, 400);
    }
    return;
  }
  if (action === "collapse") {
    const poster = state.posters.find((entry) => entry.id === id);
    updatePoster(id, { collapsed: !poster.collapsed });
    showToast(poster.collapsed ? "海报已展开" : "海报已收起");
  }
  if (action === "lock") {
    const poster = state.posters.find((entry) => entry.id === id);
    updatePoster(id, { locked: !poster.locked });
    showToast(poster.locked ? "海报位置已解锁" : "海报位置已锁定");
  }
});

posterLayer.addEventListener("dblclick", (event) => {
  const tile = event.target.closest('[data-action="launch"]');
  if (!tile) return;
  window.clearTimeout(launchClickTimer);
  const poster = state.posters.find((entry) => entry.id === tile.dataset.posterId);
  const item = poster?.items[Number(tile.dataset.itemIndex)];
  if (item) showToast(`Demo：准备启动 ${item.title}，并标记为最近活跃`);
});

posterLayer.addEventListener("pointerdown", (event) => {
  const posterElement = event.target.closest(".poster");
  if (!posterElement) return;
  selectPoster(posterElement.dataset.posterId);
  if (!state.editMode) return;

  const poster = state.posters.find((entry) => entry.id === posterElement.dataset.posterId);
  const isResize = Boolean(event.target.closest("[data-resize-handle]"));
  const isDrag = Boolean(event.target.closest("[data-drag-handle]"));
  if (event.target.closest("button") && !isResize) return;
  if (!isResize && !isDrag) return;
  if (poster.locked) {
    showToast("该海报位置已锁定，请先点击标题栏锁按钮");
    return;
  }

  event.preventDefault();
  event.target.setPointerCapture?.(event.pointerId);
  gesture = {
    type: isResize ? "resize" : "move",
    pointerId: event.pointerId,
    posterId: poster.id,
    originX: event.clientX,
    originY: event.clientY,
    rect: {
      x: poster.x,
      y: poster.y,
      width: poster.width,
      height: poster.height,
      collapsed: poster.collapsed
    },
    context: editorGeometryContext()
  };
  gesture.projection = projectPosterToEditor(gesture.rect, gesture.context);
  gesture.nextRect = containPosterInWorkArea(gesture.rect, gesture.context.workArea);
  posterElement.classList.add("is-manipulating");
});

window.addEventListener("pointermove", (event) => {
  if (!gesture || event.pointerId !== gesture.pointerId) return;
  const dx = event.clientX - gesture.originX;
  const dy = event.clientY - gesture.originY;
  const posterElement = posterLayer.querySelector(`[data-poster-id="${gesture.posterId}"]`);
  if (!posterElement) return;

  if (gesture.type === "move") {
    gesture.nextRect = movePosterFromEditorDelta(
      gesture.rect,
      { x: dx, y: dy },
      gesture.projection,
      gesture.context.workArea
    );
  } else {
    gesture.nextRect = resizePosterFromEditorDelta(
      gesture.rect,
      { x: dx, y: dy },
      gesture.projection,
      gesture.context.workArea
    );
    posterElement.style.setProperty("--poster-width", `${gesture.nextRect.width}px`);
    posterElement.style.setProperty("--poster-height", `${gesture.nextRect.height}px`);
  }
  const preview = projectPosterToEditor(gesture.nextRect, {
    ...gesture.context,
    scale: gesture.projection.scale
  });
  posterElement.style.setProperty("--poster-x", `${preview.x}px`);
  posterElement.style.setProperty("--poster-y", `${preview.y}px`);
  posterElement.style.setProperty("--poster-scale", String(preview.scale));
});

window.addEventListener("pointerup", (event) => {
  if (!gesture || event.pointerId !== gesture.pointerId) return;
  const posterElement = posterLayer.querySelector(`[data-poster-id="${gesture.posterId}"]`);
  posterElement?.classList.remove("is-manipulating");
  if (!posterElement) {
    gesture = null;
    return;
  }

  if (gesture.type === "move") {
    state = applyPosterAction(state, {
      type: "patchPoster",
      posterId: gesture.posterId,
      patch: {
        x: gesture.nextRect.x,
        y: gesture.nextRect.y
      }
    });
  } else {
    state = applyPosterAction(state, {
      type: "resizePoster",
      posterId: gesture.posterId,
      next: {
        width: gesture.nextRect.width,
        height: gesture.nextRect.height
      }
    });
    state = applyPosterAction(state, {
      type: "patchPoster",
      posterId: gesture.posterId,
      patch: {
        x: gesture.nextRect.x,
        y: gesture.nextRect.y
      }
    });
  }
  saveState();
  render();
  gesture = null;
});

editToggle.addEventListener("click", () => {
  state = applyPosterAction(state, { type: "toggleEditMode" });
  render();
  showToast(state.editMode ? "编辑模式：拖动标题栏，右下角调整尺寸" : "布局已锁定");
});

pauseToggle.addEventListener("click", () => {
  state = applyPosterAction(state, { type: "toggleManualPause" });
  render();
  showToast(state.manuallyPaused ? "动态效果已暂停" : "动态效果已恢复");
});

document.querySelector("#reset-layout").addEventListener("click", async () => {
  const result = await posterState.resetDefault();
  if (result.status !== "ready") {
    showToast("默认布局写入失败，原状态已保留");
    return;
  }
  state = createRuntimeState(posterState.runtimeSnapshot());
  document.body.dataset.posterStateStatus = "ready";
  render();
  showToast("画布已恢复到正式默认布局");
});

document.querySelector("#theme-list").addEventListener("click", (event) => {
  const option = event.target.closest("[data-theme]");
  if (!option) return;
  updatePoster(selectedPoster().id, { theme: option.dataset.theme });
});

accentControl.addEventListener("input", () => updatePoster(selectedPoster().id, { accent: accentControl.value }));
opacityControl.addEventListener("input", () => updatePoster(selectedPoster().id, { opacity: Number(opacityControl.value) }));
densityControl.addEventListener("input", () => updatePoster(selectedPoster().id, { density: Number(densityControl.value) }));
autoCollapseControl.addEventListener("change", () => {
  updatePoster(selectedPoster().id, { autoCollapse: autoCollapseControl.checked });
  showToast(autoCollapseControl.checked ? "已开启自动收起演示状态" : "已关闭自动收起");
});
autostartControl.addEventListener("change", () => {
  const requestedEnabled = autostartControl.checked;
  applyAutostartSnapshot();
  void requestAutostartChange(requestedEnabled);
});

performanceModeList.addEventListener("click", (event) => {
  const button = event.target.closest("[data-performance-mode]");
  if (!button) return;
  performanceModeList.querySelectorAll("[data-performance-mode]").forEach((option) => {
    option.classList.toggle("is-active", option === button);
  });
  applyPerformanceMode(button.dataset.performanceMode);
});

perfCollect.addEventListener("click", collectPerformanceBaseline);

document.querySelector("#add-game").addEventListener("click", () => {
  const poster = selectedPoster();
  const catalogEntry = LEGACY_DEFAULT_CATALOG[poster.items.length % LEGACY_DEFAULT_CATALOG.length];
  const nextItem = toLegacyDemoItem(catalogEntry, poster.items.length);
  nextItem.id = `${nextItem.id}-${Date.now()}`;
  updatePoster(poster.id, { collapsed: false, items: [...poster.items, nextItem] });
  showToast(`${nextItem.title} 已加入，布局已自动更新`);
});

document.addEventListener("visibilitychange", () => {
  renderRuntime();
  app.classList.toggle("is-paused", state.manuallyPaused || isRuntimeSuspended(state, document.hidden));
});

async function connectNativeControls() {
  const listen = window.__TAURI__?.event?.listen;
  const invoke = window.__TAURI__?.core?.invoke;
  if (listen) {
    await listen("runtime-state-changed", (event) => {
      applyNativePauseSnapshot(event.payload ?? {});
    });
    await listen(AUTOSTART_STATE_EVENT, (event) => {
      applyAutostartSnapshot(event.payload ?? null);
    });
    await listen(POSTER_STATE_UPDATED_EVENT, (event) => {
      if (!posterState.acceptSnapshot(event.payload)) return;
      const editMode = state.editMode;
      const manuallyPaused = state.manuallyPaused;
      state = applyPosterAction(createRuntimeState(posterState.runtimeSnapshot()), {
        type: "setEditMode",
        value: editMode
      });
      state = applyPosterAction(state, { type: "setManualPaused", value: manuallyPaused });
      document.body.dataset.posterStateStatus = "ready";
      render();
    });
    await listen("runtime-control", (event) => {
      const action = String(event.payload ?? "");
      if (action === "open-settings") {
        document.querySelector(".inspector")?.scrollIntoView({ behavior: "smooth", block: "start" });
        showToast("设置面板已打开");
      }
      if (action === "open-agent") {
        showToast("Agent 未启动：请先在后续设置页填写 API Key");
      }
    });
  }
  if (invoke) {
    try {
      applyNativePauseSnapshot(await invoke("runtime_state"));
    } catch {}
    try {
      applyAutostartSnapshot(await invoke("autostart_diagnostics"));
    } catch {
      applyAutostartSnapshot();
    }
  }
  return Boolean(listen && invoke);
}
window.addEventListener("keydown", (event) => {
  const activeTag = document.activeElement?.tagName;
  if (["INPUT", "TEXTAREA", "SELECT"].includes(activeTag)) return;
  if (event.code === "KeyE") editToggle.click();
  if (event.code === "Space") {
    event.preventDefault();
    pauseToggle.click();
  }
});

async function bootstrap() {
  applyAutostartSnapshot();
  try {
    state = await loadState();
  } catch {
    state = createRuntimeState(createEphemeralPosterRuntimeSnapshot());
    document.body.dataset.posterStateStatus = "rejected";
    showToast("正式海报状态不可用；当前仅显示未持久化的安全预览");
  }
  let nativeControlsConnected = false;
  try {
    nativeControlsConnected = await connectNativeControls();
  } catch {}
  render();
  void pollPerformance();
  if (nativeControlsConnected) {
    const invoke = nativeInvoke();
    try {
      await invoke("mark_control_center_ready");
    } catch {}
  }
}

if (typeof ResizeObserver === "function") {
  new ResizeObserver(() => applyEditorPosterProjection()).observe(stage);
}

bootstrap();
