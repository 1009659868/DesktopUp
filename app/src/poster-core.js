import { gridColumns, normalizedPoster } from "./model.js";

export function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

export function buildPosterMarkup(poster, options = {}) {
  const source = normalizedPoster(poster);
  const {
    posterId = null,
    posterIndex = 0,
    isSelected = false,
    surface = "native",
    includeGeometry = surface === "demo",
    dragHandle = surface === "native" ? "data-native-drag" : "data-drag-handle",
    kickerLabel = surface === "native" ? "DESKTOP COLLECTION" : "COLLECTION",
    footerStatus = surface === "native"
      ? source.locked ? "POSITION LOCKED" : "DRAG TO MOVE"
      : "AUTO GRID"
  } = options;

  const columns = gridColumns(source.width, source.density);
  const classes = ["poster", `theme-${source.theme}`];
  if (source.collapsed) classes.push("is-collapsed");
  if (source.locked) classes.push("is-locked");
  if (isSelected) classes.push("is-selected");

  const styleParts = [
    `--poster-accent:${source.accent}`,
    `--poster-opacity:${source.opacity / 100}`,
    `--grid-columns:${columns}`
  ];
  if (includeGeometry) {
    styleParts.push(
      `--poster-x:${source.x}px`,
      `--poster-y:${source.y}px`,
      `--poster-width:${source.width}px`,
      `--poster-height:${source.height}px`,
      `--poster-index:${posterIndex}`
    );
  }

  const posterIdAttr = posterId == null ? "" : ` data-poster-id="${escapeHtml(posterId)}"`;
  const itemPosterIdAttr = posterId == null ? "" : ` data-poster-id="${escapeHtml(posterId)}"`;
  const games = source.items.map((item, index) => `
    <button class="game-tile hue-${escapeHtml(item.hue)}" type="button" data-action="launch"${itemPosterIdAttr}
      data-item-index="${index}" title="单击启动 ${escapeHtml(item.title)}，双击启动并标记最近活跃">
      <span class="game-cover"><i></i><strong>${escapeHtml(item.mark)}</strong><small>0${index + 1}</small></span>
      <span class="game-name">${escapeHtml(item.title)}</span>
    </button>
  `).join("");

  const resizeHandle = surface === "demo"
    ? `<button class="resize-handle" type="button" data-resize-handle aria-label="调整海报大小"></button>`
    : "";

  return `
    <article class="${classes.join(" ")}"${posterIdAttr} style="${styleParts.join(";")}">
      <div class="poster-atmosphere"><i></i><i></i><i></i></div>
      <header class="poster-header" ${dragHandle}>
        <div class="poster-kicker"><span>${kickerLabel}</span><i></i><small>0${posterIndex + 1}</small></div>
        <div class="poster-actions">
          <button class="poster-lock" type="button" data-action="lock" aria-label="${source.locked ? "解锁" : "锁定"}海报位置" aria-pressed="${source.locked}"><span></span></button>
          <button class="poster-collapse" type="button" data-action="collapse" aria-label="${source.collapsed ? "展开" : "收起"}海报"><span></span></button>
        </div>
      </header>
      <div class="poster-copy">
        <span>${escapeHtml(source.subtitle)}</span>
        <h2>${escapeHtml(source.title)}</h2>
        <p>${source.items.length.toString().padStart(2, "0")} TITLES <i></i> READY</p>
      </div>
      <div class="poster-content"><div class="game-grid">${games}</div></div>
      <footer class="poster-footer">
        <span>LOCAL LIBRARY</span>
        <span class="poster-bars"><i></i><i></i><i></i><i></i></span>
        <span>${footerStatus}</span>
      </footer>
      ${resizeHandle}
    </article>
  `;
}
