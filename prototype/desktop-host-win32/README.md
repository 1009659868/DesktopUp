# TS-H01-A Desktop Overlay Host 诊断探针

状态：`TS-H01-E COMPLETE / PUBLIC PROBES + E5 FIXTURE / NO WINNER`

本探针只枚举 Windows Shell 窗口树并输出连续 JSONL 遥测，不创建海报、不更改 z-order、不调用 `SetParent`、不发送 `0x052C`，也不注入或修改 Explorer。

目标合同：

- `Docs/A_Project/20_Technical_Selection/DESKTOP_OVERLAY_HOST_PROBE_CHARTER_V1.md`
- `Docs/A_Project/20_Technical_Selection/UNIFIED_CANDIDATE_EVIDENCE_MATRIX_V1.md`

## 构建

```powershell
cmake --preset x64-release
cmake --build --preset x64-release
```

固定环境：Visual Studio 18 2026、x64、Windows SDK 10.0.26100.0、Release。

## 运行

```powershell
# 单次快照
out/build/x64-release/Release/desktop-host-probe.exe

# 每 100 ms 采样一次，共 20 次并写入 JSONL
out/build/x64-release/Release/desktop-host-probe.exe --interval-ms 100 --count 20 --log evidence.jsonl

# 持续采样直到 Ctrl+C
out/build/x64-release/Release/desktop-host-probe.exe --interval-ms 100 --count 0
```

输出包含：单调时钟、UTC、probe/OS build、进程 DPI；Explorer 世代；Progman、WorkerW、DefView 与图标视图；topology 分类；parent/owner/z-order/style；visible/iconic/cloaked/active/topmost；DPI 与三类边界；顶层窗口顺序。

这些数据只建立 TS-H01-A 的观测基础，不能单独判定 HG-Z 或 HG-IN 通过。

## 子探针状态

| 子探针 | 目录 | 当前结论 |
| --- | --- | --- |
| TS-H01-B | `ts-h01-b/` | 当前 `RAISED_PROGMAN` layered 层级 PASS；ordinary GDI FAIL |
| TS-H01-C | `ts-h01-c/` | split visual/input islands PASS |
| TS-H01-D1 | `ts-h01-d1/` | 键盘与任务栏显示桌面 10/10 + 10/10 PASS |
| TS-H01-D2 | `ts-h01-d2/` | 用户接受完成；Explorer 重建自动遥测仅 1/10，例外保留 |
| TS-H01-D3 | `ts-h01-d3/` | 用户接受完成；125%/工作区 PASS，D3-2 8/10 与未测 DPI 例外保留 |
| TS-H01-E5 | `ts-h01-e5/` | 实现中立 JSONL validator 正反例自测 PASS；无候选结果 |

所有子探针仍是公共宿主证据，不表示原生 Win32 或任何 UI/渲染框架已经获选。E5 只校验事件一致性，不替代候选人工视觉、输入、层级和性能证据。
D2/D3 的准确范围见各自 README 与 TS-H01-E 完成审计；候选下一步按 E6 交接规则独立运行 P0。
