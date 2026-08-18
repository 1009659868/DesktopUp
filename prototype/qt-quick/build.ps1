# C-QTP 构建脚本:vcvars 环境 + CMake/Ninja 编译渲染侧车 + cargo 编译核心桩
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

$vcvars = "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat"
$qtRoot = "F:\Qt\6.8.3\msvc2022_64"
if (-not (Test-Path "$qtRoot\lib\cmake\Qt6\Qt6Config.cmake")) {
    throw "Qt6 not found at $qtRoot — install via aqt or adjust path"
}

$cmake = (Get-Command cmake -ErrorAction SilentlyContinue).Source
if (-not $cmake) { $cmake = "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" }
$ninja = (Get-Command ninja -ErrorAction SilentlyContinue).Source
if (-not $ninja) { $ninja = "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\Ninja\ninja.exe" }

# 1) renderer (needs vcvars for cl)
$rendererDir = Join-Path $root "renderer"
$buildDir = Join-Path $rendererDir "build"
New-Item -ItemType Directory -Force -Path $buildDir | Out-Null

$cmd = "call `"$vcvars`" >nul && `"$cmake`" -S `"$rendererDir`" -B `"$buildDir`" -G Ninja -DCMAKE_MAKE_PROGRAM=`"$ninja`" -DCMAKE_PREFIX_PATH=`"$qtRoot`" -DCMAKE_BUILD_TYPE=Release && `"$cmake`" --build `"$buildDir`""
cmd /c $cmd
if ($LASTEXITCODE -ne 0) { throw "renderer build failed" }

# 2) core stub
Set-Location (Join-Path $root "core")
cargo build --release
if ($LASTEXITCODE -ne 0) { throw "core build failed" }

Write-Host "build ok:"
Write-Host "  renderer: $buildDir\poster-renderer.exe"
Write-Host "  core:     $(Join-Path $root 'core\target\release\desktopup-core-stub.exe')"
