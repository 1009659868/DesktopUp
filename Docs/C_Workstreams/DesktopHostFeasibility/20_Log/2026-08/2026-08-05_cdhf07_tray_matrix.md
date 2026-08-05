---
name: cdhf07-tray-gap-matrix
description: C-DHF-07 托盘功能矩阵核对结果——设计文档 vs 当前代码实现
metadata:
  type: project
---

# C-DHF-07 托盘功能矩阵核对

日期: 2026-08-05
设计输入: TRAY_AND_QUICK_ACCESS_DESIGN_V0.1 + RUNTIME_SURFACE_MODEL_V0.1
实现文件: src-tauri/src/lib.rs (#[cfg_attr(mobile…)] pub fn run() 中 TrayIconBuilder 部分)

## 核对结果总览

| 维度 | 已对齐 | 存在差距 |
|------|--------|---------|
| 基础行为 (5项) | 5 | 0 |
| 右键菜单 (14项) | 8 | 6 |
| 图标状态 (3项) | 0 | 3 |
| 生命周期 (6项) | 4 | 2 |
| 性能边界 | 3 | 0 |

## 基础行为 (全部通过)

| 行为 | 设计要求 | 代码位置 | 状态 |
|------|---------|---------|------|
| 单击托盘 | 显示/隐藏海报 | lib.rs:563-568 toggle_poster_window | PASS |
| 双击托盘 | 打开并聚焦主页 | lib.rs:569-572 show_main_window | PASS |
| 右键托盘 | 打开原生菜单 | lib.rs:536 show_menu_on_left_click(false) | PASS |
| 关闭主页 | 隐藏到托盘不退出 | lib.rs:648-654 CloseRequested → prevent_close + hide | PASS |
| 显式退出 | 保存状态,结束进程 | lib.rs:553-560 flush placement + app.exit(0) | PASS |

## 右键菜单逐项对比

菜单顺序按设计文档 §3 结构:

| 菜单项 | 设计要求 | 当前实现 | 差距 |
|--------|---------|---------|------|
| 打开主页 | "打开主页" | "打开 Desktop-Up 主页" show_main_window | OK |
| 设置… | "设置…" | "设置…" emit("open-settings") | OK |
| 桌面挂载状态 | 设计无此项目 | "桌面挂载：正在初始化" disabled | 遗留(A0) — 无需移除 |
| 运行状态 | "需要处理:…"（状态驱动） | "运行状态：正在初始化" disabled | 差距 |
| 显示海报宿主 | [✓] 可勾选 | CheckMenuItem "显示海报宿主" checked:true | OK |
| 编辑布局 | [ ] 可勾选 | CheckMenuItem "编辑海报布局" checked:false | OK |
| 锁定全部海报 | [✓] 可勾选 | CheckMenuItem "锁定全部海报" checked:true | OK |
| 暂停动态效果 | [ ] 可勾选 | CheckMenuItem "暂停动态效果" checked:false | OK |
| 快速主题子菜单 | > 深夜行动/余烬档案/玻璃陈列 | theme_menu 含三项主题 | OK |
| 快速主题-黑白线描 | 设计含此项 | 缺失 | 差距(设计含4主题,实现含3) |
| 快速主题-管理主题… | 设计含此项 | 缺失 | 差距 |
| AI 助手 | Key未配:"配置 AI…" Key已配:"AI 助手…" | 固定显示"Agent 助手(需要 API Key)" | 差距 |
| 开机自启 | [ ] 可勾选 | "开机自启(A1 接入)" disabled | 差距 |
| 重新连接桌面 | "仅在挂载降级或失败时突出显示" | "重新连接桌面宿主" 始终可见 | 差距(缺条件可见) |
| 退出 | "退出 Desktop-Up" | "退出 Desktop-Up" | OK |

## 图标状态 (全部存在差距)

| 状态 | 设计要求 | 现状 |
|------|---------|------|
| 正常 | 标准品牌图标 | 有图标(临时) |
| 已暂停 | tooltip 增加"动态效果已暂停" | tooltip 固定为"Desktop-Up · 游戏海报桌面" |
| 需要处理 | tooltip 增加故障原因 | 未实现 |

## 生命周期 (2 项差距)

| 事件 | 设计要求 | 现状 | 差距 |
|------|---------|------|------|
| 普通启动只显示海报 | 控制中心不自动创建 | 同设计(tauir.conf.json main.visible:false) | OK |
| 第二实例 | 呼回已有控制中心,不创建第二个托盘 | tauri_plugin_single_instance 已接入 | OK |
| Explorer 重启(TaskbarCreated) | 重建托盘图标 | 未实现 | 差距 |
| 正常退出 | 先取消Agent请求,保存布局,移除托盘 | lib.rs:553-560 flush+exit;Agent取消未实现 | 差距 |
| 卸载 | 清除自启和凭据 | 未实现(M6阶段) | M6 负责 |

## 总结

C-DHF-07 基准能力已具备(基础行为+核心菜单),无需阻塞当前阶段.
差距项中仅 TaskbarCreated 恢复属于 P0 硬门范围内；其余为增强项.

### 需在 C-DHF-07 内关闭

1. **TaskbarCreated 托盘恢复** — Windows Explorer 重启后重建托盘图标和菜单
2. **菜单运行状态项显示真实状态** — 当前 disabled 占位,需接入 RuntimeSnapshot

### 后续阶段再处理

3. 快速主题菜单新增黑白线描+管理主题入口
4. AI Key 状态驱动的菜单文案切换
5. 开机自启从 disabled 改为功能接入
6. 重新连接桌面条件可见(仅降级时突出显示)
7. 托盘 tooltip 动态更新(暂停/故障状态)
8. 退出时取消 Agent 请求
