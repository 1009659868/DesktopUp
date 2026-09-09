# V3-04-M05：最小内容／主题参数合同

状态：CLOSED — CONTRACT/BUILD/PROCESS PASS。日期：2026-09-09。
用户明确进入M05，只完成真实Item接入必需基础。M04收展策略体验已获认可，动画打磨延期；未逐项确认的删除/恢复等安全门保留，不补填M04全PASS。

## 本轮执行边界

1. 复用PosterDefinition.Title/Subtitle、LayoutParametersJson、TemplateKey、独立ThemeInstanceId和SQLite v5；不新建状态库/第二命令权威。
2. 参数JSON增加schemaVersion=1；内容尺寸/间距/字号与有限颜色覆盖为纯声明式数据，校验范围、组合、大小和版本。
3. 标题使用已保存文本；复用现有WinUI ScrollViewer/网格及命中几何，提供basic-box、night-ops两个内置结构解析，不装载外部主题或执行代码。
4. 源海报展开、事务内重算、恢复与renderer使用同一张海报的内容度量；不再给所有海报套一个night-ops最小高度。
5. 目标绑定不可变草稿与SetPresentation命令沿用实体CAS。UI面板后续复用该入口；本轮只增只读参数摘要，不搭完整编辑器。
6. 旧无版本JSON按已知字段解析且保留原文；首次显式参数编辑才转换为v1并保留legacy对象。未知版本/模板采用安全fallback展示、保留原配置、禁止隐式覆盖。
7. 参数变更不自动移动外框、推邻居或改保存高度。现有有效视口保留并重排/滚动；下次明确展开使用新最小高度。颜色/文本修改不取消合法条件归位证明，实体CAS仍使进行中旧请求失效。

## 排除

真实文件/Shell枚举、移动或删除，ContentItem/Collection实体与热度公式，完整主题包/热更新/AI，动画/层级/原生捕获重写，完整中心表单和便携配置导入导出均不在本轮。

## 计划与验收

- M05-A：版本/类型/单位/默认/范围、兼容读取与失效fallback。
- M05-B：目标草稿、字段白名单、entity CAS、幂等、持久化重启/克隆隔离。
- M05-C：逐海报ContentMetrics、渲染/命中/滚动同源、三种参数样例；不改outer geometry。
- M05-D：完整合同测试、独立Release与隔离进程验证、更新证据/交接；人工视觉不代填。

## 参数合同 v1

配置继续由 SQLite v5 的 PosterDefinition 行保存；不是新建外部配置文件，也不是便携配置导出功能。参数位于 LayoutParametersJson，显示单位统一 DIP。实体原有 Title/Subtitle、TemplateKey、ThemeInstanceId 不复制到 JSON 中。

| 内容字段 | night-ops 默认 | basic-box 默认 | 接受范围/语义 |
| --- | --- | --- | --- |
| itemWidthDip | 118 | 100 | 72..320；网格单元的最小期望宽度，余宽均分；窄到单列时按可用宽度裁剪 |
| itemHeightDip | 82 | 72 | 48..240；图标/卡片展示区域高度，不包含名称与行间距 |
| iconSizeDip | 48 | 40 | 16..128，且≤itemHeight−8、≤itemWidth−16；图标逻辑显示尺寸，不是源文件像素尺寸 |
| gapDip | 12 | 12 | 0..40；横纵间距共用，v1 不再另存一个可能冲突的“密度”权威 |
| labelFontSizeDip | 10 | 12 | 9..24；单行名称，超长省略，不改变完整显示名数据 |
| titleFontSizeDip | 36 | 24 | 18..48；night-ops 正文大标题字号，basic-box 无正文大标题，保留值不生效；受保护标题栏仍固定14 |
| theme.background | #21242A | #282B30 | #RRGGBB，表面底色 |
| theme.foreground | #F2F2F2 | #F2F2F2 | #RRGGBB，主要文字颜色 |
| theme.accent | #99CEFF | #68BDFF | #RRGGBB，正文强调与编辑边线；卡片示例条纹保留演示颜色 |

数字须有限；越界/不相容组合拒绝写入，不偷偷clamp。颜色仅六位十六进制，序列化统一大写；不接受代码、资源表达式、路径、URL或任意透明度。表面alpha仍固定245，标题操作区/交互提示受应用保护。本轮并未实现完整主题视觉角色表。

```json
{
  "schemaVersion": 1,
  "content": {
    "itemWidthDip": 118, "itemHeightDip": 82, "iconSizeDip": 48,
    "gapDip": 12, "labelFontSizeDip": 10, "titleFontSizeDip": 36
  },
  "theme": { "background": "#21242A", "foreground": "#F2F2F2", "accent": "#99CEFF" }
}
```

版本1允许部分字段缺省，读取按当前内置模板补默认；显式写入输出完整字段。长度上限32,768个.NET字符、嵌套深度16；根/content/theme不接受重复或未知字段。只有legacy对象是保留旧配置的不可执行载荷。主题实例ID沿用目录身份，参数覆盖按PosterId存储；克隆产生独立主题身份，之后互不联动。

