# M08-02：设置实体与迁移

日期：2026-09-11。状态：PASS（设置/迁移/合同与构建范围），不是原生自动收展完成。
前置：[M08-01合同](M08_AUTO_POSTURE_MODEL.md)纯验证通过，用户明确确认手动收起后须真实离开再进入；[V2-TBD-017](../../10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md)已RESOLVED。主线和后续边界见[M08计划](../../30_Plans/V3_04_M08_AUTO_POSTURE_PLAN.md)。

## 1. 实际交付

| 层 | 实现与约束 |
| --- | --- |
| Domain | PosterDefinition新增AutoCollapseDelaySeconds；默认3、整数1～300，与既有AutoCollapseEnabled一起属于每张海报，不属于全局DesktopUpSettings。PosterAutoPosturePolicy集中范围/默认校验 |
| Application | SetAutoPosture=7追加枚举，不重编号旧命令；仅ControlCenter来源、必须目标实体ReadSet、拒绝空间载荷和无关字段。PosterAutoPostureDraft绑定打开时的PosterId/实体版本，不跟随之后选择切换 |
| Projection | PosterSurfaceProjection.AutoPosture直接映射实体到已存在的纯AutoPosturePolicy；中心复用同一目标投影；托盘新增AutoPostureEnabledPosterCount，表示配置已启用数而非正在运行观察的数量 |
| Infrastructure | schema7追加单列、完整读取/插入/更新映射，复用原SQLite事务、备份、revision/实体CAS和持久回执 |
| 兼容指纹 | StateRequestFingerprint仅供命令/旧目录仓储请求哈希使用。精确省略新延迟的默认3，使旧v6语义相同请求保留原哈希；非默认值必须参与哈希。普通JSON序列化仍显式包含新字段 |

核心路径：草稿 → 目标命令 → 实体读集检查 → 白名单/值校验 → 同事务更新海报及回执 → 发布同一committed revision。未接UI控件；本阶段没有配置文件导入导出、控制中心开关界面或后台计时器。

修改开关/秒数只推进目标EntityVersion与全局revision，不改变姿态、几何、LayoutGeneration、邻居实体版本、全局SettingsVersion或CatalogVersion。关闭自动功能保留原延迟和当前姿态，不发反向动画。已有几何捕获通过权威seed的with更新，保留新字段。

创建默认仍OFF/3；明确提供配置的创建保存其配置；复制继承源开关/秒数、分配新身份，但不复制任何计时状态。已删除目标的旧草稿返回NotFound，零目录不补海报。无变化请求仅保存原有幂等回执，不增加committed revision或投影事件。

## 2. schema7与兼容安全

迁移名：`poster-auto-posture-settings`。

```sql
ALTER TABLE posters ADD COLUMN auto_collapse_delay_seconds INTEGER NOT NULL DEFAULT 3
    CHECK (auto_collapse_delay_seconds BETWEEN 1 AND 300);
```

- v1..v6 SQL与checksum不改；v6独立指纹从已提交53ebdb6源码提取为D5FBCD000A17312F50BC7CDEFBC0B3516B0B2C8D9AB8C83C5B8CFE17F31B3B1D，并加入旧schema夹具。
- 先验证源库并生成源版本备份，再在原迁移事务内添加列、校验列形状/默认/数据范围、登记schema和提交。失败不能留下半列、半ledger或新revision。
- 保留所有原海报字段、开关真假、实例/内容/主题参数、姿态、各版本、profile初始化标记，以及设置、操作、回执、空间证明/连续关系原行。仅增加延迟默认3，state_head更新schema与迁移时间，业务revision不增加。
- 已初始化零目录仍零；未初始化空库仍只允许原来的一次默认播种。迁移不主动执行自动动作。
- SQLite STRICT/NOT NULL/CHECK阻止小数、空值和越界入库；Domain/命令也独立校验。JSON入口不会把1.5、字符串"3"或null强转为秒数；缺失字段的旧JSON只补默认3。整数数值的SQLite无损转换不等于对小数秒取整。
- 设置事务中回执写失败，目标开关/延迟、entity/global revision和发布一起回滚；同目标旧版本冲突，不覆盖；不同目标保持实体级独立写。
- 旧v6命令与直接目录仓储回执可按旧请求重放；增加非默认延迟后复用同RequestId必须拒绝。没有清空回执或把旧请求当新命令再执行。
- schema7副本不能交给旧v6二进制强行降级；回退使用迁移前v6备份与匹配旧包。新字段更新后的库不能用旧备份直接覆盖而丢数据。本步所有迁移仅在测试临时目录进行，日常库未打开/迁移。

