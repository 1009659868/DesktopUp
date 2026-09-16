# BUG-007：原生选择器取消与退出

2026-09-16，当前 **CODE/AUTO + USER PASS，BUG-007 CLOSED**。用户在R3复验后明确反馈“验证通过”；范围按本页已发人工步骤记录，不扩称真实他机、断电或全部权限环境通过。BUG-006仍延期；按V2-DEC-008直接进入04-L01，不再回P5，无Git提交/推送。本次只补登记反馈，未重跑测试或改包。

## 当前 R3 候选与验证

- 包：`out/verify/v3-04-06-picker-r3`；App SHA256 `C0D2873CE51B543754492C3E266015003F73B2C68E63457E77019D2170703B10`，Windows DLL仍为下方R2的`3475DA7A…`。R3只纠正中心关闭测试入口，生产取消实现与R2一致。
- Release 0警告/0错误；完整合同1462/1462 PASS、0跳过，`results/picker-r3-full.trx`。
- 8项原生门PASS：`picker-gates-55b50543cc7449d6a8a55210fbcc21a2`。打开前取消2项、可见后重复取消2项各三轮、系统关闭中心并重开2项、重复托盘退出2项。全部数据库字节不变、原生结束且资源0；exit-open/PID20500为163.5483ms，exit-save/PID38516为162.8531ms。
- 同包旧门重新运行全部PASS：12项`gates-7797629c9f014941b5a4959952129dd6`；便携6项`gates-5f44828a1b9340a1a6906b7e147ef1b0`；离线窗口3项`recovery-gates-42d67b0157344543aff4f6ce66021c5e`。所有退出资源0；不能将离线窗口门当成本轮执行了真实恢复或断电。
- 上述均为自动触发原生产通道，不是鼠标点击认证；原生8门不选择文件、不进行文件IO。当前机器样本isElevated=true，如实记录，不扩大为普通权限或其他主机认证。
- `start-portable-manual.ps1 -Candidate picker -Scenario lifecycle`绑定独立R3目录和新`portable-picker-candidate.json`；必须25产物指纹、21既有门、8原生门全部匹配，缺失/失败不回退旧包。原默认`stable`和旧配置保留；新人工数据为`out/manual/v3-04-06-picker/lifecycle`。

## 已获用户通过的人工步骤（P9-4及选择器替换影响范围）

当时PS5.1新入口25指纹/29门校验通过，原stable25/21仍通过；独立人工实例PID19436就绪。随后用户反馈“验证通过”。本次登记的是用户结果，不将启动就绪或既有自动资源0扩称本次另行核对了人工退出日志。

1. “更多 → 配置与恢复（全局） → 导入”：取消或X关闭后可再次打开，中心仍可操作。
2. 导入选择器保持打开时，从托盘退出：选择器、中心、海报和托盘应正常结束，不需要再手动关选择器。
3. 用下方命令重开，在导出说明确认后进入保存选择器，保持打开再从托盘退出，预期同上。
4. 再重开，将配置导出到一个新测试JSON；重新导出同一路径时检查覆盖确认和取消。导入该JSON应能进入原有预览，随后取消、不应用；确认正常选文件链路仍可用。不要选择重要现存文件做覆盖试验。

```powershell
& 'G:\A_\Tools\DesktopUp_V2\scripts\start-portable-manual.ps1' -Candidate picker -Scenario lifecycle
```

用户已对上述R3步骤反馈通过，BUG-007/P9-4据此登记USER PASS；既有自动退出资源证据与用户结果分别保留，不虚构本次另读人工日志。当前按DEC-008进入04-L01，P5、真实他机及P7/P8等未验项延期；不再要求复验后回P5。

## 问题与修订边界

旧验收包 App `395914F0…`、lifecycle/PID12380 已收到退出请求，用户关闭原生选择器后才退出（约25秒）。阻塞在 CancelPortableAndWaitAsync 等待 PortableAction 完成，Cancel 请求不是窗口已关闭的证明。SDK内部子原因未证实，不冒称特定SDK缺陷。

