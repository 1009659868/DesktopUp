import {
  DENSITY_LABELS,
  initials,
  moveWithinBounds,
  resizeWithinBounds
} from "./model.js";
import {
  STORAGE_KEY,
  buildPosterMarkup,
  createDefaultSnapshot,
  DEFAULT_CATALOG,
  readPosterSnapshot,
  toDemoItem,
  writePosterSnapshot
} from "./poster-core.js";
import {
  applyPosterAction,
  buildRuntimeSyncArgs,
  createRuntimeState,
  isRuntimeSuspended,
  posterSnapshot,
  selectedPoster as runtimeSelectedPoster
} from "./poster-runtime.js";

const app = document.querySelector("#app");
const stage = document.querySelector("#desktop-stage");
const posterLayer = document.querySelector("#poster-layer");
const editToggle = document.querySelector("#edit-toggle");
const pauseToggle = document.querySelector("#pause-toggle");
const runtimeChip = document.querySelector("#runtime-chip");
const toast = document.querySelector("#toast");
const accentControl = document.querySelector("#accent-control");
const opacityControl = document.querySelector("#opacity-control");
const densityControl = document.querySelector("#density-control");
const autoCollapseControl = document.querySelector("#auto-collapse");
const autostartControl = document.querySelector("#autostart-control");

let state = loadState();
let gesture = null;
let toastTimer = null;
let automaticallyPaused = false;
let automaticPauseReasons = [];

saveState();

function loadState() {
  return createRuntimeState(readPosterSnapshot(localStorage) ?? createDefaultSnapshot());
}

function saveState() {
  writePosterSnapshot(localStorage, posterSnapshot(state));
}

function selectedPoster() {
  return runtimeSelectedPoster(state);
}

function render() {
  app.classList.toggle("is-editing", state.editMode);
  app.classList.toggle("is-paused", state.manuallyPaused || isRuntimeSuspended(state, document.hidden));
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
  runtimeChip.classList.toggle("is-paused", isPaused);
  runtimeChip.querySelector("strong").textContent = isPaused ? "PAUSED" : "STATIC";
  runtimeChip.querySelector("small").textContent = runtimeSuspended
    ? automaticPauseReasons.includes("displayOff")
      ? "显示器关闭 · 0 FPS"
      : automaticPauseReasons.includes("sessionLocked")
        ? "系统锁定 · 0 FPS"
        : automaticPauseReasons.includes("fullscreenForeground")
          ? "全屏覆盖 · 0 FPS"
          : automaticPauseReasons.includes("maximizedForeground")
            ? "最大化覆盖 · 0 FPS"
            : "窗口不可见 · 0 FPS"
    : state.manuallyPaused
      ? "手动暂停 · 0 FPS"
      : "按需绘制";
}

function applyNativePauseSnapshot(snapshot = {}) {
  state = applyPosterAction(state, { type: "applyRuntimeSnapshot", snapshot });
  automaticallyPaused = state.automaticallyPaused;
  automaticPauseReasons = state.pauseReasons;
  pauseToggle.setAttribute("aria-pressed", String(state.manuallyPaused));
  renderRuntime();
  app.classList.toggle("is-paused", state.manuallyPaused || isRuntimeSuspended(state, document.hidden));
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
  const poster = state.posters.find((entry) => entry.id === tile.dataset.posterId);
  const item = poster?.items[Number(tile.dataset.itemIndex)];
  if (item) showToast(`Demo：准备启动 ${item.title}`);
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
  state = createRuntimeState(createDefaultSnapshot());
  localStorage.removeItem(STORAGE_KEY);
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

document.querySelector("#add-game").addEventListener("click", () => {
  const poster = selectedPoster();
  const catalogEntry = DEFAULT_CATALOG[poster.items.length % DEFAULT_CATALOG.length];
  const nextItem = toDemoItem(catalogEntry, poster.items.length);
  nextItem.id = `${nextItem.id}-${Date.now()}`;
  updatePoster(poster.id, { collapsed: false, items: [...poster.items, nextItem] });
  showToast(`${nextItem.title} 已加入，布局已自动更新`);
});

document.addEventListener("visibilitychange", () => {
  state = applyPosterAction(state, {
    type: "applyRuntimeSnapshot",
    snapshot: {
      automaticallyPaused,
      pauseReasons: automaticPauseReasons
    }
  });
  renderRuntime();
  app.classList.toggle("is-paused", state.manuallyPaused || isRuntimeSuspended(state, document.hidden));
});

window.addEventListener("storage", (event) => {
  if (event.key !== STORAGE_KEY) return;
  const editMode = state.editMode;
  const manuallyPaused = state.manuallyPaused;
  state = applyPosterAction(loadState(), { type: "setEditMode", value: editMode });
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
  await listen("runtime-pause-state", (event) => {
    applyNativePauseSnapshot(event.payload ?? {});
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

connectNativeControls().catch(() => {});
render();
