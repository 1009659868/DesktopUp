#include "adapters/desktop-host-win32/win32_desktop_host.h"
#include "adapters/renderer-qpainter/qpainter_renderer.h"

#include <windows.h>

#include <QEventLoop>
#include <QGuiApplication>
#include <QTimer>

#include <cstdint>
#include <filesystem>
#include <iostream>
#include <optional>
#include <string>

namespace {

int failures{};

void expect(const bool condition, const char* message) {
  if (!condition) {
    std::cerr << "FAIL: " << message << '\n';
    ++failures;
  }
}

std::optional<std::string> optionValue(const int argc, char** argv, const std::string& option) {
  for (int index = 1; index < argc; ++index) {
    const std::string argument = argv[index];
    if (argument == option && index + 1 < argc) {
      return std::string(argv[index + 1]);
    }
    const std::string prefix = option + "=";
    if (argument.rfind(prefix, 0) == 0) {
      return argument.substr(prefix.size());
    }
  }
  return std::nullopt;
}

HWND asWindow(const std::uint64_t value) {
  return reinterpret_cast<HWND>(static_cast<std::uintptr_t>(value));
}
bool runUnrelatedWindowEventSource(const std::string& executable) {
  const auto path = std::filesystem::u8path(executable).wstring();
  std::wstring command = L"\"" + path + L"\"";
  STARTUPINFOW startup{};
  startup.cb = sizeof(startup);
  PROCESS_INFORMATION process{};
  if (!CreateProcessW(nullptr,
                      command.data(),
                      nullptr,
                      nullptr,
                      FALSE,
                      CREATE_NO_WINDOW,
                      nullptr,
                      nullptr,
                      &startup,
                      &process)) {
    return false;
  }

  QEventLoop eventLoop;
  QTimer::singleShot(650, &eventLoop, &QEventLoop::quit);
  eventLoop.exec();
  const DWORD waitResult = WaitForSingleObject(process.hProcess, 2000);
  DWORD exitCode = STILL_ACTIVE;
  if (waitResult == WAIT_OBJECT_0) {
    GetExitCodeProcess(process.hProcess, &exitCode);
  }
  CloseHandle(process.hThread);
  CloseHandle(process.hProcess);
  return waitResult == WAIT_OBJECT_0 && exitCode == 0;
}


}  // namespace

