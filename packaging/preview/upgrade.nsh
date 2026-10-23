; Same-channel, current-user discovery only. The helper revalidates identity and hashes
; immediately before replacement; this UI hint never authorizes deletion by itself.
!include "WordFunc.nsh"
Var UpgradeMode
Var UpgradeRelease
Var UpgradeTarget
Var UpgradeResult
Var UpgradeMessage
Var ShortcutExplicit

Function SelectUpgradeDefault
  StrCpy $2 0
  StrCpy $3 "0.0"
  StrCpy $4 ""
  loop:
    EnumRegKey $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall" $2
    StrCmp $0 "" done
    IntOp $2 $2 + 1
    StrCpy $1 $0 18
    StrCmp $1 "DesktopUp.Preview." 0 loop
    ReadRegStr $5 HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\$0" "DisplayVersion"
    StrCpy $1 $0 "" 18
    StrCmp $1 $5 0 loop
    ReadRegStr $6 HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\$0" "InstallLocation"
    ReadINIStr $7 "$6\app\preview-install.ini" "Install" "Channel"
    StrCmp $7 "DesktopUp.Preview" 0 loop
    ReadINIStr $7 "$6\app\preview-install.ini" "Install" "Path"
    StrCmp $7 $6 0 loop
    ReadINIStr $7 "$6\app\preview-install.ini" "Install" "Release"
    StrCmp $7 $5 0 loop
    ${VersionCompare} $5 $3 $7
    StrCmp $7 1 0 loop
    StrCpy $3 $5
    StrCpy $4 $6
    Goto loop
  done:
  ; A command-line /D choice has already initialized INSTDIR. Do not replace it.
  ${If} $INSTDIR == "$LOCALAPPDATA\Programs\DesktopUp Preview\${RELEASE_ID}"
  ${AndIf} $4 != ""
    StrCpy $INSTDIR $4
  ${EndIf}
  ${If} $ShortcutExplicit != "1"
    ReadINIStr $0 "$INSTDIR\app\preview-install.ini" "Install" "Release"
    ${If} $0 != ""
      StrCpy $DesktopShortcutEnabled ${BST_UNCHECKED}
      ReadINIStr $1 "$INSTDIR\app\preview-install.ini" "Install" "DesktopShortcutCreated"
      ${If} $1 == "1"
        IfFileExists "$DESKTOP\DesktopUp Preview $0.lnk" 0 +2
        StrCpy $DesktopShortcutEnabled ${BST_CHECKED}
      ${EndIf}
    ${EndIf}
  ${EndIf}
FunctionEnd

Function DetectUpgradeTarget
  StrCpy $UpgradeMode 0
  StrCpy $UpgradeRelease ""
  StrCpy $2 0
  loop:
    EnumRegKey $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall" $2
    StrCmp $0 "" done
    IntOp $2 $2 + 1
    StrCpy $1 $0 18
    StrCmp $1 "DesktopUp.Preview." 0 loop
    ReadRegStr $1 HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\$0" "InstallLocation"
    StrCmp $1 $INSTDIR 0 loop
    StrCpy $UpgradeMode 1
    StrCpy $UpgradeRelease $0 "" 18
    Return
  done:
FunctionEnd

Function RunUpgrade
  StrCpy $UpgradeTarget $INSTDIR
  InitPluginsDir
  StrCpy $INSTDIR "$PLUGINSDIR\upgrade-payload"
  ClearErrors
  !include "${INSTALL_FILES}"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  IfErrors stage_failed
  StrCpy $INSTDIR $UpgradeTarget
  ClearErrors
  ExecWait '"$PLUGINSDIR\upgrade-payload\app\DesktopUp.Setup.exe" --upgrade "$PLUGINSDIR\upgrade-payload" "$INSTDIR" "$DesktopShortcutEnabled" "$PLUGINSDIR\upgrade-result.ini"' $UpgradeResult
  IfErrors execute_failed
  ReadINIStr $UpgradeMessage "$PLUGINSDIR\upgrade-result.ini" "Upgrade" "Message"
  ${If} $UpgradeResult == 0
    DetailPrint "$UpgradeMessage"
    Return
  ${EndIf}
  ${If} $UpgradeResult == 42
    MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION "$UpgradeMessage$\r$\n$\r$\n点击重试将恢复上次安装或完成备份清理。恢复后请重新运行安装包；取消则保留现场。" /SD IDCANCEL IDCANCEL upgrade_stopped
    ClearErrors
    ExecWait '"$PLUGINSDIR\upgrade-payload\app\DesktopUp.Setup.exe" --recover "$INSTDIR" "$PLUGINSDIR\upgrade-result.ini"' $UpgradeResult
    IfErrors execute_failed
    ReadINIStr $UpgradeMessage "$PLUGINSDIR\upgrade-result.ini" "Upgrade" "Message"
  ${EndIf}
  MessageBox MB_OK|MB_ICONEXCLAMATION "$UpgradeMessage" /SD IDOK
  upgrade_stopped:
    SetErrorLevel 41
    Quit
  stage_failed:
  installation_failed:
    StrCpy $INSTDIR $UpgradeTarget
    MessageBox MB_OK|MB_ICONSTOP "新版文件准备失败；旧安装尚未修改。请检查空间和权限。" /SD IDOK
    SetErrorLevel 40
    Quit
  execute_failed:
    MessageBox MB_OK|MB_ICONSTOP "无法执行升级辅助程序。请保留原安装和升级记录，不要手动覆盖程序文件。" /SD IDOK
    SetErrorLevel 40
    Quit
FunctionEnd
