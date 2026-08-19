# C-QTP-05 证据矩阵与结论(闸口 A 交付)

日期:2026-08-18
提交:`57c7ac6`(代码)及后续证据文档提交
状态:`第一轮实机验证完成(1 项部分通过,2 项修复后待重测) → 闸口 A 复测中`

## 执行环境

- 核心桩:Rust 1.97.1 release(`desktopup-core-stub.exe`)
- 渲染侧车:Qt 6.8.3 msvc2022_64 release(`poster-renderer.exe`),CMake+Ninja
- 证据源:`prototype/qt-quick/core.log`、`capture.png`、进程采样(Get-Process)
- 沙箱边界:SendInput 输入注入被 UIPI 阻断(error 5),程序化鼠标探测改用直接样式位施加验证;真实输入事件不受影响

## 8 项矩阵逐项记录

### 1. 冷启动/拖动 20 轮/开关 10 轮/收起展开 10 轮:零条带灰框裁切 — `PARTIAL`

- ✅ 自动化:set_visible 显隐切换 10 轮,0 崩溃,10/10 回复 `ok:true`(core.log `verify: set_visible` 10 行);
- ✅ 冷启动:每次 hello → surface registered 完整;
- ⏳ **待用户**:拖动 20 轮、视觉零条带/灰框/裁切确认(实机目视)。

### 2. 空白穿透/标题栏与 Item 命中精确 — `PARTIAL(机制已证)`

- ✅ 穿透机制证明:核心桩对渲染端真实 HWND 施加/清除 `WS_EX_TRANSPARENT`,读回 exstyle 位验证 `direct-apply on -> bit=true` / `off -> bit=false`(core.log);
- ✅ 命中区域:7 区域(标题栏 + 3×2 卡片)注册与命中测试逻辑就位;WH_MOUSE_LL 已安装(`mouse hook installed`);
- ⏳ **待用户**:真实鼠标扫过空白区穿透到桌面图标、标题栏 5px 阈值拖动。

### 3. 不覆盖普通程序/文件选择器;Win+D/Explorer 重启恢复 — `待用户`

- ⏳ 窗口属性 `Qt::Tool|WindowDoesNotAcceptFocus` 已设,但该项需要真实桌面交互验证(文件选择器、Win+D、Explorer 重启)。

### 4. 静态 0 FPS;10s CPU ≤0.10s;工作集实测记录 — `PASS`

- ✅ 0-FPS:60s 内 frames 1→8,每次增长恰对应 capture 强制产帧,自检之外零换帧(帧计数来自 frameSwapped 信号);
- ✅ CPU:60s 采样(5s 粒度,两进程合计)0–0.9%,10s 合计远低于 0.10s 预算;
- ✅ 工作集:核心桩 11 MB;渲染侧车 71–72 MB(超出 50 MiB 预算,按矩阵规则交用户裁决新预算——建议以 80 MiB 为 Qt 路线预算,正式宿主再做 DLL 裁剪)。

### 5. 呈现自检可检测劣化 — `PASS`

- ✅ 正向:8/8 次 `PRESENTATION CHECK PASS: magenta=256`(16×16 品红标记整块,自抓帧内证据);
- ✅ 负向:隐藏窗口 → `magenta=0` → `PRESENTATION CHECK FAIL`(未呈现如实报失败,无陈旧缓冲误报);
- ✅ 自检即恢复:capture 先 requestUpdate 强制产帧再抓取,0-FPS 与强制重绘耦合(V1 教训落实)。

### 6. windeployqt 载荷清单与净机路径 — `PARTIAL`

- ✅ 载荷清单:21 文件 54.9 MB(大头:opengl32sw 19.7MB、Qt6Gui 8.9MB、Qt6Quick 6.0MB、Qt6Core 5.9MB、Qt6Qml 5.0MB、D3Dcompiler 4.0MB);
- 📋 裁剪建议:opengl32sw.dll(软件 GL 后备)目标机可去;正式宿主按依赖分析进一步裁剪;
- ⏳ 净机可运行路径:留闸口 B 前验证(与正式宿主打包同做)。

### 7. 主题 L0/L1 骨架 + 热切换 <200ms — `PASS`

