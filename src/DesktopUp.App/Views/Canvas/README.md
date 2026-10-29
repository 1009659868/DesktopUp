# 创作工作台

CanvasEditor.cs 管理窗口生命周期、作品切换与用例接线；Chrome 集中工作台视觉和响应式布局；Surface 管理稳定视觉、图层、选择手柄及帧合并；Input 处理直接操作；Properties 提供紧凑属性、颜色与组合保存；Library 接受控图片导入和组合。纯编辑、父子几何与历史在 Domain/Canvas，持久化经 ICanvasWorkspace，不直接访问业务数据库、真实文件操作或统计。

Behavior 管理行为预制体列表与图层绑定／排序；ScriptEditor 提供受限代码编辑、行号诊断和保存；Run 仅适配画板内收展、输入与按需渲染，不另写解释器、不修改真实海报。实际执行共用 Domain/Canvas/Behaviors 的编译器／调度器和 Presentation/Canvas 场景。Context 管理素材说明、状态样式与数据复制，不发送网络。AI工具使用 CanvasDraftTools 的版本化原子命令，不模拟控件，不取得 Apply 或文件操作权限。旧规则表单及其弹层探针已替换，旧规则只兼容读取转换。

TitleControls负责标题文字／v／更多菜单的子控件选择和v绑定面板；几何复用Domain/CanvasTitleControls，不复制坐标，不把父标题冒充v。脚本保存经工作区持久化作品草稿，入库另外保存代码快照；多行控件先设置AcceptsReturn再赋Text，保存时规范为LF，编译器兼容CR／LF／CRLF。默认样例模板只在复制后占作品预算。

图片解码由Presentation/Canvas/CanvasImages承接。CanvasContentSnapshot读取宿主已准备的可见图标和文件名供编辑参考；不开第二套文件交互或Worker扫描。参考显示可关闭，空白创作默认关闭；实际成员由唯一内容槽管理。PosterSurface提供外形属性及标题预览，标题／内容槽可在画板内直接移动和改尺寸。画板就是展开海报，应用时同步外框，外框手柄保持构图比例；改变长宽比在画板尺寸属性中完成。关闭恢复未提交的设计和外框，已应用设计与尺寸按状态事务重建。外形第一批为矩形／圆角／椭圆，原生边界由Windows适配统一实现，不把透明度当作输入穿透。
