Unicode true
!include "FileFunc.nsh"
Name "DesktopUp Preview"
OutFile "${LAUNCHER_OUTPUT}"
RequestExecutionLevel user
SilentInstall silent
AutoCloseWindow true
ShowInstDetails nevershow
VIProductVersion "${FILE_VERSION}"
VIAddVersionKey "ProductName" "DesktopUp Preview"
VIAddVersionKey "FileDescription" "DesktopUp Preview launcher"
VIAddVersionKey "FileVersion" "${RELEASE_ID}"
VIAddVersionKey "LegalCopyright" "DesktopUp project"

; Keep WinUI's executable, native manifests and PRI resources together in app/.
; Forward the original command line without shell interpretation, then exit.
; The app itself owns single-instance forwarding and the startup visibility policy.
Section
  ${GetParameters} $0
  SetOutPath "$EXEDIR\app"
  ClearErrors
  Exec '"$EXEDIR\app\DesktopUp.exe" $0'
  IfErrors launch_failed
  SetErrorLevel 0
  Quit
  launch_failed:
    MessageBox MB_OK|MB_ICONSTOP "无法启动 DesktopUp Preview。请确认 app 目录完整；若文件缺失，请重新安装。"
    SetErrorLevel 1
SectionEnd
