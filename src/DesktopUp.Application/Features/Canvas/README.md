# Canvas 设计用例

CanvasDesignSession 持有固定目标与预览令牌，预览／提交均检查资源；CanvasDesignCommands 复用状态事务、CAS及已应用设计撤销。ICanvasWorkspace 是素材／作品／组合端口，具体存储位于 Infrastructure/Canvas。CanvasLayoutCache 仅保留当前实例的一份不可变布局；不引用 WinUI 或 SQLite。

CanvasSceneDescription提供版本化画面包与能力／含义对照；CanvasContentObservation只消费成功发布的匿名可见矩形，不输出真实身份。CanvasDraftTools是单目标的受控进程内工具门：32步原子命令、范围／版本／摘要与幂等检查，预览与用户Apply分权。素材人工说明属于配置内资源元数据；本层不联网，不保存密钥，不推断素材语义。

复用现有State事务和Content会话，不能复制成员清单、文件归属或热度统计。用例与图形测量／原生发布分责；文件新鲜检查和同代提交保护不绕过。

协议：[联合设计](../../../../Docs/A_Project/20_Architecture/CanvasContracts/CONTRACT.md)。旧模板用例仍在 ../Templates，不并行维护两套生产入口。
