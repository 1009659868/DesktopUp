# 布局统一修订与Item前置布局设计

日期：2026-09-16。BUG-005 CODE / AUTO PASS，用户新包外观三项复验通过（USER PASS）；原05-05托盘人工通过保留。高自由度04-L目前仅设计/计划，未接入产品。

## 1. 已实施与兼容范围

- PosterConfigurationFactory.NewBasicBox为中心/托盘共用默认构造；统一PosterPresentationPolicy.AdaptiveGridLayout。默认海报也用同一常量。皮肤basic-box/night-ops不作为布局引擎名。
- SQLite schema v8追加canonical-created-poster-layout迁移；v1～v7 SQL/校验和未改。旧版不应打开升级后的库；独立候选/数据保持分离。
- 修复条件同时满足：grid＋basic-box＋已发布的完整默认v1参数原文＋committed的controlcenter-catalog-*或tray-create-*结果记录。其余未知布局/版本/参数/无来源证据均不猜测转换。
- 迁移前通过原备份API校验v7副本；同一事务修复所有匹配记录，实体version各+1、总revision只+1并写state_operations。零匹配不增加业务revision；重复启动不再修复。
- 不改变ID/参数原文/位置/宽高/锁定/姿态/显隐/自动设置/主题实例/集合/空间因果关系或目录版本。故障回滚整批，备份保留。
- 不支持的外观明确说明只读，不再只显示“已保存”。没有放宽未知配置保护，也未实施完整内容编辑器。

## 2. 自动证据

| 检查 | 结果 |
| --- | --- |
| 完整合同 | 1315/1315，新增13项，0 skipped；results/layout-full.trx |
| 构建 | App与SessionFixture Release均0警告0错误 |
| 实际进程门 | 0/1/3张各exercise/restart/controls/settings，12/12 PASS，退出资源均0 |
| 新增链路 | 真实菜单命令绑定新增→CanEdit→实际模板/尺度/疏密控件→单次提交→同库重启指纹；仍非原生鼠标手感认证 |
| 进程证据 | out/verify/v3-04-05-layout/gates-71e9d06991654a65a211abb48c3ea8c4 |
| PS5.1入口 | -Candidate layout -CheckOnly验证25产物指纹/12同包门通过；旧global入口不覆盖 |
| 真实旧配置副本 | 原global/three用SQLite只读备份复制至layout/three；PID51808启动迁移7→8，revision18→19；仅“新海报2”layout_kind及必要版本/更新时间变化，所有其他海报/设置/空间记录不变，备份与旧posters相同；正常退出资源0 |
| 设计稿 | 三种构图、跨度微调、界内不重叠、360px窄屏和无JS异常检查；原生生产能力未由此认证 |

App SHA256：0E2C5250708A555AF94DEB76E459921FF155C516E20AE0C80BEE494563012C5A。
Fixture SHA256：A29925C51282ACD3AB588C0C6343F810B80A4343572144BF450291E1BB474163。
25项绑定：[tray-layout-candidate.json](../../../../scripts/tray-layout-candidate.json)；候选目录out/verify/v3-04-05-layout。

首轮定向57 PASS/1 FAIL保留于layout-focused.trx：新测试错误使用“当前版本备份校验”来断言v7迁移前备份有效。原迁移已按源版本校验成功；修订测试明确验证源版本/完整性/原记录，不改生产校验或放松安全要求。全量随后通过。

## 3. 人工复验入口

用户已退出旧PID57844。使用同一份旧配置的独立迁移副本，因此仍可看到之前的“新海报2”，无需重新布置；原global/three仍为v7未修改。

2026-09-16已按上述入口启动修订候选PID53204，路径out/verify/v3-04-05-layout/app/DesktopUp.exe，使用layout/three独立副本；实际恢复原四张海报。人工结果仍待用户反馈，不把启动成功算作体验通过。

```powershell
& 'G:\A_\Tools\DesktopUp_V2\scripts\start-tray-manual.ps1' -Candidate layout
```

1. 选“新海报2”：模板、两个滑块不灰，可修改并保存；其他海报不变。
2. 分别从中心/托盘新增：外观可编辑，收起/隐藏时保存参数不应强行展开/显示。
3. 正常退出并同命令重启：模板/参数、数量、原几何/状态保持；不重复迁移增加revision。

新增功能复验不要求重做全部既有托盘门。BUG-001/004未修复，真实文件未操作。仍使用DEMO ITEMS。

## 4. 设计与主线交接

- [布局设计V1](../../20_Architecture/CONTENT_LAYOUT_FOUNDATION_V1.md)：内容事实、布局意图、几何/皮肤分层；跨格/区域/三模式、热度接口、持久化、输入/动画/热更新边界。
- [三参考可操作设计稿](../../20_Architecture/Designs/content-layout-v1.html)：插画墙/封面层级墙/横版混排，卡片选择、跨度和顺序调整；示意素材，不是正式游戏封面效果。
- [04-L01～07计划](../../30_Plans/V3_04_L_CONTENT_LAYOUT_PLAN.md)：合同→纯模型→存储→视口→编辑→热度/主题端口→集中人工，插在04-07/08之后、V3-05-01之前。

最近下一步仍为04-06-A便携配置/恢复合同（新包复验后）；预留有界布局文档/未知引擎/ID重映射，不提前冻结L数据库格式。未实现04-06-A、实际跨格WinUI编辑、热度公式或真实Item。未Git提交/推送。
