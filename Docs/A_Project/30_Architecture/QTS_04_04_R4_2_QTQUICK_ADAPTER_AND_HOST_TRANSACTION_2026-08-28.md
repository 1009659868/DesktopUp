# QTS-04-04 R4.2：Qt Quick Renderer Adapter 与 Host 分阶段提交

状态：`IMPLEMENTED / RELEASE AUTO 14 OF 14 PASS / REAL RUNTIME SMOKE PASS / MANUAL REFLOW FLASH GATE PENDING`

日期：2026-08-28

## 1. 用户决定与本轮目标

用户正式选择 Qt Quick/QML + GridView/Flickable + QAbstractListModel + QQuickRenderControl，不再继续扩展手写 QPainter grid/scroll/reflow。此次只替换 renderer adapter，并保留已验证的 DesktopHost、split input islands、Runtime 手势、StateStore/IPC 和 IRenderer 合同。

本轮同时独立修订 DesktopHost present 路径：先在不可见的本地内存中准备完整像素帧和窗口区域，再集中提交 visual geometry、input-island geometry、region 与 pixels，用来区分闪烁究竟来自 renderer 重排，还是来自 Host 在旧帧与新尺寸之间暴露中间状态。

## 2. 受控架构修订

QTS02-D04/D05 原冻结基线为常驻 Runtime 仅使用 QtCore/QtGui，并由 QPainter 输出海报。该基线在 QTS-04-04 R4.1 暴露出持续手写 Grid/Scroll/Reflow 的维护与交互风险。依据“用户最新明确决定优先”，本轮建立以下受控例外：

1. Runtime 的 renderer adapter 允许链接 QtQuick 与 QtQml；
2. Scene Graph 固定使用 software backend，QQuickRenderControl 离屏输出 QImage::Format_ARGB32_Premultiplied；
3. QML 只表达 renderer-neutral scene 的视觉投影，不成为 Item、布局、状态或主题权威；
4. 不允许 Qt Quick Controls、WebView、任意外部 QML/JS 或原生主题插件进入 Runtime；
5. QPainter renderer 暂时保留为历史视觉/fallback 回归基线，不再作为默认 Runtime renderer；
6. 常驻工作集、Scene Graph 资源和退出清理必须在 QTS-01 延期性能门恢复时重新测量。

## 3. 最小 Qt Quick renderer adapter

新增 desktopup_renderer_qtquick：

- NightOpsItemModel 使用 QAbstractListModel 发布 initials、itemTitle、accentColor；
- NightOpsPoster.qml 使用 GridView 管理六张卡片的创建、复用、动态列数与连续 contentY；
- QQuickRenderControl + hidden QQuickWindow 把软件 Scene Graph 离屏渲染到 premultiplied QImage；
- adapter 从 QML delegate 的真实几何反投影 hit regions，不再另写一套卡片行列坐标；
- GridView 固定卡片 metrics，外框 resize 只改变列数、行数和 viewport，不缩放标题/Item；
- 连续 scroll offset 支持部分 Item 逐像素进入/退出，并能到达最后一行；
- collapsed、透明外圈、滚动条和六 Item 可见/命中继续输出既有 NightOpsScene/NightOpsLayout 合同。

Qt 6.8 software adaptation 不调用 QQuickRenderControl::initialize/beginFrame/endFrame；这些调用只适用于 RHI-backed render target。相同尺寸 backing image 被复用，避免 Scene Graph 无 dirty region 时返回被重新清空的透明帧。

## 4. DesktopHost 分阶段提交

Win32 没有一个 API 能把 GDI pixels、window region、visual HWND 和多个 input-island HWND 真正作为 OS 级单事务提交。本轮采用一个 owner-thread、无消息泵间隙的 staged present：

1. 校验 frame 与 region；
2. 在本地 staged buffer 中完成整帧 premultiplied 像素转换；
3. 在改变可见 HWND 前创建完整 round region；
4. 一次 DeferWindowPos 批量提交 visual 与全部 island geometry，使用 NOREDRAW/NOACTIVATE，不在稳定 resize 中重复改变 z-order；
5. 同步 StretchDIBits 绘制已经准备好的完整像素；
6. 仅首帧在完成绘制后显示 visual/islands；后续帧不再每次 SHOW/HIDE 或 HWND_TOP；
7. island 集合 replacement 或首次 reveal 时才重新建立一次确定性 z-order。

这比原路径消除了“先 resize/show/top，再转换并绘制新像素”的明确中间窗口，但是否已消除用户看到的 reflow flash，仍须真实桌面人工门确认，不能由自动测试替代。

## 5. 自动与进程证据

- Windows x64 Release 全量构建：PASS；
- Release CTest：14/14 PASS；
- 新增 qts04.qtquick-renderer：动态 2/3/6 列、窄高/宽扁尺寸、连续部分滚动、最后一行、collapsed、透明边界、重复 render 和清理全部通过；
- qts04.input-islands：resize capture 期间变更 frame 尺寸，visual/island HWND identity 与 capture 均保持；
- 既有 QPainter 像素门、Host reconnect、Win+D/Explorer 生命周期、StateStore/IPC、Runtime exit/ledger 和 process-role 回归全部通过；
- 真实 desktopup-runtime 短程启动退出码为 0，ledger 为 exited、ownedResourceCount 0。

## 6. 人工闪烁关闭门

从 PowerShell 启动最新 Release：

~~~powershell
$repoRoot = (git rev-parse --show-toplevel).Trim()
$qtRoot = $env:QT_ROOT
if ([string]::IsNullOrWhiteSpace($qtRoot)) { throw 'Set QT_ROOT to the Qt 6.8.3 msvc2022_64 directory.' }
$env:PATH = "$(Join-Path $qtRoot 'bin');$env:PATH"
$env:QML2_IMPORT_PATH = Join-Path $qtRoot 'qml'
$env:QT_QUICK_BACKEND = "software"
$runRoot = Join-Path $repoRoot 'out\manual\qts04-r4-2'
New-Item -ItemType Directory -Force -Path $runRoot | Out-Null
& (Join-Path $repoRoot 'out\build\windows-x64-release\bin\Release\desktopup-runtime.exe') `
  --state-db (Join-Path $runRoot 'state.sqlite') `
  --pipe-name "DesktopUp.QTS04.R4.2.Manual" `
  --instance-name "DesktopUp.QTS04.R4.2.Manual" `
  --ledger (Join-Path $runRoot 'ledger.json') `
  --editing
~~~

只执行本轮最小门：

1. 连续拖动右边缘跨越 2↔3↔4↔6 列阈值，左键捕获不得中断；
2. 行列重排时整张海报不得周期性消失、露壁纸或闪旧帧；
3. 两列窄视口中连续滚轮应逐像素滚动，六张卡片均可到达，空 viewport 仍能从滚动条命中带恢复；
4. 无顶部白条、底部灰框、accent 越界或 Item/页脚遮挡；
5. 海报空白穿透、卡片/锁定/收起、普通窗口高于海报保持不回退；
6. 退出后无 visual/island/进程残留。

## 7. 当前裁决与下一步

Qt Quick adapter 与 Host staged-present 已取得自动和进程证据，但 reflow flash 仍标记为 MANUAL PENDING。用户确认第 6 节后，才能关闭 QTS-04-04 并进入 QTS-05：StateStore、控制中心与托盘；若仍闪烁，下一轮只在 Host present timing/paint synchronization 上取证，不回退为第四套 layout/scroll 实现。
