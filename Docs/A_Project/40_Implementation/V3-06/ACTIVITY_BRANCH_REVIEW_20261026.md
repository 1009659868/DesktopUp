# 真实热度分支审查与合并准备

2026-10-26；本轮只审查、登记验收及准备合并，不把热度源码合入develop，不修改main、安装包或tag。

## 1. 基线与验收状态

- 已验拖动优化：功能`c3fff8c`，根README历史归档补齐`78979ae`；非快进集成`8872028`及归档补齐`878f693`，功能分支与develop均已推送。集成后跟踪树与功能分支一致，复用原103项定向、12个标题呈现场景及两组USER PASS，不重跑未受影响业务。
- 热度来源：`origin/impl/06-real-activity@6bbfcfc0e8458dcce72020ee8f5cfb234ad191ce`；共同祖先`d922669`。独有两提交：`8156b73`产品／测试／文档，`6bbfcfc`检查点交接；相对共同祖先107文件变更。
- 远端[原交付记录](https://github.com/1009659868/DesktopUp/blob/6bbfcfc0e8458dcce72020ee8f5cfb234ad191ce/Docs/A_Project/40_Implementation/V3-06/REAL_ACTIVITY_DELIVERY.md)仍写集中体验待确认。本轮向用户核对“桌面标准尺度、热度与空间限幅／人工覆盖组合”，用户明确答复“已经全部通过，可记录”。因此记为**USER PASS / 功能验收收口，未集成**，不重复原两组验收。
- “准备合并”不作实际合入授权；组合验证尚未执行，不能把两条分支各自通过等同合并候选通过。

## 2. 源码确认的功能范围

| 领域 | 远端实现 | 当前develop的边界 |
| --- | --- | --- |
| 统计 | 成功事实完成后唤醒应用级Consumer；每批最多128条，统计／游标／待消费删除同事务，重复激活不重复计数；每对象最多32条近期记录 | 只有打开事实与旧提案基础，尚无正式统计链 |
| 身份／迁移 | ContentItemId跟随仍有有效卡片的对象；移除后同路径重加不冒领旧身份；新增027，001～026未改 | 仍为schema26；新迁移仅用隔离旧库副本验证，不试改日常配置 |
| 策略 | Domain纯策略，40/35/25排序；30天新项和14天近期保护、7～28天半衰期及10%底线；连续尺寸从作者基准派生，不累乘 | 不是旧五档注入；热度与整体倍率独立 |
| 实时发布 | 统计提交后按当前归属通知；每海报单计算在途、合并最新请求；交互／空间过渡期间保护，过时实体／文档／成员／桌面代际结果失效 | 旧“等下次展开才更新”已被分支后续决定替代；无每海报新增高频timer |
| 空间保护 | 热度沿既有规划原点必要扩框、不推邻居；受阻按现框限幅；用户缩框只重投影、不反向撑回；手动模式／手工尺寸／图钉保留 | 旧“热度绝不扩框”准备方案不是已验分支规则 |
| 配置与尺度 | 海报级热度开关、最小／最大／默认倍率和K，初值0.5／3／1.5／20；整体尺度另为0.4～1.8，标准4×4卡片、疏密1、无热度／手工覆盖时图像区域48 DIP；preferences v1合法旧值可读，超1.8先内存限幅，正常提交前不改原JSON | develop整体尺度仍0.4～3；分支已验不等于已集成发布 |

代码入口均指远端提交：Application的`ContentActivityConsumer`、`ContentActivityProducer`、`ContentHeatSession`、`ContentHeatExpansion`；Domain的`ContentActivityPolicy`、`ContentHeatSizing`、`PosterContentPreferencePolicy`；Infrastructure的`SqliteContentActivityStore`、`DirectTransferPreparation`、`Migrations/027.sql`；App的`PosterVisualWindow.Activity`、`PosterSpatialRuntime.Activity`、`ControlCenterWindow.Activity`。本轮未复制这些产品文件到工作树。

## 3. 证据与审查边界

- 已核对远端提交、核心调用链、迁移差异、消费失败／身份／限幅与人工覆盖测试源码，并用`git merge-tree --write-tree --messages`做不修改分支指针、索引或工作树的预检查。
- 远端记录含123＋88项早期定向、19项实时更新定向及标准尺度两批去重后357项通过；不同批次重叠，不能累加成新总数。另记review构建、原生隔离呈现／命中与UI检查通过。
- 本机不存在该分支记录的两个标准尺度TRX、候选DLL及最终原生result.json；自动结果仅按**远端交付记录**引用，不冒称本轮重跑或二进制核验通过。用户本次明确确认的人工结果单独登记。
- 本轮未运行热度程序、迁移数据库、安装器或操作用户文件，无新增运行测试样例。拖动样例上轮已校验归档清理。
- 本轮14份状态／计划／审查Markdown严格UTF-8与无NUL检查通过，27处新增／修改本地链接及锚点通过，`git diff --check`通过；本次后续差异仅文档，不触发产品构建或回归。
- 无高频timer、小集合计算结果不等于锁等待／大集合性能达标。剩余成本、P1/R6锁冲突、历史卸载首因及升级41继续OPEN；真实登录、多屏DPI、长时等余验保留。

## 4. 合并预检查

基线`develop@878f693`对`activity@6bbfcfc`，生成树`2a6a80b`仅供检查，未成为提交或运行候选。

**18个文本冲突全部在Markdown，产品源码无文本冲突：**

| 类别 | 文件（省略共同路径／版本后缀） |
| --- | --- |
| 导航 | Docs/README、ACTIVE_PATH、AGENT_COLD_START、REQUIREMENTS_INDEX |
| 需求 | CONTENT_ITEM_ACTIVITY_LAYOUT、REQUIREMENTS_PROFILE、MIGRATION_TRACEABILITY、RISKS_AND_OPEN_QUESTIONS、RUNTIME_SURFACES_AND_TRAY |
| 架构 | V3_ARCHITECTURE、V3_MODULE_CATALOG、GENERAL_CONTENT_MULTI_POSTER_CONTRACT |
| 计划 | V3_IMPLEMENTATION_PLAN、V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW |
| 交接／导航 | HANDOFF、CODE_ANCHORS、根README、src/README |

保留develop单一当前入口与历史归档结构，逐条吸收热度后续决定及证据；禁止整份ours/theirs覆盖，不能恢复旧共享／穿透规则或丢掉新公式／尺度／实时发布决定。本轮文档继续更新后，正式整合前须重新fetch和预检查，18不是固定合同。

源码语义关注两处共同修改及一条直接依赖：

1. `PosterVisualWindow.Content.cs`：检查树同时保留`_paintedContentFrame`平移复用与热度`Reconcile`／`WithPresentation`。平移无变化继续复用；热度／尺度变化须生成新呈现和同代命中，不能命中旧缓存。
2. `SpatialGestureSession.cs`：调度注释与热度最小外框校验可自动合并；仍需保留新版下限与原安全提交，不能用当前热度有效尺寸锁死缩框。
3. 热度安全点依赖`IsSpatialBusy`／`ActivityChanged`，共享拖动调度负责释放／取消。需验证拖动期间延期、结束后续作、迟到结果失效及静止标题收拢；Git无冲突不等于运行兼容。

## 5. 下一步与退出标准

1. **准备完成**：来源／范围已审，用户验收已记，冲突与组合风险已列；停留develop，热度分支不动。
2. **获整合授权后**：从最新develop建立隔离整合候选，保留两条来源历史并逐项协调文档；组合验证前不把候选写入集成基线，不强推远端。
3. **只做直接风险定向与一次组合冒烟**：复用统计／迁移／尺寸及拖动调度／呈现测试，重点“平移不全刷新、热度变化令缓存失效、交互结束最新热度续作”；027仅用隔离schema26副本。不复跑历史安装／卸载／拖放全矩阵，不要求等待30天。
4. **组合通过后非快进合入并推送develop**：登记实际提交与证据。原两组USER PASS继续有效，只有组合暴露新可见变化才补最小体验，不机械重复验收；main／打包／tag另行授权。

结论：**功能分支已验，合并准备完成；组合验证及实际合入尚未执行。**
