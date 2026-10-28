# J1创作工作台与样例设计重审

## 2026-10-28 状态更新

用户已认可后续 `desktopup-canvas-studio.html` 交互稿，并明确沿用其中的“作品内选中 → 组合 → 存入组合库 → 跨作品插入”流程，不增加单独组合创作入口。当前授权原生实现，详见[实施清单](CANVAS_J1_STUDIO_IMPLEMENTATION.md)。本文件以下 2026-10-27 的待确认／设计重审文字为历史记录，不能覆盖最新状态；原失败候选仍不是已验产品。两份样例素材可随本次隔离候选用于验证。

2026-10-27。原J1候选人工验收未通过，原因是预设配置器、写死组件和样例视觉效果没有证明自由创作。用户已授权修订方向与本轮设计工作，**尚未确认本视觉稿，也未授权恢复产品实现**。本轮不提交、推送或合并，保留J0及目录整理已验结论。

## 当前交付与确认点

交付一份可交互视觉稿，包含创作工作台、基础收纳作品预览、雨夜书桌作品预览。工作台有空白场景、图片导入、节点选择／移动、属性、层级、复制／删除、场景实例及本页撤销演示。它只操作浏览器内存，不调用DesktopUp、不保存配置、不操作真实文件或连接AI。

本轮只需确认两点：①工作台是否确实支持所需创作方式，创作／控制中心调参的边界是否合理；②两份作品的视觉方向及内容可读性是否符合预期。确认后再实施，运行验收另按新退出门集中进行。

可编辑设计源：[工作台](../../20_Architecture/CanvasContracts/J1Visuals/creation-workbench.template.html)、[组装脚本](../../20_Architecture/CanvasContracts/J1Visuals/build-preview.mjs)、[雨窗素材](../../20_Architecture/CanvasContracts/J1Visuals/rain-window.jpg)。内联交付位于本聊天可视化目录的`desktopup-creation-review.html`；本地检查与截图在`out/canvas-j1-design-review-20261027/`。这些都是设计资料，不纳入产品编译或精简发布源码。

## 创作工作台

- **场景树**：创建、选择、复制、重排、组合与复用节点，画布和树联动；作品名称不是内核类型。首批图像／文字／形状／布局／内容承载是通用基础能力，同类型可出现多次。
- **画布**：直接移动／缩放，配合DIP数值精调；背景、标题、装饰和内容空间独立。自由定位、锚定和流式布局有明确模式，父布局生效时不同时承诺自由坐标。
- **素材库**：用户显式导入PNG／JPEG，资源作为受控副本进入作品包，不是将本地路径写进模板，也不自动发给AI。
- **场景库**：选中装饰组合另存为子场景，固定版本插入不同作品；位置和开放参数独立覆盖。编辑源资源产生新版本，不静默改变所有实例。
- **属性检查器**：按类型／能力描述生成，不为某个样例列特制开关；作者主动选择开放哪些参数给控制中心。复制、重挂父级和变换支持草稿撤销。
- **保存与应用**：保存草稿／模板不等于应用；预览不改真实文件，应用经过同一安全用例，撤销只改变设计。视觉稿的预览与撤销仅演示内存行为，磁盘保存和原生发布仍待实现。

创作工作台不是游戏引擎全套编辑器；本轮不设计通用脚本、3D场景、素材市场、通用Agent或在线更新。内置AI以后在独立面板使用相同草稿命令，不能绕过资源、内容和提交边界。

## 基础收纳视觉方向

浅色温灰底面、低对比细线、清楚的文件图标和留白。保留必要宿主操作；不增加副标题、大标题、数量介绍或空内容提示。它的节点仍可拆开编辑，并不是一个不可修改的基础收纳组件。

这一样例验证功能优先、低干扰的最小构图。示例文件与雨夜稿相同，不能通过藏文件或改文件归属换取视觉效果。默认新增海报、原有布局／尺寸／热度和原生拖放行为不因本轮设计而改变。

## 雨夜书桌视觉方向

冷色雨窗和木桌作为可替换图像，独立文字作为画面标题，暖色便签作为可复用装饰场景，深色内容阅读底衬保证图标与名称可读。视觉稿中的雨滴属于背景图像，不是假称已实现的雨效；时间轴／动态雨效不在本稿演示范围。

便签由纸张、图钉／胶带形状和文字组成，不是内置“便签开关”。可以把整个组合插入基础收纳或空白作品，独立改变位置与开放文字。图像、文字、便签、内容区不是一张合成截图，导入替换背景后仍可编辑其余元素。

不再强制使用原J0独立椭圆灯盘。当前稿保留矩形原生承载，素材圆角和透明视觉并不意味着点击可以穿透桌面；任意旋转的真实内容区、多区域和原生空洞按J4独立验证。

## 协议与退出标准修订

