# C-IBC-04 自启边界与清理契约

日期：2026-08-13

状态：`COMPLETE — C-IBC-05 NEXT`

## 1. 结果

- 新增 `autostart-contract-v1`，冻结 HKCU Run 值、默认关闭、显式 opt-in、正式路径门、严格命令和责任 owner；
- 新增 `autostart-diagnostics-v1` Tauri 命令，只读查询当前用户注册表并区分 absent、matching、drifted、unreadable；
- 开发/便携 Release、错误文件名、缺失文件、symlink、错误值类型、错路径和额外参数均不能成为有效自启；
- 冻结 `--autostart` 单实例语义，当前明确为 `contractOnlyCrlcPending`，未误称 C-RLC 已接通；
- 清理只拥有精确 `DesktopUp` 值；值不存在为成功，恢复失败后仍必须清理，不删除父键或其他应用值。

## 2. 安全边界

- 生产代码只调用 `RegOpenKeyExW`、`RegQueryValueExW` 和 `RegCloseKey`；
- 未引入 RegCreate/RegSet/RegDelete 或其他注册表变更 API；
- 托盘和控制中心自启入口保持禁用；
- 本机 HKCU Run 的 `DesktopUp` 值在构建、启动、退出前后均不存在；
- 未创建计划任务、服务或 Startup 文件夹入口。

## 3. 验证

- C-IBC-04 Node 定向：5/5；
- C-IBC-04 Rust 定向：7/7；
- 前端/契约全量：122/122；
- Rust 全量：111/111；
- 文档链接：139 份；
- UI production、Release 和 NSIS 构建：通过；
- Release 启动烟测：通过，最终 PID `42896`；
- SQLite 烟测前后 SHA-256：`DA63A52BDF62B3FC863995D79F497584BE3BA97C1DE4C55D1D301AFBAFAB6CFC`；
- Release SHA-256：`BCEE51A578487F512A21282B2E46120987F29A6923BDC8219190943CFA47B23F`；
- NSIS SHA-256：`DBDDF6C6289D116178277D9B327AAC07399A61CA6C489720AA61BBDD6CCE0C1D`，大小 `2,972,748` bytes；
- C-IBC-03 的五项 C-RCG 打包差距保持不变。

## 4. 下一步

C-IBC-05 收敛 Release/NSIS、WebView2/VC/SQLite 依赖证据、许可证缺口和后继输入清单，完成 C-IBC 到 C-RLC 的交接门。
