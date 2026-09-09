# 启动动画配置

`poster-animation.json`只控制海报收起/展开及同批推让/归位的呈现，不决定碰撞目标、归位资格、首选尺寸或主题；不进入SQLite committed revision。本阶段没有控制中心编辑、文件监听或热更新，修改后必须正常退出并重新启动。

## 参数

| 字段 | 默认 | 范围与语义 |
| --- | --- | --- |
| schemaVersion | 1 | 当前仅支持1 |
| posture.maximumStepPixels | 12 | 1～64，单次提交中任一参与者任一外框边缘的最大变化，单位为屏幕物理像素，不是DIP。是上限，不保证每帧恰好移动此数值 |
| posture.frameIntervalMs | 16 | 4～100，连续两次动画提交的最小间隔。16约对应最高62.5次/秒的请求节奏，不等于保证屏幕达到此帧率 |
| posture.durationMs | 240 | 80～2000，目标时长，不是硬截止；步长/帧间隔/实际渲染更慢时允许延长，不丢帧追赶或突然跳大步 |
| posture.easing | ease-in-out | `ease-in-out`或`linear`；分配轨迹各点的目标时间，仍受最小间隔与实际渲染节奏限制 |

例如原来的300像素高度变化有10个安全关键帧，每段30像素。默认12上限将每段分为3段10像素，即30次提交；当前默认参数计算名义时长约492ms，可能因实际显示/负载进一步延长。改6上限则变为50次提交。求解器关键帧和拐点不会因调大上限被随意合并。

希望更细腻可先调小`maximumStepPixels`；希望更快可适当增大它或减小`frameIntervalMs`。只把`durationMs`改小不会突破像素上限。过小步长/间隔会增加布局与提交开销，不把这些参数当性能认证。

## 启动方式与优先级

本轮人工入口（兼容Windows PowerShell5.1）：

```powershell
& 'G:\A_\Tools\DesktopUp_V2\scripts\start-m07-animation.ps1'
```

默认读取本目录的`poster-animation.json`。也可指定另一份配置，支持带空格路径：

```powershell
& 'G:\A_\Tools\DesktopUp_V2\scripts\start-m07-animation.ps1' -AnimationConfig 'D:\My Config\poster-animation.json'
```

已有DesktopUp时先从托盘正常退出。入口每次创建新隔离C/B/A配置，不重建日常数据库；`-CheckOnly`使用生产同一解析器只读校验，`-Smoke`执行8条原生命令后正常退出。二进制校验不锁定用户可编辑的配置文件。

普通程序的读取优先级：

1. 命令行`--animation-config="绝对路径"`；
2. 当前用户数据目录的`poster-animation.json`，正常数据根为`%LOCALAPPDATA%\DesktopUp\V3`；
3. 程序目录`config\poster-animation.json`；
4. 没有默认文件时使用内置默认值。

显式指定的文件不存在、无效JSON、重复/未知字段、错误版本、超范围或超过16KiB时明确拒绝，不静默忽略错误。日志`configuration.posture_animation`记录本次实际路径及冻结值。配置读取不写入/覆盖原文件；跨机习惯配置导入导出仍是独立后续功能。
