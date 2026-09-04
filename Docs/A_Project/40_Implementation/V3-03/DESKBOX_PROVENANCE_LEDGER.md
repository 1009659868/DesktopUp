# DeskBox 来源登记 — V3-03

来源仓库：`https://github.com/Tianyu199509/DeskBox.git`

来源分支：`main`

来源 commit：`64f27e1aa0f16867abe20074f35ea05cd7b12ce7`

许可证：`GPL-3.0-only`

| DeskBox 锚点 | DesktopUp 用途 | V3 处理 |
| --- | --- | --- |
| `src/DeskBox/Views/WidgetWindowBase.Interaction.cs` | pointer capture、屏幕坐标 delta、每帧只消费最新 drag/resize sample、结束前 flush、结束后一次保存、capture lost 清理 | 已吸收最新采样约 16 ms 合并、最终帧刷新和单次 geometry commit；DesktopUp 改由原生稀疏 InputIsland 持有 capture，visual HWND 不直接接收背景输入 |
| `src/DeskBox/Views/WidgetWindowBase.Collapse.cs` | 展开高度保存、收起/展开状态转换和交互互斥 | 采用顶部锚定的最小模型，固定 60 DIP，保留 top/left/width/expanded height；不移植 DeskBox widget 业务状态 |
| `src/DeskBox/Views/WidgetWindowBase.Bounds.cs` | 无边框 bounds、DIP/物理转换，以及 Win10 `SetWindowPos` 使用 `SWP_NOCOPYBITS` 与 `SWP_DEFERERASE` 提交尺寸 | R5 吸收无复制/延后擦除 flags，并额外保持 `NOZORDER` 与 `NOACTIVATE`、HRGN 预提交、物理 read-back 和 InputIsland 同 revision；拖缩仍受当前工作区约束 |
| `src/DeskBox/Views/WidgetWindowBase.InputSuppression.cs` | 交互抑制与窗口生命周期协调意图 | 只吸收“恢复与用户手势不能互相覆盖”的边界；V3 使用独立 generation/recovery 协调器 |
| `src/DeskBox/Views/WidgetWindowBase.Bounds.cs` 的 `ApplyDwmBorderStyle` | DWM 边框恒设透明，圆角边线仅由 XAML `BackgroundPlate` 绘制 | 已保持 `DWMWA_BORDER_COLOR=0xFFFFFFFE`；USING 不绘制应用外框，EDITING 仅绘制 1 DIP 状态边线，避免把系统边框与编辑提示混为一谈 |
| `src/DeskBox/Views/WidgetWindowBase.Backdrop.cs` 的 `ApplyCompactBorderVisuals` | 材质、边线厚度、颜色和圆角由视觉 surface 独立控制 | 吸收“外观属于视觉层、窗口边界属于宿主层”的分离方式；本阶段不复制 DeskBox 主题资产，后续在 V3-07 按 DesktopUp 主题合同重做 |
| `src/DeskBox/Views/ContentWidgetWindow.xaml` 的八向透明 resize grid | 独立透明边缘命中区与方向光标 | R5 已在原生稀疏 InputIsland 中补齐四边四角八向命中、系统方向光标与 pointer capture；没有复制 DeskBox 整窗输入层，USING 空白仍穿透 Explorer |
| `src/DeskBox/App.xaml` 的 `WidgetCornerRadiusLarge=8`、`WidgetTitleActionButtonStyle` 24-DIP 尺度及 4/8/12/16 间距语言 | 常规 Box 的外壳尺度 | R2 使用 8-DIP 外圆角、24-DIP 操作尺度与克制间距；不复制 DeskBox XAML 控件树 |
| `src/DeskBox/Controls/WidgetShell.xaml` 的 46-DIP 标题行、`14,7,12,5` padding、2-DIP 操作间距、24-DIP `MoreButton` / `CollapseButton` 和透明 title surface | 常规 Box 的标题栏结构与操作组织 | R4 采用“收起/展开 + 更多”结构，并把可见按钮几何提升为 Domain 唯一合同供视觉和 InputIsland 共用；DesktopUp 不复制 DeskBox 整窗输入方式 |
| `src/DeskBox/Views/ContentWidgetWindow.Commands.cs` 的 `MenuFlyout.ShowAt`、菜单关闭后桌面层级恢复 | 更多菜单与弹出层生命周期 | R4 使用 WinUI `MenuFlyout` 承载锁定、编辑、控制中心及文件能力占位；关闭后重新断言 desktop-bottom。真实文件创建仍由 V3-05 事务合同实现 |
| `src/DeskBox/App.xaml` 的 `WidgetTitleActionButtonStyle` 与菜单字体、36-DIP 最小项高、`10,8` padding | 操作与菜单密度 | 吸收克制的 24-DIP 标题操作和 36-DIP 菜单项密度；颜色与完整视觉主题留待 V3-07 |
| `src/DeskBox/Services/WidgetBorderVisualCalculator.cs` 的默认 0.8-DIP/`0x18` 白色细边 | 常规深色 Box 边界 | R2 曾使用相同默认尺度，但人工截图表明在 DesktopUp 桌面海报上形成明显白框；R3 已撤销 USING 边线，EDITING 单独保留 1-DIP 状态色，此项记录为反证而非最终视觉决策 |
| `src/DeskBox/Services/WidgetCompactBoundsCalculator.cs` 的 Round 外圆角 8、内圆角 4 | 外壳与卡片圆角层级 | R2 采用 8/4 两级圆角并把外圆角同步到原生 HWND/HRGN；后者为 DesktopUp 针对 layered 视觉窗的独立补强 |
| `src/DeskBox/Services/WidgetMaterialVisualCalculator.cs` 的深色 Solid 基色 `#21242A` | 无系统 backdrop 时的稳定深色回退 | R2 用作常规 Box 实色 surface；没有提前引入 Mica/Acrylic 控制器或 V3-07 主题热更新 |