修订用每次独立 STA 持有 Common Item Dialog。取消只通知该 STA，从本实例 COM 接口获取窗口，校验同进程/线程后发送一次 WM_CLOSE。Show 返回、COM/窗口端点/资源清理后才发布完成；取消后的迟到路径丢弃。保留原 PortableAction、原子文件IO/事务和应用退出顺序；不枚举或关闭无关窗口、不强拆picker、不跳过IO等待。

参考：[微软 IFileDialog.Close 合同](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifiledialog-close)、[GTK Windows原生选择器](https://github.com/GNOME/gtk/blob/main/gtk/gtkfilechoosernativewin32.c)的持有句柄关闭路径；参考不替代本机验证，未引入GTK依赖。

## 首轮失败保留

- `out/verify/v3-04-06-picker`：App `C22203893D2E01A319B3D3BA8E82E0F331731E7A85A6522B8878E239F6C67DD8`；Windows DLL `E6194A5EF4F89E5ADD150D9B26BA2558DD9576CF7D49603AD40DD13AF164F7D3`。
- `picker-gates-6043c24425d24da6abe3c6153ce6073e/cancel-open`，PID18544：show_begin 08:31:49.764Z，shown 08:31:50.270Z；COM Close 连续返回S_OK但Show未结束，真实回归FAIL。SDK方法顺序已核对，具体原生内部原因未证实。
- 用户正常取消后08:54:04.129Z picker完成，08:54:04.200Z application.stopped/activeResources0。没有强杀。包和日志保留，不以最终资源0改写失败。

## 历史 R2 证据与夹具纠正

- 独立包 `out/verify/v3-04-06-picker-r2`；App SHA256 `088E0AB068C486F187B9FC130BF6DEE65066EB4B0D2300925DB7D407901EA916`；Windows DLL `3475DA7AB4CB04B9959C8B8EC1D28A65FB6D3E5FD69BB96839AB52C897879140`。
- Release 0警告/0错误；完整合同1462/1462通过、0跳过（新增7项取消/绑定/完成竞争测试），`results/picker-r2-full.trx`。
- 首项真实原生回归 `cancel-open` PASS，PID54504，连续三轮取消；数据库字节不变，退出资源0，退出请求至停止51.6549ms。证据 `picker-gates-d038696431f444c1b286dd94dd0a8f2c/cancel-open`。它不是鼠标人工验收，未选择文件或执行文件IO。
- R2随后完整矩阵跑到center-open/PID50496失败：测试调用Window.Close直接销毁中心，未触发AppWindow.Closing取消等待；资源2使门如实失败。已修测试为向该中心实例发送WM_CLOSE，额外断言portable.cancel_requested确实包含picker/busy，未删除场景或放宽阈值。失败目录`picker-gates-b727f145dcbe4e008e1876806dd450e8`保留。
- [微软Closing事件定义](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.appwindow.closing)区分系统关闭与直接销毁；本机日志无取消记录、直接closed佐证入口差异。生产正常退出在销毁前已等待配置交互，不能用夹具的直接销毁方式替代系统关闭验证。
- 多次审批容量错误期间命令未启动；用户明确批准后继续按审批执行。PS5.1启动校验还纠正JSON数组二次包装误拒绝；没有修改候选二进制或降低门槛。当前结果以上方R3为准。

## 历史推进门（R2阶段安排，不是当前待办；后续R3用户结果见上）

1. `scripts/smoke-native-picker.ps1 -PackageDirectory out/verify/v3-04-06-picker-r2`：打开前取消、可见后重复取消、关中心/重开、重复托盘退出，打开/保存各一组，共8场景；检查窗口结束、退出时限、数据库不变和资源0。
2. 同包12项托盘/中心/设置、6项便携配置、3项离线恢复回归，不能用旧包结果代替。
3. 当时要求自动通过后绑定独立人工候选，检查选择/取消/覆盖、选择器中退出、中心关闭重开；人工后回P5是旧排期，已被DEC-008替代。R3后来已获用户通过，不沿用本节“待验”状态。

不扩大到实际另一主机、真实断电或任意磁盘IO可立即取消的承诺；不改数据库schema、空间策略、输入岛和R4。
