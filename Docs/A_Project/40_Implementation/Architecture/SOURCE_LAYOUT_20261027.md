# 源码目录整理实施记录

2026-10-27，USER PASS / CLOSED。用户确认集中验收符合预期，本轮目录整理收口。留在 impl/07-canvas-ai-contracts，不提交、推送或合并；既有未提交J0文件保留。下一步为[J1最小画板内核](../V3-07/CANVAS_J1_KERNEL.md)。

## 范围与顺序

1. 按“工程分层 → 功能 → 职责”移动生产源码和合同测试。入口 App.xaml、Program.cs、工程文件留在根；命名空间、类型名、程序集、项目依赖和SQL资源身份不变。
2. 移动前登记[逐文件清单](SOURCE_LAYOUT_MOVES_20261027.json)及SHA256；使用同一PowerShell逐项验证源／目标绝对路径位于仓库、源摘要符合、目标不存在，再移动；立即核对字节一致。纯移动检查完成后才能改引用或清理。
3. 单独修订源码路径引用、XAML编译资源和架构检查。保持检查断言，不删测试或放宽安全规则。历史证据／移动清单不回写。
4. 清理仅供review使用而仍编入产品的控制中心观察代码；正式布局与旧配置兼容保留。对仍有生产调用的Legacy／Probe文件不按名称删除，不为满足删除行数制造重构。
5. 为Canvas声明／用例／呈现和Creation面板／用例／AI适配预留职责目录与简短导航，不添加空接口、空实现或新工程。旧模板仍标明v1适配，不假称新内核可用。

本批不改变文件归属、原生拖放、热度、存储迁移、安装或窗口交互。Modules已有按模块分层目录，保持；scripts入口和历史验收脚本本批不批量迁移，避免打断已有命令。无用入口只有核实无生产必要后才清理，不删除历史证据或用户配置。

## 验证与退出标准

影响面为编译路径、XAML装载、测试源码定位与review编译边界。先比较各生产工程及合同测试的MSBuild输入集合；移动字节核对独立记录。随后离线构建普通／review版本，跑架构、模板、中心与呈现相关定向测试；不重跑全量文件搬运／卸载矩阵。

一次隔离原生冒烟覆盖中心XAML、海报呈现及退出资源；完成后普通候选只需集中确认中心操作和海报操作仍正常。源码归位不计性能收益，不据此宣称大窗口已拆解。

自动产物仅写本批out目录，核对归属后清理；人工候选和隔离配置保留至用户验收，再收尾。不调用真实文件，不改原验收数据，不建立安装登记。

## 进度与证据

- 初始分支检查：impl/07-canvas-ai-contracts；既有修改全为J0文档与设计样例，产品源码干净。
- 纯移动：273份生产源码、241份合同测试，共514份；移动前全量校验源摘要与目标不存在，移动后514份字节一致。原命名空间和类型名保留，未移动SQL。
- 路径修订：八个工程的Compile／Page／ApplicationDefinition／ProjectReference／EmbeddedResource集合，经新旧路径映射后与基线逐项一致，见[输入摘要](SOURCE_LAYOUT_INPUTS_20261027.json)。补齐测试的完整／分段路径、辅助工程ShellUiHost通配路径及安装夹具的递归源扫描；未放宽原断言。
- 独立清理：ControlCenterWindow.UiCSample.cs归位后改名ResponsiveLayout.cs，只保留正式布局；观察记录与三个ForProbe方法原逻辑转入tools/review/App/ControlCenterWindow.ResponsiveLayoutProbe.cs。启动调用改为InitializeResponsiveLayout。后核对273份移动生产文件，仅这两份控制中心文件字节变化；未移动的生产源码零变化。
- 保留：LegacyTemplateParameterCalls仍被生产模板参数读取使用；ShellUiHost.Probe仍用于受保护的Shell准备／测试路径；必要诊断不按名字删除。没有新增空服务、DI层或程序集。
- 归位后根目录C#：App 37→1（App.xaml.cs）；Application 110→0；Domain 41→0；Infrastructure 40→0；Windows 33→0；Worker／Setup各保留Program.cs；合同测试241→0。Modules原有目录保持。
- 预留六处职责目录：Domain/Canvas、Application/Features/Canvas和Creation、App/Presentation/Canvas、App/Views/Creation、Infrastructure/AI；仅README，不计实现完成。源码导航原位重写；当前CODE_ANCHORS的69个链接目标随移动更新，历史叙述及原移动清单不改。
- 剩余读取／观察成本、P1/R6锁冲突和历史卸载／升级首因继续OPEN。