## 兼容、草稿与提交

- 无schemaVersion：仅识别旧itemGapDip/cardHeightDip；rowHeightDip只有与共享行度量完全相等才接受，避免两份尺寸权威。其他旧属性只保留、不执行；正常读取或移动不改写原JSON。
- 首次显式编辑：旧对象完整放入legacy，后续必须保持其语义不变。当前字段发生变更不得丢弃旧扩展资料。没有旧资料时不允许凭空添加legacy。
- 未知版本/模板/layout、损坏或不安全参数：显示只读basic-box安全fallback，保留数据库原文并给出警告；不把fallback写回。显式修复/重置入口不在本轮。
- PosterPresentationDraft.Open绑定打开时的PosterId/entity version；切换中心选中项不改草稿目标，取消不写库。ToCommand→SetPresentation→原StateCommandPolicy/SQLite事务/三端快照，只有ControlCenter来源允许，强制目标读集CAS。
- 只允许Title、Subtitle、TemplateKey、LayoutParametersJson变化；几何、可见/锁定/收起、集合、ThemeInstanceId等不能夹带。标题/副标题继续遵守原仓储文本校验。
- 无关B写入不使A草稿冲突；A自身变化则Conflict，无半提交；重复requestId沿用原持久回执。同值提交NoChange。不会为本轮新增SQLite表、revision或第二状态权威。

## 渲染、输入及最小高度

列数=floor((可用宽度+gap)/(itemWidth+gap))，至少1列，最多64列；单元共享余宽。外框拖缩只改变列数/滚动视口，不改保存的图标尺寸、间距和字号，也没有热度缩放。后续真实图标提供方将使用iconSizeDip请求合适的DPI图像，当前仅用于演示占位符尺寸，不能算图标提取能力。

完整行高=itemHeight+7+ceil(labelFontSize×1.6)+13；行距另算。最小可展开高度=标题栏46+正文meta+上内距10+完整一行+页脚42。night-ops meta=ceil(81+titleFontSize×1.35)，basic-box meta=0，默认阈值分别346/210 DIP。像素阈值沿用各分区独立向上取整，四档DPI夹具覆盖。

实体投影解析为同一组Presentation/ContentMetrics，渲染、输入区域、滚动步长、源展开、事务重算、恢复都读取该海报度量。外框/保存高度不随参数更新移动；已存在的临时有效视口保留并重新绑定度量版本，溢出仍可滚动，下次用户明确展开重新求合法高度。参数增大导致当前视口小于新阈值时不主动推邻居。

XAML继续用现有WinUI Grid/ScrollViewer。只有明确的展示参数/文本revision更新才替换该实例XAML内容树，HWND不换；日常拖缩不走整棵树替换。basic-box是最小内置结构（标题栏＋内容＋页脚），night-ops保留游戏海报结构；不是DeskBox逐像素复刻。六张占位卡明确标注DEMO ITEMS，没有读写桌面文件。

## 参考来源与取舍

已阅读本地DeskBox commit `64f27e1aa0f16867abe20074f35ea05cd7b12ce7`：`src/DeskBox/Models/WidgetConfig.cs` 的每实例名称、尺寸、IconSizeOverride、Metadata，`src/DeskBox/Models/AppSettings.cs` 的IconSize、TextSize、密度/横纵间距字段。采用“几何与内容尺寸分离、配置按实例覆盖”的设计；不搬其全局可变设置进每张海报，不把Metadata当可执行脚本。

本轮未逐字复制DeskBox源码或引入新库；来源GPL-3.0-only，项目现有许可证/通知保持。范围/默认/版本/CAS/安全fallback是DesktopUp本轮合同，不声称已由DeskBox验证。

## 证据与下一步

完整测试395/395（新增39项）；Release 0 warning / 0 error。初次394/395中唯一失败为旧源码断言写死背景RGB；修改为检查同源参数背景后全通过，原prepare-before-bounds/erase/no-copy检查保留。

真实窗口烟测入口`scripts/smoke-presentation.ps1`使用隔离三海报SessionFixture；2026-09-09两轮PID43944/45260通过：同HWND/同外框/独立主题身份、实际XAML字号/行高/列间距、非法草稿不写库；第二轮启动读到3套已保存v1参数，revision仍为6。空间回归PID45428通过4次姿态命令、56个原生帧及两次Full展开，均exit0/资源0。实际结果集中记录在[VERIFICATION](VERIFICATION.md)。不能把程序化读回当作人工视觉或鼠标PASS。

M06多实例端到端夹具也已完成，见[M06结果](M06_MULTI_INSTANCE_FIXTURES.md)；下一项M07测量与未完成安全门，随后原04-04管理入口和V3-05真实Item。不要在M05追加全主题平台、AI或动画项目。
