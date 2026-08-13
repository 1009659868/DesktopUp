# C-IBC 后继输入清单 V1

日期：2026-08-13

状态：`FROZEN — C-IBC-05 / PRIVATE-USE CONTRACT PACKAGING READY`

## 1. 门禁结论

`runtime-dependency-handoff-gate-v1` 对当前 Release、最终 NSIS 入口、生成后的 NSIS 脚本、Cargo 配置/锁文件、Tauri 配置、资源载荷、个人自用范围和 C-IBC-04 自启契约执行只读核对。

- 依赖证据：`PASS`；
- 契约包装门：`contractPackagingReady`；
- 当前缺口：`0`；
- 最终发布认证：`notCertifiedUntilC-RCGMatricesPass`；
- 文件、注册表、安装/卸载、进程启动与提权副作用：全部为 `false`。

契约包装门通过只表示本文件列举的产品名、路径、资源、依赖和自启清理不变量已对齐，不表示安装包已经通过净机、升级、卸载恢复、签名或公开分发认证。

## 2. 产物证据

| 产物 | SHA-256 | 结论 |
| --- | --- | --- |
| `src-tauri/target/release/DesktopUp.exe` | `78026486E2E335D7843DCE049DE853CEDF61F2BBF56B0E7C0C3A041936F0C2EB` | PE x64、Windows GUI、正式主程序名 |
| `src-tauri/target/release/bundle/nsis/DesktopUpSetup.exe` | `8BEDD10FE475B2F766C8A4F3572E41F203FE1AE8B46C45911361DC9ED0166DA7` | 契约要求的最终入口名；与版本化 NSIS 字节一致 |
| `src-tauri/target/release/nsis/x64/installer.nsi` | `9D77485871A52B893DAF3A669530ED6E9D5AD3908A688868C15AE59B40FBF240` | x64、currentUser、`%LOCALAPPDATA%\Programs\DesktopUp`、downloadBootstrapper |

## 3. 运行时依赖证据

| 依赖 | 可追溯证据 | 结论 |
| --- | --- | --- |
| WebView2 | `tauri.conf.json` 与生成 NSIS 均为 `downloadBootstrapper`；生成脚本包含 Microsoft bootstrapper URL，下载失败会中止安装 | `PASS`；目标机缺少 Evergreen Runtime 时可能需要网络 |
| VC Runtime | `.cargo/config.toml` 为 `STATIC_VCRUNTIME=true`；PE 导入表没有 `vcruntime*.dll` 或 `msvcp*.dll` | `PASS` |
| SQLite | `rusqlite 0.40.2` 开启 `bundled`，锁定 `libsqlite3-sys 0.38.2`；PE 导入表没有外部 SQLite DLL | `PASS` |
| 平台 | PE machine 为 x64，subsystem 为 Windows GUI；生成 NSIS 为 x64/currentUser | `PASS` |

PE 导入表只能证明当前二进制没有动态 VC/SQLite DLL 导入，不能替代净机运行矩阵。

## 4. 原十项阻断的处理结果

| 原缺口 | 处理结果 |
| --- | --- |
| `installRootMismatch` | 已修复：生成 NSIS 为 `%LOCALAPPDATA%\Programs\DesktopUp` |
| `mainExecutableNameMismatch` | 已修复：Release 与安装载荷均为 `DesktopUp.exe` |
| `resourcesDirectoryNotBundled` | 已修复：`src-tauri/resources/` 已纳入 bundle，生成 NSIS 已检出载荷 |
| `licensesDirectoryNotBundled` | 不适用：范围冻结为 `privateUse`，`licensePayloadRequired=false` |
| `finalInstallerNameMismatch` | 已修复：构建后生成 `DesktopUpSetup.exe` |
| `packageLicenseMetadataMissing` | 不适用：个人自用范围不要求 Cargo license metadata |
| `licensePayloadMissing` | 不适用：个人自用范围不要求 `licenses/` |
| `thirdPartyNoticeMissing` | 不适用：个人自用范围不要求第三方 NOTICE；公开分发时必须重开审计 |
| `generatedNsisLicenseUnset` | 不适用：个人自用安装器不显示许可证页 |
| `autostartCleanupValueNameMismatch` | 已修复：生成 NSIS 精确删除 HKCU Run 值 `DesktopUp` |

当前结构化 `gaps=[]`。其中五项为实际修复，五项因个人自用范围明确判定为不适用；若未来向他人公开分发，五项许可证相关门必须重新启用。

## 5. 后继输入

### C-RLC

- 消费 [`AUTOSTART_CLEANUP_CONTRACT_V1.md`](AUTOSTART_CLEANUP_CONTRACT_V1.md)；
- 只有正式安装可执行文件通过严格路径门后，才实现用户显式 opt-in 的 enable/disable；
- 第一实例与第二实例收到精确 `--autostart` 时均不得自动打开控制中心；
- 自启继续默认关闭，不接受开发/便携路径或额外参数。

### C-UGM

- 当前 `%APPDATA%\com.allmight.desktopup` 继续是正式 authority；
- 只能通过版本化备份、原子切换、来源保留和可验证回滚迁移到目标 LocalAppData；
- 不把当前尚未激活的目标根解释为已有正式数据。

### C-URV

- 提供独立、恢复感知的卸载器；
- 正常卸载、恢复失败和部分恢复失败后都幂等删除精确 HKCU Run 值 `DesktopUp`；
- 保持生成 NSIS 精确清理 `DesktopUp`，不删除 Run 父键或其他应用值。

### C-RCG

- 保持契约包装门零缺口；
- 形成 build manifest、产物哈希、签名证据及净机安装/升级/卸载矩阵；
- 冻结并验证 WebView2 在线 bootstrapper 与离线发布策略。
- 若分发范围超出个人自用，重新启用许可证与第三方 NOTICE 审计。

## 6. 使用方式

```powershell
pnpm run probe:runtime-dependencies -- --require-release-ready
```

当前命令必须返回 `contractPackagingReady`、`releaseReadiness=ready`、`gaps=[]`。该字段只代表本契约包装门，不替代 C-RCG 净机、升级、卸载恢复和签名矩阵。
