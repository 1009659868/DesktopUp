# Desktop-Up 运行状态一致性修复

日期：2026-08-08

状态：`FIX VERIFIED / USER VISUAL CONFIRMATION PENDING`

## 修复范围

本轮属于 C-DHF 关闭后的维护修复，不代表 `C-DRP`、`C-PCB` 或 M1 海报模板已经完成。目标是修复当前 A0/V1 落地前基线中可复现的状态、几何和托盘行为缺陷。

## 已确认问题

- Dev 使用 `http://127.0.0.1:4173`，Release 使用 `http://tauri.localhost`，WebView `localStorage` 按来源隔离，导致两种启动方式显示不同主题、尺寸和锁定状态；
- 前端会把超出旧 `560px` 上限的海报高度破坏性截断并写回；
- `allLocked` 可能停留在旧值，与实际海报锁定状态不一致，锁定提示文字也使用了切换前状态；
- 托盘的编辑、全局锁定和快捷主题会顺带显示主页，违背“桌面海报与主页分离”的运行表面模型；
- 海报窗口配置仍允许进入任务栏；首次无共享快照时，先操作桌面海报存在把默认双海报状态缩成单海报的边界；
- USING 模式的图标单击行为与设计稿不一致，仅双击提供启动反馈。

## 实现修复

- 新增 Rust 过渡共享存储 `poster-snapshot-bridge-v1.json`，由 Dev 与 Release 共同读取；首次无原生快照时迁移现有本地快照，随后原生快照作为权威数据，本地存储仅作 WebView 缓存；
- 共享存储整份快照写入在同一互斥区内串行化，并通过 `poster-snapshot-updated` 同步所有窗口；
- 首次启动由隐藏主页立即保存完整默认快照，桌面海报的缺省写入也以完整默认快照为底，避免丢失第二张海报；
- 海报存储包络扩展为 `240..1920 × 240..2160`，原生最终物理尺寸按当前显示器工作区钳制，避免旧上限破坏用户数据或窗口超过可用工作区；
- `allLocked` 改为始终由海报数组派生，默认锁定状态对齐 V1 的“默认解锁”，修正锁定提示文字；
- 托盘编辑、全局锁定与快捷主题只派发控制事件，不再主动显示主页；设置与 Agent 仍按设计呼出主页；
- 海报窗口设置 `skipTaskbar: true`；USING 单击提供启动反馈，双击提供启动并标记最近活跃反馈，EDITING 单击保持选择语义。

## 验证

```text
pnpm run check
  32 tests passed
  80 Markdown files verified

cargo test --locked --manifest-path src-tauri/Cargo.toml
  18 tests passed

cargo tauri build --no-bundle
  PASS
```

- Release 首次启动已生成 `%APPDATA%/com.allmight.desktopup/poster-snapshot-bridge-v1.json`；
- Release 快照为 `glass / 490×392 / unlocked`，随后启动 Dev，Dev 来源的最新缓存被同步为同一组值，证明跨来源状态分裂已消除；
- 修复版 Release 启动后进程保持 `Responding=True`，静置采样无持续 CPU 增长；
- Release 必须通过 `cargo tauri build --no-bundle` 或 `pnpm run tauri:build` 构建，不能只运行 `cargo build --release`，否则不会执行 `beforeBuildCommand`，可能继续嵌入旧的 `dist`。

## 保留边界

- `poster-snapshot-bridge-v1.json` 是从浏览器存储迁移到后续 SQLite/正式状态仓库前的过渡桥接，不是 C-DRP 最终持久化架构；
- 当前仍是单独原生海报窗体，只渲染第一张海报；单全局宿主、多海报同宿主、Shadow DOM、三套正式模板和模板安装仍属于后续计划；
- 控制中心预览坐标与原生窗体位置仍是两个不同用途的数据域，尚未完成 C-DRP 的单一几何所有权收敛；
- 图标目前仍为 Demo 数据，启动反馈不等于已经接入真实快捷方式执行。