[协议0.2设计稿](../../20_Architecture/CanvasContracts/CONTRACT.md)补充场景资源／实例、通用节点变换与布局、资源导入与依赖、开放参数、草稿结构命令。原0.1 JSON及其核对脚本作为历史资料保留；本轮不修改未通过候选的生产协议／数据库字节。

[联合实施计划](../../30_Plans/POSTER_CANVAS_ENGINE_DESIGN_PLAN.md)新增设计确认门，J1不再把自由结构编辑、素材和复用延后。实现退出必须从空白完成两例，再做第三种结构，均不新增样例C#分支／不重新编译；图片离线可读、子场景实例独立、取消／撤销／失败保护有效。

有效底层成果继续评估复用：提交新鲜检查与CAS、预览取消和恢复、稳定节点更新、Item和图标复用、按需共享调度。被替代的样例分支与专用布局限制在替代实现落地时清理，不为本轮文档任务提前删生产代码。

## 视觉稿的演示边界

| 本页可试 | 尚不是产品能力 |
| --- | --- |
| 选择三个设计视图；切换空白／基础／雨夜场景 | 产品模板库和导入器 |
| 选择节点、拖动位置、修改数值／文字／不透明度 | 原生窗口拖放、DPI／复杂命中保证 |
| 浏览器选取本机PNG／JPEG作本页预览 | 产品资源库复制、持久化、完整解码隔离 |
| 插入便签实例、保存选中装饰为本页场景、换作品插入 | 磁盘场景版本、跨会话依赖、子树高级编辑 |
| 复制／删除、上移一层、本页撤销 | 多级持久撤销、发布事务、重启恢复 |

图示文件均为虚构名称。原型不产生桌面文件、安装登记或AI请求；本页关闭不保存编辑。PNG／JPEG选择只在本页读取和解码，8MB／2400万像素限制是原型限制，不作为生产安全认证。

## 素材来源与重建

雨窗素材由内置image_gen生成，原始PNG留在本节目录，预览JPEG为相同构图的编码压缩副本，无第三方素材下载。此素材仅供本轮视觉评审，确认后再决定是否随样例交付；不自动将生成素材加入正式包。

生成提示：`Use case: stylized-concept. Asset type: background art for a DesktopUp rainy night desk visual design proposal, not a complete UI screenshot. 4:5 portrait; empty dark walnut desk in front of a rain-streaked apartment window at night; muted deep teal city silhouettes and a few soft amber lights; cinematic painterly realism, subtle texture and restrained contrast. Quiet empty desk for separate UI file icons. Warm light from upper right, no lamp or objects. No people, text, letters, logos, interface, buttons, frames, watermarks, notebooks or icons. The image is one replaceable asset; lamp, captions and content must remain separate layers.`

使用`node build-preview.mjs <明确的输出.html路径>`将JPEG内联到视觉稿。本机使用visualize技能的render.py包装作检查，Node／Playwright仅用于设计稿验证，不安装到项目、不增加产品依赖。检查脚本与结果存本轮out目录。

## 本轮核对与保留项

本轮仅文档链接／差异与视觉稿渲染／交互检查，不构建、不运行DesktopUp、不安装或打包，不以页面截图代替原生行为验收。

- 13份文档严格UTF-8解码／无NUL，1223个本地文件链接存在；不声称校验所有历史锚点。`git diff --check -- Docs`通过，分支仍impl/07-canvas-ai-contracts。
- 无头浏览器检查：两样例同为6项示例内容，基础收纳无介绍标题；复制／撤销／文字变更、指针移动、跨作品插入本页子场景、空白添加文字与本机图片预览通过；控制台运行异常0。浅／深色和窄窗口检查通过，无横向溢出。
- 视觉检查调整了便签正文／脚注间距；选中节点不改变真实叠放顺序。预览包装首次拒绝覆盖已有自建输出，确认目标后用force重建；未覆盖用户文件。
- 设计检查的临时浏览器已关闭；只保留设计稿、素材、截图与报告，不创建桌面样例／安装登记，不重启旧J1候选。当前结果为DESIGN REVIEW PENDING，不补记USER PASS。

原失败候选、配置和日志暂留供对照，不自动清理或重新启动。源码目录整理已验结论保留；剩余读取／观察成本、预览提交分配成本、P1/R6锁冲突与历史卸载／升级首因均继续OPEN。

## 设计借鉴来源

- [Godot节点与场景](https://docs.godotengine.org/en/stable/getting_started/step_by_step/nodes_and_scenes.html)：组合后可保存、实例化和嵌入，不等同于引入Godot运行时。
- [Godot资源](https://docs.godotengine.org/en/stable/tutorials/scripting/resources.html)：资源数据与运行节点分开，复用不意味着实例可互相修改共享资源。
- [Wallpaper Engine素材与层级](https://docs.wallpaperengine.io/en/scene/first/assets.html)：直接变换、父子关系和素材组织。
- [Wallpaper Engine用户参数](https://docs.wallpaperengine.io/en/scene/first/properties.html)：先创作，再选择向使用者开放的参数。
