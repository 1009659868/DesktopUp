# QTS-03 → QTS-04 交接（2026-08-27）

状态：`CONSUMED — QTS-04-01 COMPLETE / HISTORICAL QTS-04 ENTRY INPUT`

## 1. QTS-04 目标

在 QTS-03 已通过的物理骨架内，把 Host fixture 和最小 QPainter frame 替换为真实单海报生产运行时。目标层级保持：

~~~text
普通窗口 / 文件选择器 / 全屏程序
                >
          DesktopUp 海报
                >
          Explorer 桌面图标
                >
              壁纸
~~~

禁止使用 TopMost、激活窗口、隐藏再恢复或轮询 SetWindowPos 伪造这一层级。

## 2. 强制输入

1. QTS-03 审计、代码锚点和 9 项绿色夹具；
2. QTS-02 物理架构；
3. TS-H01-E2～E6 与 `DESKTOP_HOST_PROTOTYPE_INTERFACE_V1.md`；
4. 冻结的海报视觉/交互和 Runtime surfaces 需求；
5. TS-C01 历史白条、灰框、输入、Explorer、Win+D 和显示矩阵反例；候选 PASS 不能自动继承。

## 3. 建议执行顺序

| ID | 工作项 | 主要输出 | 退出条件 |
| --- | --- | --- | --- |
| QTS-04-01 | 真实 DesktopHost adapter | WorkerW/DefView 解析、目标 sibling 插入、Explorer generation/reconnect、无激活显示 | 原生层级探针 + 自动 attach/detach/重建夹具通过 |
| QTS-04-02 | 单海报 QPainter renderer | NIGHT OPS 单海报视觉、premultiplied alpha、脏区/尺寸、frame ownership | 无顶部白条、底部灰框、暗灰外框或透明污染 |
| QTS-04-03 | split input islands | 透明空白穿透、卡片/按钮/标题栏、拖动/缩放、锁定、收起/展开 | Explorer 框选/右键/图标与应用交互同时通过 |
| QTS-04-04 | 生命周期与恢复 | Win+D、桌面刷新、文件选择器、普通/全屏、Explorer 重建、显示变化、退出 | 不隐藏闪烁、不越级、不残留；generation/epoch 单调 |
| QTS-04-05 | 单海报生产 P0 | Release 自动证据 + 用户主屏人工门 + 代码锚点/交接 | QTS-04 全矩阵关闭后才进入 QTS-05 |

## 4. 必须复测的失败导向矩阵

- 首次显示、拖动中、拖动释放、打开/关闭控制中心后均无顶部白条；
- 海报外边缘和底部无灰框、暗边或非客户区残留；
- 桌面空白左拖出现 Explorer 框选并可拖出；空白右键只出现 Windows 桌面菜单；
- 海报覆盖范围内的原生图标仍可单击/双击；
- 卡片、锁定、收起/展开和标题栏仅在自己的 input island 命中；
- 普通窗口、文件选择器和全屏程序始终高于海报；
- Win+D 不隐藏、恢复或闪烁海报；
- 桌面右键/刷新、任意左键、文件选择器不触发海报瞬时置顶；
- Explorer 重启后海报有界恢复，不生成 TopMost fallback；
- 分辨率、DPI、工作区变化后 visible bounds 与 input bounds 同步；
- Runtime 退出后 HWND、bitmap、pipe、worker 和临时文件全部归零。

## 5. QTS-04 非目标

- 不提前实现完整 Control Center、托盘和状态编辑（QTS-05）；
- 不实现真实 Item、活跃度和动态排版（QTS-06）；
- 不实现主题包热更新（QTS-07）或 AI（QTS-08）；
- 不用 Qt Quick candidate 的视觉或生命周期结果替代 QPainter/真实 Host 新证据。

## 6. 立即动作

进入 `QTS-04-01：真实 DesktopHost adapter 与 attach/reconnect 自动夹具`。先替换 Host fixture，保持当前最小 QPainter frame，以便把层级/Explorer 生命周期问题与视觉问题分开定位。
