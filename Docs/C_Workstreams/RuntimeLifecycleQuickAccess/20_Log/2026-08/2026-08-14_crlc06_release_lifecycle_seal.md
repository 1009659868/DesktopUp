# C-RLC-06 Release 生命周期矩阵与封板

日期：2026-08-14

状态：`COMPLETE — C-RLC CLOSED`

## 封板结论

`runtime-lifecycle-matrix-v1` 的启动/单实例、控制中心/托盘、运行状态、自启、TaskbarCreated、手动恢复与退出行均已形成自动、Release 或用户人工证据。C-RLC-01..06 全部完成，Runtime Lifecycle and Quick Access 工作流关闭。

没有把以下后继责任冒充为 C-RLC 已完成：

- 正式 `%LOCALAPPDATA%\Programs\DesktopUp` 净机安装后的自启漂移修复与完整安装认证：`C-RCG`；
- 净机安装、升级、签名、恢复型卸载：`C-RCG` / `C-UGM` / `C-URV`；
- 多显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启与热插拔：后继平台矩阵；
- 控制中心 legacy 视觉参数回弹：后继 Template Platform 参数权威。

## 最终自动证据

- Node/语法/文档门：`159/159`，Markdown 链接 `157` 个文件通过；
- Rust：`132/132`；
- production UI 与 Tauri Release/NSIS 构建通过；
- 安装产物探针：`ready`、`gaps=[]`；运行时依赖探针：`dependencyEvidenceStatus=pass`、`releaseReadiness=ready`；
- `DesktopUp.exe` SHA-256：`AD2CB602F575D2A3A5AC79FA4AA9CCE8ED725ECE804064ECC7D046C542F9F9C6`；
- `DesktopUpSetup.exe` SHA-256：`4FE1DF7B1F27A7323877E1F1359853F9A3174AC05B91D4F0BEFB79D5876A7D9E`；版本化安装器与最终别名字节一致；
- 最终 Release 多进程探针：冷启动 `1` 个进程、`0` 控制中心；普通第二实例退出后仍为 `1` 个进程且出现 `1` 个控制中心；关闭后回到 `0`；`--autostart` 第二实例退出后仍为 `1` 个进程、`0` 控制中心；托盘消息窗口始终为 `1`；
- Release 烟雾结束后 HKCU Run `DesktopUp` 为 `absent`，证明普通/第二实例测试未隐式启用自启。

## 累计人工证据

- C-RLC-03：临时 feature-scoped eligible 路径完成真实 enable/noChange/`--autostart`/普通二次启动/disable/readback，其他 Run 值不变；
- C-RLC-04：托盘单击/双击、ready 前路由、控制中心释放重建、海报显隐/暂停、编辑/锁定/主题与核心海报回归通过；
- C-RLC-05：真实 Explorer 重启、连续恢复、状态保持、托盘/控制中心重连、无 TOPMOST 回退与退出竞态全部通过。

## 下一工作流

按冻结顺序，下一候选为 B-PCR 下的 `C-LST：Large Surface and Transparency`，随后为 `C-PSI`。C-LST 尚未在本日志中提前激活或实现；需由用户下一步明确进入后建立/更新其工作流入口。
