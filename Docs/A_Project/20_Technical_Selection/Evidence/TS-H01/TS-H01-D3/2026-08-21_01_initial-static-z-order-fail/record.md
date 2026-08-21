# TS-H01-D3 Initial Static Preflight — Z-order FAIL

状态：`FAIL — INITIAL ATTACH ORDER`

日期：2026-08-21

首个 D3 x64 Release 在 5 秒运行中生成 605 个样本，四类几何边界、DPI、样式与 revision 均一致，但 visual sibling index 为 1、DefView index 为 0，说明 visual 位于桌面图标下方。605/605 样本因此正确标记为 FAIL，进程退出码为 4。

根因是 D3 初版创建窗口后直接以 `SWP_NOZORDER` 提交几何，没有像已验证 D1 夹具一样在初次安全挂载时建立 sibling 顺序。修复只在初次挂载执行一次顺序建立；后续拖动、缩放、收起和显示变化继续使用 `SWP_NOZORDER`，不增加轮询层级修复。

该失败 run 永久保留，未被第二次静态预检覆盖。
