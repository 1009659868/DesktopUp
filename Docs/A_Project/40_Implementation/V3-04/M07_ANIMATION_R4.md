# M07 动画 R4：消除可见尺寸与根表面的覆盖缺口

日期：2026-09-10。状态：IMPLEMENTED / CONTRACT & NATIVE COVERAGE PASS / USER CONFIRMED。M07保持OPEN。

## 1. R3失败与本轮定位

用户提供 `9c754fca`、`737f3cf6` 两张截图，确认R3动画白条仍存在。实际进程是PID40500，旧包/日志保留，用户已从托盘正常退出。

图片只读像素分析：在x25..279范围，每行至少245个像素为纯白，图1白条y275..284、图2y212..221，均为10px；不是仅凭视觉估计。

对应真实复测日志：
`out/manual/v3-04-m07-animation-r3/20260910-143712-131d21/logs/desktopup.jsonl`。

| B样本（14:41:26.695，request 7e3c5d9e…） | 读回高度/底边（相对窗口） |
| --- | --- |
| 原生视觉窗口/客户区 | 220 |
| WinUI DesktopChildSiteBridge | 与客户区一致 |
| XAML根、外层surface | 210 |
| 内部固定内容structure | 360 |
| 底栏bottom | 220 |
| 输入岛区域bottom | 178 |

同一复测序列14:41:24.643再次出现220/210；收起过程反向读到220/230。图片文件写入时间不是精确呈现时间，不能把截图声称为与某条日志硬件同帧；但厚度、增长方向与真实连续样本吻合。

**已确认的缺陷**：R3只固定了内部内容，仍逐帧扩视觉HWND。WM_SIZE后根布局尚未追上时，可见区域比根表面多出一帧增量。底栏已按新高度移动，下面这10px却不受旧根覆盖。之前layoutsStable只查structure，掩盖了该缺口。重绘、连接Backdrop和内部内容固定均不能保证可见区域已有对应根表面。

输入岛在此样本止于178，不覆盖210..220条带，不能把该段归为输入岛擦白。WinUI/DWM内部究竟哪一层最终填入白色，未直接取得其呈现追踪；本轮修复的是可验证的“提前暴露未覆盖面积”机制，不冒称已抓到全部GPU因果链。

## 2. 修订机制

- `PosterTransitionSurface`分别表示本轮临时视觉容量与当前可见几何。容量取起终点有效高度较大值；自适应使用求解结果450等有效高度，不取持久首选600。收展不允许顺便改宽度。
- 开始前核验**现有可见HRGN**，在该裁切后统一准备完整视觉窗口/客户区容量。输入岛和空间几何仍是原可见尺寸，没有先展开一个大窗口再隐藏的步骤。
- 等实际XAML根覆盖已准备容量、原生容量与可见HRGN匹配，且连续两个Rendering回调就绪后再启动动画时间轴。该门不是GPU present fence；超时仍走既有取消恢复。
- 动画视觉HWND保持固定容量，带NOSIZE只移动原点；当前可见高度由HRGN与XAML视口决定。输入岛仍按当前可见范围移动、缩放。每帧记录并验证`surfacesCoverVisible`，不能只查内部内容。
- 空间快照、CAS/关系、推让和归位、控制中心状态、保存尺寸只使用可见几何。扩大后的原生容量允许暂时越过工作区，但裁切限制可见内容；它不是另一个空间占位。
- 结束/取消释放临时容量映射与固定内容，统一将原生大小恢复到权威可见几何，保留R3最终区域/输入/绘制恢复。展开结束通常不再扩大原生表面，收起/取消只回收容量。
- 新增原生`VerifyVisualSurface`，读回窗口、客户区和HRGN边界；仅允许已登记视觉窗口。常规/动画/准备/最终阶段保留既有桌面层级和输入岛策略。

这不是声称DeskBox本身采用相同原生容量策略。R3保留其内容预布局经验，R4针对本项目实测的root/原生尺寸错位补齐宿主容量边界。没有新增依赖或复制参考源码，没有改动画JSON默认值、空间策略或schema。

平台API语义参考：[SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos)、[SetWindowRgn](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowrgn)。公开WinUI白窗口问题仅作为排查线索，不能证明本项目与其同源。

## 3. 自动和原生证据

- **511/511**，0失败/0跳过；`out/verify/v3-04-m07-animation-r4/results/r4.trx`。
- App/fixture Release0警告0错误。PS5.1.26100.8875预检通过，带空格配置路径通过。
- 历史清单（现归档）`out/archive/presentation-cleanup-20260910/scripts/m07-animation-r4-build.json`锁定24项产物；App DLL SHA256 `BCC6795B5850E979CA3F223849151D79DE44A81486DF11ED433DB9A664F51C74`。

