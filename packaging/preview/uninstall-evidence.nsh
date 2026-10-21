; One literal manifest entry, one delete attempt. No wildcard, retry or reboot delete.
; System's ?e captures the error before attribute restoration or diagnostic calls.
; Keep NSIS Delete's missing-file, directory-skip and read-only attribute behavior.
Var CleanupStage
Var CleanupError
Var CleanupFailed
Var CleanupAttributes
Var CleanupAttributeError
Var CleanupRestoreError
Var CleanupPid
Var CleanupThread
Var CleanupTime
Var CleanupReport
Var CleanupRmResult
Var CleanupRmCount
Var CleanupRmRows

!macro DeleteInstalledFile RelativePath
  StrCpy $UninstallFailureFile "${RelativePath}"
  Call un.DeletePayloadFile
  ${If} $CleanupFailed != 0
    Goto uninstall_failed
  ${EndIf}
!macroend

Function un.DeletePayloadFile
  System::Store "S"
  StrCpy $CleanupError 0
  StrCpy $CleanupFailed 0
  StrCpy $CleanupAttributeError 0
  StrCpy $CleanupRestoreError 0
  StrCpy $CleanupStage "GetFileAttributesW"
  System::Call 'kernel32::GetFileAttributesW(w "$INSTDIR\$UninstallFailureFile") i.r0 ?e'
  Pop $1
  StrCpy $CleanupAttributes $0
  ${If} $0 == -1
    ; An already removed entry is normal when explicitly resuming a partial uninstall.
    ${If} $1 != 2
    ${AndIf} $1 != 3
      StrCpy $CleanupError $1
      StrCpy $CleanupFailed 1
    ${EndIf}
    Goto delete_payload_done
  ${EndIf}
  IntOp $1 $0 & 0x10
  ${If} $1 != 0
    ; Like NSIS Delete, do not recursively remove a directory replacing a file.
    Goto delete_payload_done
  ${EndIf}
  IntOp $1 $0 & 0xFFFFFFFE
  System::Call 'kernel32::SetFileAttributesW(w "$INSTDIR\$UninstallFailureFile", i r1) i.r2 ?e'
  Pop $3
  ${If} $2 == 0
    StrCpy $CleanupAttributeError $3
  ${EndIf}
  StrCpy $CleanupStage "DeleteFileW"
  System::Call 'kernel32::DeleteFileW(w "$INSTDIR\$UninstallFailureFile") i.r2 ?e'
  Pop $3
  ${If} $2 == 0
    StrCpy $CleanupError $3
    StrCpy $CleanupFailed 1
    ; Preserve the failing API's error before any other API, including restoration.
    System::Call 'kernel32::SetFileAttributesW(w "$INSTDIR\$UninstallFailureFile", i r0) i.r2 ?e'
    Pop $3
    ${If} $2 == 0
      StrCpy $CleanupRestoreError $3
    ${EndIf}
  ${EndIf}
  delete_payload_done:
  ${If} $CleanupFailed != 0
    System::Call 'kernel32::GetSystemTimeAsFileTime(*l .r1)'
    StrCpy $CleanupTime $1
    System::Call 'kernel32::GetCurrentProcessId() i.r1'
    StrCpy $CleanupPid $1
    System::Call 'kernel32::GetCurrentThreadId() i.r1'
    StrCpy $CleanupThread $1
  ${EndIf}
  System::Store "L"
FunctionEnd

Function un.CaptureCleanupOwners
  ; A read-only, bounded snapshot AFTER the failed API. Empty != proof of no owner.
  ; Never call RmShutdown/RmRestart or terminate any process.
  System::Store "S"
  StrCpy $CleanupRmCount 0
  StrCpy $CleanupRmRows ""
  System::Call 'rstrtmgr::RmStartSession(*i .r0, i 0, w .r1) i.r2'
  StrCpy $CleanupRmResult $2
  ${If} $2 != 0
    Goto cleanup_owners_done
  ${EndIf}
  System::Call '*(&w1024 "$INSTDIR\$UninstallFailureFile") p.r1'
  ${If} $1 == 0
    StrCpy $CleanupRmResult 8
    Goto cleanup_owners_end_session
  ${EndIf}
  System::Call '*(p r1) p.r2'
  ${If} $2 == 0
    System::Free $1
    StrCpy $CleanupRmResult 8
    Goto cleanup_owners_end_session
  ${EndIf}
  System::Call 'rstrtmgr::RmRegisterResources(i r0, i 1, p r2, i 0, p 0, i 0, p 0) i.r3'
  StrCpy $CleanupRmResult $3
  System::Free $2
  System::Free $1
  ${If} $3 != 0
    Goto cleanup_owners_end_session
  ${EndIf}
  StrCpy $1 0
  StrCpy $2 0
  StrCpy $3 0
  System::Call 'rstrtmgr::RmGetList(i r0, *i .r1, *i r2 r2, p 0, *i r3 r3) i.r4'
  StrCpy $CleanupRmResult $4
  StrCpy $CleanupRmCount $1
  ${If} $4 != 234
    Goto cleanup_owners_end_session
  ${EndIf}
  ; RM_PROCESS_INFO is 668 bytes on Windows (no pointers); cap diagnostic work.
  ${If} $1 > 16
    StrCpy $CleanupRmRows "snapshot exceeds 16 processes"
    Goto cleanup_owners_end_session
  ${EndIf}
  IntOp $4 $1 * 668
  System::Alloc $4
  Pop $5
  ${If} $5 == 0
    StrCpy $CleanupRmResult 8
    Goto cleanup_owners_end_session
  ${EndIf}
  StrCpy $2 $1
  System::Call 'rstrtmgr::RmGetList(i r0, *i .r1, *i r2 r2, p r5, *i r3 r3) i.r4'
  StrCpy $CleanupRmResult $4
  StrCpy $CleanupRmCount $1
  ${If} $4 == 0
    StrCpy $6 $5
    StrCpy $7 0
    ${DoWhile} $7 < $2
      System::Call '*$6(i .r8, l .r9)'
      ; PID + creation FILETIME avoids confusing a subsequently reused PID.
      StrCpy $CleanupRmRows "$CleanupRmRows$8@$9;"
      IntOp $6 $6 + 668
      IntOp $7 $7 + 1
    ${Loop}
  ${EndIf}
  System::Free $5
  cleanup_owners_end_session:
  System::Call 'rstrtmgr::RmEndSession(i r0)'
  cleanup_owners_done:
  System::Store "L"
