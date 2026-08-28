#include "adapters/desktop-host-win32/win32_desktop_host.h"
#include "adapters/desktop-host-win32/win32_input_islands.h"

#include <dwmapi.h>
#include <windows.h>

#include <algorithm>
#include <cstdint>
#include <cstring>
#include <cwchar>
#include <iterator>
#include <mutex>
#include <string_view>
#include <unordered_map>
#include <utility>
#include <vector>

namespace desktopup::adapters {

namespace {

using desktopup::contracts::DisplayEnvironment;
using desktopup::contracts::HostMaintenanceResult;
using desktopup::contracts::HostObservation;
using desktopup::contracts::HostState;
using desktopup::contracts::HostTopology;
using desktopup::contracts::PixelRect;
using desktopup::contracts::PixelSize;

constexpr wchar_t kCoordinatorClass[] = L"DesktopUp.V2.Host.Coordinator.QTS04";
constexpr wchar_t kVisualClass[] = L"DesktopUpV2QTS04HostLayeredVisual";
constexpr UINT kShellTopologyChanged = WM_APP + 0x421;
constexpr UINT_PTR kTopologyValidationTimer = 0xD044;
constexpr UINT kTopologyStabilityMs = 120;


struct ShellTarget {
  HWND progman{};
  HWND iconHost{};
  HWND defView{};
  HWND iconView{};
  DWORD explorerProcessId{};
  HostTopology topology{HostTopology::Unknown};
  bool safe{};
  bool unique{};
};

[[nodiscard]] bool processDeclaresLayeredChildCompatibility() noexcept {
  const HRSRC resource = FindResourceW(
      nullptr, MAKEINTRESOURCEW(CREATEPROCESS_MANIFEST_RESOURCE_ID), RT_MANIFEST);
  if (resource == nullptr) return false;
  const HGLOBAL loaded = LoadResource(nullptr, resource);
  const auto* bytes = static_cast<const char*>(LockResource(loaded));
  const DWORD size = SizeofResource(nullptr, resource);
  if (loaded == nullptr || bytes == nullptr || size == 0) return false;
  constexpr std::string_view windows8Compatibility =
      "{4a2f28e3-53b9-4441-ba9c-d69d4a4a6e38}";
  return std::string_view(bytes, size).find(windows8Compatibility) !=
         std::string_view::npos;
}

[[nodiscard]] bool isWindowCloaked(HWND window) noexcept {
  DWORD cloaked = 0;
  return SUCCEEDED(DwmGetWindowAttribute(window, DWMWA_CLOAKED, &cloaked, sizeof(cloaked))) &&
         cloaked != 0;
}

[[nodiscard]] PixelRect toPixelRect(const RECT& rect) noexcept {
  return PixelRect{rect.left,
                   rect.top,
                   static_cast<std::uint32_t>(std::max(0L, rect.right - rect.left)),
                   static_cast<std::uint32_t>(std::max(0L, rect.bottom - rect.top))};
}

[[nodiscard]] std::uint64_t handleValue(HWND window) noexcept {
  return static_cast<std::uint64_t>(reinterpret_cast<std::uintptr_t>(window));
}

[[nodiscard]] bool sameTarget(const ShellTarget& left, const ShellTarget& right) noexcept {
  return left.iconHost == right.iconHost && left.defView == right.defView &&
         left.iconView == right.iconView && left.explorerProcessId == right.explorerProcessId &&
         left.safe && right.safe;
}

[[nodiscard]] bool sameDisplayEnvironment(
    const contracts::DisplayEnvironment& left,
    const contracts::DisplayEnvironment& right) noexcept {
  return left.primaryTargetValid && right.primaryTargetValid &&
         left.primaryMonitor == right.primaryMonitor &&
         left.primaryWorkArea == right.primaryWorkArea &&
         left.effectiveDpi == right.effectiveDpi;
}

[[nodiscard]] contracts::DisplayEnvironment queryPrimaryDisplayEnvironmentSnapshot(
    const contracts::DisplayEnvironmentRevision revision) noexcept {
  contracts::DisplayEnvironment result{};
  result.revision = revision;
  MONITORINFO monitorInfo{};
  monitorInfo.cbSize = sizeof(monitorInfo);
  const HMONITOR monitor =
      MonitorFromPoint(POINT{0, 0}, MONITOR_DEFAULTTOPRIMARY);
  if (monitor == nullptr || !GetMonitorInfoW(monitor, &monitorInfo)) {
    return result;
  }
  result.primaryMonitor = toPixelRect(monitorInfo.rcMonitor);
  result.primaryWorkArea = toPixelRect(monitorInfo.rcWork);
  result.effectiveDpi = GetDpiForSystem();
  if (result.effectiveDpi == 0) result.effectiveDpi = 96;
  result.primaryTargetValid =
      result.primaryMonitor.width != 0 && result.primaryMonitor.height != 0 &&
      result.primaryWorkArea.width != 0 && result.primaryWorkArea.height != 0;
  return result;
}

[[nodiscard]] bool isShellTopologyWindow(HWND window) noexcept {
  wchar_t className[64]{};
  if (GetClassNameW(window, className, static_cast<int>(std::size(className))) == 0) {
    return false;
  }
  if (std::wcscmp(className, L"SysListView32") == 0) {
    const HWND parent = GetParent(window);
    wchar_t parentClassName[64]{};
    return parent != nullptr &&
           GetClassNameW(parent,
                         parentClassName,
                         static_cast<int>(std::size(parentClassName))) != 0 &&
           std::wcscmp(parentClassName, L"SHELLDLL_DefView") == 0;
  }

  return std::wcscmp(className, L"Progman") == 0 ||
         std::wcscmp(className, L"WorkerW") == 0 ||
         std::wcscmp(className, L"SHELLDLL_DefView") == 0 ||
         std::wcscmp(className, L"Shell_TrayWnd") == 0;
}

[[nodiscard]] ShellTarget discoverShellTarget() noexcept {
  struct CandidateContext {
    std::vector<HWND> defViews;
  } context;

  EnumWindows(
      [](HWND topLevel, LPARAM parameter) -> BOOL {
        auto* candidates = reinterpret_cast<CandidateContext*>(parameter);
        const HWND defView = FindWindowExW(topLevel, nullptr, L"SHELLDLL_DefView", nullptr);
        if (defView != nullptr && IsWindowVisible(defView) && !isWindowCloaked(defView)) {
          candidates->defViews.push_back(defView);
        }
        return TRUE;
      },
      reinterpret_cast<LPARAM>(&context));

  ShellTarget result{};
  result.progman = FindWindowW(L"Progman", nullptr);
  result.unique = context.defViews.size() == 1;
  if (!result.unique) {
    result.topology = HostTopology::NoSafeHost;
    return result;
  }

  result.defView = context.defViews.front();
  result.iconHost = GetParent(result.defView);
  result.iconView = FindWindowExW(result.defView, nullptr, L"SysListView32", L"FolderView");
  if (result.iconView == nullptr) {
    result.iconView = FindWindowExW(result.defView, nullptr, L"SysListView32", nullptr);
  }
  GetWindowThreadProcessId(result.defView, &result.explorerProcessId);

  if (result.iconHost == result.progman && result.progman != nullptr) {
    result.topology = HostTopology::RaisedProgman;
  } else {
    wchar_t className[64]{};
    if (result.iconHost != nullptr) {
      GetClassNameW(result.iconHost, className, static_cast<int>(std::size(className)));
    }
    result.topology = std::wcscmp(className, L"WorkerW") == 0
                          ? HostTopology::TraditionalWorkerW
                          : HostTopology::Unknown;
  }

  DWORD taskbarProcessId = 0;
  if (const HWND taskbar = FindWindowW(L"Shell_TrayWnd", nullptr); taskbar != nullptr) {
    GetWindowThreadProcessId(taskbar, &taskbarProcessId);
  }

  // QTS-04-01 inherits the already-certified raised-Progman topology. The traditional
  // WorkerW variant is recognized but deliberately fails closed until separately proven.
  result.safe = result.topology == HostTopology::RaisedProgman && result.iconHost != nullptr &&
                result.defView != nullptr && result.iconView != nullptr &&
                result.explorerProcessId != 0 &&
                (taskbarProcessId == 0 || taskbarProcessId == result.explorerProcessId);
  return result;
}

}  // namespace

struct Win32DesktopHost::Impl {
  Impl() : ownerThreadId(GetCurrentThreadId()) {}

