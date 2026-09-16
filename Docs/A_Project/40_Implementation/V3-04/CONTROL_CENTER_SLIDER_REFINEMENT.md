# 控制中心滑块修订与列表闪烁排查
日期：2026-09-15。归属：[04-04 UX计划](../../30_Plans/V3_04_04_CONTROL_CENTER_PLAN.md)、[可用性设计](../../20_Architecture/CONTROL_CENTER_USABILITY_DESIGN_V1.md)。

## 1. 本轮范围与状态

用户反馈旧UX“效果不错、未发现新问题”，但指出滑块过宽、预设不应是独立按钮，以及收展/隐藏时左列表闪烁。该反馈不扩写为所有U1～U7、混合DPI或BUG-001均通过。

按指定顺序：滑块调整 → 验证与文档 → 列表闪烁排查。滑块实现和自动门已完成；本节写入时列表仅有用户现象，尚未确定根因。当前不修改列表生产逻辑，不进入04-05，不执行Git同步。

## 2. 滑块合同与实现

- 每项最大320 DIP，空间不足时随外观内容区缩窄，左对齐；不新增独立页面或大号参数卡。
- 保留WinUI Slider原生轨道、thumb、捕获和键盘。新增非命中Canvas，只绘制三个7 DIP点与下方标签；没有透明按钮覆盖轨道。
- 尺度：小0.8、标准1、大1.5；疏密：紧凑0.5、标准1、宽松1.5。位置按真实值线性映射至thumb中心的有效行程；尺度标准约28.57%，疏密标准50%，不是等距装饰。
- 点附近静止点击选择精确预设；完整手势偏移超过4 DIP后不吸附，包括移开又返回同点。点位点击识别宽7 DIP、垂直12 DIP；普通轨道点击/拖动与方向键仍按0.01细调。
- 点位由当前WinUI HorizontalTrackRect/HorizontalThumb实际布局读回，不硬编码边距；部件不可用不猜测命中位置。标记不画在当前thumb上方，端点文字保持在控件内。
- 高频调整只预览，松手通过原ControlCenterSettingsSession提交一次；切目标、版本冲突、关闭恢复规则不变。主题/热度分层、空间/R4和持久化schema均不变。

## 3. 验证与失败保留

- 完整合同1283/1283，0 skipped，新增18项位置/端点/不同轨道宽度/拖回/非节点/未就绪测试。结果：out/verify/v3-04-04-ux-slider/results/slider-full.trx。
- app/fixture Release均0警告0错误。最终DLL SHA256：EE3C5F1A10A741EE911808646F5E5E2BFDA9968262D8C1B49A91BFDBF082FBDA。
- 六组证据：out/verify/v3-04-04-ux-slider/gates-a30e0c7cddf9415dbbab0875350a6119。

| 模式 | 0张PID | 1张PID | 3张PID | 结果 |
| --- | --- | --- | --- | --- |
| controls | 41328 | 24280 | 59312 | PASS，退出资源0 |
| settings | 52272 | 47036 | 14328 | PASS，退出资源0 |

新增实际门包括：原生轨道可读、宽度不超320、点位与实际thumb中心对齐、精确小/标准/宽松值、单revision提交、参数隔离和提交后真实XAML。控件适配器测试不是系统鼠标注入（NativeMouse=false）；三海报稳定RenderTargetBitmap已阅，不能替代真实鼠标或GPU桌面合成认证。窄窗口拖动与实际点选仍待人工。

首轮gates-1a6eb63…在初次Loaded失败；gates-6e857997…补只读异常后确认SliderPresetGeometry.Center越界，实际ScaleSlider.Minimum=0.800000011920929，精确预设为0.8。原因是XBF小数压缩，修为从Domain合同以double设置原生范围、步长与预设，不放宽参数校验。失败日志/dump保留，隔离smoke的异常日志不吞异常。

## 4. 候选与人工入口

本轮已打开三海报人工包PID53744（2026-09-15 16:23，+08:00），不启用smoke或注入鼠标。PS5.1 CheckOnly通过25项；文档91份/697相对链接/缺失0，CRLF感知diff检查通过。自动门在当前执行令牌下运行（日志isElevated=true），不扩称普通令牌/混合DPI认证。

旧out/verify/v3-04-04-ux、旧25项指纹及旧人工配置不覆盖。复用原启动/烟测脚本，显式选择Candidate slider，新25项指纹为scripts/control-center-slider-candidate.json。默认Candidate ux继续启动旧候选。

~~~powershell
& 'G:\A_\Tools\DesktopUp_V2\scripts\start-control-center-usability.ps1' -Candidate slider -Scenario three
~~~

新配置：out/manual/v3-04-04-ux-slider/three。启动前正常退出其他DesktopUp；启动器验证25项文件及匹配的六组资源门，不回退旧包。此入口不写日常配置。

请复验：三个点可点击取预设；从thumb连续微调、越过点不强制吸附；松手自动保存且A/B隔离；窄窗口无横向溢出。随后列表闪烁单独按证据安排修复。

## 5. 文档更新后的列表排查结论

已登记[V3-BUG-004](KNOWN_ISSUES.md)：Summary/RuntimeStatus参与record相等性，任一行状态变化触发整表ItemsSource替换；默认WinUI增删/内容/入场过渡解释文字闪烁，是高置信根因方向。旧PID30652日志证明当时目录仍完整、目标未丢失；重复投影不等于每次重绑，不归因于海报动画每帧重绘。

尚未做真实帧的过渡开关对照，不把静态链路和日志当最终视觉修复证据。下一步拟用稳定集合/行身份＋原位属性更新，只让目录结构变化影响集合；经用户安排后实施，保持原状态/事务/空间通道。本轮未改列表代码，滑块新包仍可能看到旧闪烁。
