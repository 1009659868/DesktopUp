# TS-C01 R5-01 原始证据盘点 2026-08-26

状态：`COMPLETE — RAW ARTIFACTS VERIFIED / SOURCE IDENTITY UNFROZEN / CONSUMED BY R5-02～05`

## 1. 盘点范围

本次只验证材料是否真实存在、格式可读、哈希稳定及构建血缘可区分。它不判断某个统一矩阵项 PASS，也不创建 Qt 赢家。

盘点根：`prototype/renderer-adapters/ts-c01-qt-quick/out/`。

## 2. 源码与构建身份

| 字段 | 结果 |
| --- | --- |
| Git branch | `impl/Refactor` |
| Git HEAD | `83962bf2f0786b68951ef5bf5f020666a1ea1c46` |
| Worktree | `DIRTY — R4/R5 文档与 TS-C01 adapter、installer、lifecycle 源码存在未提交改动` |
| Source identity | `UNFROZEN WORKTREE` |
| Qt | `6.8.3 / msvc2022_64` |
| Candidate | `TS-C01 / Qt Quick + native DesktopHost` |

结论：HEAD 只能作为当前工作树的父基线，不能单独标识已测试源码。R5-03 若要形成可长期复核的正式候选包，必须增加源码包哈希或经用户授权后的 Git 提交；在此之前不得宣称所有会话来自一个冻结源码版本。

## 3. 构建血缘

| 阶段 | EXE SHA-256 | 用途 | 血缘结论 |
| --- | --- | --- | --- |
| R4-A | `21DE8E56DB7BF79B024EC7F9B84D05E8CF7352523CF38A02B2EAC9E2902DB2C3` | 单次冷启动/输入/层级人工门 | 独立旧构建 |
| R4-B 稳定版 | `D4D89483BD3C86EF896EFEF86A589F32408664B5D72D428E6F9700FDC99298EE` | 拖动、缩放、按钮延迟修订复测 | 独立修订构建 |
| R4-C | `FF412F1BC8CBA3A88DF1C7736051CFACCF81D733A366DA9C26D07FFCDA7380E2` | Windows 层级、控制中心角色、Explorer 重建 | 独立修订构建 |
| R4-D1 | `4E9A4FCB1AB6B2ED008DF6137ECE9D42A31991580FB7BDC6FC87C46AB4600CD2` | 显示环境与几何重投影 | 独立修订构建 |
| R4-D2 | `8FF9F292B31A92CFDCDB44D81F10B647AD3D3DC6750D6F003F35A7F44E87D86D` | 三主题、fallback/LKG、隔离入口 | 独立修订构建 |
| R4-D3 | `4F1D76FCEC9C5271C1199D2CBAE48C748CA4EC0D8CA8C4D0AE50D325095CA673` | 故障、200 Item、性能 | 独立修订构建；工作集硬门失败 |
| R4-D4 | `4C0FAF65F6EB04CA10B342C851BEB93822B329A4F50DC2A616AFE0B72E50C5EE` | 正式安装、单实例、状态与卸载 | 当前生命周期构建 |

结论：现有证据跨至少七个 EXE 哈希，不能拼成“R4-D4 最终构建完整通过 R4-A～D”。这不是说旧材料无效，而是其用途只能限定到当时构建和场景。

## 4. 自动摘要与包材料

| 材料 | 字节 | SHA-256 | 盘点结果 |
| --- | ---: | --- | --- |
| `out/entry-preflight.json` | 1,011 | `43D22B58623361794DE74B80502AFEA9BB68BA681E2ACB3968891528999E6229` | JSON PASS；R0 entry only；P0 NOT RUN |
| `out/r1-preflight.json` | 1,232 | `A9EFF9BF37C235EBE7E781235BD66C726BCB62A9B65CC353F4002EFB01E58D9F` | JSON PASS；R1 adapter contract；P0 NOT RUN |
| `out/r2-preflight.json` | 1,743 | `AC2804B0039BAC55411451D57249DDD6BCBA51613FE1245FDF8F410F3B78E9D8` | JSON PASS；R2 fixture；P0 NOT RUN |
| `out/r2-manual-retest-user.jsonl` | 38,986 | `0D2C60867F13B6DF5C3E0651C82ECBA8A3730CFBD4D73BD3C5A4EACA469F5E73` | JSONL PASS；52 条 |
| `out/r3-preflight.json` | 6,226 | `70163D5740A35A227D4D54BBF0EBCB0C846C7B4DD2E24012C779B601D7F4EB30` | JSON PASS；总体 FAIL；工作集 136.332 MiB > 50 MiB；P0 NOT RUN |
| `out/r4d4-package/r4d4-preflight.json` | 1,522 | `673C9C1DD89731AC5AD21F53C6A2BC408ADC4479ABEF1BEA9DE054FB1D4E8B2A` | JSON PASS；包自动门；正式人工生命周期由后续文档补充 |
| `out/r4d4-package/DesktopUpSetup.exe` | 17,144,583 | `7807804C836FEEBB088A8DFB3B90144A2D2D686396F80DABE13E9B86C638632D` | 安装器存在且哈希与 D4 记录一致 |

## 5. 主要人工会话