## 验证结果

- 普通候选、review版本、SessionFixture离线构建均0警告／0错误；文本门通过1022份受检文件，27份冻结迁移摘要不变。
- check-architecture：生产113份，review187份（其中74份验收源）；组装边界、编译集合及Preview／Review互斥均通过。
- 定向80/80通过：ArchitectureTests、ControlCenterUxTests、TemplateRecipeLifecycleTests、PosterDragFrameSchedulerTests、PresentationReadbackEqualityTests。首次编译暴露辅助项目通配路径遗漏，随后4条架构断言暴露分段旧路径；修正引用后同范围复验全通过，不删断言。
- 原生UI：旧probe-ui-c-sample脚本的测量夹具不创建真实内容，触发Items>0断言；保留失败证据，不修改产品默认值或放宽断言。本轮改用现有--architecture-icons单海报受管文件夹具直接运行同一探针，7个宽窄／折叠／行为状态通过，2项真实内容的画面／命中／裁剪一致；PID66704退出0，资源0。宽、窄截图已查看。旧脚本默认测量夹具问题仍是工具余项，本轮人工入口不依赖它。
- 不含review代码的普通候选复启控制中心，PID69040退出0、错误0、资源0。自动进程继承执行环境为管理员，只证明本轮XAML／运行接线，不替代普通权限鼠标拖放人工。
- 安装夹具仅做语法及五份源码发现检查；未执行安装／卸载，不扩写为新打包认证。

## 产物与清理

普通候选：out/source-layout-20261027/candidate/app；启动脚本：out/source-layout-20261027/Open-Review.ps1。人工数据仅在out/drop-d3/source-layout-review01，使用现有安全夹具生成一份文本和一个带子文件的文件夹，无真实用户文件。

自动证据17份归档至out/source-layout-20261027/evidence.zip，逐项摘要复核后清除review构建370份、失败自动样例7份、散落TRX两份和截图7份。归档SHA256：39AF3239944860A8B8664083B58DAC4173EB731AE65CFDACB04570194CAF5221。失败与成功日志都保留在归档，详情和人工样例路径／摘要见同目录cleanup.json。未在桌面创建文件、未建立安装登记；当前测试进程已退出。共享bin／obj和以前的候选未批量清空。

人工验收通过后核对进程已退出；13份样例／配置及370份候选文件分别归档、逐文件摘要核验后清理。样例内容与原清单一致，仅日志、数据库和资源账本有正常验收变化，无新增真实文件。归档保留在同批目录：source-layout-review01-accepted.zip（SHA256：1C4154E589DE03E9DA50A2C8B4A60CD838BBC4203B07A6144AB47C0755556F30）；candidate-accepted.zip（SHA256：6F3D536A71645C7A8E28BE3BC262D2483FA2D50B42CE0186831A734F8F2477CC）。旧Open-Review入口不再可用，不能用已清理候选作为J1验收包。

## 集中人工验收

在普通权限PowerShell执行Open-Review.ps1，一次操作完成：打开控制中心，切换外观／行为、调窄／调宽并改一个外观参数；再拖动海报、收展并查看其内容菜单，最后从托盘退出。确认界面／内容正常、没有新错误即可；不重做完整拖入／卸载／热度矩阵，不要求使用真实文件。

用户已确认本组符合预期，收口且不合并。接续已授权J1最小画板内核，新增功能按预留目录归位；目录整理不是J1运行功能交付，也没有把原大型partial类型全部解耦。
