# 可插拔模型与性能优化：集成／发布记录

2026-10-26，INTEGRATED / RELEASED。按用户授权仅集成已验模型与性能成果，精简main、打包验证及新建`preview-20261026.2`完成；旧tag及附件保留。发布说明只写本次热度模型与拖拽协同相关变化，不宣称新模板平台已发布。

## 精确范围

- 纳入：[已验模型](PLUGGABLE_MODEL_DELIVERY.md)的统一协议、双模型选择、连续尺寸／目标位置／受限淡入、安全点与交互保护；[J1批量关系解析](../V3-07/JOINT_COST_AND_COMPONENTS.md)的测量和优化。
- 排除：T1声明式组件的产品源码、测试与验收入口。用户明确组件暂留功能分支；本次不补记组件人工PASS，不将其带入develop／main／安装包。相关说明作为分支交接保留，不代表集成线已有该能力。
- 将同一工作区拆为“模型＋性能”提交和后续“组件原型”提交。develop只合前一个已验提交，不合功能分支末端；组件候选及配置保留以便后续设计参考。
- 复用原196项及J1证据；因拆分混合文件，重新做模型／关系／架构定向构建和一次模型／拖动组合检查。发布使用独立输出、隔离安装身份与配置，不覆盖日常安装；不重复旧人工体验。
- 剩余成本、P1/R6锁冲突、历史卸载／升级首因及环境余验继续OPEN。失败保留首次证据，重试不改写首次结果。

## 后续设计准备

发布后回到develop，进入[海报画板引擎设计准备](../../30_Plans/POSTER_CANVAS_ENGINE_DESIGN_PLAN.md)。本轮不直接实施通用引擎，也不把有限介绍组件作为完整引擎的既定结构。

## Git 与发布绑定

| 对象 | 准确来源／状态 |
| --- | --- |
| 已验模型／性能 | `223e403f296cb72a8ba15d99bd88e6b4e9d3a154`，从混合工作区拆分；模型探针／fixture和混合窗口文件仅包含已验部分 |
| 功能分支 | `impl/06-pluggable-activity-model@61819dd88fb5a9a8963d53cde9da3549342d21b1`已推送；后续组件原型单独提交，不合入develop |
| develop 集成 | `ff4b0a1a80f304980fe2ade784e8b0223cd8c984`非快进合入223e403，已推送；随后仅补本轮文档回执与随包说明，不改已验代码 |
| 精简 main | `43a77e736d05289f560406713d7a4fc349276310`非快进同步ff4b0a1，已推送；旧／新产品精简差异归一化后均836行、差异0 |
| 新标签 | `preview-20261026.2`，注释标签对象`197610f8aa18c3b1d7ced108e96f9412ae41bc76`，指向main 43a77e7；与包内源码完全一致 |
| 预发布 | [DesktopUp Preview 20261026.2](https://github.com/1009659868/DesktopUp/releases/tag/preview-20261026.2)，Release ID `423986804`，2026-10-26T08:12:03Z发布；不是draft，prerelease=true |

旧`preview-20261026.1`的标签对象、说明和两附件摘要均回读一致，未替换。发布说明仅四条本次模型／拖拽协同／关系解析变化。当前停留develop；main的发布精简不反向合入开发线。

## 定向与安装验证

- 拆分后重新运行模型、关系、相邻统计／会话、拖动调度与架构门，**66/66 PASS**；TRX为`out/releases/model-protocol-validation/split-integration.trx`。此前196项及J1测量保持原记录，不累计成新测试数。
- review与普通构建、源码编码门通过。实际HWND/XAML组合PID57684退出0、资源0：模型切换／位置／同代命中、过渡结束与取消、拖动内热度延期及释放后10→12均通过；32次纯平移内容重绘0，完整刷新对照32。有限提供方模拟边界仍保留，不当作真实鼠标FPS认证。证据`model-drag-combination.json`。
- 干净main发布构建与自包含打包0警告／错误：523项源码输入、810项载荷声明摘要匹配；无测试／探针／T1、PDB或用户库；`IncludesUncommittedChanges=false`，27个冻结迁移不变。证据`package-audit.json`、`production-parity.json`。
- 用最终相同载荷及安装事务源码，只替换测试登记身份／名称／隔离配置，验证中文空格路径安装。810项安装文件匹配；安装目录内运行库加载、控制中心首次启动、正常退出资源0通过。**本轮首次卸载退出0**；配置和自建未知文件保持，日常安装／配置／自启／快捷方式前后快照一致。证据`installed-result.json`。未重复原位／迁移／真实登录等未受影响矩阵，也未覆盖用户日常安装。

全部本轮自动证据位于`out/releases/model-protocol-validation`。本轮无产品失败；清理成功后外层命令误把`LASTEXITCODE`当作该PowerShell脚本结果，曾报“Cleanup failed”，核对已完成回执与目录缺失后修正守卫再继续，未重复删除或写成卸载失败。此前Windows32首次失败等历史证据继续独立保留，不因本轮成功销项。

## 产物与收尾

- 安装包：`out/releases/main-model-final01/DesktopUp-Preview-20261026.2-win-x64-Setup.exe`，67,391,259 bytes，SHA256 `7FE2F4483BE7BD35EE8C63B442CF8A0CB1BB4B56DC349F539447EDF04CAA57C6`。
- `SHA256SUMS.txt`摘要`3973C3E2C9E959A5865778635FC6C9D557CC8E812ACB8AF2A153A002EA8D3529`；两附件上传摘要与实际下载流摘要均匹配。远端回执`stage-receipt.json`／`publish-receipt.json`／`verify-receipt.json`。
- 20份组合探针样例／配置逐文件校验归档后清理，ZIP摘要`150393C32EF1EF06EEFD6262561CD7742D62B2DCDB20C9B60FB41B5814298C24`；26份安装证据归档，ZIP摘要`0D221D1F2A206E50A5625E2F4EBBDFB726593451A8EF2A33A60961C9F6266673`。归档及清单可恢复查证。
- 已移除精确自建测试根、隔离安装登记与安装器状态、review／prebuild／打包中间目录；最终包、载荷、TRX、证据保留。不向桌面遗留样例、不清理真实收纳。
- 用户已退出T1候选PID23540；`out/joint-template/candidate/app`和`out/drop-d3/template-components-review01/profile`原样保留，供未验组件及后续引擎设计参考，不计入本次清理或验收。
