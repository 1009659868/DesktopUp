Unicode true
!include "MUI2.nsh"
!include "x64.nsh"
!include "WinVer.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "nsDialogs.nsh"

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
Var DesktopShortcutEnabled
Var DesktopShortcutCheckbox
Var InstallOptionsDialog
Var DesktopShortcutTarget
Var ContentDisposition
Var ContentReturnRadio
Var ContentKeepRadio
Var UninstallProfile
Var UninstallLease

!define MUI_WELCOMEPAGE_TEXT "Windows 11 x64 独立预览版。$\r$\n$\r$\n自带运行库，使用独立配置，不读取或迁移日常版配置，不设置开机启动。首次为空白工作区，无演示卡片；拖入收纳会移动真实文件，请先用无价值文件体验。$\r$\n$\r$\n下一步可自选安装目录。卸载可归还收纳内容或保留原位，配置保留。"
!insertmacro MUI_PAGE_WELCOME
!define MUI_DIRECTORYPAGE_TEXT_TOP "选择当前用户可写的本地目录，可以已有其他文件；已有内容会保留。若存在同名的程序文件或 app、licenses 目录，请改选目录。配置和收纳内容独立于安装目录，收纳位置可在控制中心修改。"
!insertmacro MUI_PAGE_DIRECTORY
Page custom InstallOptionsCreate InstallOptionsLeave
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\DesktopUp.exe"
!define MUI_FINISHPAGE_RUN_NOTCHECKED
!define MUI_FINISHPAGE_RUN_TEXT "启动 DesktopUp Preview"
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
UninstPage custom un.ContentOptionsCreate un.ContentOptionsLeave
!insertmacro MUI_UNPAGE_INSTFILES
!define MUI_UNFINISHPAGE_TEXT "程序卸载完成。配置、位置清单和未移出的内容不会随程序删除。$\r$\n$\r$\n位置清单位于：$\r$\n$UninstallProfile\uninstall-reports$\r$\n$\r$\n映射目录及其他版本/日常版配置未移动。"
!insertmacro MUI_UNPAGE_FINISH
!insertmacro MUI_LANGUAGE "SimpChinese"

Function InstallOptionsCreate
  !insertmacro MUI_HEADER_TEXT "附加选项" "选择是否在当前用户桌面创建启动入口。"
  nsDialogs::Create 1018
  Pop $InstallOptionsDialog
  ${If} $InstallOptionsDialog == error
    Abort
  ${EndIf}
  ${NSD_CreateCheckbox} 0 8u 100% 14u "创建桌面快捷方式"
  Pop $DesktopShortcutCheckbox
  ${NSD_SetState} $DesktopShortcutCheckbox $DesktopShortcutEnabled
  ${NSD_OnClick} $DesktopShortcutCheckbox InstallOptionsChanged
  ${NSD_CreateLabel} 0 30u 100% 32u "取消勾选后，仍可从开始菜单启动。不会设置开机启动，也不会覆盖已有同名桌面文件。"
  Pop $0
  nsDialogs::Show
FunctionEnd

Function InstallOptionsChanged
  Pop $0
  ; Update on click as well as Next, so Back / Next preserves the user's choice.
  ${NSD_GetState} $DesktopShortcutCheckbox $DesktopShortcutEnabled
FunctionEnd

Function InstallOptionsLeave
  ${NSD_GetState} $DesktopShortcutCheckbox $DesktopShortcutEnabled
FunctionEnd

Function CreateOptionalDesktopShortcut
  ${If} $DesktopShortcutEnabled != ${BST_CHECKED}
    Return
  ${EndIf}
  InitPluginsDir
  ClearErrors
  CreateShortCut "$PLUGINSDIR\desktop-entry.lnk" "$INSTDIR\DesktopUp.exe"
  IfErrors shortcut_skipped
  ; CopyFile's fail-if-exists flag is atomic with respect to name collisions.
  ; A pre-existing shortcut/file is never overwritten or claimed by this install.
  System::Call 'kernel32::CopyFileW(w "$PLUGINSDIR\desktop-entry.lnk", w "$DESKTOP\${PRODUCT}.lnk", i 1) i.r0'
  ${If} $0 == 0
    Goto shortcut_skipped
  ${EndIf}
  WriteINIStr "$INSTDIR\app\preview-install.ini" "Install" "DesktopShortcutCreated" "1"
  Return
  shortcut_skipped:
    DetailPrint "未创建桌面快捷方式（同名文件已存在或桌面不可写）；仍可从开始菜单启动。"
    ClearErrors
