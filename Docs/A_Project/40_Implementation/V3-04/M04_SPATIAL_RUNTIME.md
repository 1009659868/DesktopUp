# M04-03..05：空间运行接入与验证

日期：2026-09-08  
分支：impl/poster-redesign-V3  
状态：以下为R1及之前的历史实现/证据；R2已接入，最新实际合同见 [M04_EXPANSION_R2](M04_EXPANSION_R2.md)，347/347、真实进程回归通过，人工待确认。

09-08策略修订：旧“单方向完整推让失败即拒绝”不能覆盖最新用户需求；新完整候选回退/临时高度/最小高度轻抖见 [空间设计§5.1～5.4](../../20_Architecture/POSTER_SPATIAL_LAYOUT_AND_EXPANSION_V1.md#51-三档裁决与候选顺序r2)，实施见细化工作流§4.2。本轮未修改此处运行代码，下文NoSpace和旧命令描述保留为实际行为，不代表新策略已可用。

本文件是 [M04-02 纯合同](M04_SPATIAL_CONTRACT.md) 的后继，不改写其历史证据。产品规则仍来自 [空间设计](../../20_Architecture/POSTER_SPATIAL_LAYOUT_AND_EXPANSION_V1.md)。未提交或推送 Git。

## 1. 本轮已接入

| 子项 | 实际实现 | 验证边界 |
| --- | --- | --- |
| M04-03 | 既有 16ms 最新帧拖缩入口加入扫掠截停、沿边滑动；全部八向复用原 capture；松手一次完整读集 CAS | 260/260 阶段回归；真实连续拖缩手感尚未人工签收 |
| M04-04 | 原尺寸下展开、贴底向上、先下后上；以 compact 锚点逆收起；中心与海报按钮走同一空间执行器 | 271/271 阶段回归；高度/内容参数不因收展变更，锁定源可主动收展 |
| M04-05 | 垂直连锁推让、安全子集条件归位、v5 因果表/独立布局代次；源和邻居同事务、同 revision；视觉窗和输入岛整批移动 | 最终 288/288；真实 A→B→C 批次与归位 smoke 通过；不宣称数据库与 DWM 跨系统原子 |

## 2. 拖缩执行合同

手势开始时冻结同工作区物理占位、工作区/DPI/显示/Shell generation/运行会话，以及全目录实体版本和 CatalogVersion；包括隐藏和其他屏幕实体的读版本，不读无关 SettingsVersion。

只把已实际呈现的帧作为下一次碰撞起点。16ms 内被覆盖的鼠标样本不成为隐藏运动路径。候选首先复用原工作区/最小尺寸/相对边限制，再进行连续路径碰撞；接触后先水平再垂直滑动，顺序固定。一次高速移动不能只凭终点无重叠就穿越障碍。

碰到障碍不会释放鼠标捕获，不推动邻居。环境或任一障碍实体版本失效才取消旧手势，恢复最新 committed projection。手势松开由既有 StateProjectionCoordinator/SqliteStateCommandRepository 一次提交；提交时再次验证完整目录读集及扫掠轨迹。

HasUserAdjusted 累计合法已显示移动，即使终点回到原坐标也推进 LayoutGeneration。取消不提交；完全没调整的点击不增加代次。

## 3. 保尺寸展开、推让与归位

- 展开保留 ExpandedHeightDip、宽度、Item/标题/主题参数；下方空间够就向下；不足时先用完下方，再向上补足；整工作区也装不下则明确 NoSpace。
- 每个可显示阶段按不大于 32 物理像素的几何步长、16ms timer 推进。不会在收起动画经过正常展开最小高度以下时，把内容滚动轨道留在窗口之外。
- 组求解沿当段展开方向传播最小必需位移；只处理实际相撞的同工作区可见海报，不横向绕行，不推锁定邻居。锁定源自己的收展允许改变标题栏位置。
- 所有参与者的同时运动区间也进行连续相交检查，不只检查组终点。循环有 256 海报、4096 帧及传播次数上限；超限拒绝整个计划，不截断后执行。
- 完整推让计划先准备完；若遇固定障碍、工作区边界或无解，不开始动画，也不部分写库。
- M04-07 R1修订：收起前按既有资格/完整回程检查冻结安全归位子集；源逆恢复compact锚点的每一帧内，邻居随边界释放同步归位。稳定ID迭代求解整条链，单帧位移预算固定在帧前位置，不因迭代加速；源/邻居同批呈现并连续检查。不追加源完全收起后的归位尾帧，不强归位被调整或路径受阻者。
- 归位证明要求源与参与者的环境、会话、位置/尺寸/显隐/锁定/收起、布局代次和最后布局操作匹配；新锁定、人工移动/缩放、移走又移回、被另一操作接管均破坏资格。无关标题/主题变更不推进布局代次。
- 有资格但回程被障碍挡住的海报保留原位；其他可返回者仍可返回。源删除不会自动归位。跨进程重启的物理证明因运行会话不同而失效，不猜测恢复。

## 4. 单一状态权威与 v5 迁移

没有另建状态库或绕过原 command/query。PosterStateCommand 的 SpatialPosture 上下文在现有事务内重算确定性组计划，再生成源与邻居的异构 Updates；SpatialBatchCommand 继续作为 M04-02 的预检信封，不是第二个写入口。

追加 schema v5，不改 v1..4 SQL/校验和：

- posters.layout_generation / last_layout_operation_id；
- spatial_operations：源、schema、active/consumed/invalidated、环境/会话/compact/source proof JSON、committed revision；
- spatial_participants：推前后证明、预期布局代次、失效原因。

布局更新、代次递增、证明创建/消费和既有请求回执在同一 SQLite 事务。旧 v4 行从代次 0 开始，没有伪造归位记录。升级前保留并验证源版本备份；在迁移事务内检查新表/列，失败回滚到 v4。便携配置不携带此类物理执行证明。

无关改名不作废源证明；参与者变化可使父操作保留其他参与者的归位资格。重复 requestId 使用原回执，不再动画或二次推动。运行端不重放旧动画，事务回执本身也有幂等测试。

## 5. 原生物理提交与失败保留

每一帧先准备全部参与窗口的 WinUI 内容尺寸、圆角区域和输入布局，再用 Begin/Defer/EndDeferWindowPos 同批移动视觉窗与输入岛。保留 NOZORDER、NOACTIVATE、NOOWNERZORDER 和 NOCOPYBITS，不改变已有 owner/桌面 bottom/Win+D 保护策略。

逐个读回窗口边界、应用并校验输入岛布局。任何一处失败都会取消整个未提交操作，整批恢复 committed projection；如果物理恢复也失败，记录 spatial.physical_recovery_required 并请求原生命周期恢复，控制中心收到失败而不是假成功。若 DB 已提交而物理回读失败，不倒写旧数据库，按新 committed revision 恢复。

复测发现并修正：

1. 初次原生批次返回 Win32 87：把 SetWindowPos 的 SWP_DEFERERASE 标志照带进 DeferWindowPos；后者未列出该标志。移除后原生批次成功，新增固定标志测试。参考 [Microsoft DeferWindowPos 文档](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-deferwindowpos)。
2. 收起经过 180px 时滚动轨道起点 186px，导致输入岛布局验证失败。增加真实可滚动高度判断，并将全部命中区域裁剪至当前帧；测试逐像素覆盖 60..600 高度且保留命中顺序。
3. v5 故障注入发现结构检查必须在迁移事务提交前，现已改正并验证回滚，不把版本号更新误当作迁移成功。

这些失败留在隔离日志中，不覆盖或删除；最终 PASS 以进程 ID 区分。

## 6. 自动与真实进程证据

- M04-03：260/260；M04-04：271/271；最终 M04-05：288/288，0 失败、0 跳过。
- 最终 Release：0 warning / 0 error。
- 最终目录：out/verify/v3-04-m04-05/app/；TRX：out/verify/v3-04-m04-05/results/m04-05.trx。
- 真实链式三海报：PID 24596，6 次收展、77 个批次帧、exit 0、资源 0。前两次操作都包括 A/B/C；第二次 ConsumeOperationId 对应第一次推让证明。
- 普通三海报（含锁定/收起初态）：PID 46980，4次收展、56帧、exit0/资源0。唯一控制中心与重复激活回归：PID 40692，创建1个中心、完成2次显示、3张海报、共享监听1、exit0/资源0。最终无测试实例常驻。
- 主屏 2560×1440、工作区 2560×1392、96 DPI；副屏 1920×1200，偏移 (2560,243)。这是当前环境证据，不替代其他环境或人工观感。
- 初次失败 PID 43256/45404（批次标志）、46884（中间帧输入越界），均正常退出且资源清零；相关配置未提交。
- smoke 使用全新隔离配置，未修改日常用户数据库、桌面文件或快捷方式。--spatial-smoke 在普通用户目录被拒绝，只可配合显式隔离目录。

SHA-256：

| 产物 | 指纹 |
| --- | --- |
| DesktopUp.exe | 958D77CA81E0035094D2A603DFCD3DBBE595D048F303E574B65EF14AB32BB3A0 |
| DesktopUp.dll | B424785F71A027967362756770B1697207127B5F6C46BD7AAA15353D07F1616C |
| m04-05.trx | E19922885851900D3DB60B9D8D8EB5F9CF3F7EEC303D28B334DFD12185C63DFD |

## 7. 重跑与人工入口

先正常退出旧 DesktopUp。以下创建命令要求目标目录不存在，不覆盖已有配置；重复测试请选择另一个新目录。

```powershell
Set-Location 'G:\A_\Tools\DesktopUp_V2'
dotnet run --project tests/DesktopUp.SessionFixture/DesktopUp.SessionFixture.csproj -c Release -p:Platform=x64 -- 'G:\A_\Tools\DesktopUp_V2\out\manual\m04-spatial' 3 --spatial-chain
$env:DESKTOPUP_V3_TEST_DATA_ROOT = 'G:\A_\Tools\DesktopUp_V2\out\manual\m04-spatial'
& '.\out\verify\v3-04-m04-05\app\DesktopUp.exe' --show-control-center
```

上述链式夹具适用于当前 1392px/96DPI 主屏工作区；不同显示空间不足会拒绝展开，不暗中缩小。普通横向三海报夹具去掉 --spatial-chain。环境变量只在此 PowerShell 会话生效，测试结束可 Remove-Item Env:DESKTOPUP_V3_TEST_DATA_ROOT；不删除任何配置目录。

自动真实进程复测：

```powershell
pwsh -NoProfile -File scripts/smoke-spatial.ps1 -ExecutablePath out/verify/v3-04-m04-05/app/DesktopUp.exe -DataRoot out/verify/v3-04-m04-05/smoke-chain
```

短人工（未代填 PASS）：连续拖缩碰撞/沿边滑动及换列不丢 capture；上/中/底三处展开保尺寸；锁定邻居不可推动、空间不足保留；A→B→C 后收起归位、调整邻居后不错误归位；无白底/闪烁且普通程序仍在海报上方。M04-01/M03未明确确认的旧人工门仍列入集中回归。

## 8. 下一步和未扩大范围

后续状态：M04-06已完成输入/确认接入；当前M04-07 G1～G4用户通过，R1同步归位完成自动验证。先按 [人工记录表](M04_MANUAL_VALIDATION.md)复测G6-R1并关闭其余集中门，再M05参数→M06端到端→M07测量→原V3-04-04完整控制中心。以下边界仍保留。

新建/显示/导入/恢复的无重叠放置、异常历史布局处理及完整中心位置尺寸编辑还要接统一空间入口。本轮只对正常可见布局的拖缩与收展接线；不能宣称所有新建/恢复入口已杜绝重叠。隐藏海报的物理收展请求目前拒绝，不猜测屏幕占位。确认提示与隐藏态配置入口在后续集中收敛。

没有新增真实内容文件操作、吸附/组拖动模式、主题执行权限或 AI 行为；继续保留这些主线的既有计划。
