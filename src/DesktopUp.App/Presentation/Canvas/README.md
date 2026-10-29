# Canvas 呈现适配

CanvasSceneInstance 按稳定节点 ID 复用装饰控件，应用继承显隐、世界旋转和画板等比变换；CanvasImages 经资源端口读取并按预算解码，缩略图与画面缓存分开，不保存源路径。CanvasTypography 统一编辑器与海报的字体／字号／字距适配。CanvasContentPlacement 接既有内容槽；CanvasContentSnapshot 只读回宿主已准备的可见图标与名称供编辑器参考，不枚举磁盘、不请求Worker、不转移真实内容所有权。旧雨效使用共享时钟，新雨夜样例是普通图片／文字／形状／组，不创建每节点 timer。独立工作台在 Views/Canvas，预览接线在 Runtime/Posters。

CanvasSceneInstance.Behavior承接CanvasScriptRuntime，把值映射到缓存控件；预制体代码只在设计变化时编译，状态发布的相同设计保留等待中的执行。事件命中几何只在声明／布局变化时构建，常量值不重复写XAML。指针与脚本复用共享调度，过渡结束撤销动态需求；hover绑定仍需共享观察。标题和真实Item不成为视觉动作目标，收展只能请求宿主用例；装饰点击独立有界命中，不抢真实Item、标题、右键和滚轮。编辑器和桌面共用此场景，不各自实现脚本语义。

不处理配方存储、文件搬运、网络请求或AI会话。不向PosterVisualWindow分部文件继续增加引擎逻辑；窗口只承接输入和发布结果。矩形／圆角／椭圆按v3共享声明，任意空洞仍待J4明确。
