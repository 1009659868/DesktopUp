# DesktopUp V2 文档入口

状态：`V3-03 INPUT CLUSTER — R1 RELEASE BUILT / MANUAL RECHECK PENDING`

## 权威顺序

1. 用户最新明确决定；
2. [七份冻结需求](00_Catalog/REQUIREMENTS_INDEX.md)；
3. [V3 技术路线与总体架构](A_Project/20_Architecture/DESKTOPUP_V2_V3_ARCHITECTURE_V1.md)；
4. [V3 模块分类与端口目录](A_Project/20_Architecture/DESKTOPUP_V2_V3_MODULE_CATALOG_V1.md)；
5. [V3 制作计划](A_Project/30_Plans/DESKTOPUP_V2_V3_IMPLEMENTATION_PLAN_V1.md)；
6. [V3-03 实现记录](A_Project/40_Implementation/V3-03/VERIFICATION.md)；
7. 后续每阶段产生的实现合同、验证记录和交接材料。

需求定义“必须得到什么行为”，架构定义“当前探索分支准备如何实现”，计划定义“按什么顺序交付与验证”。发生冲突时，下层文档不能覆盖上层文档。

## V3 重置结果

- 保留 `Docs/A_Project/10_Requirements/` 下的完整需求画像、六份配套权威/支持文档和一致性审计；
- 删除旧 Qt、QPainter、Qt Quick、Win32 Host、Axiom 原型及测试证据；
- 删除旧技术选型和旧生产架构，避免它们继续作为默认实现；
- 新路线以 DeskBox 的成熟 Windows 桌面窗口、文件收纳与 Shell 交互经验为基线；
- DesktopUp 独有的透明空白穿透、海报排序、主题平台和 AI 能力仍独立设计。

## 当前阶段

V3-01 已关闭。V3-02 R2 的完整人工 P0 门已由用户确认全部通过并正式关闭。V3-03 首轮候选的 Item、标题拖动、锁定、收起、缩放和层级通过，但外围亮框、跨进程空白穿透及缩小后滚动失败。R1 已定位 WinUI `DesktopChildSiteBridge`，以 post-composition layered+transparent 视觉 HWND 配合稀疏原生 InputIsland 修复空白路由；USING 取消外框，六项区域接入标准 `ScrollViewer`，拖缩按最新采样逐帧合并。Release 构建 0 warning / 0 error、合同测试 52/52、原生命中探针、真实桌面 smoke 与退出零残留通过，当前等待 R1 人工复测。当前执行入口见 [ACTIVE_PATH](00_Catalog/ACTIVE_PATH.md)。
