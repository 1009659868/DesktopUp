# C-IBC-05 个人自用包装阻断收敛

日期：2026-08-13

状态：`COMPLETE — CONTRACT PACKAGING READY / RELEASE CERTIFICATION PENDING`

## 用户范围决定

项目当前仅供个人自用，不要求 Cargo license metadata、NSIS 许可证页面、`licenses/` 或第三方 NOTICE。若未来向他人公开分发或引入带再分发义务的素材/组件，C-RCG 必须重新启用许可证审计。

## 实际修复

- `productName` 与 `mainBinaryName` 统一为 `DesktopUp`，Release 产物为 `DesktopUp.exe`；
- 固定 tauri-bundler 2.9.4 项目内 NSIS 模板，current-user 安装根改为 `%LOCALAPPDATA%\Programs\DesktopUp`；
- 增加真实 `resources/` 静态载荷并验证生成 NSIS 包含该文件；
- 构建后把版本化 NSIS 字节一致地收口为 `DesktopUpSetup.exe`；
- NSIS 卸载逻辑精确删除 HKCU Run 值 `DesktopUp`，不再依赖产品显示名；
- 安装基线冻结 `distributionScope=privateUse`、`licensePayloadRequired=false`。

原十项中五项实际修复，五项许可证相关项判定为当前范围不适用。双层门禁均为 `ready`，组合门输出 `contractPackagingReady`、`gaps=[]`。

## 产物

| 产物 | SHA-256 |
| --- | --- |
| `DesktopUp.exe` | `78026486E2E335D7843DCE049DE853CEDF61F2BBF56B0E7C0C3A041936F0C2EB` |
| `DesktopUpSetup.exe` | `8BEDD10FE475B2F766C8A4F3572E41F203FE1AE8B46C45911361DC9ED0166DA7` |
| 生成 NSIS | `9D77485871A52B893DAF3A669530ED6E9D5AD3908A688868C15AE59B40FBF240` |

版本化 `DesktopUp_0.0.1_x64-setup.exe` 与 `DesktopUpSetup.exe` 字节一致。

## 验证

- 包装修复专项测试 15/15；
- 全量 Node/契约 130/130；
- Rust 111/111；
- 142 份 Markdown 链接；
- Release/NSIS 完整构建通过；
- `install-artifact-probe-v1 --require-ready`：`gaps=[]`；
- `runtime-dependency-handoff-gate-v1 --require-release-ready`：`contractPackagingReady`、`gaps=[]`；
- `poster-state-recovery-gate-v1`：8/8；
- 普通 Release 启动正常，自启仍 absent，目标安装/数据根未被便携烟测创建；
- 正式 SQLite SHA-256 保持 `DA63A52BDF62B3FC863995D79F497584BE3BA97C1DE4C55D1D301AFBAFAB6CFC`；最新版 PID `40548`。

## 未宣称

本轮没有执行安装或卸载。`DesktopUpSetup.exe` 尚未通过净机安装、覆盖升级、恢复型卸载、签名和 WebView2 离线矩阵，因此 `releaseCertification` 仍为 `notCertifiedUntilC-RCGMatricesPass`。