FunctionEnd

Function un.RemoveOwnedDesktopShortcut
  ReadINIStr $0 "$INSTDIR\app\preview-install.ini" "Install" "DesktopShortcutCreated"
  ${If} $0 != "1"
    Return
  ${EndIf}
  ; Only remove a link that we created and that still points to this installation.
  ; Read stored metadata, never Resolve (which may repair a link or search targets).
  StrCpy $DesktopShortcutTarget ""
  System::Call 'ole32::CoInitialize(p 0) i.r4'
  System::Call 'ole32::CoCreateInstance(g "{00021401-0000-0000-C000-000000000046}", p 0, i 1, g "{0000010b-0000-0000-C000-000000000046}", *p.r0) i.r1'
  ${If} $1 == 0
    System::Call '$0->5(w "$DESKTOP\${PRODUCT}.lnk", i 0) i.r1'
    ${If} $1 == 0
      System::Call '$0->0(g "{000214F9-0000-0000-C000-000000000046}", *p.r2) i.r1'
      ${If} $1 == 0
        System::Call '$2->3(w .r3, i ${NSIS_MAX_STRLEN}, p 0, i 4) i.r1'
        ${If} $1 == 0
          StrCpy $DesktopShortcutTarget $3
        ${EndIf}
        System::Call '$2->2()'
      ${EndIf}
    ${EndIf}
    System::Call '$0->2()'
  ${EndIf}
  ${If} $4 >= 0
    System::Call 'ole32::CoUninitialize()'
  ${EndIf}
  ${If} $DesktopShortcutTarget == "$INSTDIR\DesktopUp.exe"
    Delete "$DESKTOP\${PRODUCT}.lnk"
  ${EndIf}
FunctionEnd

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
  StrCpy $DesktopShortcutEnabled ${BST_CHECKED}
  ${GetParameters} $0
  ClearErrors
  ${GetOptions} $0 "/DESKTOPSHORTCUT=" $1
  ${IfNot} ${Errors}
    ${If} $1 != "0"
    ${AndIf} $1 != "1"
      MessageBox MB_OK|MB_ICONSTOP "DESKTOPSHORTCUT 仅接受 0（不创建）或 1（创建）。" /SD IDOK
      SetErrorLevel 17
      Quit
    ${EndIf}
    StrCpy $DesktopShortcutEnabled $1
  ${EndIf}
  ClearErrors
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
  WriteRegStr HKCU "${UNINSTALL_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S /CONTENT=keep'
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\DesktopUp.exe"
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "EstimatedSize" ${PAYLOAD_KB}
  IfErrors installation_failed
  Call CreateOptionalDesktopShortcut
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
  StrCpy $UninstallLease 0
  StrCpy $ContentDisposition "return"
  IfSilent 0 +2
    StrCpy $ContentDisposition "keep"
  ${GetParameters} $0
  ClearErrors
  ${GetOptions} $0 "/CONTENT=" $1
  ${IfNot} ${Errors}
    ${If} $1 != "return"
    ${AndIf} $1 != "keep"
      SetErrorLevel 30
      Quit
    ${EndIf}
    StrCpy $ContentDisposition $1
  ${EndIf}
  ClearErrors
  StrCpy $UninstallProfile "$LOCALAPPDATA\DesktopUp\Preview"
  ; Same explicit isolated profile as the app, used only by owned package validation.
  ReadEnvStr $0 "DESKTOPUP_V3_TEST_DATA_ROOT"
  ${If} $0 != ""
    StrCpy $UninstallProfile $0
  ${EndIf}
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

