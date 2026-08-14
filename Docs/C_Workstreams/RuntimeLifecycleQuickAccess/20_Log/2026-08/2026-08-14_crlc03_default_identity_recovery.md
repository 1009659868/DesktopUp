# C-RLC-03 干净启动默认身份与当前状态恢复

日期：2026-08-14

状态：`IMPLEMENTED / AUTOMATED PASS / CURRENT STATE RECOVERED`

## 问题边界

控制中心投影与真实海报拖动组合回归通过后，当前正式状态仍显示占位身份 `DESKTOP COLLECTION`。需要同时处理两类问题：

1. 无 SQLite、无 legacy 来源的真正干净启动，必须创建已接受的单海报身份 `NIGHT OPS / TACTICAL ARCHIVE / 01`；
2. 已存在状态不能被“默认值修复”无条件覆盖，尤其不能丢失用户已经调整的位置、尺寸、锁定、收起或风格。

旧 A0 的固定 `490 × 392` 不是正式默认几何。本轮继续保留 C-PCB 冻结的主屏近全高默认策略，只恢复视觉身份。

## 实施

- `poster-state-store.js` 的正式默认标题/副标题改为 `NIGHT OPS / TACTICAL ARCHIVE / 01`，浏览器预览、无数据启动和“重置画布”共用同一入口；
- 增加旧正式默认的完整结构识别器：只有 contract v1、revision 0、单 `poster-primary`，且标题、副标题、几何、锁定、收起、布局、风格等全部等于旧默认时，启动才提交一次 `expectedRevision=0` 的 CAS 替换；
- 任一字段被用户修改时，自动恢复不触发；CAS 冲突读取胜者，写入失败则继续显示原 SQLite 状态并返回 `recovery.status=deferred`，不降级成默认覆盖；
- 当前人工编辑后的状态已是 revision 38、`379 × 466`，不满足自动恢复签名。按用户本轮明确授权，使用一次性 Rust 恢复器调用正式 `PosterStateRepository::replace_with_expected_revision`，只替换标题和副标题，其余字段原样保留；一次性恢复器执行后已删除，未进入产品或工作区最终差异。

## 当前状态证据

- 显式 CAS：`previousRevision=38`，`revision=39`，结果 `stored`；
- 重启最新版 Release 后只读 SQLite 复核：revision 39、标题 `NIGHT OPS`、副标题 `TACTICAL ARCHIVE / 01`；
- 尺寸仍为 `379 × 466`，当前位置、展开、未锁定、`transparent-default` 和 `legacyDensity=1` 均保持；
- 当前只有一个项目 Release 进程，路径为 `src-tauri/target/release/DesktopUp.exe`。

## 自动与构建证据

- 状态存储定向测试 `12/12`：干净默认、精确旧默认 CAS、任意用户改动不覆盖、恢复写失败保留原状态；
- 全量 Node/源码/文档门 `151/151`，文档链接 `154`；
- Rust `123/123`；production UI、Release、NSIS、安装产物门和运行时依赖门通过，`gaps=[]`；
- `DesktopUp.exe` SHA-256：`D7BB7BDE3E36778F4E782D906CEF842E4CDD7A368BA36AFB61BC7FAB1C1E128D`；
- `DesktopUpSetup.exe` SHA-256：`DFC51C85B2ABC287A52E10A9DA8A9F24EAC547E3F7C8D831034B65298549CF2B`。

## C-RLC 状态

用户已确认控制中心可见、窗口缩放可见、编辑关闭重开可见，以及真实海报拖动无白条/不覆盖普通程序。上述回归不再阻塞 C-RLC-03。

C-RLC-03 尚未关闭的唯一证据门仍是 eligible 正式安装路径 `%LOCALAPPDATA%\Programs\DesktopUp\DesktopUp.exe` 的 enable/readback、精确 `--autostart` 启动和 disable/readback。G 盘测试路径只证明 ineligible 零写入，不能代替该门。

本轮没有写 HKCU、没有修改 legacy 来源/备份、没有恢复 TOPMOST 轮询，也没有扩大多显示器认证范围。
