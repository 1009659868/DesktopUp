# C-DRP-05 暂停、可见性与生命周期接口第一实现切片

日期：2026-08-10

状态：`COMPLETE — USER ACCEPTED`

## 1. 本轮目标

把前台覆盖、锁屏、显示器电源、用户暂停、海报显隐、宿主就绪和退出归约为一个确定性运行状态；所有消费者接收同一版本化快照，RuntimeState 锁内只做纯状态修改，不同步调用托盘、窗口、拓扑或诊断接口。

## 2. 审计结论

实施前存在以下分裂：

- 自动暂停与手动暂停虽然进入同一 RuntimeSnapshot，但 visibility、ready、FPS 和退出没有统一转换入口；
- 海报显隐先操作原生窗口再写状态，且只同步托盘，不向两个 WebView 发布同一快照；
- 托盘退出直接调用 app.exit，消费者无法观察 exiting/stopped；
- 控制中心与海报页各自用 document.visibilitychange 和额外变量重建第二套暂停状态；
- 控制中心无条件每 2 秒轮询性能快照；
- 部分诊断与 HostTopology/DisplayTopology 查询发生在 RuntimeState 锁内。

## 3. 已实施

- 新增 `runtime-lifecycle-v1`：`generation`、`eventCount`、`phase`、`mode`、手动/自动/有效暂停、海报可见性、宿主 ready、原因和 `lastTrigger`；
- phase 为 starting/running/exiting，mode 为 activeStatic/activeAnimated/paused/stopped；隐藏、未 ready 和退出优先进入 stopped，手动/自动暂停优先于动态性能请求；
- 重复事件增加 eventCount 但不虚增 generation；退出状态为粘性状态，后到的可见性事件不能恢复 running；
- RuntimeState 修改统一进入 `mutate_runtime_state`，释放锁后再发布原生诊断、托盘状态与 `runtime-state-changed`；旧 `runtime-pause-state` 仅作为同载荷兼容事件保留；
- 海报显隐改为先提交状态、再执行原生 show/hide；宿主 started/ready、FPS、托盘暂停、控制中心同步和退出均使用同一归约器；
- 托盘退出在 flush/app.exit 前发布 exiting/stopped，Tauri `RunEvent::ExitRequested` 也进入同一退出入口；
- 两个前端消费者只监听 `runtime-state-changed`，以 generation + eventCount 拒绝旧快照；document.hidden 只保留页面本地安全停绘，不再回写原生生命周期；
- 海报页只有 activeAnimated 才持续 requestAnimationFrame，paused/stopped 立即 cancel；控制中心持续 2 秒性能轮询已删除，改为启动一次和生命周期事件驱动更新；
- HostTopology、DisplayTopology 与性能 OS 查询移出 RuntimeState 锁；托盘文案直接反映 starting/running/hidden/paused/exiting。

## 4. 自动验证

- `cargo test --manifest-path src-tauri/Cargo.toml`：46/46 通过；
- `pnpm run check`：57/57 通过，文档链接 96 份通过；
- `pnpm run build` 通过；
- `pnpm run tauri:build` 通过；
- Release：`src-tauri/target/release/desktop-up.exe`；
- NSIS：`src-tauri/target/release/bundle/nsis/Desktop-Up_0.0.1_x64-setup.exe`；
- 最新 Release 已启动，PID 53044。

测试覆盖状态组合、重复事件、generation/eventCount 乱序、托盘临时前台、暂停安全优先、显隐/ready 恢复、退出粘性、状态优先原生显隐、锁外发布和静态/暂停无持续渲染/轮询。

## 5. Release 人工矩阵

1. 托盘连续暂停/恢复三轮：托盘文案与海报状态一致，锁定和收起按钮仍可用，无一次延迟或反向；
2. 托盘隐藏/显示海报三轮：隐藏后无残留窗口，显示后回到原位置，交互和桌面穿透不回退；
3. 最大化或全屏应用覆盖期间打开/关闭托盘菜单：自动暂停不得被托盘临时前台清除，返回桌面后恢复；
4. Win+L 后解锁，以及显示器关闭/唤醒：海报在安全状态停止，返回后只恢复一次，不闪烁、不重复；
5. 托盘退出后确认进程、托盘和海报都消失，再启动 Release：首次海报正常显示且无顶部白条。

本切片未执行第二显示器/混合 DPI 物理矩阵；该范围仍延期至 C-DRP-06。

## 6. 2026-08-11 人工矩阵验收

用户确认第 2–5 项通过：海报反复显隐、覆盖期间托盘临时前台、锁屏/显示电源恢复，以及退出重启均未发现回退。

原第 1 项“暂停动态效果”描述不准确：当前生产海报没有实际动画，因此不能以肉眼观察动画停止/恢复作为验收证据。该项改为只验证可观察的控制状态：点击后重新打开托盘，菜单项应勾选且状态显示“运行状态：手动暂停”；再次点击并重开托盘后，勾选应取消且状态恢复“运行状态：静态按需绘制”。海报不应隐藏或闪烁，锁定/收起按钮仍可使用。

用户随后确认第 1 项的四个可观察条件全部满足：托盘暂停后勾选和“运行状态：手动暂停”一致，恢复后勾选取消并回到“运行状态：静态按需绘制”，切换期间海报无闪烁或消失，锁定和收起按钮持续可用。至此 Release 人工矩阵 5/5 通过，C-DRP-05 标记 `COMPLETE — USER ACCEPTED`。

实际动态内容的停帧效果应在后续引入动画消费者后另行验证，不能由当前静态海报虚构通过。第二显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启和热插拔物理矩阵仍延期至 C-DRP-06。
