# C-V2P1-03 渲染器生产化设计件(待闸口 A 解锁实现)

状态:`设计冻结 — 实现严格等待闸口 A 裁决`

日期:2026-08-18

## 1. 目标

把 C-QTP 原型渲染侧车升级为生产渲染器:消除硬编码、命中区域语义化、配置全注入、自检契约化。改动只涉及 `prototype/qt-quick/renderer/` 内文件,并在闸口 A 通过后随宿主迁移到正式目录(`src/renderer/`)。

## 2. 命中区域 schema(hello 帧升级)

现状:7 个裸矩形,无语义。生产声明:

```json
"regions": [
  {"id": "titlebar", "x": 16, "y": 16, "w": 418, "h": 58, "actions": ["drag", "lock", "collapse"]},
  {"id": "card:0",  "x": 16, "y": 168, "w": 131, "h": 96, "actions": ["launch", "select"]},
  ... "card:5"
]
```

- `id` 语义化:核心命中测试不再依赖坐标,而是按 id 分派行为(拖动/锁定/收起/启动/选中);
- `actions` 由渲染端声明能力,核心只启用声明过的行为(能力协商);
- 核心 `hit_any_region` 改为 `hit_region_id(cursor) -> Option<id>`。

## 3. 配置注入协议

`hello` 之后核心下发 `hello_ack`:

```json
{"cmd": "hello_ack", "id": 0,
 "geometry": {"x": 1700, "y": 200, "w": 450, "h": 703},
 "params": {"accent": "#72e4ff", "opacity": 0.82},
 "theme": "night_ops"}
```

- 渲染端无自主默认值:未收到 ack 前不 show,收到后应用几何+参数再显示;
- 用户拖动产生的 `geometry_changed` 事件由核心修订后回发 ack(状态权威);
- 移除 `main.cpp` 中 `setGeometry(1700,200,450,703)` 硬编码与 QML 硬编码预设(改属性绑定)。

## 4. 参数 schema(L0/L1)

```json
{"theme": "night_ops",              // L0 预设(三模板之一,先 night_ops)
 "params": {                        // L1 参数
   "accent": "#72e4ff",
   "opacity": 0.82,
   "corner_radius": 18}}
```

- `set_params` 命令携带完整结构;渲染端做类型/范围校验(颜色格式、0.2≤opacity≤1),非法值拒绝并回复错误;
- 热切换目标 <200ms(已有 BlockingQueuedConnection 路径 0-1ms 实测)。

## 5. 自检契约(引用 IPC_PROTOCOL_V1 §6)

- capture 前 `isVisible` 前置;强制产帧(requestUpdate)后延迟 30ms 干净上下文抓取;
- 品红标记 #ff00ff 全帧扫描计数(期望 256);
- 健康回复携带 `frames` / `exposed` / `visible` / `geometry`。

## 6. 文件改动清单(解锁后按此实施)

| 文件 | 改动 |
| --- | --- |
| `renderer/src/main.cpp` | hello 帧 region 语义化;hello_ack 处理(应用几何/参数后再 show);移除硬编码几何;set_params 校验;geometry_changed 事件上报(拖动结束) |
| `renderer/qml/Main.qml` | 预设值改属性绑定;区域 id 与 actions 对应;标题栏拖动结束发事件 |
| `core/src/state.rs` | 命中区域模型改为 id+actions;hit 分派逻辑 |
| 目录 | 闸口 A 后 `prototype/qt-quick/renderer` → `src/renderer`(CMake 同步),原型目录保留为证据 |

## 7. 完成门(本任务)

- hello/hello_ack 往返:渲染端未收 ack 不显示;
- 命中按 id 分派(而非坐标);
- set_params 非法值拒绝;
- 自检契约三测回归(正向 256 / 隐藏 FAIL / 热切换 <200ms)。

## 修订记录

| 日期 | 版本 | 内容 |
| --- | --- | --- |
| 2026-08-18 | V1 | 命中 schema、配置注入、参数 schema、改动清单、完成门 |
