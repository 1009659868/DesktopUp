# Desktop-Up V2 需求索引

状态：`FROZEN PRODUCT DIRECTION — USER REVISION 2026-09-08`

最后检查：2026-09-08

本索引只登记产品需求、验收边界、风险与来源追溯。它不包含技术选型、架构决定、制作计划或实现工作流。

2026-09-08最新补充已同步画像RP-SP-05、视觉§7.4/7.5及运行表面§17：有下方合法余量时必须先下扩，再上扩；只有下方容量为零才纯上扩。原最小高度、临时尺寸不覆盖保存意图、同步条件归位保持。

| 顺序 | 权威文档 | 职责 |
| --- | --- | --- |
| 1 | [`DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md`](../A_Project/10_Requirements/DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md) | 产品目标、范围、全局不变量、数据、安全、Windows 集成、性能和总体验收 |
| 2 | [`POSTER_VISUAL_AND_INTERACTION_V1.md`](../A_Project/10_Requirements/POSTER_VISUAL_AND_INTERACTION_V1.md) | 海报视觉、构图、几何、状态、命中、拖动、缩放、锁定与收起 |
| 3 | [`RUNTIME_SURFACES_AND_TRAY_V1.md`](../A_Project/10_Requirements/RUNTIME_SURFACES_AND_TRAY_V1.md) | 海报、控制中心、托盘、设置、后台核心及生命周期关系 |
| 4 | [`CONTENT_ITEM_ACTIVITY_LAYOUT_V1.md`](../A_Project/10_Requirements/CONTENT_ITEM_ACTIVITY_LAYOUT_V1.md) | Item 模型、启动语义、活跃度、排序、缩放、固定和动态排版 |
| 5 | [`THEME_PLATFORM_AND_AI_V1.md`](../A_Project/10_Requirements/THEME_PLATFORM_AND_AI_V1.md) | 主题定制、导入导出、隔离、回退、BYOK AI 与变更权限 |
| 6 | [`REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md`](../A_Project/10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md) | 历史反例、必须证伪的风险、未冻结产品问题和暂缓范围 |
| 7 | [`REQUIREMENTS_MIGRATION_TRACEABILITY_V1.md`](../A_Project/10_Requirements/REQUIREMENTS_MIGRATION_TRACEABILITY_V1.md) | V1 到 V2 的来源映射、冲突归一和遗漏检查 |

## 审计证据

[2026-08-20 完整需求一致性审计](../A_Project/10_Requirements/REQUIREMENTS_CONSISTENCY_AUDIT_2026-08-20.md) 记录重复、矛盾、遗漏和实现假设的修订过程。该文件是非规范性证据，不构成第八份需求权威。

## 权威边界

- 上述七份文档共同组成用户确认冻结的完整需求基线；2026-08-28 用户正式采用“外框几何、内容参数、响应式排版分离”模型，并进一步纠正为 Item viewport 连续裁切、连续 offset 与独立滚动入口；这些变更只修订第 2、4 份权威文档，不增加第八份需求权威。
- 任何需求变更必须形成显式决策，并重评受影响的技术候选、证据和下游阶段。
- 技术名称只能作为需求文档中的历史说明；当前 V3 技术路线和制作顺序由独立架构/计划文档记录，不能反向改写需求。
- `impl/poster-redesign-V3` 已建立 DeskBox-based 架构和制作计划；它们是冻结需求的下游实现决策，不构成第八份需求权威。
- V1 只读来源快照已在 V3 文档重置中移出当前工作树；逐项来源结论保留在迁移追踪表，原文件仍可从 Git 历史恢复。

## 2026-09-07 当前范围修订

用户将通用桌面内容收纳、多海报、一个控制中心、一个托盘、自定义独立风格化主题、热度排版与 AI 辅助定制确认为正式方向。七份需求原位修订，不新增第八份需求权威：画像 §31、Item §20、运行表面新增多海报章节、视觉多实例章节和主题目标边界共同承接。普通文件/目录可由用户显式安全收纳；应用安装内容保护与移动前账本不变。主屏多海报不再延期，多屏完整认证仍单列。

架构合同与细化工作流是下游实现设计：前者规定数据/端口/故障边界，后者规定依赖/出口。用户确认产品方向不等于逐项实现完成；OPEN 默认值和能力认证仍按风险台账推进。历史审计不覆盖新决定。

后续同日补充：画像 §32 / RP-MP-06/07、RP-PORT-01/02 明确首次仅一张默认、管理入口和删除确认、单文件跨机配置；运行表面 §17.1/17.2、视觉 §8.3、主题 §10.4 同步。保留七份需求体系；格式/导入已有布局策略由04-06-A冻结，素材复用07-04，不把文件格式建议混作用户已指定技术。

09-07空间补充：画像 §33 / RP-SP-01..07、视觉 §7/20、运行表面 §17 同步不可重叠、收展推让、已锁定者固定、未再调整者安全条件归位和方向展开，并重申唯一中心的全目录选择。旧“顶边始终固定”与正常允许重叠已被条件规则替代。

09-08用户明确修订：先确认收起过程同步归位；随后确认完整尺寸方向回退→剩余空间临时展开→低于最小可用高度时保持收起且按钮轻抖。RP-SP-05/06与新增RP-SP-08、视觉§7.5和运行表面§17原位承接，最小高度按完整标题＋一行完整Item＋必要页脚判定，临时高度不覆盖保存尺寸。该规则替代09-07的一律原尺寸不足即拒绝；需求确认不代表代码已实现。
