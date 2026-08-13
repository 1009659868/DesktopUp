# C-IBC-05 运行时依赖与交接门

日期：2026-08-13

状态：`COMPLETE — C-IBC CLOSED / C-RLC READY TO ACTIVATE`

后续维护：本日志记录的是阻断识别时点；用户确认个人自用范围并要求按契约修复后，当前零缺口状态以 [`2026-08-13_cibc05_private_use_packaging_resolution.md`](2026-08-13_cibc05_private_use_packaging_resolution.md) 为准。

## 本轮完成

- 新增 `runtime-dependency-handoff-gate-v1`，以只读方式组合 C-IBC-03 安装产物探针、PE 元数据/导入表、Cargo 配置与锁文件、Tauri 配置、生成 NSIS、许可证目录和 C-IBC-04 自启值名；
- 新增 `pnpm run probe:runtime-dependencies`，区分“依赖证据通过”“带已分配缺口可交接”和“最终发布就绪”三种语义；
- 自动化覆盖 x64/GUI PE 解析、完整发布就绪夹具、有责任归属但禁止发布的缺口夹具、零副作用和后继 owner 冻结；
- 冻结 [`C_IBC_HANDOFF_INPUTS_V1.md`](../../10_Plan/C_IBC_HANDOFF_INPUTS_V1.md)，将输入交给 C-RLC/C-UGM/C-URV/C-RCG。

## 实际证据

- Release PE：x64、Windows GUI；动态 VC Runtime 导入 `0`，外部 SQLite DLL 导入 `0`；
- WebView2：配置与生成 NSIS 均为 `downloadBootstrapper`，下载失败中止安装；
- 静态 VC Runtime：Cargo 环境与 PE 导入表证据一致；
- bundled SQLite：Cargo manifest/lock 与 PE 导入表证据一致；
- Release、NSIS 与生成 NSIS SHA-256 分别为 `BCEE51A578487F512A21282B2E46120987F29A6923BDC8219190943CFA47B23F`、`DBDDF6C6289D116178277D9B327AAC07399A61CA6C489720AA61BBDD6CCE0C1D`、`651395C711F868313697ACF096C7F74F6BEEBE2231F2C84405FAF091A27A116B`；
- 门禁结果为 `handoffReadyWithAssignedGaps` / dependency `pass` / release `notReleaseReady`。
- 定向门禁测试 5/5、全量 Node/契约 127/127、Rust 111/111、141 份 Markdown 链接和 production UI build 通过；此前构建的同源 Release PID `42896` 仍正常运行。

## 新发现与责任归属

既有五项打包差距继续归 C-RCG；另记录 Cargo license metadata、许可证载荷、第三方 notices、生成 NSIS LICENSE 四项许可证差距，仍归 C-RCG。

生成 NSIS 使用 `${PRODUCTNAME}` 清理 HKCU Run，而当前产品名解析为 `Desktop-Up`；冻结契约值名为 `DesktopUp`。该 `autostartCleanupValueNameMismatch` 归 C-URV 修正和恢复矩阵验证，不在 C-IBC 中实际写注册表。

## 边界

- 本轮没有修改 Rust 运行时、用户数据、注册表或安装目录；
- 没有执行安装、卸载、迁移、提权或自启 enable/disable；
- 当前 NSIS 仍是开发阶段产物，不具备净机、签名、升级或恢复型卸载认证；
- C-IBC-01..05 已完成并关闭，下一候选为 C-RLC，等待用户明确激活。
