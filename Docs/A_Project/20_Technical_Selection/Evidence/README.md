# 技术选型证据目录

状态：`ACTIVE — TS-H01 + E5 FIXTURE PRESENT / TS-C01 FORMAL CANDIDATE INDEX PRESENT / R5-03 COMPLETE`

## 1. 目录结构

```text
Evidence/
├─ TS-H01/
│  ├─ TS-H01-A/
│  │  └─ <YYYY-MM-DD_run-id>/
│  ├─ TS-H01-B/
│  │  └─ <YYYY-MM-DD_run-id>/
│  ├─ TS-H01-C/
│  │  └─ <YYYY-MM-DD_run-id>/
│  ├─ TS-H01-D1/
│  │  └─ <YYYY-MM-DD_run-id>/
│  └─ TS-H01-D2/
│     └─ <YYYY-MM-DD_run-id>/
├─ TS-C01/
│  └─ <matrix-id>/
│     └─ <YYYY-MM-DD_run-id>/
├─ TS-C02/
└─ ...
```

只有真实执行后才创建候选运行目录。不得预生成 PASS 文件或空证据占位。

E5 的实现中立夹具位于 `prototype/desktop-host-win32/ts-h01-e5/`，不放入某个 TS-Cxx 目录。其 validator 正反例自测状态是 `FIXTURE SELF-TEST PASS`，不是候选运行，也不能替代录屏、人工输入/层级门或性能证据。候选真实运行必须输出 `desktopup.desktop-host.event.v1` JSONL，并按 E6 建立自己的 Evidence ID。

## 2. 每次运行至少包含

- `record.md`：按统一矩阵证据模板填写；
- `environment.txt`：OS、GPU、驱动、显示器、DPI、候选/SDK/编译器版本；
- `reproduce.md`：构建、启动、前置条件和逐步操作；
- 原始日志和窗口/渲染/状态遥测；
- 屏幕录制、必要截图和文件哈希；
- 性能项的原始采样，不只保留汇总数字；
- 结论及复核者。

## 3. 命名

- 公共宿主前置证据使用 `TS-H01/<substage>/`，不得伪装为 TS-C02 候选通过；
- Candidate：使用登记表 ID，如 `TS-C01`；
- Matrix：使用矩阵 ID，如 `HG-Z-03`；
- Run ID：`YYYY-MM-DD_HHMM_<short-purpose>`；
- 重试建立新目录，不覆盖旧运行；
- 文件名不含用户名、API Key 或不必要绝对路径。

## 4. 大文件

录屏或大型采样如不适合 Git：

- 在 `record.md` 保存受控位置、哈希、大小和保留策略；
- 不能只写一个易失临时路径；
- 最终决策所依赖的关键帧、结构化日志和摘要必须可长期复核；
- 未经用户要求不提交大型二进制。

## 5. 证据完整性

- 任何人工裁决指向具体 Evidence ID；
- 修改原型后旧证据保留，并记录新 commit/hash；
- 测试中断、环境污染或观测工具失败标记 `UNKNOWN`；
- 只有完整达到规定次数和条件才能标记 `PASS`；
- 候选失败不得删除其证据目录。
