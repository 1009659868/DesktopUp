# C-QTP 当前交接

最后更新：2026-08-18

状态：`ACTIVE — C-QTP-03 机制完成,等待交互验证`

## 当前

C-QTP-01(两进程骨架 + stdio IPC)与 C-QTP-02(透明表面与参考构图,帧级验证)已完成,证据见 `20_Log/2026-08/2026-08-18_cqtp01_02_skeleton_stdio_ipc.md`。命名管道方案因 PIPE_NOWAIT/断管死锁被弃用,当前传输为父子进程 stdio(1:1 拓扑)。

C-QTP-03 机制代码已落地并提交(`db360a8`):渲染端 hello 上报 HWND/矩形/7 个命中区域;核心端安装 WH_MOUSE_LL 钩子,按光标位置切换 WS_EX_TRANSPARENT(不加 LAYERED 位);QML 标题栏 MouseArea 5px 阈值触发 startSystemMove。修复 OnceLock 从未初始化(surface 永远未注册)、QJsonValue 双精度转换两处缺陷。当前 poster 正在桌面运行,供穿透/拖动人工验证。

## 下一动作

1. 人工验证穿透切换(core.log 每 20 次切换节流输出 `passthrough -> true/false`)与标题栏拖动;
2. C-QTP-04:呈现自检(capture 帧标记比对)与 0 FPS 静态验证、CPU/工作集采样;
3. C-QTP-05 矩阵结论 → 用户闸口 A 裁决。

## 边界

不写产品功能代码;不迁移 V1 代码;原型不进入发布路径。
