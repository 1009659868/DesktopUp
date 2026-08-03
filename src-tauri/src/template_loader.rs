#[cfg(target_os = "windows")]
mod platform {
    use std::path::PathBuf;

    pub fn resolve_template_resource(template_key: &str, filename: &str) -> Option<PathBuf> {
        // Built-in templates ship next to the executable in the "app/templates" folder.
        let exe = std::env::current_exe().ok()?;
        let base = exe.parent()?;
        let resources = base.join("../dist/templates");
        let dev = base.join("../../../app/templates");

        for root in [&resources, &dev] {
            let candidate = root.join(template_key).join(filename);
            if candidate.exists() {
                return Some(candidate);
            }
        }
        None
    }

    fn scan_css(text: &str) -> String {
        let mut result = String::with_capacity(text.len());
        let mut chars = text.chars().peekable();
        while let Some(ch) = chars.next() {
            if ch == 'u' && text[result.len()..].starts_with("url(") {
                result.push_str("url(about:invalid)");
                let skip = &text[result.len()..];
                if let Some(end) = skip.find(')') {
                    let _ = chars.nth(end);
                }
            } else if ch == '@' && text[result.len()..].starts_with("@import") {
                result.push_str("/* @import blocked */");
                while let Some(&next) = chars.peek() {
                    if next == ';' || next == '{' { break; }
                    chars.next();
                }
            } else {
                result.push(ch);
            }
        }
        result
    }

    fn scan_html(text: &str) -> String {
        text.replace("<script", "<!--script")
            .replace("</script>", "</script-->")
            .replace("<iframe", "<!--iframe")
            .replace("</iframe>", "</iframe-->")
            .replace("<object", "<!--object")
            .replace("</object>", "</object-->")
            .replace("<embed", "<!--embed")
            .replace("</embed>", "</embed-->")
    }

    pub fn load_template_bundle(template_key: &str) -> Result<String, String> {
        let manifest_path = resolve_template_resource(template_key, "manifest.json")
            .ok_or_else(|| format!("Manifest not found for template '{}'", template_key))?;
        let template_html = resolve_template_resource(template_key, "template.html")
            .ok_or_else(|| format!("template.html not found for '{}'", template_key))?;
        let template_css = resolve_template_resource(template_key, "template.css")
            .ok_or_else(|| format!("template.css not found for '{}'", template_key))?;

        let manifest_raw = std::fs::read_to_string(&manifest_path)
            .map_err(|e| format!("Failed to read manifest: {}", e))?;
        let html_raw = std::fs::read_to_string(&template_html)
            .map_err(|e| format!("Failed to read template.html: {}", e))?;
        let css_raw = std::fs::read_to_string(&template_css)
            .map_err(|e| format!("Failed to read template.css: {}", e))?;

        let manifest: serde_json::Value = serde_json::from_str(&manifest_raw)
            .map_err(|e| format!("Invalid manifest.json: {}", e))?;

        let safe_html = scan_html(&html_raw);
        let safe_css = scan_css(&css_raw);

        let bundle = serde_json::json!({
            "template_key": template_key,
            "version": manifest.get("version").and_then(|v| v.as_str()).unwrap_or("1.0.0"),
            "html": safe_html,
            "css": safe_css,
            "manifest": manifest,
            "params_schema": load_params_schema(template_key).unwrap_or_else(|_| serde_json::json!([])),
            "assets": {}
        });

        serde_json::to_string(&bundle).map_err(|e| format!("Serialization failed: {}", e))
    }

    fn load_params_schema(template_key: &str) -> Result<serde_json::Value, String> {
        let path = resolve_template_resource(template_key, "params-schema.json")
            .ok_or_else(|| format!("params-schema.json not found for '{}'", template_key))?;
        let raw = std::fs::read_to_string(&path).map_err(|e| format!("{}", e))?;
        serde_json::from_str(&raw).map_err(|e| format!("Invalid params-schema: {}", e))
    }

    pub fn load_index() -> Result<String, String> {
        let builtin = std::env::current_dir().ok()
            .and_then(|d| d.parent().map(|p| p.join("app/templates/_builtin_index.json")))
            .or_else(|| resolve_template_resource("..", "_builtin_index.json").map(|p| {
                let mut p = p;
                p.set_file_name("_builtin_index.json");
                p
            }));

        let candidates = [
            std::env::current_exe().ok().and_then(|e| e.parent().map(|p| p.join("../dist/templates/_builtin_index.json"))),
            std::env::current_exe().ok().and_then(|e| e.parent().map(|p| p.join("../../../app/templates/_builtin_index.json"))),
            builtin,
        ];

        for candidate in candidates.into_iter().flatten() {
            if candidate.exists() {
                return std::fs::read_to_string(&candidate).map_err(|e| e.to_string());
            }
        }
        Err("_builtin_index.json not found".to_string())
    }
}

#[cfg(target_os = "windows")]
pub use platform::{load_template_bundle, load_index};

#[cfg(not(target_os = "windows"))]
pub fn load_template_bundle(_template_key: &str) -> Result<String, String> {
    Err("Template loading is only supported on Windows".to_string())
}

#[cfg(not(target_os = "windows"))]
pub fn load_index() -> Result<String, String> {
    Ok("[]".to_string())
}