## DesktopUp 独立补强与本轮反证

DeskBox 的整窗 WinUI pointer 路线不能直接证明 DesktopUp 的跨进程空白穿透。V3 首轮只给顶层视觉 HWND 返回 `HTTRANSPARENT` 后，实机 `WindowFromPoint` 仍命中 WinUI 的 `Microsoft.UI.Content.DesktopChildSiteBridge`，导致桌面框选、桌面右键和原生快捷方式均失败。

R1 采用“透明视觉合成 HWND + 稀疏原生 InputIsland”：视觉 HWND 在 WinUI 完成 content bridge 初始化后重新施加 `WS_EX_LAYERED | WS_EX_TRANSPARENT`，并把这两个位纳入 attachment health；Item、标题、按钮、编辑边缘和窄滚动轨由独立 HRGN 输入岛接收。原生探针已证明空白点落到 Explorer `SysListView32`，Item 点仍落到 `DesktopUp.V3.PosterInputIsland`。此机制是 DesktopUp 的需求补强，不声称来自 DeskBox。

Item 区使用 WinUI 自带 `ScrollViewer`，不手写裁剪/滚动控件；InputIsland 只负责把跨 HWND 的滚轮意图转交给标准控件。大集合虚拟化与活跃度模型仍由 V3-06 完成。

DeskBox 的 widget 是整窗交互，不能直接满足 DesktopUp“半透明海报空白必须落到 Explorer”的要求。`PosterInputIslandManager`、同 revision 稀疏区域、视觉 HWND 全透明命中及 protected chrome 权限矩阵属于 DesktopUp V3 的独立补强；不得把 DeskBox 的现成功能当作这些行为的验证证据。

R2 仅迁移若干视觉常量并未形成可信的 DeskBox 常规 Box 复刻，用户明确判定整体差异过大。后续若重新开展视觉适配，必须以完整 shell 层次、材质、交互显隐和内容密度为单位建立独立视觉稿与验收门，不能再把常量相近等同于样式完成。
