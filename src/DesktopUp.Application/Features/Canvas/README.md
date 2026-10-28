# Canvas 设计用例

CanvasDesignSession 持有固定目标与预览令牌，预览／提交均检查资源；CanvasDesignCommands 复用状态事务、CAS及已应用设计撤销。ICanvasWorkspace 是素材／作品／组合端口，具体存储位于 Infrastructure/Canvas。CanvasLayoutCache 仅保留当前实例的一份不可变布局；不引用 WinUI 或 SQLite。

复用现有State事务和Content会话，不能复制成员清单、文件归属或热度统计。用例与图形测量／原生发布分责；文件新鲜检查和同代提交保护不绕过。

协议：[联合设计](../../../../Docs/A_Project/20_Architecture/CanvasContracts/CONTRACT.md)。旧模板用例仍在 ../Templates，不并行维护两套生产入口。
