# Desktop-Up V2

Windows 11 低资源桌面海报启动器 —— V2 重构。

V2 重构前提:V1 的桌面海报宿主(透明 WebView2 窗口模式)因顶部白条根因无法在架构内根治而推倒重做;产品目标与已验证资产(Rust 核心、SQLite、托盘、生命周期)尽量复用。渲染宿主选定实验先行。

文档入口:[`Docs/README.md`](Docs/README.md)

当前分支:`impl/Refactor`