- ✅ set_params(accent `#ff8800` / opacity 0.7)热切换往返 **0–1 ms**(BlockingQueuedConnection 同步应用后回复),远低于 200ms。

### 8. 渲染侧车崩溃 → 有限重建,核心桩不受影响 — `PASS`

- ✅ 真实崩溃 2 次(capture 路径 bug 触发,0xC0000374/0xC0000005)→ `renderer exited` → `restarting renderer 1/3、2/3` → 新 hello 重注册,核心存活;
- ✅ 主动 kill 1 次 → `restarting renderer 1/3` → 重建成功;
- ✅ bug 已修复(QTimer 干净上下文抓取替代 frameSwapped 内 grabWindow),后续 10 轮显隐搅动 0 崩溃。

## 结论

- 自动化可证项(4/5/7/8)全部 PASS;1/2 机制已证、待实机交互;3/6 部分待实机/后续阶段。
- **候选状态:未淘汰。** 请求用户执行闸口 A 实机验证(见下),全部通过后产出选定决策文档,进入 V2-P1。

## 闸口 A 第一轮实机结果(用户反馈 2026-08-18 晚)

| 项 | 结果 | 证据与根因 |
| --- | --- | --- |
| 1 拖动/开关 | ❌→修复待重测 | 拖动仅成功一次;"成功一次"后窗口移离原位,而核心表面矩形未随几何同步 → 光标恒判"在海报外" → 恒穿透,拖动失败 |
| 2 穿透/命中 | ⚠️ 空白穿透 ✓,标题栏/卡片命中 ✗ | 同上根因;位切换生效(passthrough→true 101+ 次),但从未回到 false |
| 3 Win+D/Explorer | ⚠️ Win+D ✓(海报随显隐),Explorer 重启待测 | 用户确认 Win+D 正常 |

### 修复(提交 `bf566d6`)

1. **几何同步**:健康回复 geometry → SURFACE 矩形更新(拖动后坐标跟随),并逐次重查 DPI;
2. **DPI 感知换算**:GetDpiForWindow 物理/逻辑换算(本机实测 96 无缩放;防多显示器缩放失配);
3. **命中边界诊断**:`hover.inside_changed` 事件(带 pt/surface/scale),复测证据可精确核对。

## 结论

- 自动化可证项(4/5/7/8)全部 PASS;3 项实机第一轮暴露几何失配缺陷,已修复,**待用户复测**;
- **候选状态:未淘汰。** 复测通过后产出选定决策文档,进入 V2-P1。

## 闸口 A 复测实锤(2026-08-19 11:0x,区域级诊断)

- **双向穿透切换证据**:`passthrough.toggled` True(#1,3,5)与 False(#2,4,6,10,20)交替——标题栏/卡片命中关闭穿透、空白/海报外开启穿透;
- **区域命中证据**:`hover.region_hit` region=0(标题栏)/6(卡片)/-1(空白)随光标位置正确切换,含窗口被拖动到新位置后的命中(几何同步生效);
- **拖动与坐标跟随**:用户拖动后 region 命中仍精确(窗口新位置)。
- 日志节流 bug 修复:n%20==1 数学上只记录奇数序号的切换方向,false 方向被漏记——已改为前 6 次全记 + 每 10 次一记。

### 矩阵 1/2 项最终结论

| 项 | 结论 |
| --- | --- |
| 1 拖动/开关 | 机制 PASS(拖动后几何同步、显隐 10/10);**"甩 20 次零白条"目视项待用户最终确认** |
| 2 穿透/命中 | **PASS(双向切换 + 区域命中证据齐全)** |
| 3 Win+D/Explorer | Win+D PASS(用户确认);**Explorer 重启待用户确认** |

## 闸口 A 复测清单(约 2 分钟)

1. **穿透**:鼠标扫过海报空白区 → 应能点到海报下方的桌面图标;扫过标题栏/卡片 → 点不到(core.log 出现 `hover.inside_changed` 与 `passthrough -> false` 行);
2. **拖动**:按住标题栏移动 ≥5px → 海报跟随;拖动**结束后**松开,再扫标题栏确认仍可命中;快甩 20 次,**观察无顶部白色条带/灰框/裁切**;
3. **不打扰**:重启 Explorer(任务管理器→Windows 资源管理器→重启)→ 海报仍在;Win+D 再确认一次。
