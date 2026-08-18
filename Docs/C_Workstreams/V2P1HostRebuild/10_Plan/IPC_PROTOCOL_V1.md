# IPC 协议与传输契约 v1(C-V2P1-01 设计件)

状态:`设计稿 — 实现随 V2-P1 代码落地`

日期:2026-08-18

## 1. 帧格式

```
[4 字节 LE 长度][UTF-8 JSON 载荷]
```

长度 = 载荷字节数,不含 4 字节头。单帧上限 16 MiB(防呆)。

## 2. 消息类型

| 字段 | 说明 |
| --- | --- |
| `id` | 请求编号,u64,发起方单调递增;回复/错误必须回显 |
| `cmd` | 命令名(请求)或事件名(事件) |
| `base_rev` | 状态修订号前置条件(u64);不满足 → 错误 `rev_conflict`(防回弹) |
| `ok` / `error` | 回复:成功 / 失败(`error.code` + `error.message`) |

命令(核心 → 渲染器):`hello_ack`(确认+配置注入)、`set_geometry`、`set_visible`、`set_params`、`capture`(自检)、`shutdown`。
命令(控制中心 → 核心,P2 起):`get_state`、`apply_command`(全部带 base_rev)。
事件(渲染器 → 核心):`hello`(含 hwnd/rect/regions 声明)、`geometry_changed`(用户拖动后上报)、`bye`。

## 3. 状态与修订

- 核心持有唯一状态权威;每次状态变更修订号 +1;
- 所有写命令必须带 `base_rev`;执行时比对当前修订,不一致即拒绝并回发 `rev_conflict`(携带当前修订与状态快照,供发起方重放);
- 渲染器无状态:只投影核心下发的最新状态;用户拖动只产生事件,不自行改状态。

## 4. 传输层

| 通道 | 用途 | 模式 |
| --- | --- | --- |
| stdio(父子) | 核心 ↔ 渲染侧车 | 1:1,已实证稳定,生产化沿用 |
| 命名管道 `\\.\pipe\desktopup-core-v1` | 控制中心(P2)↔ 核心 | 服务器=核心;阻塞 `ConnectNamedPipe` 循环 + 每客户端独立读线程 + 多实例(吸取 PIPE_NOWAIT 断管教训) |

约束:任何通道禁止 PIPE_NOWAIT;管道模式 byte-mode + 长度前缀帧(与 stdio 同帧格式);客户端断连 = 移除会话,不影响其他客户端。

## 5. 错误码

| code | 含义 |
| --- | --- |
| `unknown_cmd` | 未知命令 |
| `bad_frame` | 帧解析失败 |
| `rev_conflict` | 修订冲突(附当前修订+状态快照) |
| `renderer_down` | 渲染器不可用(重启中) |
| `timeout` | 命令超时(5s) |

## 6. 自检契约(渲染器必须遵守)

- capture 前 `isVisible` 前置检查,未呈现 → `magenta=0`;
- 强制产帧后延迟到事件循环干净上下文抓取(禁止 frameSwapped 内抓取);
- 健康回复携带 `frames`(换帧计数)与 `exposed/visible/geometry`。

## 修订记录

| 日期 | 版本 | 内容 |
| --- | --- | --- |
| 2026-08-18 | V1 | 帧格式、消息类型、修订防回弹、双通道传输、错误码、自检契约 |