int main(int argc, char** argv) {
  SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
  QGuiApplication application(argc, argv);
  using namespace desktopup;

  const auto durationOption = optionValue(argc, argv, "--duration-ms");
  const int durationMs = durationOption.has_value() ? std::stoi(*durationOption) : 0;
  constexpr contracts::PixelSize frameSize{300, 420};
  const auto requestedBounds = adapters::Win32DesktopHost::centeredPrimaryWorkArea(frameSize);

  adapters::Win32DesktopHost host;
  adapters::QPainterRenderer renderer;
  const bool attached = host.attach(41, requestedBounds);
  expect(attached, "real host must attach to the certified desktop target");
  auto firstObservation = host.observe();
  if (!attached) {
    std::cerr << "QTS04_HOST_ATTACH_DIAGNOSTIC topology="
              << static_cast<int>(firstObservation.topology)
              << " state=" << static_cast<int>(firstObservation.state)
              << " nativeError=" << firstObservation.nativeError
              << " resources=" << firstObservation.ownedResourceCount
              << " iconView=0x" << std::hex << firstObservation.iconViewHandle
              << " visual=0x" << firstObservation.visualHandle << std::dec << '\n';
  }
  expect(firstObservation.layeredChildCompatibilityDeclared,
         "host-capable executable must embed the Windows 8+ layered-child manifest");
  expect(firstObservation.topology == contracts::HostTopology::RaisedProgman,
         "current certified topology must be raised Progman");
  expect(firstObservation.safeTargetUnique && firstObservation.attached,
         "desktop target must be unique, safe, and attached");
  expect(!firstObservation.visible,
         "new visual must remain hidden until a complete first frame is published");
  expect(firstObservation.visualHandle != 0 && firstObservation.parentHandle != 0 &&
             firstObservation.defViewHandle != 0 && firstObservation.iconViewHandle != 0,
         "native host observation must expose the complete shell relationship");
  expect(firstObservation.hostAboveDefView,
         "DesktopUp visual must be above the Explorer icon DefView sibling");
  expect(!firstObservation.topmost && !firstObservation.ordinaryTopLevelFallback,
         "real host must never use TopMost or an ordinary top-level fallback");
  expect(firstObservation.winEventHooksActive && firstObservation.ownedResourceCount == 3,
         "coordinator, shell hook, and visual ownership must be explicit");

  auto frame = renderer.render({1, firstObservation.surfaceEpoch, frameSize});
  auto pixels = renderer.frameView(frame.buffer);
  const contracts::HitRegionSet hits{1, 1, frameSize, {}};
  expect(pixels.valid() && host.present(requestedBounds, frame, pixels, hits),         "QPainter premultiplied first frame must publish to the real host");
  firstObservation = host.observe();
  expect(firstObservation.visible, "visual must become visible only after first-frame success");
  expect(firstObservation.requestedBounds == requestedBounds &&
             firstObservation.windowBounds == requestedBounds &&
             firstObservation.clientScreenBounds == requestedBounds &&
             firstObservation.extendedFrameBounds == requestedBounds,
         "requested, window, client-screen, and visible frame bounds must agree");

  const auto displayBefore = host.observe();
  expect(host.refreshDisplayEnvironment() ==
             contracts::HostMaintenanceResult::Unchanged,
         "unchanged primary display must not create a projection event");
  const auto displayAfter = host.observe();
  expect(displayAfter.visualHandle == displayBefore.visualHandle &&
             displayAfter.shellGeneration == displayBefore.shellGeneration &&
             displayAfter.lifecycleEpoch == displayBefore.lifecycleEpoch &&
             displayAfter.displayEnvironment.revision ==
                 displayBefore.displayEnvironment.revision,
         "display polling with no environment change must preserve host identity");

  const auto eventSource = optionValue(argc, argv, "--event-source");
  if (eventSource.has_value()) {
    const auto unrelatedBefore = host.observe();
    expect(runUnrelatedWindowEventSource(*eventSource),
           "external unrelated window event source must complete");
    const auto unrelatedAfter = host.observe();
    expect(unrelatedAfter.ignoredShellEventCount >
               unrelatedBefore.ignoredShellEventCount,
           "ordinary external CREATE/DESTROY events must be classified as unrelated");
    expect(unrelatedAfter.relevantShellEventCount ==
               unrelatedBefore.relevantShellEventCount &&
             unrelatedAfter.topologyValidationCount ==
               unrelatedBefore.topologyValidationCount,
           "unrelated events must not even schedule a Shell topology validation");
    expect(unrelatedAfter.visualHandle == unrelatedBefore.visualHandle &&
             unrelatedAfter.shellGeneration ==
                 unrelatedBefore.shellGeneration &&
             unrelatedAfter.lifecycleEpoch ==
                 unrelatedBefore.lifecycleEpoch &&
             unrelatedAfter.surfaceEpoch == unrelatedBefore.surfaceEpoch &&
             unrelatedAfter.visible,
           "ordinary windows and file-dialog-like events must not hide, replace, or reattach the poster");
    firstObservation = unrelatedAfter;
  }

  const auto oldVisual = firstObservation.visualHandle;
  const auto oldShellGeneration = firstObservation.shellGeneration;
  const auto oldLifecycleEpoch = firstObservation.lifecycleEpoch;
  const auto oldSurfaceEpoch = firstObservation.surfaceEpoch;
  renderer.release(frame.buffer);

  int recoveryCallbackCount = 0;
  contracts::HostMaintenanceResult recoveryCallbackResult{};
  host.setRecoveryCallback([&](const contracts::HostMaintenanceResult result) {
    ++recoveryCallbackCount;
    recoveryCallbackResult = result;
  });

  expect(host.requestReconnect() == contracts::HostMaintenanceResult::ReattachedNeedsFrame,
         "explicit generation replacement must reattach and request a new first frame");
  expect(recoveryCallbackCount == 1 &&
             recoveryCallbackResult == contracts::HostMaintenanceResult::ReattachedNeedsFrame,
         "reconnect must notify Runtime exactly once that a new first frame is required");
  auto secondObservation = host.observe();
  expect(!IsWindow(asWindow(oldVisual)), "old generation visual must be destroyed before replacement");
  expect(secondObservation.visualHandle != 0 && secondObservation.visualHandle != oldVisual,
         "reconnect must allocate a distinct visual window");
  expect(secondObservation.shellGeneration > oldShellGeneration &&
             secondObservation.lifecycleEpoch > oldLifecycleEpoch &&
             secondObservation.surfaceEpoch > oldSurfaceEpoch,
         "shell, lifecycle, and surface identities must advance on reconnect");
  expect(!secondObservation.visible,
         "replacement visual must remain hidden until its own first frame succeeds");

  frame = renderer.render({1, secondObservation.surfaceEpoch, frameSize});
  pixels = renderer.frameView(frame.buffer);
  expect(pixels.valid() && host.present(requestedBounds, frame, pixels, hits),         "replacement generation must accept only its current-epoch first frame");
  secondObservation = host.observe();
  expect(secondObservation.visible && secondObservation.hostAboveDefView,
         "replacement must be visible at the certified desktop layer");

  std::cout << "QTS04_HOST_OBSERVATION topology=raised-progman shellGeneration="
            << secondObservation.shellGeneration
            << " lifecycleEpoch=" << secondObservation.lifecycleEpoch
            << " surfaceEpoch=" << secondObservation.surfaceEpoch
            << " visual=0x" << std::hex << secondObservation.visualHandle << std::dec << '\n';

  if (durationMs > 0) {
    QEventLoop visibleInterval;
    QTimer::singleShot(durationMs, &visibleInterval, &QEventLoop::quit);
    visibleInterval.exec();
  }

  renderer.release(frame.buffer);
  host.setRecoveryCallback({});
  host.beginExit();
  renderer.beginExit();
  const auto exitObservation = host.observe();
  expect(exitObservation.state == contracts::HostState::Exiting &&
             exitObservation.visualHandle == 0 && exitObservation.ownedResourceCount == 0 &&
             !exitObservation.winEventHooksActive,
         "BeginExit must synchronously remove visual, hook, and coordinator ownership");
  host.detach();

  if (failures == 0) {
    std::cout << "QTS04_HOST_ATTACH_RECONNECT_PASS\n";
  }
  return failures == 0 ? 0 : 1;
}
