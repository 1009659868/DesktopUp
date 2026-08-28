# QTS-03 实施日志（2026-08-27）

状态：`CLOSED — RELEASE + 9/9 CTEST PASS`

## 过程摘要

1. 按 QTS-02 交接建立根级 CMake、正式 `src/` 拓扑和两个独立应用 target；
2. 建立 framework-free contracts/core、SQLite StateStore/worker、QPainter/Host/diagnostics adapters；
3. 建立当前用户 ACL + peer token SID 的 named pipe、同会话 mutex 与三类 secondary intent；
4. 首轮 Release 构建成功；首轮 CTest 为 6/8，定位到 overlapped pipe 句柄误用同步 I/O，以及架构脚本扫描自身；
5. 改为有界 overlapped read/write、客户端实例重建重试和客户端 process token SID 校验；架构扫描仅限生产源码扩展名；
6. IPC 超长消息异步断言改为有界等待，并保持连接直至服务端完成 fail-closed；IPC 单项连续 10 轮通过；
7. 补齐 SnapshotCursor、session mutex、Runtime primary/secondary 实际分流和真实多进程 Control Center 关闭夹具；
8. 最终 Release 全量结果为 9/9 PASS；PE 核对 Runtime 无 QtQuick/QML；退出账本 ownedResourceCount 为 0。

## 未扩大范围

- 没有实现真实 WorkerW 海报；
- 没有复用 candidate demo composition root；
- 没有修改冻结需求、历史候选结论或 50 MiB FAIL；
- 没有提交、推送或合并 Git。
