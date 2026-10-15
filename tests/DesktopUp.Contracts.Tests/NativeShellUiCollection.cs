namespace DesktopUp.Contracts.Tests;

// These helpers create real property sheets and lazy Shell menus on one interactive
// desktop. Keep their bounded lifetime assertions, but do not compete with the
// full suite's other native processes and UI fixtures for that desktop/STA service.
[CollectionDefinition("Native Shell UI", DisableParallelization = true)]
public sealed class NativeShellUiCollection;
