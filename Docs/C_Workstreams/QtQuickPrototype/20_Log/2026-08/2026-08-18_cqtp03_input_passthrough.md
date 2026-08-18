# C-QTP-03 桌面层、穿透与命中 — 机制构建日志

日期:2026-08-18
状态:`机制完成,待交互验证`
提交:`db360a8`

## 目标

1. 渲染端上报 HWND + 窗口矩形 + 命中区域(标题栏 + 3×2 卡片);
2. 核心端低层鼠标钩子按光标位置切换穿透位(仅 `WS_EX_TRANSPARENT`,不加 `WS_EX_LAYERED`);
3. 标题栏原生拖动(5px 阈值);
4. 几何命令(set_geometry/set_visible)往返。

## 实现

### 渲染端(renderer/src/main.cpp + qml/Main.qml)

- hello 帧追加 `hwnd`(winId 转 double)、`rect`{x,y,w,h}、`regions`(7 项:标题栏 + 6 卡片,每项 x/y/w/h/kind);
- QML 标题栏 MouseArea:`pressed` 记起点,位移 ≥5px 调 `posterWindow.startSystemMove()`(原生拖动);
- 窗口属性:`Qt::FramelessWindowHint | Qt::Tool | Qt::WindowDoesNotAcceptFocus`,`setDefaultAlphaBuffer(true)`,`setColor(Qt::transparent)`。

### 核心端(core/src/main.rs)

- `register_surface`:解析 hello 的 hwnd(f64→u64→HWND)、rect、regions(f64→i32),存入 `SURFACE: OnceLock<Mutex<Option<Surface>>>`;
- `run_hook_thread`:安装 `WH_MOUSE_LL`;`mouse_hook` 回调中把 `POINT` 归一到 0..65535 再映射到窗口客户区坐标,命中任一区域则关闭穿透位,否则打开;固定样式位 `WS_EX_TOOLWINDOW|WS_EX_NOACTIVATE`,永不置 LAYERED;
- 穿透状态变化经 `AtomicBool` 节流(每 20 次切换输出一条 `core.log`);
- 命令面新增 `set_geometry`/`set_visible`/`capture` 转发。

## 缺陷与修复

| 缺陷 | 现象 | 修复 |
| --- | --- | --- |
| OnceLock 从未初始化 | `SURFACE.get()` 恒为 None → 区域恒 0、钩子永不启动 | 三处调用改 `get_or_init(|| Mutex::new(None))` |
| QJsonValue 类型歧义 | C++ 端 int/qulonglong 赋值编译失败 | 统一 `static_cast<double>`;核心端 `as_f64` 解析 |

## 验证证据(smoke)

- `core.log`:`hello #1`(完整 JSON)、`parsed 7 regions`、`store inside lock: parsed=7 stored=7`、`surface registered hwnd=0x125C0A58 rect=1700,200,450,703 regions=7`、健康轮询持续;
- 待人工验证:`passthrough -> true/false` 节流行(空白区 vs 标题栏/卡片)、标题栏拖动、5px 阈值。

## 结论(暂定)

机制路径成立:单穿透机制(WS_EX_TRANSPARENT) + 区域命中 + 原生拖动,无 LAYERED、无 blur。人工交互验证后进入 C-QTP-04。
