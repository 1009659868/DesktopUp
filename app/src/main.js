import {
  DENSITY_LABELS,
  gridColumns,
  initials,
  moveWithinBounds,
  normalizedPoster,
  resizeWithinBounds
} from "./model.js";

const STORAGE_KEY = "desktop-up.a0-demo.v1";

const catalog = [
  ["AFTERLIGHT", "AL", "violet"],
  ["GHOST SIGNAL", "GS", "cyan"],
  ["IRON VEIL", "IV", "red"],
  ["LAST ORBIT", "LO", "amber"],
  ["SABLE RUN", "SR", "blue"],
  ["HOLLOW CROWN", "HC", "green"],
  ["RED SECTOR", "RS", "red"],
  ["DEEP SIGNAL", "DS", "cyan"],
  ["ASHLINE", "AS", "amber"]
];

const defaults = {
  editMode: false,
  manualPaused: false,
  runtimeSuspended: false,
  selectedPosterId: "night-ops",
  posters: [
    {
      id: "night-ops",
      title: "NIGHT OPS",
      subtitle: "TACTICAL ARCHIVE / 01",
      theme: "nocturne",
      accent: "#72e4ff",
      opacity: 94,
      density: 1,
      x: 72,
      y: 154,
      width: 490,
      height: 392,
      collapsed: false,
      locked: true,
      autoCollapse: false,
      items: catalog.slice(0, 6).map(toItem)
    },
    {
      id: "indie-arcade",
      title: "INDIE ARCADE",
      subtitle: "SMALL WORLDS / BIG IDEAS",
      theme: "ember",
      accent: "#ff735c",
      opacity: 92,
      density: 0,
      x: 610,
      y: 226,
      width: 376,
      height: 314,
      collapsed: true,
      locked: true,
      autoCollapse: false,
      items: catalog.slice(6).map(toItem)
    }
  ]
};

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

function toItem([title, mark, hue], index) {
  return { id: `${title.toLowerCase().replace(/\W+/g, "-")}-${index}`, title, mark, hue };
}

function loadState() {
  try {
    const stored = JSON.parse(localStorage.getItem(STORAGE_KEY));
    if (!stored || !Array.isArray(stored.posters)) return structuredClone(defaults);
    return {
      ...structuredClone(defaults),
      ...stored,
      editMode: false,
      manualPaused: false,
      runtimeSuspended: false,
      posters: stored.posters.map(normalizedPoster)
    };
  } catch {
    return structuredClone(defaults);
  }
}

function saveState() {
  const snapshot = {
    selectedPosterId: state.selectedPosterId,
    posters: state.posters
  };
  localStorage.setItem(STORAGE_KEY, JSON.stringify(snapshot));
}

