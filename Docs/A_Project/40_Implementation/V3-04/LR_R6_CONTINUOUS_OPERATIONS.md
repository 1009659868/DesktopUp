# LR-R6 连续操作组合实施记录

2026-09-28按需分发补充：用户请求Windows 11 x64独立预览包，交付及新包回归见[安装包记录](PREVIEW_INSTALLER_DELIVERY.md)。本轮只加分发身份/打包，不实施2B；安装后的16场景/构图/协议复跑不扩张为R6新门或人工通过。

更新：2026-09-28。状态：**R6 IN PROGRESS；第一批及第二批2A主动缩框 CODE / CONTRACT / MANAGED APP PASS；2B被动空间接线、第三批及原生人工未完成。**

本页记录实施与证据，不重新裁决产品规则。范围归[主计划§8](../../30_Plans/V3_04_L_REMEDIATION_REVIEW_PLAN.md#8-当前出口和最近下一步)与[执行地图§9](LR_MILESTONE_EXECUTION.md#9-r6-接线准备r5核心已收口实现尚未开始)。UI C、唯一空间所有者、R4呈现/恢复、完整CAS及单revision继续复用；BUG-026与OPT-MOTION-02不纳入本批修复。

## 1. 批次和文件归属

| 批次 | 范围 | 当前状态 / 后续门 |
| --- | --- | --- |
| 第一批 | 实际循环副本抓取、未折返导航、结束锚、两轴两模式取消/保存/撤销 | candidate02合同和真实App处理器回归通过；不是OS鼠标/键盘/桌面像素/人工通过 |
| 第二批 | 主动最小跨轴尺寸、ContentSpacePolicy、被动二维只读与恢复、仅改高度及R4组合 | 2A主动下限及预览/提交校验通过，见§5；2B真实环境准入/二维导航/恢复尚未接入，不能将2A替代整批验收 |
| 第三批 | 按tile去重的资源可见集、假源变化、完整连续/生命周期与原生人工门 | 未实施；沿既有端口，不接真实文件、不重建solver |

本轮主agent持有源码/测试/文档，未启子agent；构建和GUI串行。起点`impl/poster-redesign-V3@98e0204`，此前文档修改和本地记忆保留；没有Git提交/推送，也没有日常库迁移。

## 2. 第一批实现

- `ContentDragNavigation`冻结按下snapshot/viewport/实际tile/内部抓取点，保存有界的未折返主轴导航量；渲染偏移才取模。有限模式只累计实际允许的导航，不积累被边界拒绝的距离。影子跟指针，不把周期偏移当Item位置。
- `ContentDragPolicy.Map`在原完整见证验证后支持活动循环的cycle 0及接缝副本。移动先移除完整周期再网格取整；自动输出唯一读序，手动回合法canonical网格，尾缝不可解释的格点明确拒绝；Resize仍是尺寸增量，不按周期折返。旧伪造/过期tile继续拒绝。
- `PosterVisualWindow.ContentEditing`的首次输入、16ms既有调度和末次松手接入该模型；边缘导航使用累计位移，不相减两个已取模offset。取消/无效释放/零变化/成功保存交接实际抓取Item的浏览锚；有限边界按既有RestoreAnchor作必要夹紧。
- `EnsureContentSnapshot`一次性消费结束锚，避免换成首个可见邻居。暂停、实体版本失效、frame/scale失效不复用旧结束像素；清理表面时清空临时锚。未新增持久字段、数据库、solver、计时器种类或输入捕获通道。
- 复用`App.ContentBrowseProbe`及`probe-content-browse.ps1`，新增两轴循环拖动组；`PosterVisualWindow.ContentLoopDragProbe`从实际发布的tile区域构造托管事件，进入生产press/flush/end，逐步核对真实XAML与发布命中区域。探针不直接注入Proposal，不把托管事件称为OS输入。

## 3. 反例与自动证据

证据根：`out/lr-r6-evidence/`，均为本机新隔离目录；原候选和数据未覆盖。

| 证据 | 结果 / 范围 |
| --- | --- |
| `contracts/loop-red.trx` | 10/10按预期失败，均命中旧活动循环拒绝门；先取得红测，再实现 |
| `contracts/loop-model-01.trx` | 48相关合同通过；含两轴cycle0/1、两模式整圈无操作、canonical保存、CA-01的980/300/1000/950及70→95/抓点10、正反跨缝、有限夹紧/成员删除、安全预算。该轮测试方法名Main触发2个编译警告，已改为Along |
| 最终App/fixture构建 | `candidate02/{app,fixture}`均0警告/0错误；首次直接-o未带目录分隔符被既有构建守卫拒绝，随后使用正式build.ps1，不绕过守卫 |
| `contracts/r6-batch1-full-02.trx` | 2133通过、0失败、0跳过；不以此前2115结果替代本轮 |
| `loop-drag-02/observation.json` | 14外层场景；其中两轴各6项两模式连续子场景、各320检查（合计640）。覆盖导航正反各96DIP、取消、单revision提交/单文档版本、撤销、零位移与无效释放；102条原滚动事件及XAML/命中一致性继续通过 |
| `finite-protocol-02/observation.json` | 既有有限直接操作Protocol及同库进程重启通过，防止循环接线破坏有限路径；不把该有限重启外推成全部循环配置重启认证 |
| 退出/绑定 | 最终三进程12476、46548、35684均exit0、activeResources0，产物指纹未漂移；candidate01及其两组成功材料仍保留 |

最终App SHA256：`957953046CA245DB3EACEB8BCDCAB2B4A01CBBE682A97E921A46C7ACD40A3A6B`。
源码绑定：`candidate02/source-manifest.json`共449文件，`source-snapshot/`逐文件复制并验证同hash；包含本批新增文件，不仅依赖未包含untracked文件的Git diff。原远端候选25的PASS不继承给本包。

本轮应用是隔离测试进程；不认证普通权限全部环境。**未执行R6 OS鼠标/键盘/滚轮注入，未做拖动过程原生合成像素或人工体验验收；有限协议重启不是循环连续链的完整产品验收。** 本批不关闭R6，也不以资源0证明无拖影或性能达标。

## 4. 下一批入口和剩余风险

1. 继续第二批2B：为被动C<M、恢复最新作者/成员、迟到松手补连续用例；接入已有`ContentSpacePolicy`，明确空间/显示/度量/发布代次。主动下限2A已接入，不重复制作。
2. `Reclip`/绘制/命中/导航统一携带获准的`ContentBrowseMode`；只有真实被动不足可以有限双轴只读，恢复不覆盖原意图、不续接旧手势。
3. 2A已将最大完整Item的`MinimumCrossAxisDip`加chrome传给原空间owner，后续保留该门；继续补宽高、收展终态与物理环境失效键组合，不重写外框空间策略。
4. 再处理tile资源集合和假源端口，完成循环Resize、复杂旧区域/图钉、空间/生命周期等完整组合及原生人工门；现有合同/有限路径覆盖不外推这些尚未完成的R6场景。

用户已确认的正常缩框、不缩Item、极端被动二维、模式退出A、直接拖动等不重新提问。若遇到无法同时满足的具体产品约束，再携反例请求裁决；不凭实施困难自行降级。

## 5. 第二批2A：主动缩框下限（2026-09-28）

### 5.1 实现与边界

- 红测确认：控制中心仍能把大Item缩进不足的跨轴空间；当固定边到工作区边界的距离小于下限时，旧`Math.Clamp`会抛异常。不是动效优化，也不涉及延期BUG-026。
- 新`ContentFrameMinimumPolicy`复用唯一solver的`MinimumCrossAxisDip`，仅在跨轴加入现有chrome；主轴保留既有240 DIP宿主下限。预留现有编辑描边两侧共2 DIP，避免刚缩到下限再进入编辑就遮挡Item；按各轴DPI向外取整。空内容只用宿主下限；未知原文不冒充空布局。
- `TryReadContentFrameMinimum`取作者布局与当前热度呈现下限的较大值；首次外框输入冻结下限到原`SpatialGestureSession`。既有16ms采样、空间owner、碰撞、native ApplyFrame和事务保持，未新增solver/持久字段/计时器种类。
- 控制中心生产入口同样验证当前有效下限。`ControlCenterSpatial.Plan/Prepare`与`SpatialGestureCommitPolicy.Validate`从权威内容重新校验，伪造旧240下限轨迹不能绕过提交门。只有主动Resize受此门约束；位置移动继续保留当前受限尺寸/保存高度，不被误判为缩框。
- 固定边剩余空间不能满足下限时，原缩框提案保持整帧，不抛异常、不偷偷降低下限或挪动固定边。宽高之外的作者数据不变；取消恢复既有提交位置。

### 5.2 自动证据（独立candidate03）

| 证据 | 结果与限制 |
| --- | --- |
| `contracts/frame-red.trx` | 10个反例全部按预期失败：6个两轴/DPI非法缩框获准，4个边界Math.Clamp异常 |
| `contracts/frame-green-01.trx` | 新反例及既有控制中心/空间事务95通过 |
| `contracts/r6-frame-full-01.trx` | 首次扩展全量2155通过、1失败；新测试ItemId未带owner前缀，属于夹具错误，修正身份且保留失败材料 |
| `contracts/r6-frame-full-02.trx` | 最终2158通过、0失败、0跳过；比第一批新增25项。含两轴96/120/144 DPI精确下限及少1像素、仅主轴缩框、空内容、可读下限、未知原文、受限位置移动、伪造最终轨迹 |
| candidate03构建 | App与fixture均0警告/0错误 |
| `frame-browse-03/observation.json` | 16场景、原102滚动事件及640循环抓取检查继续通过；新增两轴缩框共56报告检查：实际发布边缘→生产OnInput→16ms flush→原空间owner，缩到下限/反向/再缩/取消；XAML/命中/裁切一致，Item尺寸不变、预览无写入、取消归原帧。96 DPI实测纵向最小412×240、横向240×454；不是硬编码产品常量 |
| `frame-composition-03/observation.json` | 既有正式控制中心宽窄变化/R4呈现、适配后整图承接与一次撤销、5张XAML取样及同库重启通过；此门是垂直有限构图，不外推全部二维/高度组合 |
| `frame-protocol-03/observation.json` | 既有有限直接操作Protocol及同库重启通过 |
| 退出/资源/绑定 | 五进程31924、50432、26164、50872、47068均exit0、activeResources0、产物未漂移，结束无DesktopUp进程 |

candidate03 App SHA256：`48142C30C6DA0189A94B576BDF84002D71A2FAF05F83575E0BE0FC52D5CA1AB1`。`candidate03/source-manifest.json`与`source-snapshot/`绑定452个源码/工程/配置/脚本文件。旧候选及配置未覆盖、日常库未操作、无Git提交推送。

### 5.3 下一实施仍为第二批2B，不跳第三批

本轮闭合可独立验证的主动缩框门。`ContentSpacePolicy`的App生产接线仍未完成：当前`PrepareSpatialFrame/EnsureContentSnapshot`会先计算候选，`CommitSpatialFrames`才验证整组native/裁切/input；后续必须记**最后成功发布**的空间决定，不能把prepare候选或部分失败帧记成已发布。被动准入还需来自原空间owner的实际显示/DPI/工作区见证，普通缩框、旧约束与邻居碰撞不能自行开启二维。

2B继续按§4接获准模式的Reclip/绘制/命中/有限双轴导航、最新作者/成员恢复、取消旧capture/draft与拒绝迟到松手，再补仅高度和收展连续组合。以上不是本轮PASS范围；本包未执行OS鼠标/键盘/滚轮、原生合成像素或人工体验，提升权限的隔离进程也不认证普通权限。R6尚未收口，不交人工验收、不进入模块/Rain/R7。
