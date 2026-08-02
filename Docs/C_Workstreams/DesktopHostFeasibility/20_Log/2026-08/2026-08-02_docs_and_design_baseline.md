# 2026-08-02 文档与设计基线

状态：`COMPLETE`

## 目标

参考 TerrainGenerationPlugin 的 A/B/C 文档方法，为 Desktop-Up 建立更迭结构、首版设计、技术方案和制作计划。

## 完成事实

- 建立 A 项目、B 模块、C 工作流三级权威链；
- 每层使用 Catalog、Plan、Log、Sync 语义分区；
- 建立产品设计 V0.1、技术架构 V0.1 和交付计划 V0.1；
- 建立安装和一键卸载规则；
- 把桌面宿主可行性设为首个活跃工作流。

## 决策与理由

- 第一版采用自管理海报启动器，不把 Explorer 原生图标坐标作为产品数据源；原因是兼容性和可恢复性优先。
- 当前提议 Rust + Tauri 2 + Svelte；最终是否接受需由宿主和性能原型验证。
- 卸载恢复是产品数据安全链的一部分，不能推迟到发布末尾才设计。

## 验证证据

- 文档目录和权威入口已创建；
- 文档中未宣称存在应用代码、安装包或已测性能数据；
- 后续需要执行相对链接和索引一致性检查。

## 遗留问题

- 正式产品名和品牌未确定；
- 工程尚未初始化；
- 桌面挂载、输入穿透和暂停仍需真实 Windows 验证；
- 安装器技术细节需在工程建立后验证。

## 关联文档与代码

- [`PRODUCT_DESIGN_V0.1.md`](../../../../A_Project/10_Plan/PRODUCT_DESIGN_V0.1.md)
- [`DELIVERY_PLAN_V0.1.md`](../../../../A_Project/10_Plan/DELIVERY_PLAN_V0.1.md)
- [`RELEASE_AND_UNINSTALL_POLICY.md`](../../../../Rules/RELEASE_AND_UNINSTALL_POLICY.md)

当前没有代码锚点。
