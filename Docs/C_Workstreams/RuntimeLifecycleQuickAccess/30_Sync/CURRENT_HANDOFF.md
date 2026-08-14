# Runtime Lifecycle and Quick Access 最终交接

最后更新：2026-08-14

状态：`COMPLETE — C-RLC CLOSED / C-LST COMPLETE / C-PSI NEXT`

## 完成事实

- C-RLC-01..06 全部完成；[`runtime-lifecycle-matrix-v1`](../10_Plan/RUNTIME_LIFECYCLE_MATRIX_V1.md) 已封存为 `SEALED — C-RLC COMPLETE`；
- `launch-intent-v1` 统一普通、自启、内部性能诊断和非法参数；首/次实例只保留一套海报、托盘与按需控制中心；
- `autostart-contract-v1` 保持默认关闭、正式路径门、HKCU Run `DesktopUp` 自有值、写后读回和两端统一快照；临时 feature-scoped eligible 路径的真实闭环已通过，正式净机安装认证延期给 C-RCG；
- `control-center-lifecycle-v1` 已实现显式 ready、有序命令队列、零/一控制中心、优先释放 WebView，以及托盘原生 runtime/SQLite CAS 快捷动作；用户人工矩阵通过；
- `taskbar-recovery-v1` 已实现原生 TaskbarCreated、去抖、三次有界重试、唯一托盘验证/回退重建、degraded/手动重连及不可逆 Exiting；真实 Explorer 与退出竞态人工矩阵通过；
- 最终 Node/文档 `159/159`、Rust `132/132`、production UI、Release/NSIS、安装内容与运行时依赖门通过；
- 最终 Release 多进程探针验证：普通第二实例只打开一个控制中心，`--autostart` 第二实例不改变 UI，全程一个主进程和一个托盘消息窗口；烟雾结束后 HKCU Run `DesktopUp` 为 absent；
- 最终 `DesktopUp.exe` SHA-256 为 `AD2CB602F575D2A3A5AC79FA4AA9CCE8ED725ECE804064ECC7D046C542F9F9C6`；`DesktopUpSetup.exe` 为 `4FE1DF7B1F27A7323877E1F1359853F9A3174AC05B91D4F0BEFB79D5876A7D9E`。

## 明确延期与 owner

- 正式安装路径自启漂移修复、净机安装、构建清单、签名和完整发布矩阵：C-RCG；
- 数据目录迁移与恢复型卸载：C-UGM / C-URV；
- 多显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启和热插拔：后继平台矩阵；
- 控制中心 legacy 视觉参数回弹：后继 Template Platform 参数权威；
- 当前发布认证继续仅限主屏单海报。

## 精确下一步

1. 后继 C-LST 已通过既有实现吸收审计并关闭；
2. 用户明确进入后激活已提前规划的 C-PSI，并先执行现状审计；
3. C-PSI 完成后进入已提前规划的 C-CIM，让硬编码演示卡片退出正式路径。

## 工作区与边界

- C-RLC-01..06 文档和代码尚未提交或推送；最近远端同步点仍为 `be29fc9`；
- 本轮没有执行 Git commit/push，没有安装/卸载、没有写入自启值，也没有移动用户桌面文件；
- C-RLC 关闭不等于 V1-P1、M1、C-RCG、净机发布、多显示器或 Item 能力完成。
