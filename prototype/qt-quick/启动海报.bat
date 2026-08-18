@echo off
rem DesktopUp V2 C-QTP 原型 — 用户实机启动脚本
rem 作用:从普通用户上下文启动核心+渲染器(完整输入权限,钩子/穿透验证用)
rem 注意:V2-P1 才实现单实例互斥体,本脚本先杀旧实例再启动,避免双海报叠放。
cd /d "%~dp0"
taskkill /f /im desktopup-core-stub.exe >nul 2>&1
taskkill /f /im poster-renderer.exe >nul 2>&1
timeout /t 1 /nobreak >nul
start "" "%~dp0core\target\release\desktopup-core-stub.exe" "%~dp0renderer\build\poster-renderer.exe"
echo DesktopUp poster started (core.log / renderer.log 记录运行证据)
