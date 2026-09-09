# M04-02：空间合同与纯求解夹具

阶段历史说明：下文保留M04-02当时的243/243纯合同边界。后续M04-03..05已接运行端并追加v5，最新状态与启动见 [空间运行合同](M04_SPATIAL_RUNTIME.md)。

日期：2026-09-08

状态：AUTO PASS；纯合同门完成，243/243，Release 0 warning / 0 error。桌面运行端尚未启用空间约束；下一项M04-03。

前置：M01..03与M04-01既有自动证据；用户要求按02→03→04→05顺序推进。M04-01人工点击仍待确认，不影响本项无窗口的计算验证。需求见[空间设计](../../20_Architecture/POSTER_SPATIAL_LAYOUT_AND_EXPANSION_V1.md)，参考取舍见[DeskBox记录](DESKBOX_SPATIAL_REFERENCE.md)。

## 1. 实施边界

本项冻结并实现物理像素占位、逻辑到像素的保守投影、布局合法性、连续路径检查、无障碍两段展开、归位资格与完整读集的纯检查。碰撞截停/沿边滑动的手势接入在03，展开动画与标题输入同步在04，推让传播/归位执行/异构批量事务及呈现失败恢复在05。不会把静态终点合法当作路径合法，不会为通过本项改写Host、capture或已发布migration。

## 2. 坐标与占位

- 输入工作区来自同一稳定显示快照：绝对屏幕物理像素，整数、正宽高，扣除任务栏；允许负坐标。求解器不访问显示设备。
- 逻辑配置使用工作区局部DIP；转换显式携带物理原点和DpiX/Y。外框左/上floor、右/下ceil，转换后重新检查工作区与相交。工作区本身采用原生整数矩形，不向外扩大。
- 阴影不占位；圆角/透明区仍按完整外框占位；边界接触合法，正面积相交不合法。首版硬碰撞间距为0，不擅自冻结吸附阈值。
- 收起只占当前标题条，原展开高度另存；隐藏不占位，但重新显示不得绕过占位校验。普通程序、图标、壁纸不进入海报障碍列表。
- 历史越界/相交返回NeedsRecovery类原因，保留输入意图；此项不自动装箱/缩小/删除。恢复、新建/显示/导入以后复用校验端口，但本项不宣称这些入口已接入。

## 3. 纯计划与失败

结果含明确状态、稳定ID排序的阻挡原因、完整候选变更和分段路径。失败没有可提交的变更/轨迹，不部分修改输入。输入列表在快照构造时复制，结果集合只读。四边四角采用已有PosterGestureKind及对边固定规则；本项对请求作接受/拒绝判断，03才把连续碰撞结果转换成合法的截停/滑动预览。

路径检查是线性边界的连续区间求交，不靠固定帧数采样；终点空闲但中途穿过障碍仍失败。所有工作区/障碍版本保持至提交复核，环境变化取消整轮而非继续用旧坐标。

展开保持宽度和原展开高度H；先下扩d=min(H-c,B-(y+c))，剩余u=H-c-d再固定底边向上。纯计划提供每阶段整数增量采样，任意跳帧仍单调，最终尺寸精确；H超工作区明确NoSpace。本项有障碍先拒绝，不提前冒充05的连锁推动。锁定源自己的收展允许，锁定邻居不可被动移动。

## 4. 命令、读集与提交边界

SpatialBatchCommand为尚未暴露给UI的异构布局信封：operationId、sourcePosterId、origin、environment、observedRevision、完整StateReadSet和每个变更的Before/After/原因/布局代次。它不是第二套状态仓库，也没有直接写库执行器。

M04首版读集保守包含当前完整目录的所有实体（含隐藏与其他屏幕），加CatalogVersion，不包含SettingsVersion；因此无关全局暂停不阻塞，但其他海报改名可能产生可重试冲突。这是明确的安全优先取舍，不声称已优化成最小空间依赖集。隐藏变可见、跨工作区移动、未移动障碍变化和新增目录幻影均不能漏检。未来缩小读集必须增加空间索引版本/范围证明，不能仅移除未移动者。

复核除实体/Catalog版本还包含工作区矩形、DPI、拓扑generation、Shell generation及运行会话ID。这是纯诊断检查，不替代同事务repository CAS；真正提交时在同一SQLite事务再次验证全部读集，单一committed revision发布给三端。

## 5. 归位因果与迁移登记

几何代次LayoutGeneration独立于EntityVersion/global revision。手动移动/缩放即使最终返回同一位置，也推进代次；其他推让接管、锁定/显隐/收起/工作区归属改变均使旧证明失效。主题/标题的无关修改不推进该代次。几何自身的成功推让记录包含operationId与源ID、参与者Before/After、推后代次、状态、工作区环境和运行会话。

接入约束：手势开始至完成必须累计HasUserAdjusted（曾实际改变过合法预览），不能仅用终点与起点比较。结束时坐标相同但该标志为真，仍需提交布局代次变化；真正未发生调整的点击不推进代次。本项的NoChange仅表示当前几何候选相同，不是清除该事实的许可。取消手势不提交几何；因果记录消费与新权威状态同时落盘。

归位资格先检查源仍存在且同一证明有效，再检查邻居未再次调整/未被接管/未锁定/状态和环境一致；坐标相同但代次不同也拒绝。资格通过只表示可尝试，仍须检查回程连续路径和归位子集相互占位。源删除不自动归位；记录一次消费；同一请求重放由既有回执返回，不能重复移动。

