# Canvas 呈现适配

CanvasSceneInstance 按稳定节点 ID 复用装饰控件，应用继承显隐、世界旋转和画板等比变换；CanvasImages 经资源端口读取并按预算解码，缩略图与画面缓存分开，不保存源路径。CanvasTypography 统一编辑器与海报的字体／字号／字距适配。CanvasContentPlacement 接既有内容槽；CanvasContentSnapshot 只读回宿主已准备的可见图标与名称供编辑器参考，不枚举磁盘、不请求Worker、不转移真实内容所有权。旧雨效使用共享时钟，新雨夜样例是普通图片／文字／形状／组，不创建每节点 timer。独立工作台在 Views/Canvas，预览接线在 Runtime/Posters。

不处理配方存储、文件搬运、网络请求或AI会话。不向PosterVisualWindow分部文件继续增加引擎逻辑；窗口只承接输入和发布结果。现有矩形输入边界保持，异形空洞待J4明确。
