# TS-H01-E6 候选交接、证据隔离与退出规则

状态：`COMPLETE — E6 HANDOFF FROZEN`

日期：2026-08-21

## 1. 目的

E6 规定 TS-C01 至 TS-C07 如何在同一 `DesktopHost` 合同、Night Ops fixture 和 P0 矩阵下开始候选适配。稳定编号只用于身份和证据隔离，不是推荐排名；E6 完成也不表示任何候选获选。

## 2. 候选顺序

按登记 ID 顺序建立独立 adapter：

```text
TS-C01 Qt 6 / Qt Quick + native DesktopHost
TS-C02 native Win32 + Direct2D/DirectComposition
TS-C03 WinUI 3 / Composition
TS-C04 WPF
TS-C05 Avalonia
TS-C06 WebView2
TS-C07 Slint
```

可以在资源允许时并行准备事实卡，但不得因先完成而改变编号或降低门槛。TS-C08 组合/双栈只在至少一个海报候选真正通过 P0 后评估；不能用组合候选掩盖单候选宿主、输入或生命周期失败。

## 3. 进入门

每个 adapter 开工前必须同时满足：

1. 引用冻结的 E2 数据合同、E3 生命周期、E4 原子投影和 E5 诊断 schema；
2. 固定候选精确版本、SDK/toolchain、build hash、运行依赖和许可证/分发事实；
3. 明确 SurfaceOffer、transport、ownership、事件循环和销毁边界；
4. 能构建同一个 Night Ops 单海报夹具，不能用候选专属简化视觉替代矩阵；
5. E5 正反例 validator 自测通过；
6. 证据目录为空或只含该候选真实运行，未预填 PASS；
7. 公共 TS-H01 证据只作 host 前置输入，不借为该 adapter 的 P0 结果。

未满足进入门时状态保持 `FACT CHECKED / P0 NOT RUN`。

## 4. Adapter 最小交付包

```text
prototype/<candidate-adapter>/
  README.md                 exact build/run/reproduce commands
  adapter boundary          SurfaceOffer + ownership + lifecycle mapping
  Night Ops fixture         same content, dimensions and interactions
  E5 event output           desktopup.desktop-host.event.v1 JSONL
  bounded fault hooks       shell/device/transport/stale callback tests

Evidence/TS-Cxx/<matrix-id>/<run-id>/
  record.md
  environment.txt
  reproduce.md
  raw events and samples
  video/screenshots/hashes
  summary with disposition
```

候选私有 API、调试日志和 renderer 内部 revision 可增加，但公共合同字段必须完整。adapter 不得绕开 DesktopHost 自行修改 parent、owner、z-order、Win+D 或 Explorer recovery。

## 5. P0 运行与证据隔离

- 每个候选独立执行统一矩阵的 P0 子集和 `conformance-manifest.json` 场景；
- 真实运行后才创建 `Evidence/TS-Cxx` 路径；失败、污染或中断的 run 永久保留并标记 disposition；
- 代码/依赖变化后新建 run，不覆盖旧结果；
- 自动日志、人工视频和复核结论使用同一 Evidence ID；
- 公共 D2/D3 用户接受例外不会自动传播，候选未运行项保持 `NOT_RUN/UNKNOWN`；
- framework documentation、官方 sample 或“理论可行”只能更新事实卡，不能更新 P0 PASS。

## 6. Fail-fast 条件

任一候选出现下列行为，停止当前 P0 扩展并记录 FAIL：

1. 需要 TopMost、前台激活、隐藏后恢复、轮询抬层或普通顶层 fallback；
2. Win+D 发生任何可见 hide/minimize/cloak/detach/re-attach 帧；
3. 海报空白不能产生 Explorer 原生框选、菜单和快捷方式交互；
4. Explorer 重建期间覆盖普通程序、重复海报或无法安全 degraded；
5. geometry/hit/render 只能分步修补，产生旧命中、旧 frame、白条、灰框或固定像素补偿；
6. 不能输出 E5 关键身份、边界、revision、恢复原因或资源账本；
7. 正常退出、失败回滚或 adapter timeout 留有宿主/renderer 资源；
8. 依赖固定 HWND、枚举序号、Explorer 注入或修改图标坐标。

Fail-fast 只结束该候选当前路线，不改写其他候选，也不反推某个候选胜出。

## 7. 候选退出状态

| 状态 | 定义 | 后续 |
| --- | --- | --- |
| `NOT_RUN` | 尚无本候选实测 | 保持未裁决 |
| `PASS` | 全部 P0 必需自动+人工门达到规定次数 | 可进入 full matrix/短名单 |
| `FAIL` | 硬门失败或有界修正后仍失败 | 保留证据，退出当前路线 |
| `CONDITIONAL` | 单一、明确、不会改合同的有界缺口 | 允许一次修正和一次全量 P0 重跑 |
| `WITHDRAWN` | 工具链、许可、维护或事实门主动撤回 | 记录原因，不等同技术 FAIL |

`CONDITIONAL` 不得用于多个系统性问题、合同绕过或无限延期。修正必须在进入前写清影响文件、假设和成功判据；重跑仍不满足即 `FAIL`。任何部分结果、用户接受例外、未测硬件或 validator 单独通过都不是 P0 PASS。

## 8. 比较与决策边界

- 先比较所有达到 P0 PASS 的候选；不以首个能显示海报的候选提前结束选型。
- 视觉保真、主题适配、生命周期、输入、性能、部署和维护成本都使用统一矩阵，不增加候选专属加分项。
- 若没有候选通过，回到已失败假设或候选集合，不把 DesktopHost 硬门降级。
- 最终 winner/组合只能由后续 selection decision record 冻结；E6 不填写 winner。

## 9. TS-H01-E 退出结论

E1-E6 的完成意味着：公共宿主证据已被转换成实现中立接口、生命周期 fence、原子投影、诊断夹具和候选交接规则。下一动作是为 TS-C01 至 TS-C07 建立真实 adapter/P0 证据；此刻所有候选仍保持 `P0 NOT RUN`，TS-C08 保持 `DEFERRED UNTIL ONE POSTER CANDIDATE PASSES P0`。
