# Desktop Runtime Foundation 最终交接

最后更新：2026-08-14

状态：`COMPLETE — C-DHF/C-DRP/C-RLC CLOSED`

## 完成事实

- C-DHF-01..08、C-DRP-01..06 与 C-RLC-01..06 全部完成；
- 单全局透明宿主、主屏单海报几何/命中/桌面层恢复、事件驱动暂停、托盘、单实例、控制中心、自启与退出契约已形成自动、Release 和用户人工证据；
- C-RLC 最终 Node/文档 159/159、Rust 132/132、Release/NSIS、安装内容/依赖、多进程与注册表探针通过；
- B-DRF 对 Poster Core 暴露的 HostSurface、DisplayTopology、InteractionRegion、RuntimeSnapshot 与 lifecycle 边界保持冻结。

## 延期边界

- 多显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启和热插拔未认证；
- 净机安装、升级、签名和恢复型卸载不属于 B-DRF 完成结论；
- 不恢复 TOPMOST/NOTOPMOST 脉冲，不把 Explorer 坐标作为业务权威。

## 下一步

后继 C-LST 已完成吸收审计；当前回到 B-PCR，等待用户明确进入后激活 C-PSI。不得重新打开 B-DRF 来实现 Item、模板或多海报。
