# TS-H01-B-20260820-02 — GDI child visual failure

状态：`FAIL — HWND STRUCTURE EXISTS BUT SURFACE IS NOT COMPOSITED`

日期：2026-08-20

## 触发

用户运行 15 秒人工视觉门后报告：主屏中央没有显示青色诊断卡。

这否决了上一份 Evidence `TS-H01-B-20260820-01` 从窗口树结构推导实际可见性的隐含假设。上一份证据仍只保留“自动结构子门 PASS”，不能升级为 TS-H01-B 视觉通过。

## 复现结果

同一 Release 探针再次运行 15 秒。在窗口存活期间记录到：

- `Progman = 0x3221344`；
- 自有 child HWND 存在，`IsWindow = true`；
- `IsWindowVisible = true`；
- 窗口矩形为 `1100,586–1460,806`，client 为 `360x220`；
- `GetUpdateRect = false`，没有待处理的无效区域；
- `PrintWindow(PW_CLIENTONLY)` 返回成功，并完整取得青色卡片；
- 同一时刻主屏中央截图在该矩形内只有壁纸，没有卡片。

`host-printwindow.png` 证明 WndProc/GDI 已经绘制；`screen-center.jpg` 证明这些像素没有进入最终桌面画面。这不是坐标错位、窗口未创建或 WM_PAINT 未执行。

## 关键宿主事实

TS-H01-A 在同一 Explorer 世代记录的 `Progman` 扩展样式为 `0x200080`，其中包含 `WS_EX_NOREDIRECTIONBITMAP (0x00200000)`。微软对该样式的定义是该窗口不渲染到 redirection surface；当前普通 GDI child 虽能拥有有效 HWND 和离屏内容，但没有被当前 Explorer/DWM 桌面路径合成。

官方资料：

- [Extended Window Styles — WS_EX_NOREDIRECTIONBITMAP](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles)
- [Window Features — Layered Windows](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features)
- [SetLayeredWindowAttributes](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setlayeredwindowattributes)

## 判定

- `raised sibling HWND/z-order structure`：仍为自动结构 PASS；
- `raised + ordinary non-layered GDI child visual surface`：FAIL；
- `TS-H01-B complete`：FAIL/PENDING NEW CONTROLLED VARIANT；
- `TS-H01-C`、Win+D 和框架候选：未开始；
- traditional WorkerW：`UNKNOWN_NOT_PRESENT`。

下一步只改变一个变量：在相同 parent、geometry、z-order 和 GDI fixture 下，改用 Windows 8-aware manifest + `WS_EX_LAYERED` child + `SetLayeredWindowAttributes`，验证专用 redirection surface 是否进入最终桌面合成。若仍不可见，停止该分支并转入显式 DirectComposition surface 探针；不得修改 Explorer 的 `WS_EX_NOREDIRECTIONBITMAP`。

## 文件完整性

- `host-printwindow.png`：2450 bytes，SHA-256 `74B7213F2FAAD10AD3BCA7A3C2B8E900AC7A603A4D836463FAB7718A31F0BB64`；
- `screen-center.jpg`：89802 bytes，SHA-256 `C468B28C3FE7C512701DE802502C742B7C6101F861A7BBEA02A1854275B5F43B`；
- `live.jsonl`：210919 bytes，SHA-256 `06F1CE5A9367EB9404B8C54FFCF186407FC536882BA49B0FA58D0E96965C3241`；
- 探针 SHA-256：`6DBCD3209547DEEADEC6EE02E882778E1FE5B158A309D34CC0B49761701D8941`。
