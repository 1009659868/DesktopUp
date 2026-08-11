# C-PCB-02 几何契约与 C-PCB-03 激活

日期：2026-08-11

状态：`C-PCB-02 COMPLETE / C-PCB-03 ACTIVE`

## 已冻结决策

- 默认几何只接受宿主提供的主显示器工作区和非空不透明 `displayId`；不在 C-PCB 内访问 Windows 显示器 API。
- 可见 Poster 的 top/right/bottom 安全边距均为 `24px`。
- 默认宽度为 `round(workArea.width × 0.22)`；默认下限 `280px`、绝对下限 `240px`、上限 `floor(workArea.width × 45%)`。
- expanded 默认/最大高度为 `floor(workArea.height - 48px)`，expanded 最小高度为 `240px`。
- collapsed 可见内容高度为 `60px`；宿主继续拥有四周 `10px` gutter，因此 collapsed host 高度为 `80px`。
- gutter 不进入 Poster 持久宽高。默认创建使用 `top-right` 初始锚点；收起、展开及后续呈现变化统一提交 `preserve`，保持同一 host top-left。

## 实施

- 新增 `app/src/poster-geometry.js`：提供产品常量、默认几何解析和 expanded/collapsed 宿主包络纯函数。
- 新增公开 Rust 同构模块 `src-tauri/src/poster_geometry.rs`；公开模块是后续 C-PSS/C-LST/C-PSI 可使用的契约入口，不产生未使用代码告警。
- `createDefaultPosterSnapshot` 改为要求主工作区证据并使用几何解析结果；A0 legacy adapter 继续走显式分流，不会自动重写现有用户数据。
- 新增 `tests/fixtures/poster-geometry-v1.json`，JS/Rust 共用 1366×768、1920×1080、4K、非主屏、过窄和过矮工作区判定。
- 契约样本名称由 `valid single-poster default` 修正为 `valid single-poster contract sample`，避免把固定 490×392 合法样本误称为工作区派生默认值。

## 验证

- C-PCB JS 专项：14/14 PASS（海报契约 10 项 + 几何契约 4 项）。
- `pnpm run check`：72/72 PASS；109 个 Markdown 文件链接有效。
- `cargo test --manifest-path src-tauri/Cargo.toml`：61/61 PASS。
- 新增 Rust dead-code 告警已消除；仅保留既有 MSVC 导入库 linker message。

## 边界

- 新几何契约尚未接管当前 A0/Release 表面投影，因此本切片没有改变现有海报视觉、输入、收起/展开或窗口层级，不需要新增 Release 人工验收。
- 当前发布认证仍只覆盖主屏单海报；未实现多海报创建，也不声称第二显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启或热插拔已经验证。

## 下一步

C-PCB-03 冻结 `USING/EDITING × expanded/collapsed × locked/unlocked` 标题栏权限矩阵，并以 JS/Rust 共用判定样本约束拖动、控件命中、项目启动和编辑能力。锁定状态仍必须允许解锁、收起/展开和启动项目；运行时接入继续留给 C-LST/C-PSI。
