# C-DRP-02 用户验收、物理矩阵延期与 C-DRP-03 激活

日期：2026-08-10

状态：`C-DRP-02 CURRENT GATE COMPLETE / C-DRP-03 COMPLETE`

## 用户验收与决定

用户确认当前最新版 Release：

- 退出并重新启动后首次显示没有顶部白条；
- 收起/展开往返没有裁切；
- 海报按钮命中正常。

用户同时决定暂不执行第二显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启和热插拔物理矩阵，并授权直接进入 C-DRP-03。

本次状态处理为：

- C-DRP-02 关闭当前依赖门；
- 未执行的物理矩阵不记为 PASS，也不声称已经完成多屏实机验证；
- 该矩阵移交 C-DRP-06 Release 门，在 C-DRP 完成前补测，或形成用户明确接受的发布限制。

## C-DRP-03 首个实现切片

- 在不提升 `host-contract-v1` 版本的前提下增加兼容 `surfaceRegistry v1`；旧 `interactionRegion` 仅保留给兼容请求，两者同时出现时拒绝；
- Rust/JS 同步校验 registry revision、surface/region ID 唯一性、零到多表面、区域容量和宿主逻辑边界；
- 生产 `buildHostSurfaceSyncArgs` 改为提交单表面 registry，宿主仍只有一个 canonical `sync_host_surface` 写入口；
- 原生层以空 HRGN 为起点，将全部圆角 interaction regions 通过 `CombineRgn(..., RGN_OR)` 合并，再由 `SetWindowRgn` 排除宿主空白；零区域提交得到空窗口区域；
- 显示变化 resync、旧 resize 兼容包装、宿主重配置和 ready 握手均保留 registry，并以相同 revision/`effectiveScaleFactor` 重应用完整并集；
- RuntimeSnapshot 的 desktopHost 诊断新增 registry version/revision、registered surface count、region count 和区域摘要。

## 自动化证据

- `pnpm run check`：49 项 Node/契约/静态架构测试通过，94 个 Markdown 文档链接通过；
- Rust 测试：35 项通过；
- `pnpm run build`：通过；
- 覆盖零/一/多/重叠区域、重复 surface/region ID、registry revision 不一致、legacy/registry 歧义和 100%/125%/200% DPI 单次换算；
- `git diff --check` 通过。

## 后续完成结论

C-DRP-03 后续视觉/输入分离重做和最终 Release 人工矩阵已经完成：

1. 海报空白框选、桌面右键和原生快捷方式穿透通过；
2. 卡片、标题栏拖动、锁定、无白条及收起/展开通过；
3. C-DRP-03 标记 `COMPLETE — USER ACCEPTED`，C-DRP-04 已激活。
