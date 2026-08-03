const SCHEMA_CACHE = new Map();

function requireTemplateBundle(bundle) {
  if (!bundle || typeof bundle !== "object") throw new TypeError("模板包缺失");
  if (typeof bundle.html !== "string" || bundle.html.length === 0) throw new Error("模板 HTML 缺失");
  if (typeof bundle.css !== "string") throw new Error("模板 CSS 缺失");
  if (!bundle.template_key) throw new Error("模板 template_key 缺失");
  return bundle;
}

function interpolate(html, data) {
  return html.replaceAll(/\{\{([^}]+)\}\}/g, (_match, expression) => {
    const trimmed = expression.trim();

    if (trimmed.includes("===") || trimmed.includes("!==") || trimmed.includes(" ? ") || trimmed.includes(" : ")) {
      const ternaryMatch = trimmed.match(/^([^?]+)\s*\?\s*(.+)\s*:\s*(.+)$/);
      if (ternaryMatch) {
        const condition = ternaryMatch[1].trim().replaceAll(
          /poster\.(\w+)|'([^']*)'/g,
          (_m, prop, str) => str !== undefined ? JSON.stringify(str) : `data["${prop}"]`
        );
        const trueVal = JSON.parse(ternaryMatch[2].trim().replaceAll("'", '"'));
        const falseVal = JSON.parse(ternaryMatch[3].trim().replaceAll("'", '"'));
        try {
          const fn = new Function("data", `return (${condition}) ? trueVal : falseVal`);
          return String(fn(data)).replaceAll("trueVal", trueVal).replaceAll("falseVal", falseVal);
        } catch {
          return "";
        }
      }
      return "";
    }

    const safeExpr = trimmed.replaceAll(
      /poster\.(\w+)/g,
      (_m, prop) => `"${data[prop] ?? ""}"`
    );

    const parts = safeExpr.split(".");
    let value = data;
    for (const part of parts) {
      if (value == null) break;
      value = value[part.trim()];
    }
    return value != null ? String(value) : "";
  });
}

function injectTemplateCSSVariables(container, templateKey, params = {}) {
  const vars = {};
  for (const [key, value] of Object.entries(params)) {
    if (key.startsWith("--tpl-")) {
      vars[key] = value;
    }
  }
  container.style.cssText = Object.entries(vars).map(([k, v]) => `${k}:${v}`).join(";");
}

function switchTemplateCSSClass(element, oldKey, newKey) {
  if (oldKey) element.classList.remove(`tpl-${oldKey}`);
  element.classList.add(`tpl-${newKey}`);
}

function applyUserSlotOverrides(html, slotOverrides) {
  if (!slotOverrides || typeof slotOverrides !== "object") return html;
  let result = html;
  for (const [slotName, overrideHTML] of Object.entries(slotOverrides)) {
    const slotRegex = new RegExp(
      `<[^>]+\\sdata-slot="${slotName}"[^>]*>[\\s\\S]*?<\\/[^>]+>`,
      "g"
    );
    result = result.replace(slotRegex, (_match) => {
      const tagMatch = _match.match(/^<([^\s>]+)/);
      const tag = tagMatch ? tagMatch[1] : "div";
      const attrMatch = _match.match(/class="([^"]*)"/);
      const originalClass = attrMatch ? attrMatch[1] : "";
      const extraAttrs = _match.match(/style="([^"]*)"/);
      const styleAttr = extraAttrs ? ` style="${extraAttrs[1]}"` : "";
      return `<${tag} class="${originalClass}" data-slot="${slotName}"${styleAttr}>${overrideHTML}</${tag}>`;
    });
  }
  return result;
}

function compileTemplateCSS(cssText, params = {}) {
  const inferred = {};
  for (const [key, value] of Object.entries(params)) {
    if (key.startsWith("--tpl-")) {
      inferred[key] = value;
    }
  }
  let result = cssText;
  for (const [key, value] of Object.entries(inferred)) {
    const regex = new RegExp(`var\\(${key.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}\\s*,\\s*[^)]+\\)`, 'g');
    result = result.replace(regex, `var(${key}, ${value})`);
  }
  return result;
}

async function loadManifest(templateBasePath, templateKey) {
  try {
    const resp = await fetch(`${templateBasePath}/${templateKey}/manifest.json`);
    return await resp.json();
  } catch {
    return null;
  }
}

async function loadParamSchema(templateBasePath, templateKey) {
  try {
    const resp = await fetch(`${templateBasePath}/${templateKey}/params-schema.json`);
    return await resp.json();
  } catch {
    return [];
  }
}

async function loadTemplateBundle(templateKey, templateBasePath = "/templates") {
  const manifest = await loadManifest(templateBasePath, templateKey);
  if (!manifest) throw new Error(`模板 "${templateKey}" 的 manifest 加载失败`);

  const [htmlResp, cssResp, schema] = await Promise.all([
    fetch(`${templateBasePath}/${templateKey}/template.html`),
    fetch(`${templateBasePath}/${templateKey}/template.css`),
    loadParamSchema(templateBasePath, templateKey)
  ]);

  const html = await htmlResp.text();
  const css = await cssResp.text();

  const bundle = {
    template_id: `builtin-${templateKey}`,
    template_key: templateKey,
    version: manifest.version || "1.0.0",
    html,
    css,
    manifest,
    params_schema: schema,
    assets: {}
  };

  SCHEMA_CACHE.set(templateKey, bundle);
  return bundle;
}

export {
  requireTemplateBundle,
  interpolate,
  injectTemplateCSSVariables,
  switchTemplateCSSClass,
  applyUserSlotOverrides,
  compileTemplateCSS,
  loadTemplateBundle,
  SCHEMA_CACHE
};
