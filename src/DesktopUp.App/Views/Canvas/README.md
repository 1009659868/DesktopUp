# 创作工作台

CanvasEditor.cs 管理窗口生命周期、作品切换与用例接线；Chrome 集中工作台视觉和响应式布局；Surface 管理稳定视觉、图层、选择手柄及帧合并；Input 处理直接操作；Properties 提供紧凑属性、颜色与组合保存；Library 接受控图片导入和组合。纯编辑、父子几何与历史在 Domain/Canvas，持久化经 ICanvasWorkspace，不直接访问业务数据库、真实文件操作或统计。

图片解码由 Presentation/Canvas/CanvasImages 承接。CanvasContentSnapshot 读取宿主已准备的可见图标和文件名供编辑参考；不开第二套文件交互或Worker扫描。参考显示可关闭，空白创作默认关闭；实际成员由唯一内容槽管理。画板按声明尺寸创作，原生等比例适配并保留标题／输入边界与空间检查。关闭恢复未提交预览，已应用设计按状态事务重建。
