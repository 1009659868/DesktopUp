# C-QTP Qt Quick 原型

DesktopUp V2 渲染宿主选定原型(C-QTP)。两个进程:

```text
core/      Rust 核心桩:启动/重启渲染器、命名管道 IPC、看门狗、呈现自检(GDI 像素采样)
renderer/  C++/Qt Quick 渲染侧车:透明圆角海报 + 自制标题栏 + 六占位卡片 + 健康标记
```

## 构建

```powershell
# 前置:Qt 6.8.3 (G:\A_\Tools\Qt\6.8.3\msvc2022_64)、CMake、Ninja、VS18、Rust
powershell -File build.ps1
```

产物:`renderer/build/poster-renderer.exe` 与 `core/target/release/desktopup-core-stub.exe`。

## 运行

```powershell
core\target\release\desktopup-core-stub.exe
```

核心桩会启动渲染侧车并建立管道。IPC 消息为 UTF-8 JSON(长度前缀 4 字节 LE)。

## 命令

| cmd | 参数 | 说明 |
| --- | --- | --- |
| ping | — | 探活 |
| set_geometry | x,y,w,h | 窗口几何 |
| set_visible | visible | 显隐 |
| set_paused | paused | 暂停(原型阶段为占位) |
| set_params | {accent,opacity} | L0/L1 参数演示 |
| get_health | — | exposed/frameSwapped + 核心侧屏幕采样比对 |
| shutdown | — | 退出 |
