# Desktop-Up 项目主线

最后检查：2026-08-02

状态：`SUPERSEDED`

> 本计划冻结于 2026-08-04，不再新增任务。V1 当前唯一 A 层实施计划为 [`DESKTOPUP_V1_IMPLEMENTATION_PLAN.md`](DESKTOPUP_V1_IMPLEMENTATION_PLAN.md)。本文保留用于追溯旧 A0–A6 阶段模型；其中未完成硬门已迁移到新计划，不因冻结而取消。

## 1. 产品定位

Desktop-Up（暂定名）是 Windows 11 上的低资源、可换皮桌面海报启动器。它首先解决游戏快捷方式杂乱的问题：用户可把快捷方式收纳到可拖动、可折叠、自动排版的风格化海报中，并继续从海报启动游戏。

项目不是完整的 Explorer 替代品，也不在第一版篡改 Windows 原生桌面图标布局。它维护自己的可恢复快捷方式集合、海报布局与主题运行时。

## 2. 产品原则

1. **可恢复优先**：收纳、迁移、卸载都不得丢失原快捷方式或用户文件。
2. **静默低负载**：桌面不可见、系统锁定或全屏游戏运行时停止渲染。
3. **渐进式定制**：普通用户调参数，高级用户导素材，专家用户导受限代码。
4. **编辑与使用分离**：默认使用态不误拖；显式进入编辑态后才能移动、缩放和改布局。
5. **游戏只是首个目标群体**：集合、项目、布局和主题数据模型不得写死为游戏。
6. **桌面主体与管理界面分离**：正常运行只持续显示桌面海报；控制中心、设置和 Agent 必须按需呼出且生命周期独立。

## 3. 项目阶段

| 阶段 | 目标 | 退出条件 | 状态 |
| --- | --- | --- | --- |
| A0 | 可行性与体验定型 | 桌面宿主硬门全部通过，交互原型得到确认 | `ACTIVE` |
| A1 | 本地 MVP | 主动导入/扫描、预览、仅添加、安全收纳、启动、恢复、拖动与折叠闭环 | `PLANNED` |
| A2 | 主题、作者工具与可选 AI | 参数主题、素材包、安全导入、本地 BYOK Agent 和可撤销变更闭环 | `PLANNED` |
| A3 | 游戏来源发现 | Steam、Epic、Xbox/Store 本地发现与去重 | `PLANNED` |
| A4 | 稳定性与性能封板 | 多屏、DPI、Explorer 重启、暂停、升级迁移通过 | `PLANNED` |
| A5 | 安装与发布 | `DesktopUpSetup.exe` 和 `uninstall.exe` 通过净机验收 | `PLANNED` |
| A6 | 通用集合扩展 | 支持应用、文件夹和用户自定义对象 | `FUTURE` |

## 4. 当前 A0 主线

当前只激活 `Desktop Runtime Foundation`，由 `Desktop Host Feasibility` 工作流验证：

- [`运行表面模型 V0.1`](RUNTIME_SURFACE_MODEL_V0.1.md) 中的默认可见性和控制中心分离契约；

- Windows 11 桌面层宿主及回退模式；
- 海报区域交互和空白区域鼠标穿透；
- Win+D、Explorer 重启、显示器热插拔和 DPI 变化；
- 前台全屏/最大化、锁屏和显示器关闭时的事件驱动暂停；
- Windows 通知区域图标、右键主页/设置/快速主题菜单和单实例生命周期；
- 静态状态无持续帧循环；
- 运行异常时不影响 Explorer 和正常桌面使用。

## 5. A1 已排定的真实导入模型

A0 硬门通过后激活 `Collection and Launch`。A1 按“只读扫描与数据模型 → 导入预览与仅添加 → 两阶段安全收纳 → 启动、异常与卸载恢复”推进。任何真实桌面移动能力都晚于只读导入验证，且必须逐批获得用户确认。权威契约见 [`快捷方式导入与安全收纳模型 V0.1`](SHORTCUT_IMPORT_AND_COLLECTION_MODEL_V0.1.md)。

## 6. 发布定义

首个可发布版本目标为 `0.1.0`，面向 Windows 11 x64、按用户安装、不要求管理员权限。正式输出：

```text
dist/
  DesktopUpSetup.exe
```

安装完成后至少包含：

```text
DesktopUp.exe
uninstall.exe
resources/
licenses/
```

详细约束见 [`RELEASE_AND_UNINSTALL_POLICY.md`](../../Rules/RELEASE_AND_UNINSTALL_POLICY.md)。

## 7. 当前不做

- 不替换 Explorer，不注入 Explorer 进程。
- 不承诺在所有 Windows 内部桌面窗口结构上强制挂载成功。
- 不执行来源不明的原生 DLL、EXE 或无限权限脚本主题。
- 不提供 Desktop-Up 官方 AI Key、云代理、计费或无 Key 后台 Agent。
- 不在 MVP 自动下载受版权保护的游戏封面。
- 不删除游戏本体、游戏平台或桌面上的普通用户文件。
