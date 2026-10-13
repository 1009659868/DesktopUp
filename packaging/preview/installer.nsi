Unicode true
!include "MUI2.nsh"
!include "x64.nsh"
!include "WinVer.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"

!ifndef RELEASE_ID
  !error "Build using scripts/package-preview.ps1"
!endif
!define PRODUCT "DesktopUp Preview ${RELEASE_ID}"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp.Preview.${RELEASE_ID}"
Name "${PRODUCT}"
OutFile "${SETUP_OUTPUT}"
RequestExecutionLevel user
InstallDir "$LOCALAPPDATA\Programs\DesktopUp Preview\${RELEASE_ID}"
SetCompressor /SOLID lzma
ShowInstDetails show
ShowUninstDetails show
VIProductVersion "${FILE_VERSION}"
VIAddVersionKey "ProductName" "DesktopUp Preview"
VIAddVersionKey "FileDescription" "DesktopUp Windows 11 x64 independent preview installer"
VIAddVersionKey "FileVersion" "${RELEASE_ID}"
VIAddVersionKey "LegalCopyright" "DesktopUp project"
Var GuardPath
Var GuardError
Var GuardCursor
Var GuardParent
Var GuardAttributes

!define MUI_WELCOMEPAGE_TEXT "Windows 11 x64 独立预览版。$\r$\n$\r$\n自带运行库，使用独立配置，不读取或迁移日常版配置，不设置开机启动。首次为空白工作区，无演示卡片；拖入收纳会移动真实文件，请先用无价值文件体验。$\r$\n$\r$\n下一步可自选安装目录。卸载保留配置和已收纳文件。"
!insertmacro MUI_PAGE_WELCOME
!define MUI_DIRECTORYPAGE_TEXT_TOP "选择当前用户可写的本地目录，可以已有其他文件；已有内容会保留。若存在同名的程序文件或 app、licenses 目录，请改选目录。配置和收纳内容独立于安装目录，收纳位置可在控制中心修改。"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\DesktopUp.exe"
!define MUI_FINISHPAGE_RUN_NOTCHECKED
!define MUI_FINISHPAGE_RUN_TEXT "启动 DesktopUp Preview"
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"

; Check every ancestor, including an existing destination; never follow a junction.
!macro ReparseGuard Prefix
Function ${Prefix}CheckReparsePath
  StrCpy $GuardError 0
  StrCpy $GuardCursor $GuardPath
  loop:
    System::Call 'kernel32::GetFileAttributesW(w "$GuardCursor") i.s'
    Pop $GuardAttributes
    ${If} $GuardAttributes != -1
      IntOp $GuardAttributes $GuardAttributes & 0x400
      ${If} $GuardAttributes != 0
        StrCpy $GuardError 14
        Return
      ${EndIf}
    ${EndIf}
    ${GetParent} "$GuardCursor" $GuardParent
    ${If} $GuardParent == ""
    ${OrIf} $GuardParent == $GuardCursor
      Return
    ${EndIf}
    StrCpy $GuardCursor $GuardParent
    Goto loop
FunctionEnd
!macroend
!insertmacro ReparseGuard ""
!insertmacro ReparseGuard "un."

!macro CheckOwnedName Name
  System::Call 'kernel32::GetFileAttributesW(w "$INSTDIR\${Name}") i.r0'
  ${If} $0 != -1
    Return
  ${EndIf}
!macroend

