# C-PCB-01 契约同构与 C-PCB-02 几何审计

日期：2026-08-11

状态：`C-PCB-01 COMPLETE / C-PCB-02 IN PROGRESS — GEOMETRY EVIDENCE AUDITED`

## C-PCB-01 实施结果

- 新增 JS `poster-contract-v1`：严格 envelope/Poster 字段、版本、revision、ID/选择关系、枚举、逻辑矩形、尺寸、自动收起、锁定、布局和模板校验。
- 默认创建只产生一个 `poster-primary`，默认状态为 `primary/top-right/expanded/unlocked/autoCollapse=false/transparent-default`；标题与几何要求调用者显式提供，避免把 A0 文案和尺寸误当产品默认。
- interaction/runtime 字段被正式持久边界拒绝；`minimal-fallback` 只能作为无副作用的临时渲染结果，不能写回用户 `templateKey`。
- A0 `theme/collapsed/locked/x/y` 只经显式 legacy adapter 转换；adapter 要求工作区证据，不修改原输入。
- 新增 Rust 同构类型与严格校验。带 `contractVersion` 的正式快照必须通过 v1 校验，不能再退回“存在 posters 数组”的 legacy 判断；无版本 A0 快照暂由独立 legacy 分支继续读取，不在本切片重写用户数据。
- JS 与 Rust 共用 `tests/fixtures/poster-contract-v1.json` 判定矩阵；它验证纯数据裁决，不声称已经验证多海报 UI。

## 自动验证

- JS C-PCB 专项：10/10 PASS；
- 项目全量 `pnpm run check`：68/68 PASS，107 份 Markdown 链接有效；
- Rust：59/59 PASS；
- `cargo fmt --check` 等价格式已经由 `cargo fmt` 应用；
- Rust 链接器仅报告既有 MSVC import library 信息性 warning。

## C-PCB-02 盒模型证据

设计目标与当前 A0 的两个高度不是同一契约：

- `POSTER_STATE_MACHINE_V0.1` 规定 COLLAPSED 只显示 `60px` 标题条；
- 当前 CSS 的收起海报是 `116px`，包含 `43px` header 以及第二行海报标题；
- 当前宿主再在内容四周添加 `10px` gutter，因此原生逻辑窗体收起高度为 `136px`；
- 所以 `116` 不是简单的“60 + gutter”，而是 A0 保留两行内容的过渡视觉。

用户此前验收的是收起/展开顶部不移动、无裁切和按钮命中，不能据此把 A0 的 116px 两行视觉提升为目标设计。C-PCB-02 应冻结不同层级的常量：可见标题条高度、Poster 内容高度、宿主 gutter 与最终宿主 envelope，并由测试保证收起/展开保持同一 top-left。

## 尚缺的几何决策

已经有权威值：

- 默认宽度 `max(workArea.width × 0.22, 280px)`；
- 最小宽度 `240px`；
- 最大宽度 `workArea.width × 45%`；
- 默认锚点 `primary/top-right`；
- 目标收起可见标题条 `60px`。

尚没有权威数值：

- top/right/bottom 安全边距的像素值；
- expanded 最小高度与绝对存储上限；
- 目标 60px 标题条是否另加当前 10px 宿主 gutter（按现有宿主分层，建议最终逻辑 envelope 为 80px）。

当前 `tauri.conf.json` 的 `x=72/y=154/490×392` 与 `model.js` 的 `240..1920/240..2160` 都属于 A0 过渡配置；在确认上述产品数值前不得静默提升为 C-PCB 正式默认。
