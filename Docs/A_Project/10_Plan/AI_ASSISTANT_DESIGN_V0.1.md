# Desktop-Up 本地 AI 助手设计 V0.1

状态：`ACTIVE DESIGN BASELINE`

日期：2026-08-02

## 1. 产品定位

AI 助手是 Desktop-Up 的可选本地功能，帮助用户用自然语言调整海报、图标和主题，或生成受限的主题草稿。它不是在线账户服务，也不是 Desktop-Up 正常运行的必要依赖。

核心规则：

```text
未配置 API Key
  → 不初始化 Provider
  → 不启动 Agent 会话
  → 不发送任何网络请求
  → 其他功能完整可用

配置 API Key
  → 用户主动打开 Agent
  → 请求发送到用户选择的模型服务
  → 费用、额度和限制由用户的 API Key 所属平台决定
```

项目不建设账户、官方额度、付费套餐、云代理、计费或商业防滥用系统。

## 2. 首版供应商范围

| Provider | 配置 | 说明 |
| --- | --- | --- |
| OpenAI | API Key、Model | 使用 OpenAI Platform API Key |
| DeepSeek | API Key、Model | 使用官方 API 及工具调用能力 |
| OpenAI-Compatible | Base URL、API Key、Model | 为其他兼容端点预留；默认要求 HTTPS |

模型名称不硬编码为长期常量。设置页允许用户填写或从一次在线模型查询结果选择；查询失败时保留手动输入。

## 3. 设置与启停

设置页字段：

- AI 助手总开关；
- Provider；
- Base URL（预设 Provider 自动填写）；
- Model；
- API Key；
- “测试连接”；
- “删除凭据”；
- 是否保存本地对话历史；
- 单次最大输出长度和超时。

状态：

| 状态 | Agent 入口 |
| --- | --- |
| 未配置 Key | 不启动；入口显示“配置 AI”或保持隐藏 |
| Key 已保存、未测试 | 可测试连接，不开放执行 |
| 测试成功 | 允许打开 Agent |
| 鉴权失败 | 立即停用当前会话并提示重新配置 |
| 网络不可用 | 不影响桌面海报；保留未发送输入 |

## 4. 交互流程

```text
打开 Agent
  → 自动附加当前选中海报的最小上下文
  → 用户描述修改
  → 模型返回结构化 ChangePlan / Tool Calls
  → 本地校验参数和权限
  → 生成视觉预览与变更摘要
  → 用户应用或拒绝
  → 应用后进入统一撤销栈
```

示例：

> 将当前海报改成黑白线描风格，图标改成 58px，并按名称排序。

对应受控操作：

```json
{
  "operations": [
    { "tool": "set_theme_preset", "arguments": { "preset": "outline_mono" } },
    { "tool": "set_icon_size", "arguments": { "size": 58 } },
    { "tool": "sort_items", "arguments": { "field": "name", "direction": "ascending" } }
  ]
}
```

## 5. 首版工具白名单

读取工具：

- `get_current_poster`；
- `get_available_themes`；
- `get_selected_items`；
- `get_theme_parameter_schema`。

修改工具：

- `set_theme_preset`；
- `set_theme_parameters`；
- `set_icon_size`；
- `set_icon_spacing`；
- `set_layout_columns`；
- `sort_items`；
- `move_items`；
- `set_poster_locked`；
- `collapse_poster`；
- `create_theme_draft`；
- `preview_changes`；
- `apply_changes`；
- `undo_changes`。

模型只能提出调用；真正执行者是 Desktop-Up 的本地命令系统。所有参数必须经过 JSON Schema 和业务约束双重检查。

锁定海报属于用户保护边界：Agent 可以建议解锁，但不得在同一变更计划中静默解锁并移动海报。只有用户明确要求解锁，且在预览中单独确认后，后续计划才可改变其位置、尺寸或锚点。海报内部的图标大小、间距与排序不受位置锁影响。

## 6. 主题代码辅助

开放顺序：

