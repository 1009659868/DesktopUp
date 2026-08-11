import {
  DENSITY_LABELS,
  initials,
  moveWithinBounds,
  resizeWithinBounds
} from "./model.js";
import {
  buildPosterMarkup
} from "./poster-core.js";
import {
  LEGACY_POSTER_STORAGE_KEY,
  LEGACY_DEFAULT_CATALOG,
  createLegacyDefaultSnapshot,
  isLegacyPosterSnapshot,
  readLegacyPosterSnapshot,
  readSharedLegacyPosterSnapshot,
  toLegacyDemoItem,
  writeLegacyPosterSnapshot,
  writeSharedLegacyPosterSnapshot
} from "./poster-legacy-bridge.js";
import {
  applyPosterAction,
  buildRuntimeSyncArgs,
  createRuntimeState,
  isRuntimeSuspended,
  runtimeMode,
  posterInteractionMode,
  posterSnapshot,
  selectedPoster as runtimeSelectedPoster
} from "./poster-runtime.js";
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

let state = createRuntimeState(createLegacyDefaultSnapshot());
let gesture = null;
let toastTimer = null;
let launchClickTimer = null;
const PERFORMANCE_SAMPLE_INTERVAL_MS = 2000;
const PERFORMANCE_SAMPLE_COUNT = 6;
const PERFORMANCE_MAIN_PRIVATE_WORKING_SET_BUDGET_BYTES = 50 * 1024 * 1024;
const PERFORMANCE_TREE_PRIVATE_WORKING_SET_BUDGET_BYTES = 256 * 1024 * 1024;
let performanceCollecting = false;

function nativeInvoke() {
  return window.__TAURI__?.core?.invoke;
}

async function loadState() {
  const snapshot = await readSharedLegacyPosterSnapshot(localStorage, nativeInvoke());
  return createRuntimeState(snapshot ?? createLegacyDefaultSnapshot());
}

function saveState() {
  void writeSharedLegacyPosterSnapshot(localStorage, nativeInvoke(), posterSnapshot(state));
}

function selectedPoster() {
  return runtimeSelectedPoster(state);
}

function render() {
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

  renderPosters();
  renderInspector();
  renderRuntime();
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
  pauseToggle.setAttribute("aria-pressed", String(state.manuallyPaused));
  renderRuntime();
  app.classList.toggle("is-paused", state.manuallyPaused || isRuntimeSuspended(state, document.hidden));
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
    rect: { x: poster.x, y: poster.y, width: poster.width, height: poster.height }
  };
  posterElement.classList.add("is-manipulating");
});

window.addEventListener("pointermove", (event) => {
  if (!gesture || event.pointerId !== gesture.pointerId) return;
  const dx = event.clientX - gesture.originX;
  const dy = event.clientY - gesture.originY;
  const posterElement = posterLayer.querySelector(`[data-poster-id="${gesture.posterId}"]`);
  if (!posterElement) return;

  if (gesture.type === "move") {
    const next = moveWithinBounds(gesture.rect, stage.getBoundingClientRect(), {
      x: gesture.rect.x + dx,
      y: gesture.rect.y + dy
    });
    posterElement.style.setProperty("--poster-x", `${next.x}px`);
    posterElement.style.setProperty("--poster-y", `${next.y}px`);
  } else {
    const next = resizeWithinBounds(gesture.rect, {
      width: gesture.rect.width + dx,
      height: gesture.rect.height + dy
    });
    posterElement.style.setProperty("--poster-width", `${next.width}px`);
    posterElement.style.setProperty("--poster-height", `${next.height}px`);
  }
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
      type: "movePoster",
      posterId: gesture.posterId,
      stage: stage.getBoundingClientRect(),
      next: {
        x: Number.parseFloat(posterElement.style.getPropertyValue("--poster-x")),
        y: Number.parseFloat(posterElement.style.getPropertyValue("--poster-y"))
      }
    });
  } else {
    state = applyPosterAction(state, {
      type: "resizePoster",
      posterId: gesture.posterId,
      next: {
        width: Number.parseFloat(posterElement.style.getPropertyValue("--poster-width")),
        height: Number.parseFloat(posterElement.style.getPropertyValue("--poster-height"))
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

document.querySelector("#reset-layout").addEventListener("click", () => {
  state = createRuntimeState(createLegacyDefaultSnapshot());
  localStorage.removeItem(LEGACY_POSTER_STORAGE_KEY);
  render();
  showToast("画布已恢复到 Demo 默认布局");
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
  showToast(autostartControl.checked ? "Demo：开机自启已标记为开启" : "Demo：开机自启已标记为关闭");
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

window.addEventListener("storage", (event) => {
  if (event.key !== LEGACY_POSTER_STORAGE_KEY) return;
  const editMode = state.editMode;
  const manuallyPaused = state.manuallyPaused;
  const snapshot = readLegacyPosterSnapshot(localStorage) ?? createLegacyDefaultSnapshot();
  state = applyPosterAction(createRuntimeState(snapshot), { type: "setEditMode", value: editMode });
  state = applyPosterAction(state, { type: "setManualPaused", value: manuallyPaused });
  render();
});

async function connectNativeControls() {
  const listen = window.__TAURI__?.event?.listen;
  if (!listen) return;
  const invoke = window.__TAURI__?.core?.invoke;
  if (invoke) {
    try {
      applyNativePauseSnapshot(await invoke("runtime_state"));
    } catch {}
  }
  await listen("runtime-state-changed", (event) => {
    applyNativePauseSnapshot(event.payload ?? {});
  });
  await listen("poster-snapshot-updated", (event) => {
    if (!isLegacyPosterSnapshot(event.payload)) return;
    const editMode = state.editMode;
    const manuallyPaused = state.manuallyPaused;
    writeLegacyPosterSnapshot(localStorage, event.payload);
    state = applyPosterAction(createRuntimeState(event.payload), {
      type: "setEditMode",
      value: editMode
    });
    state = applyPosterAction(state, { type: "setManualPaused", value: manuallyPaused });
    render();
  });
  await listen("runtime-control", (event) => {
    const action = String(event.payload ?? "");
    if (action === "toggle-edit") editToggle.click();
    if (action === "toggle-pause") pauseToggle.click();
    if (action === "toggle-lock-all") {
      const shouldLock = !state.posters.every((poster) => poster.locked);
      state = applyPosterAction(state, { type: "setAllLocked", locked: shouldLock });
      saveState();
      render();
      showToast(shouldLock ? "全部海报位置已锁定" : "全部海报位置已解锁");
    }
    if (action === "open-settings") {
      document.querySelector(".inspector")?.scrollIntoView({ behavior: "smooth", block: "start" });
      showToast("设置面板已打开");
    }
    if (action.startsWith("theme:")) {
      const theme = action.slice("theme:".length);
      updatePoster(selectedPoster().id, { theme });
      showToast("已从托盘切换快捷主题");
    }
    if (action === "open-agent") {
      showToast("Agent 未启动：请先在后续设置页填写 API Key");
    }
    if (action === "reconnect-icons") {
      showToast("安全 Demo：未读取或移动真实桌面快捷方式");
    }
  });
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
  try {
    state = await loadState();
  } catch {
    state = createRuntimeState(createLegacyDefaultSnapshot());
  }
  saveState();
  render();
  void pollPerformance();
  connectNativeControls().catch(() => {});
}

bootstrap();