Function ValidateDestination
  StrCpy $GuardError 12
  StrCpy $0 $INSTDIR 2 1
  ${If} $0 != ":\"
    Return
  ${EndIf}
  ; NSIS GetFullPathName can return empty for a not-yet-created directory.
  ; Win32 canonicalization supports a new destination without creating it first.
  System::Call 'kernel32::GetFullPathNameW(w "$INSTDIR", i ${NSIS_MAX_STRLEN}, w .r2, p 0) i.r0'
  ${If} $0 <= 0
  ${OrIf} $0 >= ${NSIS_MAX_STRLEN}
    Return
  ${EndIf}
  StrCpy $INSTDIR $2
  StrLen $0 $INSTDIR
  ${If} $0 <= 3
  ${OrIf} $0 > ${MAX_INSTALL_PATH}
    Return
  ${EndIf}
  ; Canonical path without a trailing separator, used again by the uninstall marker.
  StrCpy $1 $INSTDIR 1 -1
  ${If} $1 == "\"
    StrCpy $INSTDIR $INSTDIR -1
  ${EndIf}
  StrLen $0 "$LOCALAPPDATA\DesktopUp"
  StrCpy $1 $INSTDIR $0
  ${If} $1 == "$LOCALAPPDATA\DesktopUp"
    Return
  ${EndIf}
  StrLen $0 "$WINDIR"
  StrCpy $1 $INSTDIR $0
  ${If} $1 == "$WINDIR"
    Return
  ${EndIf}
  ${If} $INSTDIR == $PROFILE
  ${OrIf} $INSTDIR == $DESKTOP
  ${OrIf} $INSTDIR == $DOCUMENTS
  ${OrIf} $INSTDIR == $LOCALAPPDATA
  ${OrIf} $INSTDIR == $APPDATA
  ${OrIf} $INSTDIR == $PROGRAMFILES
  ${OrIf} $INSTDIR == $PROGRAMFILES64
  ${OrIf} $INSTDIR == $TEMP
    Return
  ${EndIf}
  StrCpy $GuardPath $INSTDIR
  Call CheckReparsePath
  ${If} $GuardError != 0
    Return
  ${EndIf}
  StrCpy $GuardError 12
  System::Call 'kernel32::GetFileAttributesW(w "$INSTDIR") i.s'
  Pop $0
  ${If} $0 != -1
    IntOp $0 $0 & 0x10
    ${If} $0 == 0
      Return
    ${EndIf}
  ${EndIf}
  ; Reserve only owned names. Unrelated existing files are never overwritten.
  StrCpy $GuardError 16
  !insertmacro CheckOwnedName "DesktopUp.exe"
  !insertmacro CheckOwnedName "Uninstall.exe"
  !insertmacro CheckOwnedName "PREVIEW-README.txt"
  !insertmacro CheckOwnedName "app"
  !insertmacro CheckOwnedName "licenses"
  StrCpy $GuardError 0
FunctionEnd

Function .onInit
  SetShellVarContext current
  SetRegView 64
  ${IfNot} ${IsNativeAMD64}
    MessageBox MB_OK|MB_ICONSTOP "此预览包仅支持 AMD64 / x64 电脑。" /SD IDOK
    SetErrorLevel 10
    Quit
  ${EndIf}
  ${IfNot} ${AtLeastWin11}
    MessageBox MB_OK|MB_ICONSTOP "此预览包要求 Windows 11。" /SD IDOK
    SetErrorLevel 11
    Quit
  ${EndIf}
  ReadRegStr $0 HKCU "${UNINSTALL_KEY}" "InstallLocation"
  ${If} $0 != ""
    MessageBox MB_OK|MB_ICONSTOP "此版本已安装。请先从托盘退出并卸载同版本；配置会保留。" /SD IDOK
    SetErrorLevel 13
    Quit
  ${EndIf}
  ; Keep /D and the directory page selection. Never force a fixed installation path.
FunctionEnd

Function .onVerifyInstDir
  Call ValidateDestination
  ${If} $GuardError != 0
    Abort
  ${EndIf}
FunctionEnd

Section "DesktopUp Preview"
  Call ValidateDestination
  ${If} $GuardError != 0
    MessageBox MB_OK|MB_ICONSTOP "目录不可用。请选择可写的本地目录，且不含同名程序文件或 app、licenses 目录；其他已有文件可以保留。不使用磁盘根、用户配置、Windows目录、联接目录或过长路径。" /SD IDOK
    SetErrorLevel $GuardError
    Quit
  ${EndIf}
  SetShellVarContext current
  SetRegView 64
  ClearErrors
  !include "${INSTALL_FILES}"
  SetOutPath "$INSTDIR"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteINIStr "$INSTDIR\app\preview-install.ini" "Install" "Channel" "DesktopUp.Preview"
  WriteINIStr "$INSTDIR\app\preview-install.ini" "Install" "Release" "${RELEASE_ID}"
  WriteINIStr "$INSTDIR\app\preview-install.ini" "Install" "Path" "$INSTDIR"
  IfErrors installation_failed
  CreateDirectory "$SMPROGRAMS\${PRODUCT}"
  CreateShortCut "$SMPROGRAMS\${PRODUCT}\DesktopUp Preview.lnk" "$INSTDIR\DesktopUp.exe"
  CreateShortCut "$SMPROGRAMS\${PRODUCT}\预览说明.lnk" "$INSTDIR\PREVIEW-README.txt"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "${PRODUCT} (x64)"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${RELEASE_ID}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "${UNINSTALL_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\DesktopUp.exe"
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "EstimatedSize" ${PAYLOAD_KB}
  IfErrors installation_failed
  Goto installation_done
  installation_failed:
    MessageBox MB_OK|MB_ICONSTOP "写入安装文件失败，安装未完成。请检查目录权限和剩余空间；保留现场，不会删除其他文件。" /SD IDOK
    SetErrorLevel 15
    Quit
  installation_done:
