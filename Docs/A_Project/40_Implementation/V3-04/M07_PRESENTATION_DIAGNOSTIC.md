# M07 呈现诊断：同步几何取证与原位重绘对照

> 历史记录：本页命令、PID、测试数和“当前/待验”均属于当时轮次，不再是活动入口。R1/R2/R3未解决白条，诊断轮不是修复。最终用户确认、清理及稳定入口见 [R4](M07_ANIMATION_R4.md)；已移除脚本/源码本地保留于 out/archive/presentation-cleanup-20260910，不再推荐执行本页旧命令。

最新结果：用户已提交四张异常截图，明确原位重绘画面无变化；实际PID41252日志三次重绘成功且几何/revision不变。另确认收起60px窗口仍保留meta/footer行的布局缺陷。用户已正常退出并授权继续修复，现已实现[R3](M07_ANIMATION_R3.md)，自动门通过、白条视觉待验。下方“等待PID41252截图”为历史；此旧诊断包不覆盖。

日期：2026-09-10。状态：DIAGNOSTIC BUILD/NATIVE PASS；PIXEL ATTRIBUTION PENDING；不是修复版。M07 仍 OPEN。

## 1. 本轮顺序与边界

用户授权按“定位条带归属 → 保几何重绘对照 → 据证据定方案 → 修改/验证”推进，承接 [DeskBox 研究](M07_DESKBOX_PRESENTATION_RESEARCH.md)。本轮只加入默认关闭的观测及显式重绘对照，未改变原收展绘制标志、内容布局策略、HRGN 更新策略、空间求解、输入岛、CAS 或动画配置。

computer-use 技能已完整读取并按要求初始化；两次初始化失败，最终错误 `node_repl kernel exited unexpectedly; windows sandbox failed: CreateProcessWithLogonW failed: 1385`。没有用备用 UI 注入或截图程序绕过。实际桌面像素未自动取得，不能把当前取证称为完整同帧像素归因。

用户已确认保存截图并正常退出 R2 PID44360。R1/R2 固定包、日常配置、桌面文件均保留。无 commit/push。

## 2. 已实现的诊断能力

- `--presentation-diagnostic` 且显式隔离数据根同时成立才启用；正常启动没有诊断菜单、采样计时器或 Rendering 订阅。
- `PresentationDiagnostics.cs` 只读本进程窗口：window/client 的屏幕坐标、HRGN 包围盒及返回类型、DPI、原生内容子窗口。HRGN 包围盒不是完整像素掩模，也不能单独排除圆角局部问题。
- `PosterVisualWindow.PresentationDiagnostic.cs` 记录 requestId、海报/session、revision、目标/缓存边界、root/surface/structure/content/items/footer 实际布局、滚动参数、采样 UTC 和 QPC 起止。
- 布局到屏幕的映射字段明确命名为 `estimatedScreenPixels`：由 root 相对布局、XamlRoot 比例与原生客户区原点计算，不伪装成屏幕截图测量。
- 相位覆盖 begin、每次 Rendering 回调前、布局前、布局后原生提交前、原生/区域/输入提交后、final-restored、至少 250ms/1000ms 后。后继操作会取消前次尚未发生的静止采样，不能把旧 request 的延迟观测冒充新终态。
- 样本内存缓冲，每段收展结束/取消/关闭时刷日志，避免每步多次同步落盘；每个缓冲上限 4096。启用诊断仍可能改变时序，不用于认证原版性能/呈现 FPS。
- 日志经过会话包装，样本在 `data.detail`，`data.posterId/sessionEpoch` 标识会话；不能按 `data.phase` 解析。

菜单仅诊断版新增：

1. **诊断：标记当前画面**：菜单关闭至少 500ms 后记录基线，再于至少 1000ms 后记录同一画面状态，不请求重绘。
2. **诊断：原位重绘对照**：同样等菜单关闭，再对当前海报自身 HWND 请求 `RDW_INVALIDATE | RDW_ALLCHILDREN | RDW_UPDATENOW`。不请求额外 erase，不 Move/Resize/SetWindowRgn/改样式/激活，不发送状态命令。

