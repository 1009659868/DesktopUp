# DeskBox 来源登记 — V3-03

来源仓库：`https://github.com/Tianyu199509/DeskBox.git`

来源分支：`main`

来源 commit：`64f27e1aa0f16867abe20074f35ea05cd7b12ce7`

许可证：`GPL-3.0-only`

| DeskBox 锚点 | DesktopUp 用途 | V3 处理 |
| --- | --- | --- |
| `src/DeskBox/Views/WidgetWindowBase.Interaction.cs` | pointer capture、屏幕坐标 delta、每帧只消费最新 drag/resize sample、结束前 flush、结束后一次保存、capture lost 清理 | 已吸收最新采样约 16 ms 合并、最终帧刷新和单次 geometry commit；DesktopUp 改由原生稀疏 InputIsland 持有 capture，visual HWND 不直接接收背景输入 |
| `src/DeskBox/Views/WidgetWindowBase.Collapse.cs` | 展开高度保存、收起/展开状态转换和交互互斥 | 采用顶部锚定的最小模型，固定 60 DIP，保留 top/left/width/expanded height；不移植 DeskBox widget 业务状态 |
| `src/DeskBox/Views/WidgetWindowBase.Bounds.cs` | 无边框 bounds、DIP/物理转换和窗口边界提交 | 复用 V3-02 已登记的宿主与边界策略；拖缩仍受当前工作区和读回约束 |
| `src/DeskBox/Views/WidgetWindowBase.InputSuppression.cs` | 交互抑制与窗口生命周期协调意图 | 只吸收“恢复与用户手势不能互相覆盖”的边界；V3 使用独立 generation/recovery 协调器 |
| `src/DeskBox/Views/WidgetWindowBase.Bounds.cs` 的 `ApplyDwmBorderStyle` | DWM 边框恒设透明，圆角边线仅由 XAML `BackgroundPlate` 绘制 | 已保持 `DWMWA_BORDER_COLOR=0xFFFFFFFE`；USING 不绘制应用外框，EDITING 仅绘制 1 DIP 状态边线，避免把系统边框与编辑提示混为一谈 |
| `src/DeskBox/Views/WidgetWindowBase.Backdrop.cs` 的 `ApplyCompactBorderVisuals` | 材质、边线厚度、颜色和圆角由视觉 surface 独立控制 | 吸收“外观属于视觉层、窗口边界属于宿主层”的分离方式；本阶段不复制 DeskBox 主题资产，后续在 V3-07 按 DesktopUp 主题合同重做 |
| `src/DeskBox/Views/ContentWidgetWindow.xaml` 的八向透明 resize grid | 独立透明边缘命中区与方向光标 | 当前先保持需求内的右、下、右下三向 InputIsland；待需求扩展时可按同一输入岛合同补齐，不直接让整窗重新接管输入 |

## DesktopUp 独立补强与本轮反证

DeskBox 的整窗 WinUI pointer 路线不能直接证明 DesktopUp 的跨进程空白穿透。V3 首轮只给顶层视觉 HWND 返回 `HTTRANSPARENT` 后，实机 `WindowFromPoint` 仍命中 WinUI 的 `Microsoft.UI.Content.DesktopChildSiteBridge`，导致桌面框选、桌面右键和原生快捷方式均失败。

R1 采用“透明视觉合成 HWND + 稀疏原生 InputIsland”：视觉 HWND 在 WinUI 完成 content bridge 初始化后重新施加 `WS_EX_LAYERED | WS_EX_TRANSPARENT`，并把这两个位纳入 attachment health；Item、标题、按钮、编辑边缘和窄滚动轨由独立 HRGN 输入岛接收。原生探针已证明空白点落到 Explorer `SysListView32`，Item 点仍落到 `DesktopUp.V3.PosterInputIsland`。此机制是 DesktopUp 的需求补强，不声称来自 DeskBox。

Item 区使用 WinUI 自带 `ScrollViewer`，不手写裁剪/滚动控件；InputIsland 只负责把跨 HWND 的滚轮意图转交给标准控件。大集合虚拟化与活跃度模型仍由 V3-06 完成。

DeskBox 的 widget 是整窗交互，不能直接满足 DesktopUp“半透明海报空白必须落到 Explorer”的要求。`PosterInputIslandManager`、同 revision 稀疏区域、视觉 HWND 全透明命中及 protected chrome 权限矩阵属于 DesktopUp V3 的独立补强；不得把 DeskBox 的现成功能当作这些行为的验证证据。
