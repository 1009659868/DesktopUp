# Poster Core Runtime 当前交接

最后更新：2026-08-11

状态：`ACTIVE AT B-PCR / C-PCB-05 CONTRACT SEAL AND C-PSS HANDOFF`

## 已建立

- C-DRP-01..06 已关闭；生产宿主通过 `host-contract-v1`、`sync_host_surface` 与 `runtime-lifecycle-v1` 向海报核心提供稳定边界。
- 当前 Release 只认证主屏单海报。多显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启和热插拔没有被表述为已验证。
- 用户已验收当前单海报的空白穿透、卡片命中、标题栏拖动/锁定、收起/展开、无顶部白条、Win+D 稳定性、生命周期以及控制中心按需创建。
- C-PCB 已激活；首个切片冻结版本化快照边界、唯一默认快照、持久态白名单与临时回退规则。

## 当前边界

- 当前默认快照只创建一个海报，但契约本身不通过数组长度封死未来多海报。
- `interaction` 与 `runtime` 是运行时派生状态，不进入海报持久快照。
- `transparent-default` 是正常默认模板；`minimal-fallback` 是内部临时渲染选择，不能持久化为用户模板。
- A0 的 `theme`、`collapsed`、`locked`、两海报演示快照仅可经显式 legacy adapter 读取，不能成为 C-PCB 权威字段。
- 窗口物理坐标、DPI、WorkerW/Progman、输入钩子继续归宿主；C-PCB 只提交逻辑表面请求。

## 精确下一步

C-PCB-01..04 已完成。正常默认模板、用户模板与内部 fallback 已形成 16 场景 JS/Rust 共用失败矩阵；回退保留原始请求且不写回持久态，fallback 自身失败明确返回不可渲染。全量 JS 83/83、Rust 65/65 通过，现有 Release 投影未改变。当前进入 C-PCB-05，下一步封板公共契约、收敛唯一 legacy bridge、核对 canonical 宿主调用、执行单海报 Release 核心回归并形成 C-PSS 输入清单。

## 工作区注意事项

- 当前分支：`impl/poster-redesign-V1`；未经用户明确要求不提交、不推送、不合并。
- 工作区包含此前 C-DRP 的已验证未提交改动，必须保留。