| 会话 | session SHA-256 | events SHA-256 / 条数 | 盘点结论 |
| --- | --- | --- | --- |
| `r4-manual-20260824-171814-r4a-01` | `D562E267D40DC4ADFAF23B16398CCB256D6D494F5E3B078A454E6373B86B06A0` | `668A2381CD64CD77FB6BE72C0500F231D6F0DD3290B31E65BF7DFE30B6BE6C1E` / 51 | 格式有效；用户 PASS；冻结 20 次仅 1 次 |
| `r4-manual-20260825-112934-r4b-stability-01` | `1569EE110D5DD9AEBE73CF7AAA272C7585E01B6BF5DAD8E45694A922D6FAB5E9` | `E936F56558D54B5845E30AC6BD189E3F20C4D655B2FF0DF7BAC59FB4752EF158` / 583 | 格式有效；稳定版用户 PASS；动作计数不足 |
| `r4-manual-20260825-120817-r4c-c1-windows-01` | `8BDC8BCCFBA59FB1F9C607E2D43FA98FC47386A40AC2AAA5739C048BF8A21AAB` | `28F5FEC3ED20BD43CC28262BB5B3B8AF3C35A3E0F73C3F8CB0769F2E622AC79C` / 520 | 格式有效；C1 用户 PASS；详细循环数待 R5-02 映射 |
| `r4-manual-20260825-124220-r4c-c2-control-center-02` | `1E3BE5AB2A8DF32930D3828C43A8C6B877BCD7982A943087179FF1FE62B8A452` | `249CE283D58F8220B08579D0580D841C5BDABAF6245DDA02466294E1DB9B668E` / 234 | 格式有效；7 次打开/6 次关闭，不是 20/20 |
| `r4-manual-20260825-143817-r4c-c3-explorer-04` | `21FCA11A35677A4E1563C4088CB0AB235D9C1EF30D19706C22086AB1830222E4` | `373F9E86C22C9E61FD71CD13073B97106C1F3299EB2A49E4D06EA1C39E0615DF` / 183 | 格式有效；Explorer 1/10 计数例外 |
| `r4-manual-20260825-153036-r4d-d1-01` | `A34D90A47740A165DF0EE080B4F8E98DB727796447B7DD8F655F0B3432780178` | `C7AD240F440F67F48247608A085D23E312637DDCDF0BA30D230ED665DB083860` / 412 | 格式有效；减少后的单次显示环境门 |
| `r4-manual-20260825-160909-r4d2-control-panel-r2b` | `9E5D5FACF54F08DEE1C70E932D9E8A8AD4A746DBB878F34D80F8D70551D8B045` | `2B74294A30702C164E45A886FAED3BD22DF601145C6667C16AD53DE0C13DD3D3` / 137 | 格式有效；三主题与 fallback/LKG 用户 PASS |

所有上述 JSONL 已逐行执行 JSON 解析，错误数为 0。会话内 `e5Validation=PASS` 与 `cleanupZero=true` 只适用于对应构建和会话。

## 6. D4 正式生命周期材料

R4-D4 文档记录了可选目录 `<user-selected-test-root>\DesktopUp` 的正式安装、唯一实例、普通应用重启状态恢复、精确清单卸载、未知文件保留、候选数据删除和最终托盘消失用户确认。

其边界为：

- 普通第二实例、应用重启、卸载机器扫描和托盘目视：已执行并通过；
- 开机自启：`DEFERRED BY USER`；
- 设备重启：`SKIPPED BY USER`；
- `r4d4-preflight.json` 生成时仍写 `formalInstallLifecycle=NOT_RUN`，因为该文件早于后续人工正式安装；不能反向修改原始文件；
- 正式人工生命周期目前主要由权威 D4 文档和用户确认承载，未形成独立机器可读 Candidate Evidence run；R5-03 必须把这一证据等级差异写入正式索引。

## 7. 已知缺口与例外

1. R4-A 冷启动 1/20；
2. R4-B 只记录 6 个 geometry gesture 与 16 个 command，非冻结 20 轮；
3. R4-C2 控制中心角色 7 次打开/6 次关闭，非 20/20；
4. R4-C3 Explorer 重建 1/10；
5. R4-D1 重复 DPI 轮次被用户豁免，多显示器、混合 DPI、负坐标、主屏切换和热插拔仍延期；
6. 触控板显示桌面入口无独立完整证据；
7. R4-D3 `HG-PERF-02` 工作集实测峰值 136.332 MiB，超过冻结 50 MiB 目标；
8. R4-D4 开机自启延期、设备重启跳过；
9. Qt 分发许可证材料审查未闭合；
10. 控制中心、StateStore、主题包平台、内容 Item 和安装产品化仍含角色探针或候选壳，不等同完整产品能力；
11. 不同阶段使用不同 EXE 哈希，最终 D4 构建未完整回归 R4-A～D；
12. 当前源码工作树未冻结，无法由单一 Git commit 重现已测试源码。

## 8. R5-01 结论

- 原始自动摘要、主要人工会话、事件日志和安装器真实存在；所列 JSON/JSONL 格式校验通过，哈希已冻结在本文；
- 材料足以进入 R5-02 逐矩阵映射；
- 材料尚不足以宣称同一最终构建完整 P0 PASS；
- 正式 `Evidence/TS-C01/` 候选包尚未建立，避免在矩阵映射前预填结论；
- 本盘点已由 R5-02～05 消费；当前下一动作见 [R6 交接输入](TS_C01_R5_05_R6_HANDOFF_INPUT_V1.md)。