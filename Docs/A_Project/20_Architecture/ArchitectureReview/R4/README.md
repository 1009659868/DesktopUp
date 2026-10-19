# R4 现状与目标对照

历史待验快照：R4现已获用户确认并收口，当前结论见[收口后架构与原目标对照](../R4-Closed/README.md)。本目录图源及其指纹保留，不改写旧交付证据。

本图对应impl/architecture-foundation的未提交工作树，不绑定旧提交冒充当前代码。R1～R3人工已通过，R4代码/自动验证通过、集中人工待验。详细证据唯一入口：[实施计划第19节](../../../40_Implementation/Architecture/FOUNDATION_REMAINDER_PLAN.md#r4-composition-boundary)。

## 目标边界与实际入口

| 目标 | 当前入口 | 增量开发边界 |
| --- | --- | --- |
| 薄界面 | PosterVisualWindow、ControlCenterService/Window、App.Startup | 采集意图、控件与线程适配；文件副作用交应用用例 |
| 现有业务用例 | Application/Features/Content各用例、Ports | 复用原子事务、账本、身份和一次执行，不能建立第二事实库 |
| 海报内核 | PosterContentSession、ContentPresentationLayout、ContentInputGeometry | 草稿/提案/代次与同代帧，不查询SQLite、不持有HWND |
| 呈现与宿主 | PosterContentPresenter、ContentRasterBinding、ContentPublication | 增量绘制、有界暖图；窗口原生接纳与回滚，不新造输入算法 |
| 组合根 | App/Bootstrap/ApplicationComposition、App.Startup及其余四个组装文件 | 显式端口与按需所有权；不引入运行时Resolve |
| 持久化/平台 | Infrastructure原子适配、Windows/Content/LocalContentReferenceProbe | 不把OS探测塞入纯核心；Worker不读数据库 |
| 热度接入点 | ContentHeatSession、PosterContentSession、既有提案探针 | 下一功能只加事实幂等消费、策略、提案及组装；真实评分尚未实施 |

原生输入、窗口、Dispatcher、场景材质仍属WinUI宿主；WinUiPosterSessionFactory按可见代次创建内容会话/窗口，Presenter及栅格绑定由窗口拥有。它们不是应该搬到无UI核心的“残留”。图中“Presenter→原生帧提交”表示整体呈现发布链，由窗口协调，Presenter本身不调用Win32。

六程序集依赖：Domain无项目依赖；Application→Domain；Windows→Application/Domain；Infrastructure→Application/Domain/Windows；ContentWorker→Application/Windows；App引用前述程序集（Worker仅构建/载荷依赖）。图展示逻辑调用/数据流而非每条编译引用，因此应用用例“事务端口”箭头不代表直接引用Infrastructure。

## 尚未实现与保留议题

- 动态模板/形状、AI受限计划、独立更新器及真实热度评分仍未实现；本轮不声称整体目标平台完成。
- 后台扫描规模化OPT-CONTENT-SYNC-01仍待实测/设计，原监听与兜底周期保持。结构清理不等同于性能实测提升。
- 发布同步、安装包及真实登录自启继续暂停。本批只验证构建和Worker发布载荷，不做系统安装。
- 基础架构出口还需本轮集中人工及准入复核，不因图表通过而放行。

## 图表回执

- diagram_type: architecture
- output: G:/A_/Tools/DesktopUp_V2/out/architecture-r4-composition/current.architecture.html
- specification_sha256: 9d4cfe7ae8e3b0a3f94aca338f7e7deb6bf645293ab75022f4d81c5db3f8f888
- artifact_sha256: b838118e3b108c760cc6af982a72170ed6be54eb9052365bb8236bb7db5e4fe9
- validation: 9/9 showcase, 0 errors, 0 warnings
- browser_evidence: passed（显式使用本机Edge；1440×900、1600×1000、1920×1080、2048×1320均无溢出）
- visual_review: passed（查看2048×1320浅/深截图；无交叉遮挡、布局可读、无明显底部空带）
- correction_rounds: 2（标签避让；收窄右列以满足字号投影）
- 初次自动浏览器发现未找到默认Chrome；指定现有Edge后同一冻结HTML通过，未修改Skill或HTML。

复现：node C:/Users/allmightwen/.agents/skills/archify/bin/archify.mjs deliver architecture Docs/A_Project/20_Architecture/ArchitectureReview/R4/current.architecture.json out/architecture-r4-composition/current.architecture.html --quality showcase --json。图源冻结后未再编辑。
