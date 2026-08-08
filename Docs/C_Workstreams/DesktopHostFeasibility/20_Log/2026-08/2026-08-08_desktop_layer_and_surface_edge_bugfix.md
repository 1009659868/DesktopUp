# Desktop-Up 桌面层级与顶部亮带修复

日期：2026-08-08

状态：`FIX VERIFIED / USER VISUAL CONFIRMATION PENDING`

## 用户报告

1. 桌面海报会在点击后覆盖普通应用窗口；正确层级应为普通应用在上、海报保持桌面级、桌面图标仍可位于海报之上；
2. 海报顶部仍存在约 10px 的浅色亮带。

## 根因

### 窗口层级

ToolWindow 回退虽然在初始化、Shell 恢复和拖动结束时被放回 `Progman` 上方，但窗口仍允许激活。用户点击海报按钮时，Windows 可以把该顶层窗口提升到普通应用窗口之上。Tauri 的 `alwaysOnBottom` 与原生桌面锚定同时存在，也造成两个 Z 序所有者。

### 顶部亮带

原生探针再次确认海报窗口客户区与外框完全重合，未残留 Windows 标题栏或非客户区。截图像素显示亮带高约 8–10px，与独立海报宿主的 `surface-arrival` 入场位移一致。自动暂停可能在启动阶段冻结该动画，使透明宿主边缘/合成背景持续暴露；此前只清理 Windows 样式无法解决这一前端运行时条件。

## 修复

- ToolWindow 扩展样式显式增加 `WS_EX_NOACTIVATE` 并清除 `WS_EX_APPWINDOW`；
- 子类处理 `WM_MOUSEACTIVATE`，先重申桌面锚点，再返回 `MA_NOACTIVATE`，保留鼠标交互但不抢前台；
- 统一 `anchor_tool_window_to_desktop`，Shell 恢复、拖动结束和鼠标激活共用同一 Z 序入口；
- Tauri 海报窗口改为 `focusable: false`，关闭 `alwaysOnBottom`，让原生桌面锚点成为唯一 Z 序所有者；
- 窗口创建配置和运行时同时显式设置 `[0,0,0,0]` 全透明背景；
- 独立桌面海报移除 `surface-arrival` 入场动画，避免自动暂停把位移/缩放冻结在中间帧；控制中心预览动画不受影响。

## 自动验证

```text
pnpm run check
  32 tests passed

cargo test --locked --manifest-path src-tauri/Cargo.toml
  19 tests passed

cargo tauri build --no-bundle
  PASS
```

修复版 Release PID `71540` 的原生探针：

```text
WindowRect      = 1948,0 612x163
ClientRect      = 1948,0 612x163
ExtendedStyle   = 0x08000090
WS_EX_NOACTIVATE= true
WM_MOUSEACTIVATE= 3 (MA_NOACTIVATE)
Z-order below   = Progman / explorer.exe
Responding      = true
Working set     ≈ 28.7 MB
```

顶部中心列的屏幕像素由壁纸深色直接过渡到海报深色，没有出现用户截图中的高亮白色段；旧截图同位置的亮带约为 `RGB(214,234,250)`。自动探针通过，仍等待用户在真实桌面布局中做最终目视确认。

## 边界

- 本轮修复的是当前 ToolWindow 回退模式；未来单全局桌面宿主仍由 C-DRP 收敛；
- 不采用高频 Z 序轮询，不注入 Explorer，也不强制创建隐藏 WorkerW；
- 当前视觉仍是延期改版前的深色 Demo，不代表米白清新主题已经开始制作。