新增迁移需求登记（本项不执行迁移）：M04-05接持久因果前追加下一版本（当前v4，预定v5，实施时复核），不改v1..4内容/指纹。新增per-poster layout_generation及last_layout_operation_id；存放空间操作/参与者记录的表，包含原compact锚点、推前后几何、预期代次、有效/已消费/失效状态。更新布局与记录必须同事务；旧库初始化代次0且无可回滚证明。没有有效持久证明或运行会话不匹配时跨重启不自动归位。便携配置不携带这些执行证明。

登记字段：posters追加layout_generation（非负64位整数）和last_layout_operation_id（可空）；spatial_operations以operation_id为主键，记录source_poster_id、schema_version、状态(active/consumed/invalidated)、运行会话ID、工作区/DPI/拓扑/Shell代次、原compact锚点和源的推后布局证明；spatial_participants以(operation_id, poster_id)为键，记录before/after矩形、推后layout_generation、锁定/显隐/收起状态、expanded_height及失效原因。数据库具体SQL与备份/回滚测试在05落地；像素证明绑定环境，原持久逻辑意图不被替换为像素。旧库没有记录则不猜测归位。主题修改可能使当前命令的保守实体CAS重试，但不永久撤销未变的归位资格。

数据库、多个窗口与合成器不是跨系统原子事务。05仍须全部预备、同轮批量提交、输入/视觉读回与失败账本；本项没有物理提交成功声明。

## 6. 被新需求替代的旧策略

| 旧代码/测试 | 处置 |
| --- | --- |
| PosterFrameGeometryPolicy.ToggleCollapsed / CollapsePreservesTopLeftWidthAndExpandedHeight | 保留旧低层toggle测试；不作为边界展开产品要求，04接新两段计划 |
| StateCommandPolicy.NormalizeCollapse / CollapseAndExpandKeepTheSameNormalizedTop | 标记legacy兼容路径，04替代生产收展；本项不删测试 |
| PosterGeometryProjector / GeometryPreservesRightBottomAnchorAndClampsToWorkArea | 保留旧投影回归；不能用其自动缩小证明超大恢复成功，后续入口按NoSpace/恢复状态处理 |

## 7. 验证记录

基线commit：a6ac6cf，开始时工作区干净。本轮修改尚未提交/推送。

- 完整合同测试243/243，新增67项；0失败、0跳过。包含三展开算例逐像素采样、500组固定种子展开性质、500条随机线性路径与独立密集采样对照、临界接触/高速越过1px障碍、8向候选、DPI保守取整、隐藏/历史坏布局、归位失效、障碍实体/目录幻影/环境失效、异构端点批次与锁定边界。
- 保留旧176项，其中3项加Legacy合同标签，未删除/跳过测试。最初两次夹具编译暴露目标类型/测试环境命名冲突，已修正；最终全量通过，不以首次失败为通过证据。
- Release完整构建0警告、0错误；产物out/verify/v3-04-m04-02/app/。TRX：out/verify/v3-04-m04-02/results/m04-02.trx。
- 本轮没有启动应用窗口、没有运行smoke/鼠标/Win+D/Explorer重建；因此没有新增物理交互或普通用户证据。未改用户数据、Host、capture或schema v1..4；测试使用隔离临时库（既有测试）及新纯快照。

复现命令（仓库根目录PowerShell）：

```powershell
dotnet test tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj -c Release -p:Platform=x64 -o out/verify/v3-04-m04-02/tests/ --logger 'trx;LogFileName=m04-02.trx' --results-directory out/verify/v3-04-m04-02/results
./scripts/build.ps1 -Configuration Release -OutputDirectory 'out/verify/v3-04-m04-02/app/'
```

SHA-256：

| 产物 | 指纹 |
| --- | --- |
| DesktopUp.dll | 2C1905E94826851F7BA4ECE9C37E694C91E348C332838F531309F356CC006361 |
| DesktopUp.Domain.dll | F9BDF7712B90ADDCC86960475D841171E38CCCA679410DE4E4CFB096ACA24E6E |
| DesktopUp.Application.dll | 5D1E54255D1F514EE219D5C09FD77ED89AB816D80CD3749F0AAFD02095D5C84E |

## 8. 代码锚点与下一步

- Domain/PosterSpatialGeometry.cs：占位/环境/不可变快照/计划、DIP转像素、连续线性边界求交（decimal临界比较）。
- Domain/PosterSpatialSolver.cs：布局检查、候选接受/拒绝、两段展开及归位资格。拒绝返回空变更集，不自动推动邻居。
- Application/SpatialCommandContracts.cs：冻结信封、完整目录读集、版本/环境和候选端点预检；未提供写库或跨窗口执行器。预检不证明多窗运动路径安全。
- 三个新增Tests：PosterSpatialSolverTests、SpatialReturnContractTests、SpatialCommandContractTests。

M04-03下一步：在既有手势候选阶段复用几何类型/连续检测，实现扫掠截停和沿边滑动；保持全部8向边、唯一capture和结束一次提交；冻结输入时的环境与完整读集。不要将当前接受/拒绝直接表现为丢捕获或松手重按。随后04接保尺寸展开和标题/输入轨迹，05才接连锁推动/归位/新迁移与事务/物理批次。新建/显示/恢复仍需要其负责入口完成接线，不能宣称本项已消除运行版重叠。
