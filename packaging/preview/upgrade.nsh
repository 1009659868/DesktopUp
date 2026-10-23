; Discover all registrations; destination selection cannot opt out of migration.
!include "WordFunc.nsh"
Var UpgradeRoot
Var UpgradeTarget
Var UpgradeResult
Var UpgradeMessage
Var ShortcutExplicit

Function SelectUpgradeDefault
  StrCpy $UpgradeRoot ""
  IfFileExists "$LOCALAPPDATA\DesktopUp\Installer\pending.ini" pending
  StrCpy $2 0
  StrCpy $8 0
  loop:
    EnumRegKey $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall" $2
    StrCmp $0 "" done
    IntOp $2 $2 + 1
    StrCmp $0 "DesktopUp" found
    StrCpy $1 $0 18
    StrCmp $1 "DesktopUp.Preview." 0 loop
  found:
    IntOp $8 $8 + 1
    ${If} $8 > 1
      MessageBox MB_OK|MB_ICONSTOP "当前用户有多份 DesktopUp 安装登记。请先核对并处理旧安装；不会任意选择一份或新增安装。" /SD IDOK
      SetErrorLevel 18
      Quit
    ${EndIf}
    ReadRegStr $5 HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\$0" "DisplayVersion"
    ReadRegStr $6 HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\$0" "InstallLocation"
    StrCmp $6 "" invalid
    StrCmp $5 "" invalid
    StrCmp $0 "DesktopUp" marker
    StrCpy $1 $0 "" 18
    StrCmp $1 $5 0 invalid
  marker:
    FindFirst $1 $7 "$6\.desktopup-upgrade-*"
    FindClose $1
    StrCmp $7 "" +3
    StrCpy $UpgradeRoot $6
    Goto loop
    ReadINIStr $7 "$6\app\preview-install.ini" "Install" "Channel"
    StrCmp $7 "DesktopUp.Preview" 0 invalid
    ReadINIStr $7 "$6\app\preview-install.ini" "Install" "Path"
    StrCmp $7 $6 0 invalid
    ReadINIStr $7 "$6\app\preview-install.ini" "Install" "Release"
    StrCmp $7 $5 0 invalid
    StrCpy $UpgradeRoot $6
    Goto loop
  pending:
    ReadINIStr $UpgradeRoot "$LOCALAPPDATA\DesktopUp\Installer\pending.ini" "Install" "Path"
    StrCmp $UpgradeRoot "" invalid
  done:
    ${If} $INSTDIR == "$LOCALAPPDATA\Programs\DesktopUp"
    ${AndIf} $UpgradeRoot != ""
      StrCpy $INSTDIR $UpgradeRoot
    ${EndIf}
    ${If} $ShortcutExplicit != "1"
    ${AndIf} $UpgradeRoot != ""
      ReadINIStr $0 "$UpgradeRoot\app\preview-install.ini" "Install" "Release"
      ReadINIStr $1 "$UpgradeRoot\app\preview-install.ini" "Install" "DesktopShortcutCreated"
      StrCpy $DesktopShortcutEnabled ${BST_UNCHECKED}
      ${If} $1 == "1"
        IfFileExists "$DESKTOP\DesktopUp Preview $0.lnk" 0 +2
        StrCpy $DesktopShortcutEnabled ${BST_CHECKED}
      ${EndIf}
    ${EndIf}
    Return
  invalid:
    MessageBox MB_OK|MB_ICONSTOP "DesktopUp 安装登记、目录或标记不一致。已停止，不会覆盖或并行安装；请保留现场。" /SD IDOK
    SetErrorLevel 18
    Quit
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
