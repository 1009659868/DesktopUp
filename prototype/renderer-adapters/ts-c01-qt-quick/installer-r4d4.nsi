Unicode true
ManifestDPIAware true
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 32

!include "MUI2.nsh"
!include "FileFunc.nsh"

!ifndef PAYLOAD_ROOT
  !error "PAYLOAD_ROOT is required"
!endif
!ifndef OUTPUT_FILE
  !error "OUTPUT_FILE is required"
!endif
!ifndef INSTALL_GUARD_INCLUDE
  !error "INSTALL_GUARD_INCLUDE is required"
!endif
!ifndef UNINSTALL_INCLUDE
  !error "UNINSTALL_INCLUDE is required"
!endif

Name "DesktopUp V2 TS-C01 Candidate"
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\DesktopUp"
InstallDirRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp" "InstallLocation"
ShowInstDetails show
ShowUninstDetails show
BrandingText "DesktopUp V2 / R4-D4 candidate lifecycle probe"

!define MUI_ABORTWARNING
!define MUI_DIRECTORYPAGE_VERIFYONLEAVE
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"

Var R4D4PackageProbe

Function .onInit
  StrCpy $R4D4PackageProbe "0"
  ${GetParameters} $R0
  ClearErrors
  ${GetOptions} $R0 "/R4D4PACKAGEPROBE=" $R1
  IfErrors init_done
  StrCmp $R1 "1" 0 init_done
  StrCpy $R4D4PackageProbe "1"
init_done:
FunctionEnd

Function .onVerifyInstDir
  StrCmp $R4D4PackageProbe "1" verify_existing_target

  ReadRegStr $R2 HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp" "InstallLocation"
  StrCmp $R2 "" verify_existing_target
  StrCmp $R2 $INSTDIR verify_existing_target
  MessageBox MB_OK|MB_ICONEXCLAMATION "DesktopUp 已安装在：$\r$\n$R2$\r$\n$\r$\n请先卸载现有版本，再选择新的安装目录。"
  Abort

verify_existing_target:
  IfFileExists "$INSTDIR\resources\r4d4-formal-install.flag" verify_done
  ${GetRoot} "$INSTDIR" $R3
  StrCmp $INSTDIR $R3 0 verify_collisions
  StrCpy $INSTDIR "$INSTDIR\DesktopUp"

verify_collisions:
  !include "${INSTALL_GUARD_INCLUDE}"

verify_done:
FunctionEnd

Section "DesktopUp" SEC_MAIN
  SetShellVarContext current
  SetOutPath "$INSTDIR"
  File /r "${PAYLOAD_ROOT}\*.*"
  WriteUninstaller "$INSTDIR\uninstall.exe"

  StrCmp $R4D4PackageProbe "1" install_done formal_install

formal_install:
  FileOpen $R0 "$INSTDIR\resources\r4d4-formal-install.flag" w
  FileWrite $R0 "desktopup-ts-c01-r4d4-formal-install-v2$\r$\n"
  FileClose $R0

  CreateDirectory "$SMPROGRAMS\DesktopUp"
  CreateShortcut "$SMPROGRAMS\DesktopUp\DesktopUp.lnk" "$INSTDIR\DesktopUp.exe"

  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp" "DisplayName" "DesktopUp V2 TS-C01 Candidate"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp" "DisplayVersion" "0.1.0-r4d4"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp" "Publisher" "DesktopUp"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp" "DisplayIcon" "$INSTDIR\DesktopUp.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp" "NoRepair" 1

install_done:
SectionEnd

Section "Uninstall"
  SetShellVarContext current
  IfFileExists "$INSTDIR\resources\r4d4-formal-install.flag" formal_uninstall sandbox_uninstall

formal_uninstall:
  ExecWait '"$INSTDIR\DesktopUp.exe" --request-exit'
  Sleep 1500
  ClearErrors
  Delete "$INSTDIR\DesktopUp.exe"
  IfErrors 0 process_stopped
  MessageBox MB_OK|MB_ICONSTOP "DesktopUp is still running. Close it and run uninstall.exe again."
  Abort

process_stopped:
  StrCpy $R0 '"$INSTDIR\DesktopUp.exe" --autostart'
  ReadRegStr $R1 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "DesktopUp"
  StrCmp $R1 $R0 0 preserve_foreign_autostart
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "DesktopUp"

preserve_foreign_autostart:
  Delete "$SMPROGRAMS\DesktopUp\DesktopUp.lnk"
  RMDir "$SMPROGRAMS\DesktopUp"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp"
  RMDir /r "$LOCALAPPDATA\DesktopUp\Candidate\TS-C01"
  RMDir "$LOCALAPPDATA\DesktopUp\Candidate"
  RMDir "$LOCALAPPDATA\DesktopUp"
  Goto common_cleanup

sandbox_uninstall:
  ; The explicit /R4D4PACKAGEPROBE=1 mode is the automated package probe. It never writes
  ; Start Menu, uninstall registration, Run, or the production data root.

common_cleanup:
  !include "${UNINSTALL_INCLUDE}"
SectionEnd
