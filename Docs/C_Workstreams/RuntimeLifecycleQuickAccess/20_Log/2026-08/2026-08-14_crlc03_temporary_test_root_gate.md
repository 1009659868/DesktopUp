# C-RLC-03 临时测试路径真实自启门

日期：2026-08-14

状态：`C-RLC-03 COMPLETE / C-RLC-04 NEXT`

## 授权与边界

用户明确将本轮测试位置暂时调整为 `G:\A_\Tools\Test\`。测试可执行文件固定为 `G:\A_\Tools\Test\DesktopUp.exe`。正式生产合同仍是 `%LOCALAPPDATA%\Programs\DesktopUp\DesktopUp.exe`，默认 Cargo features 为空；临时资格只在显式 `autostart-test-root` 构建、且当前可执行文件与测试路径完全相等时成立。正式 Release/NSIS 已用默认特性重建并确认不含测试门标记。

## 实施

- `autostart_contract.rs` 增加 feature-scoped 目标解析和 `eligibilityScope` 诊断；
- `autostart_test_gate.rs` 让测试请求经第二实例转交已有第一实例，由真实 `AutostartCoordinator` 执行并发布统一读回快照；
- `lib.rs` 仅在测试 feature 下允许内部测试请求进入单实例转发，产品未知参数拒绝规则不变；
- Node 契约确认默认特性关闭、正式安装根不变、测试路径必须精确匹配；
- Rust 单元测试覆盖默认合同与精确临时目标，正式 UI/安装器随后用默认特性重新构建。

## 真实 Windows 证据

测试二进制：`G:\A_\Tools\Test\DesktopUp.exe`

SHA-256：`5D4542632BCDAD0C22FD2EF9080E0BCB4EAE5D5C91818E691C3312EA148F684C`

1. 初始 HKCU Run `DesktopUp` 为 absent；
2. enable 写入 `REG_SZ` 精确命令 `"G:\A_\Tools\Test\DesktopUp.exe" --autostart`，读回 `matching / readyEnabled / temporaryTest`；
3. 重复 enable 返回 `noChange`，保持 matching；
4. 精确 `--autostart` 冷启动只保留一个进程，控制中心 `Tauri Window` 保持隐藏；
5. 第二个 `--autostart` 保持同一 PID、无 UI 变化；
6. 普通第二实例保持同一 PID，并只显示一个 `Desktop-Up · A0 Demo` 控制中心；
7. disable 返回 `disabled`，读回 `absent / readyDisabled / temporaryTest`；
8. 其他 7 个 HKCU Run 值匿名指纹前后均为 `DED7A9FC331D0C3016F2CF6B896F40249E08E55644A2F17E5FA9728F82CA507F`；
9. 临时报告已删除，最终 HKCU Run `DesktopUp` 为 absent，测试位置普通启动实例保持一份。

## 自动与构建结果

- Node：152/152；
- Rust 默认特性：123/123；
- Rust `autostart-test-root`：125/125；
- 正式默认特性 Release SHA-256：`DAB5F8AC802CBC8FF03CA7A2623504BBCED35339BD41C424B622124D0A3211A1`；
- 正式 NSIS `DesktopUpSetup.exe` SHA-256：`4AACB98B65164A85DA63E4E78BCA75FBF1AD500CB8109D2EF2A6742D68954559`；
- 正式 Release 不含 `crlc-autostart-test-gate-v1` 标记。

## 结论

C-RLC-03 的唯一剩余真实门按用户授权的临时测试范围通过并关闭。正式安装路径合同没有改写为 G 盘；净机正式安装、升级/卸载恢复与发布认证仍由 C-RLC-06、C-RCG、C-UGM/C-URV 的对应门负责。下一任务为 C-RLC-04“控制中心与托盘快捷入口收敛”。