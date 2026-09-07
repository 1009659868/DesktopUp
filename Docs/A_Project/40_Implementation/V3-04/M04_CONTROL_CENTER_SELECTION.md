# M04-01：控制中心海报列表与目标切换

日期：2026-09-07
状态：AUTO PASS / MANUAL PENDING
分支：impl/poster-redesign-V3；基于1766070及此前dirty状态继续修改，未commit/push。

## 1. 参考吸收先行

[DeskBox空间参考记录](DESKBOX_SPATIAL_REFERENCE.md) 已登记吸附迟滞/候选比较、组预备/批量提交/结束保存、展开尺寸意图/可行性与锚点的实际来源、测试算例和SHA-256。采用小型纯计算策略＋现有宿主的适配方向，不复制整个WidgetManager，不照搬Auto缩小、超大组任意移动、展开抬层。空间能力按M04-02..05后续接入，未声称此版本已经吸附或推让。

## 2. 实际实现

- `Application/ControlCenterSelection.cs`：UI会话级选择模型，消费完整ControlCenterProjection；SelectedPosterId独立于默认海报兼容别名，不写SQLite、不推进revision。
- 默认优先选择默认ID，否则首张；随后按ID保留选择，标题重复、改名、状态刷新不会改投其他海报。所选被删除时清空目标并提示，按钮禁用，不静默将原操作转到另一张；零目录仍有效，后续新增可重新初始化选择。
- `ControlCenterView.CreatePosterCommand`：以点击时显示的目标快照构造命令，冻结ID、期望实体版本与意图；App不再二次解析当前选择。版本过期由现有实体读集CAS拒绝，错误回到中心提示；无关海报更新不强制冲突。
- `ControlCenterWindow.xaml(.cs)`：采用WinUI ListView显示全部海报（含隐藏者），显示锁定/收起/运行状态、选中ID与revision，所选目标可锁定、收展、显隐。全局暂停单独标注“作用于全部海报”。仅选择变化不重建列表。
- `ControlCenterService`持有选择；窗口关闭只释放窗口，重开保持本次进程内选择。没有承诺跨进程恢复选中项，业务状态仍按已有库恢复。新建/复制/删除管理按钮、全桌面预览和参数编辑仍在原04-04，复用本列表。
- App从M03会话快照提供Ready/Degraded/等待宿主/恢复中等呈现信息，不加入额外轮询或持久化运行状态。隐藏者保留目录但不持有可见会话，仍可选择显示。托盘“编辑”作用于中心当前目标，全局锁定仍为原批量合同。

没有改Host/原生层级、海报拖缩/收展算法、schema v4或已发布迁移，没有操作日常数据库/真实文件。所选海报收展仍可能受旧边界策略影响，此问题留在M04-04，不借中心改动宣称已修复。

## 3. 自动验证

完整合同176/176，通过且无跳过；在历史163项基础上新增13项执行用例：全目录含隐藏/重名、选择不写状态、三种目标命令、冻结命令不重定向、无关/同实体并发、删除目标、零目录再新增、旧投影拒绝、改名/视图重建保留选择、运行状态分离、隐藏后显示。

```powershell
dotnet test tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj -c Release -p:Platform=x64 -o out/verify/v3-04-m04-01/tests/ --logger 'trx;LogFileName=m04-01.trx' --results-directory out/verify/v3-04-m04-01/results
& ./scripts/build.ps1 -Configuration Release -OutputDirectory 'out/verify/v3-04-m04-01/app/'
```

最终Release：0 warning / 0 error；TRX：`out/verify/v3-04-m04-01/results/m04-01.trx`。

| 真实进程检查 | 结果 |
| --- | --- |
| 首次配置，PID26340 | 1海报、1中心、2次show复用、1共享监听、exit0/resources0；此为列表焦点优化前的首次启动证据 |
| 最终三张配置，PID20360 | 3会话均Ready；中心PosterCount=3且三个ID完整，默认所选poster-main；1中心/2次show、exit0/resources0 |
| 最终零目录，PID39740 | 中心目录0、选择空、无海报会话；1中心/2次show、1共享监听、exit0/resources0 |

隔离数据与日志：`out/verify/v3-04-m04-01/smoke-first`、`smoke-three`、`smoke-zero`。运行环境elevated=true，不当普通用户权限认证；启动检查不等于点击/视觉人工门。

最终指纹：

- DesktopUp.exe：4DC0182DD7E6CD9C333474D94878358441B34C013D8014C0D7515A71401292F8（原生启动壳可能不随托管改动变化）。
- DesktopUp.dll：A835D5DB0DB0B1D0300148F132E5F84584B787B86351A6ED440CF54F2DC89960。
- DesktopUp.Application.dll：DA4C3E373F24E9FE454BBFAE7562AE56962FFA9A5AC8B65C4C95657BDFC95902。

computer-use初始化及reset后重试均因Windows沙箱CreateProcessWithLogonW错误1385失败；未发送窗口点击，不记录实际GUI操作通过，也未改用自制UI Automation绕过该限制。

收尾：42份Markdown、144个本地文件链接检查，失效0；git diff --check exit0；测试176通过/0失败/0跳过；DesktopUp测试进程剩余0；参考DeskBox工作树仍干净。保留所有隔离证据和用户已有修改，未提交或推送。

## 4. 短人工门与启动

已准备三张隔离手工配置：`out/manual/v3-04-m04-01`。先从托盘正常退出旧版；PowerShell 7.4+执行下列命令，不自动退出、不覆盖日常配置：

```powershell
Start-Process -FilePath 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m04-01\app\DesktopUp.exe' `
  -ArgumentList '--show-control-center' -WindowStyle Hidden `
  -Environment @{ DESKTOPUP_V3_TEST_DATA_ROOT = 'G:\A_\Tools\DesktopUp_V2\out\manual\v3-04-m04-01' }
```

1. 列表显示A/B/C；切换后标题、ID、锁定/收起与实际目标对应，A/B/C均可独立控制。
2. 选择B，切换锁定、收起/展开，只有B改变；选择C后操作不能落到默认A。
3. 隐藏所选海报后列表仍保留它；点击“显示”恢复且不多造一张。运行状态随会话变化，隐藏不冒充正常可见。
4. 选B关闭中心，海报仍运行；托盘呼回仍选B；不增加中心/托盘。列表鼠标/键盘切换不丢焦点，不出现XAML异常。
5. 普通程序始终高于海报；点选/桌面刷新和正常退出不回归。Win+D/Explorer等M03未逐项确认项保持在M04-07集中门，不因本项自动开窗而记PASS。

删除所选和并发冲突目前由真实SQLite合同测试覆盖；删除管理UI尚未交付，不要求人工通过不存在的按钮。碰撞/展开/推让为后续空间门，暂不拿本版本复测新空间规则。

## 5. 后续执行顺序

M04-01人工结果确认后关闭本项；下一开发项M04-02空间合同与纯求解夹具，继而03连续拖缩碰撞、04保尺寸两阶段展开、05连锁推让与条件归位、06多cluster输入/删除确认、07集中多窗口门。吸附是合法几何候选，不替代硬碰撞；锁定者固定，失败保留原布局。原04-04复用本列表，不重新实现一套中心选择。