SectionEnd

Function un.RequireSafeDirectory
  Call un.CheckReparsePath
  ${If} $GuardError != 0
    MessageBox MB_OK|MB_ICONSTOP "安装目录被替换为联接/符号链接，已停止卸载，不删除链接指向的文件。" /SD IDOK
    SetErrorLevel 23
    Quit
  ${EndIf}
FunctionEnd

Function un.onInit
  SetShellVarContext current
  SetRegView 64
  ReadRegStr $0 HKCU "${UNINSTALL_KEY}" "InstallLocation"
  ${If} $0 == ""
  ${OrIf} $INSTDIR != $0
    Goto unsafe_uninstall
  ${EndIf}
  StrCpy $GuardPath $INSTDIR
  Call un.RequireSafeDirectory
  !include "${UNINSTALL_GUARDS}"
  ReadINIStr $0 "$INSTDIR\app\preview-install.ini" "Install" "Path"
  ${If} $0 != $INSTDIR
    Goto unsafe_uninstall
  ${EndIf}
  ReadINIStr $0 "$INSTDIR\app\preview-install.ini" "Install" "Release"
  ${If} $0 != "${RELEASE_ID}"
    Goto unsafe_uninstall
  ${EndIf}
  ReadINIStr $0 "$INSTDIR\app\preview-install.ini" "Install" "Channel"
  ${If} $0 != "DesktopUp.Preview"
    Goto unsafe_uninstall
  ${EndIf}
  ; A prior partial uninstall may have removed the exe. Marker + registry still bind the target.
  IfFileExists "$INSTDIR\app\DesktopUp.exe" 0 safe_uninstall
  System::Call 'kernel32::CreateFileW(w "$INSTDIR\app\DesktopUp.exe", i 0x40000000, i 1, p 0, i 3, i 0, p 0) p.r1'
  ${If} $1 == -1
    MessageBox MB_OK|MB_ICONSTOP "预览版仍在运行或文件不可写。请从托盘正常退出后再卸载；不会强制结束进程。" /SD IDOK
    SetErrorLevel 21
    Quit
  ${EndIf}
  System::Call 'kernel32::CloseHandle(p r1)'
  safe_uninstall:
    Return
  unsafe_uninstall:
    MessageBox MB_OK|MB_ICONSTOP "卸载目录、注册记录或安装标识不一致，已停止自动删除。" /SD IDOK
    SetErrorLevel 20
    Quit
FunctionEnd

Section "Uninstall"
  ClearErrors
  !include "${UNINSTALL_FILES}"
  ; Empty-directory removals may fail for unknown user files: leave these untouched.
  ClearErrors
  Delete "$INSTDIR\app\preview-install.ini"
  IfErrors uninstall_failed
  RMDir "$INSTDIR\app"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  Delete "$SMPROGRAMS\${PRODUCT}\DesktopUp Preview.lnk"
  Delete "$SMPROGRAMS\${PRODUCT}\预览说明.lnk"
  RMDir "$SMPROGRAMS\${PRODUCT}"
  DeleteRegKey HKCU "${UNINSTALL_KEY}"
  Goto uninstall_done
  uninstall_failed:
    MessageBox MB_OK|MB_ICONSTOP "部分程序文件仍被占用或不可写。保留卸载入口和配置；关闭占用程序后重试。" /SD IDOK
    SetErrorLevel 24
    Quit
  uninstall_done:
SectionEnd
