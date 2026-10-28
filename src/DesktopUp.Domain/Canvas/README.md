# Canvas 声明与纯规则

CanvasDeclaration 严格读取 v1/v2，校验能力、素材摘要、画板尺寸、文字样式和唯一内容槽；CanvasAuthoring 管理40步历史、组／拆组、独立实例。CanvasLayout 共用父子变换及等比例适配，CanvasEditingGeometry 处理旋转命中和尺寸手柄，CanvasLayerState 单次父序遍历解析显隐／锁定。CanvasPosterGeometry 保持原生内容矩形边界。不依赖 UI/I/O。样例位于 Infrastructure/Canvas/Samples；旧 CanvasSamples 只在 tools/review/App 作为 v1 夹具。

不拥有真实文件、密钥、网络、控件、定时器或持久化。已有配方v1仍在 ../Templates，通过适配接入，不把旧模板复制成第二套权威。

设计来源：[联合协议](../../../Docs/A_Project/20_Architecture/CanvasContracts/CONTRACT.md)。不因预留目录增加程序集或空接口。
