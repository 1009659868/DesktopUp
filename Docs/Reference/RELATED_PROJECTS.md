# 同类项目与工程参考

状态：`REFERENCE`

日期：2026-08-02

本文件记录可借鉴的行为和架构，不构成依赖选择或代码复制授权。

## Stardock Fences

链接：<https://www.stardock.com/products/fences/>

参考点：分组、规则整理、折叠、文件夹门户、个性化参数和低干扰访问。Desktop-Up 应重点学习“收纳后仍易找到”的交互，但产品差异是把集合表现为可编程风格海报。

## Rainmeter

链接：<https://github.com/rainmeter/rainmeter>

参考点：桌面皮肤生态、配置与素材分发、组件化小部件。需避免把初次使用建立在手写配置文件之上。

## Lively Wallpaper

链接：<https://github.com/rocksdanister/lively>

参考点：Windows 桌面承载、多显示器、动态内容和全屏暂停。其 GPL-3.0 代码不能未经许可证评估直接并入不同许可证项目。

## Wallpaper Engine

链接：<https://help.wallpaperengine.io/en/performance/game.html>

参考点：按全屏应用暂停，以及“停止并释放内存”的性能策略。Desktop-Up 应把暂停策略做成事件状态机，而不是主题自行决定。

## Desktop Fences+

链接：<https://github.com/limbo666/DesktopFences>

参考点：开源围栏产品对拖入/移出、Portal 和图标恢复的处理。使用任何代码前必须核对当前文件级许可证和来源历史。

## Tauri

链接：<https://v2.tauri.app/reference/webview-versions/>

参考点：在 Windows 上使用系统 WebView2，适合用 Web 技术实现高可定制界面。仍需通过原型测量 WebView2 的进程数、内存和暂停行为。

## Windows Shell Links

链接：<https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ishelllinkw>

参考点：用公开 Shell 接口解析和执行 `.lnk`，而不是自行猜测快捷方式二进制格式。

## Windows 事件监听

链接：<https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook>

参考点：事件驱动监听前台与窗口状态变化，减少高频轮询。真实全屏判定仍需结合窗口矩形、DWM 和显示器状态测试。