  ~Impl() { beginExit(); }

  [[nodiscard]] bool onOwnerThread() const noexcept {
    return GetCurrentThreadId() == ownerThreadId;
  }

  [[nodiscard]] bool ensureInfrastructure() {
    if (!onOwnerThread() || exitStarted) {
      return false;
    }

    std::call_once(classRegistrationOnce, [] {
      WNDCLASSEXW coordinatorClass{};
      coordinatorClass.cbSize = sizeof(coordinatorClass);
      coordinatorClass.hInstance = GetModuleHandleW(nullptr);
      coordinatorClass.lpfnWndProc = &Impl::coordinatorWindowProcedure;
      coordinatorClass.lpszClassName = kCoordinatorClass;
      RegisterClassExW(&coordinatorClass);
    });

    return true;
  }

  [[nodiscard]] bool installShellEventHook() {
    if (exitStarted) return false;
    if (coordinator == nullptr) {
      coordinator = CreateWindowExW(0,
                                    kCoordinatorClass,
                                    L"",
                                    0,
                                    0,
                                    0,
                                    0,
                                    0,
                                    HWND_MESSAGE,
                                    nullptr,
                                    GetModuleHandleW(nullptr),
                                    this);
    }
    if (coordinator == nullptr) {
      nativeError = GetLastError();
      return false;
    }
    if (shellEventHook == nullptr) {
      shellEventHook = SetWinEventHook(EVENT_OBJECT_CREATE,
                                      EVENT_OBJECT_DESTROY,
                                      nullptr,
                                      &Impl::shellWinEventProcedure,
                                      0,
                                      0,
                                      WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
      if (shellEventHook != nullptr) {
        const std::scoped_lock lock(hookMapMutex);
        hookCoordinators[shellEventHook] = coordinator;
      }
    }
    if (shellEventHook == nullptr) {
      nativeError = GetLastError();
      DestroyWindow(coordinator);
      coordinator = nullptr;
      return false;
    }
    return true;
  }

  void releaseInfrastructure() noexcept {
    if (coordinator != nullptr) {
      KillTimer(coordinator, kTopologyValidationTimer);
    }
    if (shellEventHook != nullptr) {
      {
        const std::scoped_lock lock(hookMapMutex);
        hookCoordinators.erase(shellEventHook);
      }
      UnhookWinEvent(shellEventHook);
      shellEventHook = nullptr;
    }
    if (coordinator != nullptr) {
      DestroyWindow(coordinator);
      coordinator = nullptr;
    }
  }

  [[nodiscard]] bool createHiddenVisual() {
    if (!target.safe || target.iconHost == nullptr || requestedBounds.width == 0 ||
        requestedBounds.height == 0 || visual != nullptr) {
      nativeError = 0xD001;
      return false;
    }

    if (!processDeclaresLayeredChildCompatibility()) {
      nativeError = 0xD002;
      return false;
    }

    if (!visualClassRegistered) {
      WNDCLASSEXW visualClass{};
      visualClass.cbSize = sizeof(visualClass);
      visualClass.lpfnWndProc = &Impl::visualWindowProcedure;
      visualClass.hInstance = GetModuleHandleW(nullptr);
      visualClass.hCursor = LoadCursorW(nullptr, IDC_ARROW);
      visualClass.lpszClassName = kVisualClass;
      if (RegisterClassExW(&visualClass) == 0) {
        const DWORD registrationError = GetLastError();
        WNDCLASSEXW existing{};
        existing.cbSize = sizeof(existing);
        const bool ownsExistingClass =
            registrationError == ERROR_CLASS_ALREADY_EXISTS &&
            GetClassInfoExW(GetModuleHandleW(nullptr), kVisualClass, &existing) != FALSE &&
            existing.lpfnWndProc == &Impl::visualWindowProcedure;
        if (!ownsExistingClass) {
          nativeError = registrationError;
          return false;
        }
      }
      visualClassRegistered = true;
    }

    SetLastError(ERROR_SUCCESS);
    visual = CreateWindowExW(WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE |
                                 WS_EX_TOOLWINDOW,
                             kVisualClass,
                             L"DesktopUp V2 QTS-04 Layered Visual",
                             WS_CHILD | WS_CLIPSIBLINGS,
                             0,
                             0,
                             1,
                             1,
                             target.iconHost,
                             nullptr,
                             GetModuleHandleW(nullptr),
                             this);
    if (visual == nullptr) {
      nativeError = GetLastError();
      if (nativeError == ERROR_SUCCESS) {
        nativeError = 0xD003;
      }
      return false;
    }
    if (!SetLayeredWindowAttributes(visual, 0, 238, LWA_ALPHA)) {
      nativeError = GetLastError();
      DestroyWindow(visual);
      visual = nullptr;
      return false;
    }
    if (!inputIslands.initialize(target.iconHost, visual)) {
      nativeError = 0xD006;
      DestroyWindow(visual);
      visual = nullptr;
      return false;
    }
    if (!SetWindowPos(visual,
                      HWND_TOP,
                      0,
                      0,
                      0,
                      0,
                      SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_HIDEWINDOW)) {
      nativeError = GetLastError();
      DestroyWindow(visual);
      visual = nullptr;
      return false;
    }
    // The 1x1 child is intentionally invisible. Requested geometry is applied while still
    // hidden in present(), then the complete first frame is atomically revealed.
    state = HostState::AttachedSafe;
    firstFramePresented = false;
    return true;
  }

  void destroyVisual() noexcept {
    inputIslands.clear();
    if (visual != nullptr) {
      ShowWindow(visual, SW_HIDE);
      DestroyWindow(visual);
      visual = nullptr;
    }
    firstFramePresented = false;    paintPixels.clear();
    paintSize = {};
    paintSucceeded = false;
  }

  [[nodiscard]] bool attach(contracts::SurfaceEpoch epoch, PixelRect bounds) {
    if (!onOwnerThread() || exitStarted || epoch == 0 || bounds.width == 0 ||
        bounds.height == 0) {
      return false;
    }

    detachSurfaceOnly();
    state = HostState::Discovering;
    nativeError = ERROR_SUCCESS;
    if (!ensureInfrastructure()) {
      state = HostState::Faulted;
      return false;
    }

    auto currentDisplay = queryPrimaryDisplayEnvironmentSnapshot(
        displayEnvironment.revision == 0 ? 1 : displayEnvironment.revision);
    if (!currentDisplay.primaryTargetValid) {
      nativeError = ERROR_NOT_FOUND;
      state = HostState::Faulted;
      return false;
    }
    if (displayEnvironment.primaryTargetValid &&
        !sameDisplayEnvironment(displayEnvironment, currentDisplay)) {
      currentDisplay.revision = displayEnvironment.revision + 1;
    } else if (displayEnvironment.primaryTargetValid) {
      currentDisplay.revision = displayEnvironment.revision;
    }
    displayEnvironment = currentDisplay;

    target = discoverShellTarget();
    if (!target.safe) {
      nativeError = ERROR_NOT_FOUND;
      state = HostState::DegradedNoDesktopHost;
      return false;
    }

    state = HostState::Attaching;
    requestedBounds = bounds;
    surfaceEpoch = epoch;
    ++shellGeneration;
    ++lifecycleEpoch;
    if (!createHiddenVisual()) {
      state = HostState::Faulted;
      return false;
    }
    if (!installShellEventHook()) {
      destroyVisual();
      releaseInfrastructure();
      state = HostState::Faulted;
      return false;
    }
    return true;
  }

  [[nodiscard]] bool paintVisualImmediately() {
    if (visual == nullptr || paintPixels.empty() || paintSize.width == 0 ||
        paintSize.height == 0) {
      return false;
    }

    HDC dc = GetDC(visual);
    if (dc == nullptr) {
      return false;
    }
    BITMAPINFO bitmapInfo{};
    bitmapInfo.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bitmapInfo.bmiHeader.biWidth = static_cast<LONG>(paintSize.width);
    bitmapInfo.bmiHeader.biHeight = -static_cast<LONG>(paintSize.height);
    bitmapInfo.bmiHeader.biPlanes = 1;
    bitmapInfo.bmiHeader.biBitCount = 32;
    bitmapInfo.bmiHeader.biCompression = BI_RGB;
    const int copied = StretchDIBits(
        dc,
        0,
        0,
        static_cast<int>(paintSize.width),
        static_cast<int>(paintSize.height),
        0,
        0,
        static_cast<int>(paintSize.width),
        static_cast<int>(paintSize.height),
        paintPixels.data(),
        &bitmapInfo,
        DIB_RGB_COLORS,
        SRCCOPY);
    const bool released = ReleaseDC(visual, dc) != 0;
    ValidateRect(visual, nullptr);
    return released && copied != 0 && copied != GDI_ERROR;
  }

  [[nodiscard]] bool present(const PixelRect screenBounds,
                             const contracts::FrameDescriptor& frame,
                             const contracts::PremultipliedFrameView& pixels,
                             const contracts::HitRegionSet& hitRegions) {
    if (!onOwnerThread() || exitStarted || state != HostState::AttachedSafe ||
        visual == nullptr || frame.surfaceEpoch != surfaceEpoch || frame.buffer == 0 ||
        screenBounds.width == 0 || screenBounds.height == 0 ||
        screenBounds.size() != frame.bounds ||
        !pixels.valid() || pixels.buffer != frame.buffer || pixels.bounds != frame.bounds ||
        hitRegions.revision == 0 || hitRegions.projectionRevision == 0 ||
        hitRegions.visualBounds != frame.bounds) {
      return false;
    }

    // Prepare the complete replacement frame before mutating any visible HWND.
    // This removes the former interval where Windows could composite the new
    // window size while CPU conversion still operated on the new pixels.
    const std::size_t rowBytes = static_cast<std::size_t>(frame.bounds.width) * 4U;
    std::vector<std::uint8_t> stagedPixels(
        rowBytes * static_cast<std::size_t>(frame.bounds.height));
    for (std::uint32_t row = 0; row < frame.bounds.height; ++row) {
      const auto* source =
          pixels.pixels + static_cast<std::size_t>(row) * pixels.strideBytes;
      auto* destination =
          stagedPixels.data() + static_cast<std::size_t>(row) * rowBytes;
      for (std::uint32_t column = 0; column < frame.bounds.width; ++column) {
        const std::uint8_t alpha = source[column * 4U + 3U];
        const auto unpremultiply = [alpha](const std::uint8_t channel) {
          if (alpha == 0) return static_cast<std::uint8_t>(0);
          return static_cast<std::uint8_t>(std::min(
              255U,
              (static_cast<unsigned int>(channel) * 255U + alpha / 2U) /
                  static_cast<unsigned int>(alpha)));
        };
        destination[column * 4U + 0U] = unpremultiply(source[column * 4U + 0U]);
        destination[column * 4U + 1U] = unpremultiply(source[column * 4U + 1U]);
        destination[column * 4U + 2U] = unpremultiply(source[column * 4U + 2U]);
        destination[column * 4U + 3U] = 0;
      }
    }

    const int regionDiameter = std::max(
        2, static_cast<int>((48ULL * frame.bounds.width + 210ULL) / 420ULL));
    HRGN region = CreateRoundRectRgn(
        2,
        2,
        static_cast<int>(frame.bounds.width) - 2,
        static_cast<int>(frame.bounds.height) - 2,
        regionDiameter,
        regionDiameter);
    if (region == nullptr) {
      nativeError = GetLastError();
      if (nativeError == ERROR_SUCCESS) nativeError = 0xD004;
      return false;
    }

    paintPixels.swap(stagedPixels);
    paintSize = frame.bounds;
    if (SetWindowRgn(visual, region, FALSE) == 0) {
      DeleteObject(region);
      nativeError = GetLastError();
      if (nativeError == ERROR_SUCCESS) nativeError = 0xD004;
      return false;
    }

    // Win32 has no single API that combines GDI pixels, window region and
    // child input HWNDs. The Host therefore commits them as one owner-thread
    // transaction: visual + islands share one DeferWindowPos batch with
    // SWP_NOREDRAW, followed immediately by the synchronous staged-frame paint.
    // Stable frames do not repeat HWND_TOP or SWP_SHOWWINDOW.
    const bool wasPresented = firstFramePresented;
    if (!inputIslands.publish(hitRegions, screenBounds, wasPresented)) {
      nativeError = 0xD007;
      inputIslands.clear();
      ShowWindow(visual, SW_HIDE);
      firstFramePresented = false;
      return false;
    }

    paintSucceeded = false;
    paintSucceeded = paintVisualImmediately();
    if (!paintSucceeded) {
      nativeError = 0xD005;
      ShowWindow(visual, SW_HIDE);
      firstFramePresented = false;
      return false;
    }

    if (!wasPresented &&
        !inputIslands.publish(hitRegions, screenBounds, true)) {
      nativeError = 0xD007;
      inputIslands.clear();
      ShowWindow(visual, SW_HIDE);
      firstFramePresented = false;
      return false;
    }

    requestedBounds = screenBounds;
    firstFramePresented = true;
    return true;
  }

  [[nodiscard]] bool relocate(const PixelRect screenBounds,
                              const contracts::HitRegionSet& hitRegions) {
    if (!onOwnerThread() || exitStarted || state != HostState::AttachedSafe ||
        visual == nullptr || !firstFramePresented ||
        screenBounds.width == 0 || screenBounds.height == 0 ||
        screenBounds.size() != requestedBounds.size() ||
        hitRegions.revision == 0 || hitRegions.projectionRevision == 0 ||
        hitRegions.visualBounds != screenBounds.size()) {
      return false;
    }

    if (!inputIslands.relocate(hitRegions, screenBounds)) {
      nativeError = 0xD008;
      return false;
    }
    requestedBounds = screenBounds;
    return true;
  }
  [[nodiscard]] HostMaintenanceResult reconnect() {
    if (!onOwnerThread() || exitStarted || state == HostState::Detached) {
      return exitStarted ? HostMaintenanceResult::Exiting : HostMaintenanceResult::Failed;
    }

    destroyVisual();
    state = HostState::Recovering;
    nativeError = ERROR_SUCCESS;
    ++lifecycleEpoch;

    target = discoverShellTarget();
    if (!target.safe) {
      state = HostState::DegradedNoDesktopHost;
      return HostMaintenanceResult::DegradedNoDesktopHost;
    }

    ++shellGeneration;
    ++surfaceEpoch;
    if (!createHiddenVisual()) {
      state = HostState::Faulted;
      return HostMaintenanceResult::Failed;
    }
    ++shellRecoveryCount;
    return HostMaintenanceResult::ReattachedNeedsFrame;
  }

  [[nodiscard]] HostMaintenanceResult refreshDisplayEnvironment() {
    if (!onOwnerThread() || exitStarted || state == HostState::Detached) {
      return exitStarted ? HostMaintenanceResult::Exiting
                         : HostMaintenanceResult::Failed;
    }
    auto current = queryPrimaryDisplayEnvironmentSnapshot(
        displayEnvironment.revision == 0 ? 1 : displayEnvironment.revision);
    if (!current.primaryTargetValid) {
      return HostMaintenanceResult::Failed;
    }
    if (sameDisplayEnvironment(displayEnvironment, current)) {
      return HostMaintenanceResult::Unchanged;
    }
    current.revision = displayEnvironment.revision + 1;
    displayEnvironment = current;
    ++displayChangeCount;
    return HostMaintenanceResult::DisplayEnvironmentChangedNeedsProjection;
  }

  void notifyMaintenance(const HostMaintenanceResult result) {
    if (result != HostMaintenanceResult::Unchanged && recoveryCallback) {
      recoveryCallback(result);
    }
  }

  void scheduleTopologyValidation(const DWORD processId,
                                  const bool topologyClass) {
    if (!onOwnerThread() || exitStarted || state == HostState::Detached) {
      return;
    }
    const bool relevant =
        topologyClass || processId == target.explorerProcessId ||
        state == HostState::DegradedNoDesktopHost ||
        state == HostState::ShellGenerationLost ||
        state == HostState::Recovering;
    if (!relevant) {
      ++ignoredShellEventCount;
      return;
    }
    ++relevantShellEventCount;
    if (coordinator == nullptr ||
        SetTimer(coordinator, kTopologyValidationTimer, kTopologyStabilityMs,
                 nullptr) == 0) {
      notifyMaintenance(handleTopologyEvent());
    }
  }

  [[nodiscard]] HostMaintenanceResult handleTopologyEvent() {
    if (!onOwnerThread() || exitStarted || state == HostState::Detached) {
      return exitStarted ? HostMaintenanceResult::Exiting
                         : HostMaintenanceResult::Unchanged;
    }

    ++topologyValidationCount;
    const ShellTarget discovered = discoverShellTarget();
    if (visual != nullptr && IsWindow(visual) && sameTarget(target, discovered)) {
      return HostMaintenanceResult::Unchanged;
    }
    if (visual == nullptr && !target.safe && !discovered.safe) {
      return HostMaintenanceResult::Unchanged;
    }

    const bool generationChanged = !sameTarget(target, discovered);
    destroyVisual();
    state = HostState::ShellGenerationLost;
    ++lifecycleEpoch;
    target = discovered;
    if (!target.safe) {
      state = HostState::DegradedNoDesktopHost;
      return HostMaintenanceResult::DegradedNoDesktopHost;
    }

    state = HostState::Recovering;
    if (generationChanged) ++shellGeneration;
    ++surfaceEpoch;
    if (!createHiddenVisual()) {
      state = HostState::Faulted;
      return HostMaintenanceResult::Failed;
    }
    ++shellRecoveryCount;
    return HostMaintenanceResult::ReattachedNeedsFrame;
  }

  [[nodiscard]] HostObservation observe() const noexcept {
    HostObservation observation{};
    observation.state = state;
    observation.topology = target.topology;
    observation.safeTargetUnique = target.safe && target.unique;
    observation.attached = visual != nullptr && state == HostState::AttachedSafe;
    observation.shellGeneration = shellGeneration;
    observation.lifecycleEpoch = lifecycleEpoch;
    observation.surfaceEpoch = surfaceEpoch;
    observation.requestedBounds = requestedBounds;
    observation.explorerProcessId = target.explorerProcessId;
    observation.visualHandle = handleValue(visual);
    observation.parentHandle = handleValue(GetParent(visual));
    observation.defViewHandle = handleValue(target.defView);
    observation.iconViewHandle = handleValue(target.iconView);
    observation.winEventHooksActive = shellEventHook != nullptr;
    observation.nativeError = nativeError;
    const auto inputObservation = inputIslands.observe();
    observation.ownedResourceCount = static_cast<std::uint32_t>(
        (coordinator != nullptr ? 1 : 0) + (shellEventHook != nullptr ? 1 : 0) +
        (visual != nullptr ? 1 : 0) +
        static_cast<int>(inputObservation.windowHandles.size()));
    observation.layeredChildCompatibilityDeclared =
        processDeclaresLayeredChildCompatibility();
    observation.hostProjectionRevision = inputObservation.projectionRevision;
    observation.hitRegionRevision = inputObservation.hitRevision;
    observation.inputIslandCount =
        static_cast<std::uint32_t>(inputObservation.windowHandles.size());
    observation.displayEnvironment = displayEnvironment;
    observation.relevantShellEventCount = relevantShellEventCount;
    observation.ignoredShellEventCount = ignoredShellEventCount;
    observation.topologyValidationCount = topologyValidationCount;
    observation.shellRecoveryCount = shellRecoveryCount;
    observation.displayChangeCount = displayChangeCount;
    observation.inputEventCount = inputObservation.eventCount;
    observation.inputEnabled = inputObservation.inputEnabled;
    observation.visualPointerTransparent = true;

    if (visual == nullptr) {
      return observation;
    }

    observation.visible = firstFramePresented && IsWindowVisible(visual) != FALSE;
    const LONG_PTR style = GetWindowLongPtrW(visual, GWL_STYLE);
    const LONG_PTR extendedStyle = GetWindowLongPtrW(visual, GWL_EXSTYLE);
    observation.topmost = (extendedStyle & WS_EX_TOPMOST) != 0;
    observation.ordinaryTopLevelFallback = (style & WS_CHILD) == 0;

    int visualIndex = -1;
    int defViewIndex = -1;
    int index = 0;
    for (HWND child = GetWindow(target.iconHost, GW_CHILD); child != nullptr;
         child = GetWindow(child, GW_HWNDNEXT), ++index) {
      if (child == visual) {
        visualIndex = index;
      }
      if (child == target.defView) {
        defViewIndex = index;
      }
    }
    observation.hostAboveDefView =
        visualIndex >= 0 && defViewIndex >= 0 && visualIndex < defViewIndex;

    RECT windowRect{};
    if (GetWindowRect(visual, &windowRect)) {
      observation.windowBounds = toPixelRect(windowRect);
    }

    RECT clientRect{};
    if (GetClientRect(visual, &clientRect)) {
      POINT points[2]{{clientRect.left, clientRect.top}, {clientRect.right, clientRect.bottom}};
      SetLastError(ERROR_SUCCESS);
      if (MapWindowPoints(visual, nullptr, points, 2) != 0 ||
          GetLastError() == ERROR_SUCCESS) {
        observation.clientScreenBounds =
            PixelRect{points[0].x,
                      points[0].y,
                      static_cast<std::uint32_t>(std::max(0L, points[1].x - points[0].x)),
                      static_cast<std::uint32_t>(std::max(0L, points[1].y - points[0].y))};
      }
    }

    RECT extendedFrame{};
    if (SUCCEEDED(DwmGetWindowAttribute(visual,
                                        DWMWA_EXTENDED_FRAME_BOUNDS,
                                        &extendedFrame,
                                        sizeof(extendedFrame)))) {
      observation.extendedFrameBounds = toPixelRect(extendedFrame);
    } else {
      observation.extendedFrameBounds = observation.windowBounds;
    }
    return observation;
  }

  void detachSurfaceOnly() noexcept {
    destroyVisual();
    target = {};
    requestedBounds = {};
    surfaceEpoch = 0;
    if (!exitStarted) {
      state = HostState::Detached;
    }
  }

  void detach() noexcept {
    if (!onOwnerThread()) {
      return;
    }
    detachSurfaceOnly();
    releaseInfrastructure();
  }

  void beginExit() noexcept {
    if (exitStarted || !onOwnerThread()) {
      return;
    }
    exitStarted = true;
    recoveryCallback = {};
    state = HostState::Exiting;
    destroyVisual();
    releaseInfrastructure();
    target = {};
    requestedBounds = {};
    surfaceEpoch = 0;
  }

  static LRESULT CALLBACK coordinatorWindowProcedure(HWND window,
                                                      UINT message,
                                                      WPARAM wParam,
                                                      LPARAM lParam) {
    Impl* instance = reinterpret_cast<Impl*>(GetWindowLongPtrW(window, GWLP_USERDATA));
    if (message == WM_NCCREATE) {
      const auto* creation = reinterpret_cast<const CREATESTRUCTW*>(lParam);
      instance = static_cast<Impl*>(creation->lpCreateParams);
      SetWindowLongPtrW(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(instance));
    }
    if (message == kShellTopologyChanged && instance != nullptr) {
      instance->scheduleTopologyValidation(
          static_cast<DWORD>(lParam), wParam != 0);
      return 0;
    }
    if (message == WM_TIMER && instance != nullptr &&
        wParam == kTopologyValidationTimer) {
      KillTimer(window, kTopologyValidationTimer);
      instance->notifyMaintenance(instance->handleTopologyEvent());
      return 0;
    }
    return DefWindowProcW(window, message, wParam, lParam);
  }

  static LRESULT CALLBACK visualWindowProcedure(HWND window,
                                                 UINT message,
                                                 WPARAM wParam,
                                                 LPARAM lParam) {
    Impl* instance = reinterpret_cast<Impl*>(GetWindowLongPtrW(window, GWLP_USERDATA));
    if (message == WM_NCCREATE) {
      const auto* creation = reinterpret_cast<const CREATESTRUCTW*>(lParam);
      instance = static_cast<Impl*>(creation->lpCreateParams);
      SetWindowLongPtrW(window,
                        GWLP_USERDATA,
                        reinterpret_cast<LONG_PTR>(instance));
      if (instance != nullptr) instance->visual = window;
    }
    switch (message) {
      case WM_MOUSEACTIVATE:
        return MA_NOACTIVATE;
      case WM_ERASEBKGND:
        return 1;
      case WM_PAINT: {
        PAINTSTRUCT paint{};
        HDC dc = BeginPaint(window, &paint);
        bool painted = false;
        if (instance != nullptr && dc != nullptr && !instance->paintPixels.empty() &&
            instance->paintSize.width != 0 && instance->paintSize.height != 0) {
          BITMAPINFO bitmapInfo{};
          bitmapInfo.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
          bitmapInfo.bmiHeader.biWidth = static_cast<LONG>(instance->paintSize.width);
          bitmapInfo.bmiHeader.biHeight = -static_cast<LONG>(instance->paintSize.height);
          bitmapInfo.bmiHeader.biPlanes = 1;
          bitmapInfo.bmiHeader.biBitCount = 32;
          bitmapInfo.bmiHeader.biCompression = BI_RGB;
          const int copied = StretchDIBits(
              dc,
              0,
              0,
              static_cast<int>(instance->paintSize.width),
              static_cast<int>(instance->paintSize.height),
              0,
              0,
              static_cast<int>(instance->paintSize.width),
              static_cast<int>(instance->paintSize.height),
              instance->paintPixels.data(),
              &bitmapInfo,
              DIB_RGB_COLORS,
              SRCCOPY);
          painted = copied != 0 && copied != GDI_ERROR;
        }        if (instance != nullptr) instance->paintSucceeded = painted;
        EndPaint(window, &paint);
        return 0;
      }
      default:
        return DefWindowProcW(window, message, wParam, lParam);
    }
  }

  static void CALLBACK shellWinEventProcedure(HWINEVENTHOOK hook,
                                               DWORD event,
                                               HWND window,
                                               LONG objectId,
                                               LONG childId,
                                               DWORD,
                                               DWORD) {
    if (event != EVENT_OBJECT_CREATE && event != EVENT_OBJECT_DESTROY) {
      return;
    }
    if (window == nullptr || objectId != OBJID_WINDOW || childId != CHILDID_SELF) {
      return;
    }
    HWND destination = nullptr;
    {
      const std::scoped_lock lock(hookMapMutex);
      if (const auto found = hookCoordinators.find(hook); found != hookCoordinators.end()) {
        destination = found->second;
      }
    }
    if (destination != nullptr) {
      DWORD processId = 0;
      GetWindowThreadProcessId(window, &processId);
      PostMessageW(destination,
                   kShellTopologyChanged,
                   isShellTopologyWindow(window) ? 1 : 0,
                   static_cast<LPARAM>(processId));
    }
  }

  std::vector<std::uint8_t> paintPixels{};
  PixelSize paintSize{};
  bool paintSucceeded{};
  DWORD ownerThreadId{};
  HWND coordinator{};
  HWINEVENTHOOK shellEventHook{};
  Win32InputIslandSet inputIslands{};
  HWND visual{};
  ShellTarget target{};
  PixelRect requestedBounds{};
  std::uint64_t shellGeneration{};
  std::uint64_t lifecycleEpoch{};
  contracts::SurfaceEpoch surfaceEpoch{};
  HostState state{HostState::Detached};
  contracts::DisplayEnvironment displayEnvironment{};
  std::uint64_t relevantShellEventCount{};
  std::uint64_t ignoredShellEventCount{};
  std::uint64_t topologyValidationCount{};
  std::uint64_t shellRecoveryCount{};
  std::uint64_t displayChangeCount{};
  DWORD nativeError{};
  bool visualClassRegistered{};
  bool firstFramePresented{};
  bool exitStarted{};
  RecoveryCallback recoveryCallback{};

  static std::once_flag classRegistrationOnce;
  static std::mutex hookMapMutex;
  static std::unordered_map<HWINEVENTHOOK, HWND> hookCoordinators;
};

std::once_flag Win32DesktopHost::Impl::classRegistrationOnce;
std::mutex Win32DesktopHost::Impl::hookMapMutex;
std::unordered_map<HWINEVENTHOOK, HWND> Win32DesktopHost::Impl::hookCoordinators;

Win32DesktopHost::Win32DesktopHost() : impl_(std::make_unique<Impl>()) {}

Win32DesktopHost::~Win32DesktopHost() = default;

bool Win32DesktopHost::attach(contracts::SurfaceEpoch epoch,
                              contracts::PixelRect screenBounds) {
  return impl_->attach(epoch, screenBounds);
}

bool Win32DesktopHost::present(const contracts::PixelRect screenBounds,
                               const contracts::FrameDescriptor& frame,
                               const contracts::PremultipliedFrameView& pixels,
                               const contracts::HitRegionSet& hitRegions) {
  return impl_->present(screenBounds, frame, pixels, hitRegions);
}

bool Win32DesktopHost::relocate(
    const contracts::PixelRect screenBounds,
    const contracts::HitRegionSet& hitRegions) {
  return impl_->relocate(screenBounds, hitRegions);
}

void Win32DesktopHost::setInputCallback(InputCallback callback) {
  impl_->inputIslands.setInputCallback(std::move(callback));
}

contracts::HostMaintenanceResult Win32DesktopHost::requestReconnect() {
  const auto result = impl_->reconnect();
  if (impl_->recoveryCallback) {
    impl_->recoveryCallback(result);
  }
  return result;
}

contracts::HostMaintenanceResult Win32DesktopHost::refreshDisplayEnvironment() {
  const auto result = impl_->refreshDisplayEnvironment();
  if (result != contracts::HostMaintenanceResult::Unchanged &&
      impl_->recoveryCallback) {
    impl_->recoveryCallback(result);
  }
  return result;
}

contracts::HostObservation Win32DesktopHost::observe() const noexcept {
  return impl_->observe();
}

void Win32DesktopHost::beginExit() noexcept {
  impl_->beginExit();
}

void Win32DesktopHost::detach() noexcept {
  impl_->detach();
}

void Win32DesktopHost::setRecoveryCallback(RecoveryCallback callback) {
  impl_->recoveryCallback = std::move(callback);
}

contracts::PixelRect Win32DesktopHost::centeredPrimaryWorkArea(
    contracts::PixelSize size) noexcept {
  RECT workArea{};
  if (!SystemParametersInfoW(SPI_GETWORKAREA, 0, &workArea, 0)) {
    workArea = RECT{0, 0, GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN)};
  }
  const int availableWidth = std::max(0L, workArea.right - workArea.left);
  const int availableHeight = std::max(0L, workArea.bottom - workArea.top);
  const int requestedWidth = static_cast<int>(size.width);
  const int requestedHeight = static_cast<int>(size.height);
  return contracts::PixelRect{
      workArea.left + std::max(0, (availableWidth - requestedWidth) / 2),
      workArea.top + std::max(0, (availableHeight - requestedHeight) / 2),
      size.width,
      size.height};
}

contracts::DisplayEnvironment Win32DesktopHost::primaryDisplayEnvironment() noexcept {
  return queryPrimaryDisplayEnvironmentSnapshot(1);
}

}  // namespace desktopup::adapters
