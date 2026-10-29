# Canvas 声明与纯规则

CanvasDeclaration 严格读取 v1/v2/v3，校验能力、素材摘要、画板尺寸、文字样式和唯一内容槽；CanvasAuthoring 管理40步历史、组／拆组、独立实例。v3的CanvasSurfaceGeometry统一整张海报原点、标题操作区、基础外形包含关系与可读空间下限；CanvasLayout不再在标题下居中放置v3画板。旧v1/v2保留原规则，只在编辑草稿中升级。CanvasEditingGeometry处理旋转命中和尺寸手柄，CanvasLayerState单次父序遍历解析显隐／锁定。CanvasPosterGeometry保持原生内容矩形边界。不依赖UI/I/O。样例位于Infrastructure/Canvas/Samples；旧CanvasSamples只在tools/review/App作为v1夹具。

不拥有真实文件、密钥、网络、控件、定时器或持久化。已有配方v1仍在 ../Templates，通过适配接入，不把旧模板复制成第二套权威。

Behaviors/ 按职责分为受限语言编译（CanvasScript）、预制体／绑定与锁定校验（CanvasBehaviorPrefabs）、有界执行调度（CanvasScriptRuntime）。CanvasBehaviorRuntime只保留值／过渡，不再另有旧事件执行路径。旧Rules在内存中转换到脚本；新作品保存代码快照与有序绑定。组合导出／插入一并复制并重映射内部行为，外部图层引用拒绝。执行器只请求宿主收展并等待结果，不能调用文件／网络或自行写状态。

设计来源：[联合协议](../../../Docs/A_Project/20_Architecture/CanvasContracts/CONTRACT.md)。不因预留目录增加程序集或空接口。