FunctionEnd

Function un.WriteCleanupEvidence
  ; Capture before file I/O. Recording failure never permits cleanup to continue.
  Call un.CaptureCleanupOwners
  StrCpy $CleanupReport ""
  StrCpy $GuardPath "$UninstallProfile\uninstall-reports"
  Call un.CheckReparsePath
  ${If} $GuardError != 0
    Return
  ${EndIf}
  CreateDirectory "$GuardPath"
  StrCpy $CleanupReport "$GuardPath\cleanup-$CleanupTime-$CleanupPid.ini"
  StrCpy $GuardPath $CleanupReport
  Call un.CheckReparsePath
  ${If} $GuardError != 0
    StrCpy $CleanupReport ""
    Return
  ${EndIf}
  System::Store "S"
  ; CREATE_NEW: never replace earlier failure evidence. UTF-16 BOM for Windows INI.
  System::Call 'kernel32::CreateFileW(w "$CleanupReport", i 0x40000000, i 1, p 0, i 1, i 0x80, p 0) p.r0'
  ${If} $0 == -1
    StrCpy $CleanupReport ""
    Goto cleanup_report_done
  ${EndIf}
  System::Call '*(i 0xFEFF) p.r1'
  ${If} $1 == 0
    System::Call 'kernel32::CloseHandle(p r0)'
    StrCpy $CleanupReport ""
    Goto cleanup_report_done
  ${EndIf}
  System::Call 'kernel32::WriteFile(p r0, p r1, i 2, *i .r2, p 0) i.r3'
  System::Free $1
  System::Call 'kernel32::CloseHandle(p r0)'
  ${If} $3 == 0
  ${OrIf} $2 != 2
    StrCpy $CleanupReport ""
    Goto cleanup_report_done
  ${EndIf}
  ClearErrors
  WriteINIStr "$CleanupReport" "Failure" "Schema" "1"
  WriteINIStr "$CleanupReport" "Failure" "Release" "${RELEASE_ID}"
  WriteINIStr "$CleanupReport" "Failure" "InstallPath" "$INSTDIR"
  WriteINIStr "$CleanupReport" "Failure" "File" "$UninstallFailureFile"
  WriteINIStr "$CleanupReport" "Failure" "Api" "$CleanupStage"
  WriteINIStr "$CleanupReport" "Failure" "Win32Error" "$CleanupError"
  WriteINIStr "$CleanupReport" "Failure" "UninstallExitCode" "24"
  WriteINIStr "$CleanupReport" "Failure" "UtcFileTime" "$CleanupTime"
  WriteINIStr "$CleanupReport" "Failure" "Pid" "$CleanupPid"
  WriteINIStr "$CleanupReport" "Failure" "ThreadId" "$CleanupThread"
  WriteINIStr "$CleanupReport" "Failure" "AttributesBefore" "$CleanupAttributes"
  WriteINIStr "$CleanupReport" "Failure" "AttributePreparationError" "$CleanupAttributeError"
  WriteINIStr "$CleanupReport" "Failure" "AttributeRestoreError" "$CleanupRestoreError"
  WriteINIStr "$CleanupReport" "OwnerSnapshot" "AfterFailure" "1"
  WriteINIStr "$CleanupReport" "OwnerSnapshot" "Result" "$CleanupRmResult"
  WriteINIStr "$CleanupReport" "OwnerSnapshot" "Count" "$CleanupRmCount"
  WriteINIStr "$CleanupReport" "OwnerSnapshot" "PidAndCreationFileTime" "$CleanupRmRows"
  ${If} ${Errors}
    ; Leave a partial artifact for inspection, but do not claim a complete report.
    StrCpy $CleanupReport ""
  ${EndIf}
  cleanup_report_done:
  System::Store "L"
FunctionEnd
