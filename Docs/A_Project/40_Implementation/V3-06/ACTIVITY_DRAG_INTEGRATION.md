# 真实热度与海报拖动：集成记录

2026-10-26，用户授权整合既有已验成果、验证组合边界、合入并推送develop；本轮不改main、安装包、tag或Release。

## 来源与验收承接

- 集成基线：`impl/develop@023a76f`，含拖动优化`c3fff8c`及历史归档补齐`78979ae`；非快进来源保留。
- 热度来源：`origin/impl/06-real-activity@6bbfcfc`，功能提交`8156b73`，共同基线`d922669`。
- 用户2026-10-26明确确认桌面标准尺度、热度／空间限幅与人工覆盖组合全部通过。原热度记录的“待体验”是旧检查点，记USER PASS，不重复人工验收；拖动原两组USER PASS继续承接。
- 独立整合分支：`impl/06-activity-drag-integration`。产品源码自动合并；新增代码仅是review组合探针，不更改生产行为。

## 文档冲突逐项处理

实际18个冲突文件均为Markdown。保留主线导航精简和历史归档，热度分支后续决定落实到正文；不整树选择ours/theirs。

| 冲突组 | 协调结果 |
| --- | --- |
| 根README、Docs/README、ACTIVE_PATH、AGENT_COLD_START | 保留精简导航，以CURRENT_STATUS为现状；不恢复旧机器路径、未实施或旧待验指令 |
| REQUIREMENTS_INDEX、Item需求、需求概要、迁移追踪、风险决定、运行界面（6份） | 吸收双保护衰减、连续独立倍率、实时更新、48 DIP与0.4～1.8；保留零海报、自启后台、单一归属及已撤功能 |
| 总架构、模块目录、通用合同、src/README | 真实consumer/producer、schema27与七工程职责准确落地；保留事件式拖动、增量呈现及平台安全保护；动态模板/AI仍属后续 |
| 总计划、细化工作流 | 比对热度分支变更后保留主线精简结构，原位更新状态、公式／空间语义与组合出口，不重新堆入历史阶段全文 |
| HANDOFF、CODE_ANCHORS | 当前入口结合双方有效内容；旧日期记录标为历史，热度原C交付保留，不用新通过抹去旧失败或未定因 |

## 本轮验证（PASS）

- 项目SDK10.0.112，Release/x64，`--no-restore -m:1 -nr:false`。
- 定向测试209/209通过，无失败/跳过：ContentActivity*、ContentHeatSession、PosterContentSession、ContentPresentationLayout、PosterDragFrameScheduler、TitlePresentationDriver、ContentFrameResize、ContentLayoutPersistence、UxContentPreference、MigrationFingerprint。TRX：`out/activity-drag-integration/tests/integration-targeted.trx`；不与历史211/357等重复相加。
- review候选及当前SessionFixture构建0警告/0错误，源码文本门1009文件、27冻结迁移通过。001～027未改；只用隔离配置，不启动日常数据库。
- 组合冒烟入口：`scripts/test-activity-drag-integration.ps1`，自建四海报／真实HWND/XAML；生产打开服务、统计、策略、会话和呈现链真实，提供方成功响应模拟，不打开外部程序，不操作鼠标，不认证显示器FPS。
- 组合冒烟首次通过：PID33324，退出0、activeResources=0、无error/critical/fatal；标准图像48 DIP，整体范围0.4～1.8。编辑期3次成功／3次重复只计3次；释放后版本4→8，无需收展；正常打开到10；拖动中第五次成功统计完成但不改变画面，取消移动后版本10→12，热项跨度5.381818→5.477778，作者意图及样例SHA256不变。Canvas/Input同代，原生输入区域重置恢复、过期／失败帧拒绝通过。
- 同一探针完整路径与纯平移各32样本：内容重绘32→0，UI线程分配1,568,048→856,040 bytes；中位提交4.4677→2.7026ms，P95 5.7479→4.0269ms。只说明本轮短样本，不认证FPS、尾延迟或大规模负载。
- 普通候选`out/activity-drag-integration/candidate/app/DesktopUp.exe`：Release/x64、ReviewTools=false，0警告／0错误；DLL SHA256 `B5FD0283E6896C868CE0D5733E765B0B625E9C76787D5C51E72625F59B09CF4B`。review DLL为`C4B3A7FBEBA2FBD54FC9DA002541A0407E828E4D7F63856BBF6697F40127DA2A`，已清理可重建输出。
- 最终文本门1010文件／27冻结迁移通过；架构门生产112源文件、0 review源，review开启184源／72探针，PreviewGuard通过。首次终端包装错误检查了负向PreviewGuard故意返回的LASTEXITCODE，脚本自身四项均PASS；已用PowerShell脚本成功状态单独复核通过，未因此改动产品或放宽门槛。
- 本轮30份Markdown严格UTF-8／无NUL／无冲突标记检查通过，125处新增本地链接及锚点通过，暂存diff检查通过。合入前重新fetch确认develop仍023a76f、热度仍6bbfcfc、main仍ecb0c04，没有覆盖他人新提交。

## 测试收尾（完成）

核对无DesktopUp／Worker进程、精确样例目录无重解析点或未知文件，初始化样例及新引用SHA256保持。23文件逐一与ZIP摘要比对后，移除`out/drop-d3/activity-drag-integration01`；可恢复归档为`out/activity-drag-integration/evidence/activity-drag-integration01.zip`，SHA256 `77301FA18B5DE9D0A3981B44C9BA260498589CEEE8AA0D4B77AE3EE9E85F279B`。另移除本轮可重建`out/activity-drag-integration/probe`输出；普通候选、TRX和归档／回执保留。没有创建桌面样例、安装登记、自启项或修改日常配置；其他轮次产物未动。

## Git回执

验证与清理已完成，按授权非快进提交整合树并合入develop；实际提交及远端回读在完成同步后补记。本轮不改源热度分支、main或tag，不强推。

## 保持开放的边界

剩余全局读／校验／连接等待、无变化证据写入、慢单根扫描与全局版本竞争仍按架构§30.4先测量再决定。P1 final02／R6 probe02锁冲突、历史卸载中断与升级41首因未定继续OPEN；新通过不用于定因或销项。真实登录、多屏DPI／高刷新率与联合长稳未验，模板／AI／在线更新平台不扩入本轮。