1. 参数 JSON；
2. Desktop-Up 主题 DSL；
3. CSS 与 SVG；
4. 受限 JavaScript。

生成流程：

```text
生成草稿
  → 写入临时主题目录
  → 路径/大小/类型检查
  → 禁止外部网络和系统接口
  → 隔离预览
  → 显示代码差异与视觉对比
  → 用户确认
  → 安装为新的主题版本
```

AI 不得生成后直接执行 PowerShell、CMD、EXE、DLL、Rust 或任意原生插件。JavaScript 主题不得访问文件系统、注册表、进程、网络或 Tauri IPC，并必须能够被宿主超时终止。

## 7. 线描风格边界

- 参数化线描：通过主题 DSL、CSS、SVG Filter、边框和色板完成，属于首版 Agent 能力。
- 真正的图片线稿重绘：需要单独的图像生成/编辑 API，并会上传用户选择的图片；不进入首版文本 Agent。

## 8. Key 与请求安全

- Key 只存入 Windows Credential Manager；
- WebView/JavaScript 只能读取“是否已配置”，不能读取完整 Key；
- HTTP 请求由 Rust Provider 层发出；
- Key、Authorization、用户目录和请求正文不进入普通日志；
- Base URL 默认只允许 HTTPS；本地模型场景未来可显式允许 loopback HTTP；
- 删除 Key 和卸载时清理对应 Credential Manager 条目；
- Agent 不在后台自动发请求，只有用户主动提交或明确允许的重试才联网。

## 9. 上下文与隐私

默认只发送完成任务所需的最小上下文：

- 当前海报结构与参数；
- 项目显示名和匿名 ID；
- 可用工具 schema；
- 当前主题的受限源码片段。

默认不发送快捷方式绝对路径、Windows 用户名、桌面截图、游戏安装位置或其他海报内容。发送图片必须单独确认。

## 10. 错误与回退

- Provider 返回无效工具参数：拒绝执行并可要求模型修正一次；
- 超时或限流：停止本次操作，保留用户输入；
- 输出被截断：不安装不完整主题；
- 主题预览崩溃：终止预览进程并回到原主题；
- 模型建议未知工具：忽略并提示能力边界；
- 无网络或 Key：AI 不可用，但手动设置完全可用。

## 11. 验收标准

- 无 Key 启动时没有 AI 网络连接和 Provider 初始化；
- Key 不出现在前端内存快照、数据库、配置、日志和崩溃报告；
- OpenAI 与 DeepSeek 至少各通过一次测试连接和结构化参数修改；
- 所有 Agent 修改先预览、后确认、可撤销；
- 100 组无效/越界 Tool Calls 均无法越过本地校验；
- 主题草稿不能访问网络、文件、进程和 Tauri IPC；
- API 失败不影响桌面宿主、海报启动或手动主题编辑。

## 12. 建议实施阶段

| 子阶段 | 结果 | 依赖 |
| --- | --- | --- |
| AI-0 | 统一 Desktop-Up Command、ChangePlan、预览与撤销契约 | 布局/主题命令稳定 |
| AI-1 | Credential Manager、Provider 设置与测试连接 | Rust 后端可构建 |
| AI-2 | 自然语言参数控制与排序 | AI-0、AI-1 |
| AI-3 | JSON/DSL/CSS/SVG 主题草稿 | 主题包与隔离预览稳定 |
| AI-4 | 受限脚本主题 | 可选后续，不阻塞首版 |

## 13. 非目标

- 不提供 Desktop-Up 官方 AI Key；
- 不代理或承担用户调用费用；
- 不自动操作桌面文件；
- 不允许 Agent 执行 Shell 或原生代码；
- 不要求用户配置 AI 才能使用工具；
- 不在首版实现语音、多 Agent、云同步或在线主题市场。

## 修订记录

| 日期 | 版本 | 内容 |
| --- | --- | --- |
| 2026-08-02 | V0.1 | 接受本地 BYOK、无 Key 不启动、受控工具与主题草稿方案 |
