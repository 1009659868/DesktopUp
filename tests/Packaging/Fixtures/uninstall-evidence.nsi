Unicode true
!include "LogicLib.nsh"
Name "DesktopUp cleanup evidence fixture"
OutFile "${FIXTURE_EXE}"
RequestExecutionLevel user
SilentInstall silent
SilentUnInstall silent
SetCompress off
Var UninstallFailureFile
Var UninstallProfile
Var GuardPath
Var GuardError
!include "${EVIDENCE_INCLUDE}"

; This fixture only operates on its compiled, runner-owned directory below out.
; Real installation/reparse/registration guards are exercised by the actual package.
Function un.CheckReparsePath
  StrCpy $GuardError 0
FunctionEnd
Section
  WriteUninstaller "${FIXTURE_ROOT}\Uninstall.exe"
SectionEnd
Section "Uninstall"
  StrCpy $INSTDIR "${FIXTURE_ROOT}"
  StrCpy $UninstallProfile "${FIXTURE_ROOT}\profile"
  !insertmacro DeleteInstalledFile "first.bin"
  !insertmacro DeleteInstalledFile "later.bin"
  SetErrorLevel 0
  Goto done
  uninstall_failed:
    Call un.WriteCleanupEvidence
    WriteINIStr "${FIXTURE_ROOT}\result.ini" "Failure" "File" "$UninstallFailureFile"
    WriteINIStr "${FIXTURE_ROOT}\result.ini" "Failure" "Api" "$CleanupStage"
    WriteINIStr "${FIXTURE_ROOT}\result.ini" "Failure" "Win32Error" "$CleanupError"
    WriteINIStr "${FIXTURE_ROOT}\result.ini" "Failure" "Report" "$CleanupReport"
    SetErrorLevel 24
  done:
SectionEnd
