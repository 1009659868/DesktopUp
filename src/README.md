# 源码目录导航

当前为7个生产工程，保持原项目依赖和命名空间。目录采用“工程分层 → 功能 → 职责”；根目录只保留必要入口与工程设置。文件归位不代表窗口类型已经拆分，也不计为性能优化。

## 从哪里找代码

| 需求 | 首先查看 |
| --- | --- |
| 启动、组装和退出 | App/Bootstrap、App/Lifecycle；平台实现见Windows/Lifecycle |
| 控制中心界面 | App/Views/ControlCenter；编辑规则见Application/Features/ControlCenter |
| 海报窗口和日常交互 | App/Views/Poster、App/Runtime/Posters |
| 内容显示和图标 | App/Presentation；Application/Features/Content/Icons、Layout |
| 新建、命名、删除、Win菜单 | Application/Features/Content/FileActions；Infrastructure/ContentActions |
| 拖入、拖出、跨海报 | Application/Features/Content/Transfers；Infrastructure/ContentTransfers；Windows/DragDrop |
| 文件观察、归还、位置迁移 | Application/Features/Content/Observation、Lifecycle、Storage；同职能存储适配 |
| 热度和布局模型 | Domain/Content；Application/Features/Content/Layout |
| 模板v1、参数与实例覆盖 | Domain/Templates；Application/Features/Templates |
| J1画板／草稿编辑 | Domain/Canvas；Application/Features/Canvas；App/Views/Canvas与Presentation/Canvas |
| 数据、事务与迁移 | Application/Features/State及Ports/State；Infrastructure/Persistence |
| Windows桌面与命中 | Windows/Desktop、Display、Posters；App/Runtime/Desktop |
| Shell及大图标进程 | ContentWorker/Shell、Icons；入口Program.cs |
| 安装与升级 | Setup/Installation、Upgrade；入口Program.cs |
| 内置灯／雨模块 | Modules/Lamp、Modules/Rain，分别含Domain／Application／App |

## 主要文件层级

```text
src/
├─ DesktopUp.App/
│  ├─ App.xaml / App.xaml.cs
│  ├─ Bootstrap/                  显式组装
│  ├─ Lifecycle/                  应用寿命、托盘
│  ├─ Features/{Content,Configuration}/
│  ├─ Runtime/{Desktop,Posters}/
│  ├─ Views/{ControlCenter,Poster,Recovery,Controls,Canvas,Creation}/
│  ├─ Presentation/               内容呈现及Canvas增量节点／内容槽适配
│  ├─ Scenes/
│  └─ Diagnostics/
├─ DesktopUp.Application/
│  ├─ Features/
│  │  ├─ Content/{Catalog,FileActions,Icons,Layout,Lifecycle,Observation,Open,Storage,Transfers}/
│  │  ├─ Posters/{AutoPosture,Input,Lifecycle,Presentation,Spatial,Title}/
│  │  └─ Configuration/ ControlCenter/ State/ Lifecycle/ Templates/ Scenes/ Canvas/ Creation/
│  └─ Ports/{Configuration,Content,Storage,State,Platform}/
├─ DesktopUp.Domain/
│  ├─ Content/                    内容实体与模型；Layout/、Transfers/
│  ├─ Posters/{Configuration,Presentation,Spatial,Title}/
│  └─ State/ Templates/ Lifecycle/ Compatibility/ Canvas/
├─ DesktopUp.Infrastructure/
│  ├─ ContentActions/ ContentIcons/ ContentLifecycle/ ContentObservation/ ContentTransfers/
│  ├─ Persistence/{Content,Migrations,Recovery,State,Transfers}/
│  └─ StorageLocation/ StorageOwnership/ Workers/ Configuration/ Lifecycle/ Diagnostics/ AI/
├─ DesktopUp.Windows/
│  └─ Content/ Desktop/ Display/ DragDrop/ Posters/ Dialogs/ Lifecycle/ Diagnostics/
├─ DesktopUp.ContentWorker/
│  └─ Program.cs / Icons/ / Shell/
└─ DesktopUp.Setup/
   └─ Program.cs / Installation/ / Upgrade/
```

## 画板与AI如何增量加入

- [Domain/Canvas](DesktopUp.Domain/Canvas/README.md)：声明和纯规则。
- [Application/Features/Canvas](DesktopUp.Application/Features/Canvas/README.md)：预览／应用／保存／撤销用例。
- [App/Presentation/Canvas](DesktopUp.App/Presentation/Canvas/README.md)：测量、增量呈现及同代几何适配。
- [Application/Features/Creation](DesktopUp.Application/Features/Creation/README.md)：窄创作会话与工具边界。
- [App/Views/Creation](DesktopUp.App/Views/Creation/README.md)：独立内置创作面板。
- [Infrastructure/AI](DesktopUp.Infrastructure/AI/README.md)：提供方传输与凭据适配。

Canvas三层已接入J1最小切片，独立编辑器归Views/Canvas；Creation与AI仍仅职责导航。沿[联合计划](../Docs/A_Project/30_Plans/POSTER_CANVAS_ENGINE_DESIGN_PLAN.md)逐步落地；不为目录对称增加接口，不把画板或AI业务堆入窗口分部文件。旧模板v1保持兼容；选择旧模板会显式退出当前画板，不并行拥有内容。

## 依赖与安全边界

Application→Domain；Infrastructure→Application/Domain/Windows；Windows→Application/Domain；ContentWorker→Application/Windows。App只在Bootstrap组装具体实现；App到Worker的项目引用仅保证构建独立进程。Setup独立，不引用业务工程。迁移001～027原字节冻结，未随目录整理移动或重写。

正式行为、统计与存储语义保持。普通构建不包含[tools/review](../tools/review/README.md)，验收观察代码只在显式review构建中编译；必要的生产诊断与兼容仍保留。目录整理没有拆分PosterVisualWindow等partial类型，后续只在触及职责时提取实际逻辑，不以拆文件数量替代解耦。

合同测试按功能归入tests/DesktopUp.Contracts.Tests子目录，支持类放Support；命名空间与测试筛选名不变。scripts现有公共命令及历史验收入口本批不迁移。

## 整理证据与当前状态

本轮纯移动清单、输入集合核对、后续独立清理和验证见[目录整理记录](../Docs/A_Project/40_Implementation/Architecture/SOURCE_LAYOUT_20261027.md)。历史A/R8移动清单保持原样用于追溯，不作为本轮路径索引。当前功能／验收状态和未定因项只维护于[CURRENT_STATUS](../Docs/00_Catalog/CURRENT_STATUS.md)。