function selectedPoster() {
  return state.posters.find((poster) => poster.id === state.selectedPosterId) ?? state.posters[0];
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function render() {
  app.classList.toggle("is-editing", state.editMode);
  app.classList.toggle("is-paused", state.manualPaused || state.runtimeSuspended);
  editToggle.setAttribute("aria-pressed", String(state.editMode));
  editToggle.querySelector("span:last-child").textContent = state.editMode ? "完成编辑" : "进入编辑";
  pauseToggle.setAttribute("aria-pressed", String(state.manualPaused));

  renderPosters();
  renderInspector();
  renderRuntime();
  syncNativeRuntimeState();
}

function syncNativeRuntimeState() {
  const invoke = window.__TAURI__?.core?.invoke;
  if (!invoke) return;
  invoke("update_runtime_state", {
    editMode: state.editMode,
    manuallyPaused: state.manualPaused,
    allLocked: state.posters.every((poster) => poster.locked)
  }).catch(() => {});
}

function renderPosters() {
  posterLayer.innerHTML = state.posters.map((poster, posterIndex) => {
    const columns = gridColumns(poster.width, poster.density);
    const isSelected = poster.id === state.selectedPosterId;
    const games = poster.items.map((item, index) => `
      <button class="game-tile hue-${escapeHtml(item.hue)}" type="button"
        data-action="launch" data-poster-id="${poster.id}" data-item-index="${index}"
        title="双击启动 ${escapeHtml(item.title)}">
        <span class="game-cover"><i></i><strong>${escapeHtml(item.mark)}</strong><small>0${index + 1}</small></span>
        <span class="game-name">${escapeHtml(item.title)}</span>
      </button>
    `).join("");

    return `
      <article class="poster theme-${poster.theme}${poster.collapsed ? " is-collapsed" : ""}${poster.locked ? " is-locked" : ""}${isSelected ? " is-selected" : ""}"
        data-poster-id="${poster.id}"
        style="--poster-x:${poster.x}px;--poster-y:${poster.y}px;--poster-width:${poster.width}px;--poster-height:${poster.height}px;--poster-accent:${poster.accent};--poster-opacity:${poster.opacity / 100};--grid-columns:${columns};--poster-index:${posterIndex}">
        <div class="poster-atmosphere"><i></i><i></i><i></i></div>
        <header class="poster-header" data-drag-handle>
          <div class="poster-kicker"><span>COLLECTION</span><i></i><small>0${posterIndex + 1}</small></div>
          <div class="poster-actions">
            <button class="poster-lock" type="button" data-action="lock" aria-label="${poster.locked ? "解锁" : "锁定"}海报位置" aria-pressed="${poster.locked}">
              <span></span>
            </button>
            <button class="poster-collapse" type="button" data-action="collapse" aria-label="${poster.collapsed ? "展开" : "收起"}海报">
              <span></span>
            </button>
          </div>
        </header>
        <div class="poster-copy">
          <span>${escapeHtml(poster.subtitle)}</span>
          <h2>${escapeHtml(poster.title)}</h2>
          <p>${poster.items.length.toString().padStart(2, "0")} TITLES <i></i> READY</p>
        </div>
        <div class="poster-content">
          <div class="game-grid">${games}</div>
        </div>
        <footer class="poster-footer">
          <span>LOCAL LIBRARY</span>
          <span class="poster-bars"><i></i><i></i><i></i><i></i></span>
          <span>AUTO GRID</span>
        </footer>
        <button class="resize-handle" type="button" data-resize-handle aria-label="调整海报大小"></button>
      </article>
    `;
  }).join("");
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
  const isPaused = state.manualPaused || state.runtimeSuspended;
  runtimeChip.classList.toggle("is-paused", isPaused);
  runtimeChip.querySelector("strong").textContent = isPaused ? "PAUSED" : "STATIC";
  runtimeChip.querySelector("small").textContent = state.runtimeSuspended
    ? automaticPauseReasons.includes("displayOff")
      ? "显示器关闭 · 0 FPS"
      : automaticPauseReasons.includes("sessionLocked")
        ? "系统锁定 · 0 FPS"
        : automaticPauseReasons.includes("fullscreenForeground")
          ? "全屏覆盖 · 0 FPS"
          : automaticPauseReasons.includes("maximizedForeground")
            ? "最大化覆盖 · 0 FPS"
            : "窗口不可见 · 0 FPS"
    : state.manualPaused
      ? "手动暂停 · 0 FPS"
      : "按需绘制";
}

function applyNativePauseSnapshot(snapshot = {}) {
  state.manualPaused = Boolean(snapshot.manuallyPaused);
  automaticallyPaused = Boolean(snapshot.automaticallyPaused);
  automaticPauseReasons = Array.isArray(snapshot.runtimeVisibility?.pauseReasons)
    ? snapshot.runtimeVisibility.pauseReasons
    : [];
  state.runtimeSuspended = automaticallyPaused || document.hidden;
  pauseToggle.setAttribute("aria-pressed", String(state.manualPaused));
  renderRuntime();
  app.classList.toggle("is-paused", state.manualPaused || state.runtimeSuspended);
}

function updatePoster(id, patch) {
  state.posters = state.posters.map((poster) => poster.id === id ? normalizedPoster({ ...poster, ...patch }) : poster);
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
  state.selectedPosterId = id;
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
    updatePoster(gesture.posterId, {
      x: Number.parseFloat(posterElement.style.getPropertyValue("--poster-x")),
      y: Number.parseFloat(posterElement.style.getPropertyValue("--poster-y"))
    });
  } else {
    updatePoster(gesture.posterId, {
      width: Number.parseFloat(posterElement.style.getPropertyValue("--poster-width")),
      height: Number.parseFloat(posterElement.style.getPropertyValue("--poster-height"))
    });
  }
  gesture = null;
});

editToggle.addEventListener("click", () => {
  state.editMode = !state.editMode;
  render();
  showToast(state.editMode ? "编辑模式：拖动标题栏，右下角调整尺寸" : "布局已锁定");
});

pauseToggle.addEventListener("click", () => {
  state.manualPaused = !state.manualPaused;
  render();
  showToast(state.manualPaused ? "动态效果已暂停" : "动态效果已恢复");
});

document.querySelector("#reset-layout").addEventListener("click", () => {
  state = structuredClone(defaults);
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
  const catalogEntry = catalog[poster.items.length % catalog.length];
  const nextItem = toItem(catalogEntry, poster.items.length);
  nextItem.id = `${nextItem.id}-${Date.now()}`;
  updatePoster(poster.id, { collapsed: false, items: [...poster.items, nextItem] });
  showToast(`${nextItem.title} 已加入，布局已自动更新`);
});

document.addEventListener("visibilitychange", () => {
  state.runtimeSuspended = automaticallyPaused || document.hidden;
  renderRuntime();
  app.classList.toggle("is-paused", state.manualPaused || state.runtimeSuspended);
});

window.addEventListener("storage", (event) => {
  if (event.key !== STORAGE_KEY) return;
  const editMode = state.editMode;
  const manualPaused = state.manualPaused;
  state = loadState();
  state.editMode = editMode;
  state.manualPaused = manualPaused;
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
      state.posters = state.posters.map((poster) => normalizedPoster({ ...poster, locked: shouldLock }));
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