## 3. 验证矩阵与结果

[AutoPostureSettingsTests](../../../../tests/DesktopUp.Contracts.Tests/AutoPostureSettingsTests.cs)与[AutoPostureSettingsMigrationTests](../../../../tests/DesktopUp.Contracts.Tests/AutoPostureSettingsMigrationTests.cs)新增42项：

- 默认OFF/3，1/3/300有效，-1/0/301/int最大值拒绝，非整数JSON拒绝，旧JSON缺字段兼容。
- 目标草稿/切换不串写，命令来源/读集/无关字段限制，三端相同revision及目标changed IDs；只改策略不改布局/锁定/收起/邻居/全局设置。
- 两连接同目标冲突、不同目标独立、NoChange/重放、同ID换载荷拒绝；回执故障全回滚及同ID成功重试。
- 创建/复制/旧目录仓储/直接保存/重新打开数据库保持秒数；删除后旧草稿不复活海报，零目录无副作用。
- v6全部旧列及相关表原行与源备份对照、开关真假保留、已初始化/未初始化空库、迁移后重复初始化；迁移后段触发器故障完整回滚，备份目录不可用时迁移不开始；当前缺列和未来schema失败关闭。
- schema7备份完整性及300秒读回；旧命令和旧目录回执跨迁移仍幂等。

最终新42/42，全量619/619，0失败、0跳过；独立WinUI Release构建0警告/0错误。首轮旧577项中9项只是当前schema期望6而实际7；只更新这些“当前版本”断言，旧迁移指纹和旧业务revision断言未弱化。原M08-01的69项连续模型测试仍在全量回归中。

```powershell
dotnet test tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj -c Release -p:Platform=x64 --filter FullyQualifiedName~AutoPostureSettings --logger 'trx;LogFileName=m08-02-settings.trx' --results-directory out/verify/v3-04-m08-02
dotnet test tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj -c Release -p:Platform=x64 --logger 'trx;LogFileName=m08-02-full.trx' --results-directory out/verify/v3-04-m08-02
dotnet build src/DesktopUp.App/DesktopUp.App.csproj -c Release -p:Platform=x64 -o 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m08-02\app\' -bl:out/verify/v3-04-m08-02/build.binlog
```

环境：Windows/x64，SDK10.0.103；项目目录权限经审查后构建/测试。App源码、窗口和输入实现未修改。新包只构建，未启动，无新增鼠标、Shell/DPI、动画、资源性能或实际用户设置UI认证。保留M07稳定包，24项指纹只读检查全部匹配，不要求重复M07人工。

产物均在`out/verify/v3-04-m08-02/`，SHA256：

| 文件 | SHA256 |
| --- | --- |
| m08-02-settings.trx | 6217D49E3F1DC81ECC9466B4C43DF31653ECC27BAB62D47F1C29D85E56EF8E1E |
| m08-02-full.trx | 2DF2CA3D172F3822530B3C1FDD8780CB7FF45589363F933354CC7E800965CEA8 |
| app/DesktopUp.dll | 705AF90E49AA7919B94B0D3F25193F6F966816DBB01A813098C85BFA15ABD453 |
| app/DesktopUp.Domain.dll | FC696F8DFF521B2F67B2EBAF2CCF73E4CA9BACDB87E46D767D8895022EEE9F04 |
| app/DesktopUp.Application.dll | 3F42678CFA6A092771DFDCCE34677447D8AB7FB465C42BEE2D0E0C0847D21207 |
| app/DesktopUp.Infrastructure.dll | D4A35AA7CD044F92072EA66E13E6819E2B68F2D4E890B7D1D7FDAF80D3808345 |

## 4. 下一步与接线检查项

下一项M08-03被动指针观察与交互保护；04接共享调度与System收展，05才做最小中心设置界面。本阶段只沿用01已阅读的DeskBox行为经验与本项目既有SQLite/CAS，没有新增第三方复制代码或许可证变化。

本轮另核对到：PosterSpatialRuntime能区别System来源以抑制按钮抖动，但StateProjectionCoordinator.Run目前仍拒绝System命令。这不是可用自动提交通道。M08-04必须补受控的系统姿态提交入口、完成/取消通知和新鲜读集验证，不能删除通用来源限制或把仅“不抖按钮”写成自动功能通过。本步只登记，不越级修改。

未commit/push，保留之前未提交文档/脚本与M08-01成果；不操作旁边旧DesktopUp仓库，不修改任何用户源文件。