| 样本（目录位于out/manual/v3-04-m07-animation-r4） | 结果 |
| --- | --- |
| PID22752，native-20260910-145119 | 默认240帧/8命令通过；620条动画/准备提交后样本可见未覆盖高度全部0；8次准备门和最终恢复通过 |
| PID38956，20260910-145355-292bd0 | 6px/8ms/320ms/linear，400帧/8命令；1020条对应样本覆盖缺口0；20次布局准备/释放配对 |
| PID42240，up-20260910-145713 | 先下后上，108帧；完整600px展开到y392..992，恢复成功 |
| PID41584，constrained-20260910-145725 | 受限450px、84帧，首选600不被写坏；容量与有效高度一致 |
| PID29956，cancel-slow-20260910-150014 | 独立慢速配置，仅第10帧后正常退出；surface-disposed取消、原生/布局恢复true；未将8命令未完成记作PASS |
| PID17276，presentation-20260910-145846 | 三海报主题/内容参数投影和同窗口读回通过 |

以上均exit0、资源账本clean/0资源。默认3300ms首次退出样本PID42664恰在第一个命令完成后退出，**不作为动画中途取消证据**；因此另跑慢速样本29956。慢速配置仅在被忽略的out目录，不更改用户默认配置。

终态采样：收起后原生、根和可见高度均60，展开终态均360；临时容量/Clip已解除。测试覆盖的显示环境为主屏2560×1440、工作区2560×1392、96DPI；不扩称多DPI人工认证。

## 4. 人工结果与稳定入口

用户本次反馈“ok,看起来解决了”，对应 R4 PID49284；本轮登记为原白条故障 USER CONFIRMED。随后用户正常退出。既有菜单/收展体验认可保留；不补写未逐项反馈的全部交互、混合DPI或长时稳定性。
当时独立数据为 `out/manual/v3-04-m07-animation-r4/20260910-150321-a23f5a/`。自动屏幕工具环境仍不可用，未取得逐帧像素录像；人工确认与原生覆盖证据分别记录。

当前唯一动画入口（Windows PowerShell5.1）：
```powershell
& 'G:\A_\Tools\DesktopUp_V2\scripts\start-m07-animation.ps1'
```
支持 `-CheckOnly`、`-Smoke`、`-AnimationConfig`；删除 `-PresentationDiagnostic`。每次新建隔离 C/B/A 数据，已有进程须正常退出，不覆盖日常配置。当前包为 `out/verify/v3-04-m07-animation-stable/`。

## 5. 清理范围与方法论

本次只清理诊断接线，不改变 R4 容量/裁切/恢复算法：
- 移除 PresentationDiagnostic App partial、Windows读取/重绘工具及其3项测试。
- 移除诊断菜单、额外 Rendering/settle timer、有界样本缓存、continuity-smoke后附加重绘实验。
- 将 R2/R3/R4/独立诊断的4个启动器及4个清单移出 scripts；原 `start-m07-animation.ps1`/`m07-animation-build.json`更新为本次稳定版本。
- 上述11个文件移入 `out/archive/presentation-cleanup-20260910/`，按原相对路径保留，可本地恢复；不进入远程源码。旧运行包、原始日志、配置和数据没有删除。
- 保留独立窗口背景、内容租约、Viewport/Surface纯模型、原生安全验证、必要生命周期日志及8命令/上扩/受限/取消/主题有效回归。仅移除诊断工具测试，不删除修复防回归。
- README/ACTIVE_PATH/HANDOFF/计划前言/锚点统一为当前状态，失败轮次保留为明确标记的历史。
- 方法论：[Windows/WinUI呈现缺陷排查](../../20_Architecture/PRESENTATION_DIAGNOSIS_PLAYBOOK_V1.md)。原则是先定位完整覆盖链、固定变量对照、据证据设计，再验证过程与终态。

## 6. 清理后独立回归

- App与fixture Release均0警告0错误；完整 **508/508**、0失败/0跳过，`out/verify/v3-04-m07-animation-stable/results/stable.trx`。由511减少3项，原因是删除诊断工具自身测试。
- 当前24项产物清单 `scripts/m07-animation-build.json`；App DLL SHA256 `C699CAB63D50E224B697E2BD4F8918ED629BFB4BC7E40CBD7375113BB403BDAF`。
- 两次动画回归均由 Windows PowerShell5.1 运行，诊断已经关闭；每帧实际根容量/HRGN覆盖、布局稳定、8次准备/恢复及资源归零保持断言。

| PID / 目录（out/manual/v3-04-m07-animation-stable/） | 结果 |
| --- | --- |
| 35412 / 20260910-153008-7ef863 | 默认8命令、240帧；两种嵌套顺序、覆盖与恢复通过 |
| 44116 / 20260910-153228-6c048c | 含空格配置路径；6px/8ms/320ms/linear，8命令、400帧通过 |
| 49700 / up-20260910-153728 | 上扩完整600、108帧、收起恢复通过 |
| 43612 / constrained-20260910-153740 | 受限有效450、首选600保留，84帧、恢复通过 |
| 38248 / cancel-20260910-153751 | 慢速第12帧正常退出；取消与布局恢复true，不冒充8命令完成 |
| 13752 / theme-20260910-153755 | 三海报主题/内容参数投影通过 |

六组进程均exit0、clean=true、资源0；取消样本的 continuity surface-removed/未完成日志为预期，其他异常仍需检查。清理包没有再要求用户重复整套人工；这里不新增清理包逐帧像素认证。
用户已授权 Git 同步，按非强推流程提交现有 `impl/poster-redesign-V3`，不合并集成/发布分支。M07的剩余安全/完整资源门仍OPEN，下一项见[交接](HANDOFF.md)。
