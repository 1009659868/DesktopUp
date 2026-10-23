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
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp"
Name "${PRODUCT}"
OutFile "${SETUP_OUTPUT}"
RequestExecutionLevel user
InstallDir "$LOCALAPPDATA\Programs\DesktopUp"
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
Var UninstallFailureFile
Var InstallationLease
Var WizardLease

!include "${__FILEDIR__}\uninstall-evidence.nsh"
!include "${__FILEDIR__}\upgrade.nsh"

!define MUI_WELCOMEPAGE_TEXT "Windows 11 x64 独立预览版。$\r$\n$\r$\n自带运行库，不读取或迁移日常版配置；各 Preview 版本共享预览配置。首次为空白工作区，无演示卡片；拖入收纳会移动真实文件。$\r$\n$\r$\n同一用户只保留一份安装。可自选目录：原位置覆盖升级，新位置迁移升级。保留配置、收纳内容与自启动状态，请先从托盘退出旧版。"
!insertmacro MUI_PAGE_WELCOME
!define MUI_DIRECTORYPAGE_TEXT_TOP "自动识别当前用户唯一安装。保留原目录将覆盖升级；选择新目录将迁移程序并清理旧程序和入口，不会并行安装。配置、收纳内容及自启动状态保留；未知文件不删除。"
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
  ${NSD_CreateLabel} 0 30u 100% 32u "升级时替换本安装的旧入口，自启动状态保持。取消勾选仍可从开始菜单启动；不覆盖其他同名桌面文件。"
  Pop $0
  ${If} $UpgradeRoot != ""
    ${NSD_CreateLabel} 0 66u 100% 26u "原位置：$UpgradeRoot"
    Pop $0
  ${EndIf}
  ${NSD_CreateLabel} 0 98u 100% 32u "目标位置：$INSTDIR"
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

; The UI lease prevents concurrent wizards. The transaction lease is shared by
; the helper and uninstaller, independent of the runtime profile override.
!macro InstallerLease Prefix FunctionName FileName Variable
Function ${Prefix}${FunctionName}
  StrCpy $GuardPath "$LOCALAPPDATA\DesktopUp\Installer\${FileName}"
  Call ${Prefix}CheckReparsePath
  ${If} $GuardError != 0
    SetErrorLevel 23
    Quit
  ${EndIf}
  CreateDirectory "$LOCALAPPDATA\DesktopUp\Installer"
  System::Call 'kernel32::CreateFileW(w "$GuardPath", i 0xC0000000, i 0, p 0, i 4, i 0, p 0) p.r1'
  ${If} $1 == -1
    MessageBox MB_OK|MB_ICONSTOP "另一个安装、升级或卸载正在进行，或安装状态目录不可写；请稍后重试。未修改安装。" /SD IDOK
    SetErrorLevel 31
    Quit
  ${EndIf}
  StrCpy ${Variable} $1
FunctionEnd
!macroend
!insertmacro InstallerLease "" "AcquireWizardLease" "wizard.lock" "$WizardLease"
!insertmacro InstallerLease "un." "AcquireInstallationLease" "transaction.lock" "$InstallationLease"

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
  ${If} $UpgradeRoot == $INSTDIR
    StrCpy $GuardError 0
    Return
  ${EndIf}
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
    StrCpy $ShortcutExplicit "1"
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
  Call AcquireWizardLease
  Call SelectUpgradeDefault
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
  Call RunUpgrade
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
  Call un.AcquireInstallationLease
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
  !ifdef STARTUP_REVIEW
    StrCpy $UninstallProfile "$LOCALAPPDATA\DesktopUp\StartupReview"
  !endif
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
  ReadRegStr $0 HKCU "${UNINSTALL_KEY}" "DisplayVersion"
  ${If} $0 != "${RELEASE_ID}"
    Goto unsafe_uninstall
  ${EndIf}
  IfFileExists "$LOCALAPPDATA\DesktopUp\Installer\pending.ini" unsafe_uninstall
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
  ; No registration mutation until all cancellable content preparation succeeds.
  ; Another version may own DesktopUp now: remove only our exact launcher command.
  ClearErrors
  ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "DesktopUp"
  ${If} $0 == '$\"$INSTDIR\DesktopUp.exe$\" --autostart'
    ClearErrors
    DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "DesktopUp"
    ${If} ${Errors}
      MessageBox MB_OK|MB_ICONSTOP "无法移除本安装的启动登记，已停止卸载；程序和配置保留。" /SD IDOK
      SetErrorLevel 35
      Quit
    ${EndIf}
    ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "DesktopUp"
    ${If} $0 == '$\"$INSTDIR\DesktopUp.exe$\" --autostart'
      SetErrorLevel 35
      Quit
    ${EndIf}
  ${EndIf}
  SetOutPath "$TEMP"
  ClearErrors
  !include "${UNINSTALL_FILES}"
  Call un.RemoveOwnedDesktopShortcut
  ; Empty-directory removals may fail for unknown user files: leave these untouched.
  ClearErrors
  !insertmacro DeleteInstalledFile "app\preview-install.ini"
  RMDir "$INSTDIR\app"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  Delete "$SMPROGRAMS\${PRODUCT}\DesktopUp Preview.lnk"
  Delete "$SMPROGRAMS\${PRODUCT}\预览说明.lnk"
  RMDir "$SMPROGRAMS\${PRODUCT}"
  DeleteRegKey HKCU "${UNINSTALL_KEY}"
  Goto uninstall_done
  uninstall_failed:
    Call un.WriteCleanupEvidence
    ; Best-effort diagnosis in the existing, ownership-checked installation marker.
    ; Failure to write this record must never authorize further deletion or change exit 24.
    WriteINIStr "$INSTDIR\app\preview-install.ini" "LastCleanupFailure" "File" "$UninstallFailureFile"
    WriteINIStr "$INSTDIR\app\preview-install.ini" "LastCleanupFailure" "ExitCode" "24"
    WriteINIStr "$INSTDIR\app\preview-install.ini" "LastCleanupFailure" "Api" "$CleanupStage"
    WriteINIStr "$INSTDIR\app\preview-install.ini" "LastCleanupFailure" "Win32Error" "$CleanupError"
    WriteINIStr "$INSTDIR\app\preview-install.ini" "LastCleanupFailure" "Report" "$CleanupReport"
    DetailPrint "程序文件清理失败：$INSTDIR\$UninstallFailureFile（$CleanupStage，Win32 $CleanupError；卸载退出码24）"
    MessageBox MB_OK|MB_ICONSTOP "无法删除程序文件：$\r$\n$INSTDIR\$UninstallFailureFile$\r$\n$CleanupStage：Windows 错误 $CleanupError（卸载退出码24）$\r$\n$\r$\n保留卸载入口、配置及剩余文件，未继续删除。请检查占用或访问权限后再试。诊断位置：$\r$\n$CleanupReport$\r$\n若报告不可写，请保留此提示；app\preview-install.ini 中也会尽力记录。" /SD IDOK
    SetErrorLevel 24
    Quit
  uninstall_done:
SectionEnd
