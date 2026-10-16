# 开发验收源

本目录的App/内置场景partial仅在显式 `DesktopUpReviewTools=true` 时链接到DesktopUp.App；普通构建不编入，不是运行时插件。

```powershell
# 产品候选：不包含独立App验收执行器
./scripts/build.ps1 -Offline -OutputDirectory out/my-product
# 开发探针：使用独立输出和独立数据
./scripts/build.ps1 -Offline -ReviewTools -OutputDirectory out/my-review
# 实际Compile集合和发布/验收互斥检查
./scripts/check-architecture.ps1 -Offline
```

现有smoke/probe脚本仍在scripts/，保持原相对路径；需要探针参数的脚本应传入ReviewTools构建目录，不可把普通候选当成探针候选。打包入口始终关闭ReviewTools，并拒绝Preview+ReviewTools组合。普通运行/有限时退出诊断、文件来源检查和Shell只读准备不等于验收执行器，继续保留。

未按名称清空全部Probe字段：现有无副作用观测槽/诊断仍在宿主；它们的调用者由此目录提供。移动不改变private访问，也不意味着已完成窗口职责解耦。后续按会话/呈现抽取，禁止为测试而将私有业务能力扩大成公开API。

模块删除后其对应验收源不再编译；Lamp/Rain仍以Modules下的生产声明判断是否存在。新验收入口放此处，不再添加到产品源码根目录。

首批精确移动清单和保留例外见[实施计划](../../Docs/A_Project/40_Implementation/Architecture/A_FOUNDATION.md)。
