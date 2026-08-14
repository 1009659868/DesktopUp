# Install Baseline Contract 当前交接

最后更新：2026-08-13

状态：`COMPLETE — C-IBC/C-RLC/C-LST CLOSED / C-PSI NEXT`

## 已完成

- C-DRP-04 无 TOPMOST 维护矩阵由用户确认全部通过；
- `install-baseline-v1` 冻结当前用户安装、程序/数据分离、目标数据布局、迁移边界、自启和依赖责任；
- Tauri current-user NSIS、WebView2 download bootstrapper 与 Cargo `STATIC_VCRUNTIME=true` 已显式配置；
- 当前 `%APPDATA%\com.allmight.desktopup` SQLite authority 保持不变，没有创建或写入目标 `%LOCALAPPDATA%\DesktopUp`；
- C-IBC-01 Node 专项 4/4、前端/契约 109/109、Rust 94/94、135 份文档链接、UI production build 与 Release/NSIS 全部通过；
- `install-path-diagnostics-v1` 已统一解析目标安装根、目标数据根、当前 roaming authority、当前/目标 SQLite、可执行文件位置和七个目标数据角色；
- setup 已改为统一消费 `RuntimePathResolution`，但 placement、bridge 和 SQLite 仍打开当前 `app_config_dir()` 根；
- 诊断明确 `preMigrationAuthoritative`、`C-UGM`、target inactive、来源保留/回滚和零解析副作用；
- C-IBC-02 专项 Rust 6/6、C-IBC Node 8/8、全量前端/契约 113/113、Rust 100/100、136 份文档链接、Release/NSIS 全部通过；
- Release 启停前后当前 SQLite SHA-256 均为 `DA63A52BDF62B3FC863995D79F497584BE3BA97C1DE4C55D1D301AFBAFAB6CFC`，目标 `%LOCALAPPDATA%\DesktopUp` 始终不存在；
- `install-content-probe-v1` 最初审计 `DesktopUp.exe`、`uninstall.exe`、`resources/`、`licenses/`；个人自用范围修正后最低内容为前三项，不再要求许可证载荷；
- 构建侧 `install-artifact-probe-v1` 只读审计 Tauri 配置、本机构建工具 NSIS 模板与产物哈希；不解包、不安装、不卸载、不提权，也不创建目录；
- 本次 tauri-cli 2.11.4 构建环境已确认 current-user、`RequestExecutionLevel user` 与生成真实 `uninstall.exe`；该模板不是项目 Cargo.lock 直接锁定的依赖，恢复行为仍归 C-URV，压缩 payload 未独立提取；
- C-IBC-03 当时记录的五项包装差距已在后续维护中处理：四项实际配置/产物差距修复，`licenses/` 按个人自用范围判定不适用；
- C-IBC-03 定向 Node 4/4、Rust 4/4；全量前端/契约 117/117、Rust 104/104、137 份文档链接、UI production、Release/NSIS 和 Release 启动烟测通过；
- Release 烟测前后 SQLite SHA-256 均为 `DA63A52BDF62B3FC863995D79F497584BE3BA97C1DE4C55D1D301AFBAFAB6CFC`，目标数据根与目标安装根均未创建；
- `autostart-contract-v1` 冻结 HKCU Run 的 `DesktopUp`/`REG_SZ`、默认关闭、显式 opt-in、正式安装路径和精确 `--autostart` 命令；
- `autostart-diagnostics-v1` 只读区分 absent/matching/drifted/unreadable；生产代码只有 RegOpen/RegQuery/RegClose，没有注册表变更 API；
- 开发/便携 Release、缺失或 symlink 目标、错误值类型、错路径和额外参数均不可注册；当前运行时明确为 `contractOnlyCrlcPending`；
- C-URV 清理只拥有精确值，值不存在为成功，正常卸载、恢复失败和部分恢复失败后均必须幂等执行；
- 本机 HKCU Run 的 `DesktopUp` 在构建、启动和退出前后均不存在，托盘/控制中心入口继续禁用；
- C-IBC-04 定向 Node 5/5、Rust 7/7；全量前端/契约 122/122、Rust 111/111、139 份文档链接、Release/NSIS 和启动烟测通过；
- SQLite SHA-256 保持 `DA63A52BDF62B3FC863995D79F497584BE3BA97C1DE4C55D1D301AFBAFAB6CFC`，目标安装/数据目录未创建；
- 最新 `DesktopUp.exe`/`DesktopUpSetup.exe`/生成 NSIS SHA-256 分别为 `78026486E2E335D7843DCE049DE853CEDF61F2BBF56B0E7C0C3A041936F0C2EB`、`8BEDD10FE475B2F766C8A4F3572E41F203FE1AE8B46C45911361DC9ED0166DA7`、`9D77485871A52B893DAF3A669530ED6E9D5AD3908A688868C15AE59B40FBF240`；最新版 Release PID 为 `40548`。
- `runtime-dependency-handoff-gate-v1` 已确认 Release 为 x64/Windows GUI，动态 VC Runtime 与外部 SQLite DLL 导入均为 0；WebView2 download bootstrapper、静态 VC 和 bundled SQLite 证据通过；
- 门禁状态现为 `contractPackagingReady`、`releaseReadiness=ready`、`gaps=[]`；原十项中五项实际修复，五项许可证相关项按 `privateUse` 判定不适用；
- [`C_IBC_HANDOFF_INPUTS_V1.md`](../10_Plan/C_IBC_HANDOFF_INPUTS_V1.md) 已冻结 C-RLC/C-UGM/C-URV/C-RCG 输入；C-IBC-01..05 全部完成。
- 维护修正后全量 Node/契约 130/130、Rust 111/111、142 份 Markdown 链接、Release/NSIS build、双层零缺口门与隔离恢复 8/8 通过；SQLite SHA-256 保持 `DA63A52BDF62B3FC863995D79F497584BE3BA97C1DE4C55D1D301AFBAFAB6CFC`。

## 精确下一步

C-IBC 已关闭，C-RLC 已消费冻结输入并激活；C-RLC-03 已按用户授权的 feature-scoped 临时测试门关闭，正式安装路径合同保持不变。C-RLC-04 已实现并等待默认特性 Release 人工矩阵；C-UGM/C-URV/C-RCG 保持计划状态，不并行实施。

## 禁止

- 不直接把 SQLite 路径改到 LocalAppData；
- 不复制、删除或双写用户数据；
- 不启用托盘/控制中心自启；
- 不把 `DesktopUpSetup.exe` 命名和契约包装门通过当成独立卸载器或净机矩阵验收；
- 不扩张主屏单海报的平台认证。