Function un.ContentOptionsCreate
  !insertmacro MUI_HEADER_TEXT "收纳内容" "先处理文件，再卸载程序。不会删除收纳文件。"
  nsDialogs::Create 1018
  Pop $0
  ${If} $0 == error
    Abort
  ${EndIf}
  ${NSD_CreateRadioButton} 0 4u 100% 16u "归还收纳内容后卸载（推荐）"
  Pop $ContentReturnRadio
  ${NSD_CreateLabel} 12u 24u 100% 36u "返回原目录，原目录不可用或来源未知时返回系统桌面。包含收纳目录中未显示在海报内的文件；保留当前文件名，重名由 Windows 处理。取消或失败将停止卸载。"
  Pop $0
  ${NSD_CreateRadioButton} 0 70u 100% 16u "保留在原收纳位置，仅卸载程序"
  Pop $ContentKeepRadio
  ${NSD_CreateLabel} 12u 90u 100% 38u "文件不移动，配置继续保留；在配置目录生成本机位置清单。目录映射与外部引用在两种方式下均保持原位。所有预览版本共用当前预览配置，日常版不受影响。"
  Pop $0
  ${If} $ContentDisposition == "return"
    ${NSD_Check} $ContentReturnRadio
  ${Else}
    ${NSD_Check} $ContentKeepRadio
  ${EndIf}
  nsDialogs::Show
FunctionEnd

Function un.ContentOptionsLeave
  ${NSD_GetState} $ContentReturnRadio $0
  ${If} $0 == ${BST_CHECKED}
    StrCpy $ContentDisposition "return"
  ${Else}
    StrCpy $ContentDisposition "keep"
  ${EndIf}
FunctionEnd

Function un.PrepareContents
  StrCpy $GuardPath $UninstallProfile
  Call un.RequireSafeDirectory
  CreateDirectory "$UninstallProfile"
  StrCpy $GuardPath "$UninstallProfile\uninstall-session.lock"
  Call un.RequireSafeDirectory
  ; Held until the uninstaller exits. The ordinary app holds this same file while running.
  System::Call 'kernel32::CreateFileW(w "$UninstallProfile\uninstall-session.lock", i 0xC0000000, i 0, p 0, i 4, i 0, p 0) p.r1'
  ${If} $1 == -1
    MessageBox MB_OK|MB_ICONSTOP "DesktopUp 或另一个卸载正在使用此配置，请先正常退出后再试。未删除程序或内容。" /SD IDOK
    SetErrorLevel 31
    Quit
  ${EndIf}
  StrCpy $UninstallLease $1
  ; Success is mandatory. A missing/broken helper cannot silently authorize cleanup.
  ; Only explicit preservation may resume a partially completed, previously prepared
  ; uninstall whose helper/dependencies were already removed. No file moves are replayed.
  ${If} $ContentDisposition == "keep"
    ReadINIStr $0 "$INSTDIR\app\preview-install.ini" "Uninstall" "PreparedPath"
    ${If} $0 == $INSTDIR
      ReadINIStr $0 "$INSTDIR\app\preview-install.ini" "Uninstall" "PreparedProfile"
      ${If} $0 == $UninstallProfile
        Goto content_prepared
      ${EndIf}
    ${EndIf}
  ${EndIf}
  IfFileExists "$INSTDIR\app\DesktopUp.exe" 0 content_failed
  StrCpy $0 ""
  IfSilent 0 +2
    StrCpy $0 "--uninstall-silent"
  SetOutPath "$INSTDIR\app"
  ClearErrors
  ExecWait '"$INSTDIR\app\DesktopUp.exe" --prepare-uninstall=$ContentDisposition $0' $1
  IfErrors content_failed
  ${If} $1 != 0
    Goto content_failed
  ${EndIf}
  ClearErrors
  WriteINIStr "$INSTDIR\app\preview-install.ini" "Uninstall" "PreparedPath" "$INSTDIR"
  WriteINIStr "$INSTDIR\app\preview-install.ini" "Uninstall" "PreparedProfile" "$UninstallProfile"
  IfErrors content_failed
  content_prepared:
  DetailPrint "内容处理完成。配置及位置清单保留在 $UninstallProfile"
  Return
  content_failed:
    MessageBox MB_OK|MB_ICONSTOP "内容处理未完成或已取消，已停止卸载。程序、配置和剩余文件保留；已归还文件保持原位。可正常启动核对，或重新卸载并明确选择保留原位。" /SD IDOK
    SetErrorLevel 32
    Quit
FunctionEnd

Function un.onGUIEnd
  ${If} $UninstallLease != 0
    System::Call 'kernel32::CloseHandle(p $UninstallLease)'
    StrCpy $UninstallLease 0
  ${EndIf}
FunctionEnd

Section "Uninstall"
  Call un.PrepareContents
  SetOutPath "$TEMP"
  ClearErrors
  !include "${UNINSTALL_FILES}"
  Call un.RemoveOwnedDesktopShortcut
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
