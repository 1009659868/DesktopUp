# 架构图源与重生成

## 当前 R4收口后的架构与目标对照

2026-10-19，R1～R4 USER PASS / CLOSED。现状以[源码对照与图表回执](R4-Closed/README.md)为准，图源R4-Closed/current.architecture.json；HTML在out/architecture-r4-closed。原目标图源不变，明确区分基础职责已落地、能力接点部分具备与未来能力未实现。R4目录及下方A/B/C、Integrated-53b6bec均为历史快照，不覆盖本段。

## A/B/C 集成后更新（2026-10-16）

当前代码基线为 `impl/develop@53b6bec`。见[集成后当前架构与流程说明](Integrated-53b6bec/README.md)：整体架构已通过 9/9 showcase、四尺寸浏览器检查及深浅主题目视检查；三张新流程图因 Archify 布线校验冲突停止于图源，未交付 HTML，文字流程与诊断已保留。本次不改产品源码，不提交/推送，不开始下一批。

## 原始审查快照（4ae822a）

审查基线为 `impl/develop@4ae822a`。现状与目标建议分开，详见[审查报告](../ARCHITECTURE_EVOLUTION_REVIEW_2026-10-16.md)。

- 两张 architecture：当前运行职责、建议目标职责。
- 三张 workflow：当前打开、建议能力热换、建议应用更新。
- 本轮完整交付身份和验证状态：[delivery-receipt.json](delivery-receipt.json)。
- HTML/截图/浏览器原始回执位于仓库忽略目录 `out/architecture-review-20261016/`，不进入发布包。生成物清理后须重新生成；不能沿用旧哈希或把旧浏览器回执视为新版本证据。

## 本机重生成示例

需要 Node.js 和 Archify 2.17。以下命令不安装依赖；在仓库根运行。其他机器请替换工具路径。

```powershell
$archify = 'C:/Users/allmightwen/.agents/skills/archify/bin/archify.mjs'
$diagram = 'Docs/A_Project/20_Architecture/ArchitectureReview/current.architecture.json'
& node $archify validate architecture $diagram --quality showcase --repo-root . --json
& node $archify deliver architecture $diagram 'out/architecture-review-20261016/current.architecture.html' --quality showcase --repo-root . --json
$env:ARCHIFY_CHROME = 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'
& node $archify visual-check 'out/architecture-review-20261016/current.architecture.html' --json
```

其余图将类型/文件名改为对应名称；只有当前架构需要 `--repo-root` 验证固定提交的来源。workflow 使用 schema v2，不接受 `--repo-root`。

每次编辑图源后重新 validate，冻结后 deliver 成功才能 visual-check；如检查失败不能继续声称通过。结构验证、真实浏览器测量和截图目视审查是三个独立结果，必须分别记录。不得仅依据自动回执宣称目视通过。

## 内容范围

现状架构箭头表示主要运行调用/职责，不是完整程序集依赖图；完整工程引用与具体存储旁路在报告中说明。现有打开流程只表达与成功事实/热度消费相关的主链和关键失败分支，不穷举所有取消和恢复路径。目标图不是现有功能声明，也不是冻结接口。

五张图的共同建议是保留已验内核，逐步抽离内容用例、会话/呈现，再接热度、受限模板和AI计划；应用二进制更新另走独立更新器与受控重启。
