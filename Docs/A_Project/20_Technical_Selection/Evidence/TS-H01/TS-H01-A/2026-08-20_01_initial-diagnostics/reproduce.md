# 复现步骤

工作目录：`prototype/desktop-host-win32`

## 构建

```powershell
cmake --preset x64-release
cmake --build --preset x64-release
```

## 单次汇总验证

```powershell
out/build/x64-release/Release/desktop-host-probe.exe
```

将单行 JSON 交给 PowerShell `ConvertFrom-Json`，核对 `schema`、`os`、`process_dpi`、`explorer`、`topology`、`desktop_windows` 和 `top_level_windows`。

## 连续原始遥测

```powershell
out/build/x64-release/Release/desktop-host-probe.exe `
  --interval-ms 50 `
  --count 5 `
  --log Docs/A_Project/20_Technical_Selection/Evidence/TS-H01/TS-H01-A/2026-08-20_01_initial-diagnostics/snapshot.jsonl
```

预期：5 个可独立解析的 JSONL 对象，sequence 为 0–4；同一未重启 Explorer 前 generation 一致；每个对象都记录 topology 与窗口诊断字段。

## 安全前置

- 探针只读；
- 不启动旧 V1/V2 海报宿主；
- 不发送 Shell 私有消息；
- 不执行 Explorer 重启；
- 本轮不测试 Win+D、输入或实际挂载。
