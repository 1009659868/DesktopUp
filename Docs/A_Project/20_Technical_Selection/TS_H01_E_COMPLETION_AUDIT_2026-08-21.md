# TS-H01-E 完成与一致性审计

状态：`COMPLETE — EXIT GATE SATISFIED / NO CANDIDATE WINNER`

日期：2026-08-21

## 1. 审计范围

本审计核对 TS-H01-E1 至 E6 是否把公共宿主证据完整转换成实现中立接口，并确认入口、候选状态、历史例外和机器夹具没有互相改写。它不重跑 D1-D3 人工实验，也不产生候选 P0 结果。

## 2. 工作包与产出

| 工作包 | 冻结产出 | 结论 |
| --- | --- | --- |
| E1 | H0-H5 证据范围、例外和残余风险 | `COMPLETE` |
| E2 | DesktopHost 能力、数据、surface、状态权威、revision 与错误 | `COMPLETE / E3-A1 CONTROLLED AMENDMENT` |
| E3 | ownership、first-frame、Shell/device loss、recovery、detach、exit | `COMPLETE / FROZEN` |
| E4 | DIP/anchor、gesture、collapse/lock、geometry-hit-render 原子发布 | `COMPLETE / FROZEN` |
| E5 | JSONL schema、manifest、validator、PASS/FAIL samples | `COMPLETE / FIXTURE SELF-TEST PASS` |
| E6 | adapter 进入门、证据隔离、fail-fast、退出状态 | `COMPLETE / FROZEN` |

## 3. 冻结接口核对

- 未暴露固定 HWND、class、枚举序号或 Win32 探针对象作为候选 ABI；
- SurfaceOffer/Plan 明确 transport 和 ownership，失败时无普通顶层/TopMost fallback；
- SourceSnapshotRevision 与 HostProjectionRevision 分离，宿主不是第二业务权威；
- HostSession/Lifecycle/Surface/Recovery 身份可 fencing；
- Attach、recovery、detach、exit 具备单飞、幂等、有界预算和迟到事件规则；
- geometry、hit regions、render target 与有效输入/呈现由一个 projection revision 原子关联；
- E5 事件能观测层级、可见性、DPI/边界、命中、Shell generation、frame 和清理；
- TS-C01 至 TS-C07 可复用同一 manifest、validator 与 Night Ops fixture。

## 4. 历史证据保真

以下事实未被 E 阶段提升：

- Explorer 重建自动证据仍是 1/10，结论为用户接受例外；
- D3-2 自动变化识别仍是 8/10，人工接受不改写自动覆盖；
- 100% DPI 是部分证据；150%/200% 为用户跳过/UNKNOWN；
- traditional WorkerW、多屏、混合 DPI、负坐标和热插拔为 `UNKNOWN/DEFERRED`；
- 实际 Qt/Direct2D/WinUI/WPF/Avalonia/WebView2/Slint surface、device loss、跨进程断连仍是 `NOT_RUN`；
- 公共 raised Progman 探针结果不等于 TS-C02 P0，也不借给其他候选。

## 5. E5 夹具核对

自测必须同时满足：

```text
pass-minimal.jsonl       -> exit 0
fail-late-frame.jsonl    -> non-zero; reports stale lifecycle/surface/projection/render
test-validator.ps1       -> exit 0 only when both expectations hold
```

该结论只登记为 `FIXTURE SELF-TEST PASS`。透明白条、灰框、闪烁、原生桌面输入、普通/全屏窗口层级和性能仍要求统一矩阵中的媒体与人工证据。

## 6. 候选与决策边界

- TS-C01 至 TS-C07：`FACT CHECKED / P0 NOT RUN`；
- TS-C02 仅额外保留 `COMMON HOST USER-ACCEPTED COMPLETE WITH RECORDED EXCEPTIONS / NOT A WINNER`；
- TS-C08：`DEFERRED UNTIL ONE POSTER CANDIDATE PASSES P0`；
- selection decision：`NOT READY / NO WINNER`；
- 架构、制作计划、工作流和生产代码仍未激活。

## 7. 退出门结果

| 退出门 | 结果 |
| --- | --- |
| 实现中立类型、方法、错误与状态机 | `SATISFIED` |
| 每项能力携带 disposition、范围和例外 | `SATISFIED` |
| attach/recover/detach 所有权与幂等明确 | `SATISFIED` |
| geometry/hit/render 原子性可测试 | `SATISFIED` |
| 诊断覆盖公共硬门 | `SATISFIED` |
| E5 夹具可由 C01-C07 原样复用 | `SATISFIED` |
| 公共例外不借给候选 | `SATISFIED` |

## 8. 最终结论

TS-H01-E 可以标记 `COMPLETE`。下一步属于 TS-02 的候选 adapter/P0：按稳定 ID 建立真实实现与独立 Evidence run。该动作仍是技术选型，不是架构或生产实现；没有 P0 原始证据前不得产生赢家。
