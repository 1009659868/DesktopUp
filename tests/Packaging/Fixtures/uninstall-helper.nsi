Unicode true
Name "DesktopUp owned uninstall fixture"
OutFile "${FIXTURE_OUTPUT}"
RequestExecutionLevel user
SilentInstall silent
Section
  ReadEnvStr $0 "DESKTOPUP_FIXTURE_EXIT_CODE"
  StrCmp $0 "" 0 +2
    StrCpy $0 0
  SetErrorLevel $0
SectionEnd