重绘前后比较外层及子窗口的窗口/客户区/区域/DPI，记录 sameGeometry、sameRevision。期间发生收展/拖缩、恢复或 revision 改变则取消，不把被干扰样本算有效对照。API 成功只标 requested，像素结果固定 `UNOBSERVED`，等待真实截图。

## 3. 构建与自动证据

- 合同测试 493/493、0 失败/跳过；新增测试验证缺失数据不能算几何一致、父子窗口/裁切/DPI 改变会使对照失效，以及正常进程不能进入诊断读取/重绘。
- App/SessionFixture Release 最终均 0 warning / 0 error。App SHA256 `55A25C4270D4905B38AC9DB625E3CCFFECC3BD00ED75C13EBBB02B21740AEE3D`。
- 独立包 `out/verify/v3-04-m07-presentation-diagnostic/`；24 项指纹 `scripts/m07-presentation-diagnostic-build.json`。
- PowerShell 5.1.26100.8875 入口预检及最终 Smoke 通过。
- 最终回归 PID43032，目录 `out/manual/v3-04-m07-presentation-diagnostic/20260910-120445-064ca6/`：既有两种嵌套关闭顺序共 8 命令，2624 条布局/窗口样本；三张海报原位重绘请求成功，即时/延迟 sameGeometry 和 sameRevision 均 true；正常退出、ledger clean=true/resources=[]。
- 测试结果 `out/verify/v3-04-m07-presentation-diagnostic/results/presentation-diagnostic-final.trx`。

中间过程如实保留：首次并行 build/test 共用 obj 发生文件锁冲突，改为顺序构建；诊断文件漏 Application 引用已修正。PID3512 样本正常但旧脚本误读 data.phase，造成入口误报失败；PID48348 对照通过后发现计时器改间隔不能单靠 tick 序号证明延时，改成固定采样节拍、Stopwatch 截止判定，最终 PID43032 已重跑。不将这些中间失败隐藏或当作产品修复证据。

## 4. 当前能与不能下的结论

最终回归所采到的展开终态（360px 高度）中，客户区、`Microsoft.UI.Content.DesktopChildSiteBridge`、根布局和底栏底边对齐，差值为 0。部分动画早期可见高度不足固定行总高度时，内部 structure/footer 超出可见窗口并被裁切；不能仅凭内部布局越界就认定它产生白条。

这些证据目前只说明：**本轮采到的终态没有持续少 10px 的内容子窗口或多 10px 的窗口几何。** 因为没有对应画面，不知道该次取样是否正在显示同一白条，不能排除用户样本的布局问题，更不能声称具体白色来源已确认。

原位重绘操作本身已通过几何/状态不变检查；“重绘后白条是否消失”仍未观测。因此 D1 像素定位和 D2 视觉对照仍 OPEN，D3 最终方案和 D4 修复尚未开始。

## 5. 当前人工实例与最短操作

已启动最终诊断版 PID41252，数据根 `out/manual/v3-04-m07-presentation-diagnostic/20260910-120558-57f5c2/`，日志在其 `logs/desktopup.jsonl`。

只需复现本次问题，不重跑完整功能矩阵：

1. 用 C/B 收展复现异常，停止动画。先截一张保留完整异常海报和相邻海报边界的图；若能录到短段动画，可一并提供，不必重复既有功能验收。
2. 在异常海报“…”选择“诊断：原位重绘对照”，等至少 2 秒，不拖缩、不收展，再截同一区域。
3. 报告白条消失、仍在或本次未复现，并说明是 A/B/C。若打开菜单本身就使白条消失，也必须报告，这不是重绘按钮的效果。
4. 如果方便，使用“标记当前画面”留下无重绘对照；菜单的关闭本身可能引起重绘，前后截图应在菜单已关闭时比较。

重新启动命令（先正常退出已有实例）：

```powershell
& 'G:\A_\Tools\DesktopUp_V2\scripts\start-m07-presentation-diagnostic.ps1'
```

拿到画面对照后再决定最小修复：若布局一致且重绘能清除，优先验证脏区/呈现收尾；若不清除，则继续定位原生背景/内容表面及裁切，不直接得出“不是呈现问题”；若布局发生变化，对照无效需先查变化来源。DeskBox 的预布局、整段稳定内容、共享提交、最终恢复保持候选方向，不孤立套用 NOREDRAW。
