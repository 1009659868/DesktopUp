#include "adapters/desktop-host-win32/win32_input_islands.h"

#include <windowsx.h>

#include <algorithm>
#include <cstdint>
#include <cwchar>
#include <mutex>
#include <unordered_set>
#include <utility>

namespace desktopup::adapters {

namespace {

constexpr wchar_t kIslandClass[] = L"DesktopUpV2QTS04InputIsland";

[[nodiscard]] std::uint64_t handleValue(const HWND window) noexcept {
  return static_cast<std::uint64_t>(reinterpret_cast<std::uintptr_t>(window));
}

[[nodiscard]] bool regionFits(const contracts::HitRegion& region,
                              const contracts::PixelSize bounds) noexcept {
  if (region.id == 0) return false;
  if (!region.enabled) return true;
  if (region.bounds.width == 0 || region.bounds.height == 0 ||
      region.bounds.left < 0 || region.bounds.top < 0) {
    return false;
  }
  const auto right = static_cast<std::uint64_t>(region.bounds.left) + region.bounds.width;
  const auto bottom = static_cast<std::uint64_t>(region.bounds.top) + region.bounds.height;
  return right <= bounds.width && bottom <= bounds.height;
}

[[nodiscard]] LPCWSTR cursorForRole(const contracts::HitRegionRole role) noexcept {
  switch (role) {
    case contracts::HitRegionRole::ResizeRight:
      return IDC_SIZEWE;
    case contracts::HitRegionRole::ResizeBottom:
      return IDC_SIZENS;
    case contracts::HitRegionRole::ResizeBottomRight:
      return IDC_SIZENWSE;
    case contracts::HitRegionRole::Item:
    case contracts::HitRegionRole::LockToggle:
    case contracts::HitRegionRole::PresentationToggle:
      return IDC_HAND;
    case contracts::HitRegionRole::TitleDrag:
      return IDC_SIZEALL;
    case contracts::HitRegionRole::ItemScroll:
      return IDC_ARROW;
  }
  return IDC_ARROW;
}

}  // namespace

struct Win32InputIslandSet::Impl {
  struct IslandState {
    Impl* owner{};
    contracts::HitRegion region{};
    HWND window{};
  };

  [[nodiscard]] bool initialize(const HWND parentWindow, const HWND visualWindow) {
    if (parentWindow == nullptr || visualWindow == nullptr ||
        GetParent(visualWindow) != parentWindow) {
      return false;
    }

    std::call_once(classRegistrationOnce, [] {
      WNDCLASSEXW windowClass{};
      windowClass.cbSize = sizeof(windowClass);
      windowClass.style = CS_DBLCLKS;
      windowClass.lpfnWndProc = &Impl::windowProcedure;
      windowClass.hInstance = GetModuleHandleW(nullptr);
      windowClass.hCursor = LoadCursorW(nullptr, IDC_ARROW);
      windowClass.lpszClassName = kIslandClass;
      RegisterClassExW(&windowClass);
    });

    WNDCLASSEXW registered{};
    registered.cbSize = sizeof(registered);
    if (GetClassInfoExW(GetModuleHandleW(nullptr), kIslandClass, &registered) == FALSE ||
        registered.lpfnWndProc != &Impl::windowProcedure) {
      return false;
    }

    parent = parentWindow;
    visual = visualWindow;
    return true;
  }

  [[nodiscard]] bool validSet(const contracts::HitRegionSet& set) const noexcept {
    if (parent == nullptr || visual == nullptr || set.revision == 0 ||
        set.projectionRevision == 0 || set.visualBounds.width == 0 ||
        set.visualBounds.height == 0) {
      return false;
    }

    std::unordered_set<contracts::RegionId> ids;
    for (const auto& region : set.regions) {
      if (!regionFits(region, set.visualBounds)) return false;
      if (!ids.insert(region.id).second) return false;
    }
    return true;
  }

  [[nodiscard]] std::vector<contracts::HitRegion> orderedRegions(
      const contracts::HitRegionSet& set) const {
    std::vector<contracts::HitRegion> ordered = set.regions;
    std::stable_sort(ordered.begin(), ordered.end(),
                     [](const auto& left, const auto& right) {
                       if (left.priority != right.priority) {
                         return left.priority < right.priority;
                       }
                       return left.id < right.id;
                     });
    return ordered;
  }

  [[nodiscard]] bool canUpdateInPlace(
      const std::vector<contracts::HitRegion>& ordered) const noexcept {
    if (ordered.size() != islands.size()) return false;
    for (std::size_t index = 0; index < ordered.size(); ++index) {
      if (islands[index]->region.id != ordered[index].id ||
          islands[index]->region.role != ordered[index].role) {
        return false;
      }
    }
    return true;
  }

  [[nodiscard]] POINT parentOrigin(const contracts::PixelRect screenBounds) const noexcept {
    POINT origin{screenBounds.left, screenBounds.top};
    SetLastError(ERROR_SUCCESS);
    if (MapWindowPoints(nullptr, parent, &origin, 1) == 0 &&
        GetLastError() != ERROR_SUCCESS) {
      return POINT{0, 0};
    }
    return origin;
  }

  [[nodiscard]] bool placeAndOrder(
      const std::vector<contracts::HitRegion>& ordered,
      const contracts::PixelRect screenBounds,
      const bool show) {
    const POINT origin = parentOrigin(screenBounds);
    HDWP batch = BeginDeferWindowPos(static_cast<int>(ordered.size() + 1U));
    if (batch == nullptr) return false;
    const bool visualVisible = IsWindowVisible(visual) != FALSE;
    const UINT visualVisibility =
        visualVisible == show ? 0U : (show ? SWP_SHOWWINDOW : SWP_HIDEWINDOW);
    batch = DeferWindowPos(batch,
                           visual,
                           nullptr,
                           origin.x,
                           origin.y,
                           static_cast<int>(screenBounds.width),
                           static_cast<int>(screenBounds.height),
                           SWP_NOACTIVATE | SWP_NOZORDER | SWP_NOREDRAW |
                               visualVisibility);
    if (batch == nullptr) return false;
    for (std::size_t index = 0; index < ordered.size(); ++index) {
      const auto& bounds = ordered[index].bounds;
      const bool regionVisible = show && ordered[index].enabled;
      const bool islandVisible =
          IsWindowVisible(islands[index]->window) != FALSE;
      const UINT visibility =
          islandVisible == regionVisible
              ? 0U
              : (regionVisible ? SWP_SHOWWINDOW : SWP_HIDEWINDOW);
      batch = DeferWindowPos(batch, islands[index]->window,
                             nullptr,
                             origin.x + bounds.left,
                             origin.y + bounds.top,
                             static_cast<int>(std::max<std::uint32_t>(1, bounds.width)),
                             static_cast<int>(std::max<std::uint32_t>(1, bounds.height)),
                             SWP_NOACTIVATE | SWP_NOZORDER | SWP_NOREDRAW |
                                 visibility);
      if (batch == nullptr) return false;
    }
    return EndDeferWindowPos(batch) != FALSE;
  }

  [[nodiscard]] bool establishZOrder(
      const std::vector<contracts::HitRegion>& ordered) {
    if (!SetWindowPos(visual,
                      HWND_TOP,
                      0,
                      0,
                      0,
                      0,
                      SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE |
                          SWP_NOREDRAW)) {
      return false;
    }
    for (std::size_t index = 0; index < ordered.size(); ++index) {
      if (!SetWindowPos(islands[index]->window,
                        HWND_TOP,
                        0,
                        0,
                        0,
                        0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE |
                            SWP_NOREDRAW)) {
        return false;
      }
    }
    return true;
  }

  [[nodiscard]] bool replace(
      const std::vector<contracts::HitRegion>& ordered,
      const contracts::PixelRect screenBounds,
      const bool reveal) {
    std::vector<std::unique_ptr<IslandState>> previous = std::move(islands);
    std::vector<std::unique_ptr<IslandState>> replacement;
    replacement.reserve(ordered.size());

    const POINT origin = parentOrigin(screenBounds);
    for (const auto& region : ordered) {
      auto state = std::make_unique<IslandState>();
      state->owner = this;
      state->region = region;
      const std::wstring title = L"DesktopUp Input Region " + std::to_wstring(region.id);
      state->window = CreateWindowExW(WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW,
                                      kIslandClass,
                                      title.c_str(),
                                      WS_CHILD | WS_CLIPSIBLINGS,
                                      origin.x + region.bounds.left,
                                      origin.y + region.bounds.top,
                                      static_cast<int>(
                                          std::max<std::uint32_t>(1, region.bounds.width)),
                                      static_cast<int>(
                                          std::max<std::uint32_t>(1, region.bounds.height)),
                                      parent,
                                      nullptr,
                                      GetModuleHandleW(nullptr),
                                      state.get());
      if (state->window == nullptr ||
          !SetLayeredWindowAttributes(state->window, 0, 1, LWA_ALPHA)) {
        if (state->window != nullptr) DestroyWindow(state->window);
        for (auto iterator = replacement.rbegin(); iterator != replacement.rend(); ++iterator) {
          if ((*iterator)->window != nullptr) DestroyWindow((*iterator)->window);
        }
        islands = std::move(previous);
        return false;
      }
      replacement.push_back(std::move(state));
    }

    islands = std::move(replacement);
    if (!placeAndOrder(ordered, screenBounds, reveal)) {
      for (auto iterator = islands.rbegin(); iterator != islands.rend(); ++iterator) {
        if ((*iterator)->window != nullptr) DestroyWindow((*iterator)->window);
      }
      islands = std::move(previous);
      return false;
    }
    if (reveal && !establishZOrder(ordered)) {
      for (auto iterator = islands.rbegin(); iterator != islands.rend(); ++iterator) {
        if ((*iterator)->window != nullptr) DestroyWindow((*iterator)->window);
      }
      islands = std::move(previous);
      return false;
    }

    for (auto iterator = previous.rbegin(); iterator != previous.rend(); ++iterator) {
      if ((*iterator)->window != nullptr && IsWindow((*iterator)->window)) {
        DestroyWindow((*iterator)->window);
      }
    }
    return true;
  }

  [[nodiscard]] bool publish(const contracts::HitRegionSet& set,
                             const contracts::PixelRect screenBounds,
                             const bool reveal) {
    if (!validSet(set) || set.visualBounds != screenBounds.size()) return false;
    auto ordered = orderedRegions(set);
    if (canUpdateInPlace(ordered)) {
      const bool firstReveal =
          reveal && IsWindowVisible(visual) == FALSE;
      if (!placeAndOrder(ordered, screenBounds, reveal)) return false;
      if (firstReveal && !establishZOrder(ordered)) return false;
      for (std::size_t index = 0; index < ordered.size(); ++index) {
        islands[index]->region = ordered[index];
      }
    } else if (!replace(ordered, screenBounds, reveal)) {
      return false;
    }

    currentSet = set;
    currentBounds = screenBounds;
    return true;
  }

  [[nodiscard]] bool relocate(const contracts::HitRegionSet& set,
                              const contracts::PixelRect screenBounds) {
    if (!validSet(set) || currentSet.revision == 0 ||
        currentBounds.size() != screenBounds.size() ||
        set.visualBounds != screenBounds.size() ||
        set.visualBounds != currentSet.visualBounds || visual == nullptr ||
        IsWindow(visual) == FALSE || IsWindowVisible(visual) == FALSE) {
      return false;
    }

    auto ordered = orderedRegions(set);
    if (!canUpdateInPlace(ordered)) return false;
    for (std::size_t index = 0; index < ordered.size(); ++index) {
      if (ordered[index].bounds != islands[index]->region.bounds) return false;
    }

    const POINT origin = parentOrigin(screenBounds);
    HDWP batch = BeginDeferWindowPos(static_cast<int>(ordered.size() + 1U));
    if (batch == nullptr) return false;
    batch = DeferWindowPos(batch,
                           visual,
                           nullptr,
                           origin.x,
                           origin.y,
                           0,
                           0,
                           SWP_NOACTIVATE | SWP_NOZORDER | SWP_NOSIZE);
    if (batch == nullptr) return false;
    for (std::size_t index = 0; index < ordered.size(); ++index) {
      const auto& bounds = ordered[index].bounds;
      batch = DeferWindowPos(batch,
                             islands[index]->window,
                             nullptr,
                             origin.x + bounds.left,
                             origin.y + bounds.top,
                             0,
                             0,
                             SWP_NOACTIVATE | SWP_NOZORDER | SWP_NOSIZE);
      if (batch == nullptr) return false;
    }
    if (EndDeferWindowPos(batch) == FALSE) return false;

    for (std::size_t index = 0; index < ordered.size(); ++index) {
      islands[index]->region = ordered[index];
    }
    currentSet = set;
    currentBounds = screenBounds;
    return true;
  }

  void clear() noexcept {
    for (auto iterator = islands.rbegin(); iterator != islands.rend(); ++iterator) {
      if ((*iterator)->window != nullptr && IsWindow((*iterator)->window)) {
        DestroyWindow((*iterator)->window);
      }
      (*iterator)->window = nullptr;
    }
    islands.clear();
    currentSet = {};
    currentBounds = {};
  }

  void dispatch(const IslandState& state,
                const contracts::PointerPhase phase,
                const contracts::PointerButton button,
                POINT screenPoint,
                const std::int32_t wheelDelta = 0) {
    ++eventCount;
    if (!callback) return;
    callback(contracts::HostInputEvent{
        currentSet.revision,
        currentSet.projectionRevision,
        state.region.id,
        state.region.role,
        phase,
        button,
        screenPoint.x - currentBounds.left,
        screenPoint.y - currentBounds.top,
        screenPoint.x,
        screenPoint.y,
        state.region.actionIndex,
        wheelDelta,
    });
  }

  static LRESULT CALLBACK windowProcedure(HWND window,
                                          UINT message,
                                          WPARAM wParam,
                                          LPARAM lParam) {
    auto* state = reinterpret_cast<IslandState*>(
        GetWindowLongPtrW(window, GWLP_USERDATA));
    if (message == WM_NCCREATE) {
      const auto* create = reinterpret_cast<const CREATESTRUCTW*>(lParam);
      state = static_cast<IslandState*>(create->lpCreateParams);
      SetWindowLongPtrW(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(state));
      if (state != nullptr) state->window = window;
    }

    const auto screenPoint = [window, lParam] {
      POINT point{GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam)};
      ClientToScreen(window, &point);
      return point;
    };

    switch (message) {
      case WM_MOUSEACTIVATE:
        return MA_NOACTIVATE;
      case WM_SETCURSOR:
        if (state != nullptr && state->region.enabled) {
          SetCursor(LoadCursorW(nullptr, cursorForRole(state->region.role)));
          return TRUE;
        }
        break;
      case WM_LBUTTONDOWN:
        if (state != nullptr && state->owner != nullptr && state->region.enabled) {
          SetCapture(window);
          state->owner->dispatch(*state,
                                 contracts::PointerPhase::Pressed,
                                 contracts::PointerButton::Left,
                                 screenPoint());
        }
        return 0;
      case WM_MOUSEMOVE:
        if (state != nullptr && state->owner != nullptr &&
            (wParam & MK_LBUTTON) != 0 && GetCapture() == window) {
          state->owner->dispatch(*state,
                                 contracts::PointerPhase::Moved,
                                 contracts::PointerButton::Left,
                                 screenPoint());
        }
        return 0;
      case WM_LBUTTONUP:
        if (state != nullptr && state->owner != nullptr) {
          const POINT point = screenPoint();
          if (GetCapture() == window) ReleaseCapture();
          state->owner->dispatch(*state,
                                 contracts::PointerPhase::Released,
                                 contracts::PointerButton::Left,
                                 point);
        }
        return 0;
      case WM_LBUTTONDBLCLK:
        if (state != nullptr && state->owner != nullptr) {
          state->owner->dispatch(*state,
                                 contracts::PointerPhase::DoubleClicked,
                                 contracts::PointerButton::Left,
                                 screenPoint());
        }
        return 0;
      case WM_RBUTTONUP:
        if (state != nullptr && state->owner != nullptr && state->region.enabled) {
          state->owner->dispatch(*state,
                                 contracts::PointerPhase::Released,
                                 contracts::PointerButton::Right,
                                 screenPoint());
        }
        return 0;
      case WM_MOUSEWHEEL:
        if (state != nullptr && state->owner != nullptr && state->region.enabled) {
          const POINT point{GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam)};
          state->owner->dispatch(
              *state,
              contracts::PointerPhase::Wheel,
              contracts::PointerButton::None,
              point,
              GET_WHEEL_DELTA_WPARAM(wParam));
        }
        return 0;
      case WM_CAPTURECHANGED:
        if (state != nullptr && state->owner != nullptr) {
          POINT point{};
          GetCursorPos(&point);
          state->owner->dispatch(*state,
                                 contracts::PointerPhase::CaptureLost,
                                 contracts::PointerButton::None,
                                 point);
        }
        return 0;
      case WM_ERASEBKGND:
        return 1;
      case WM_PAINT: {
        PAINTSTRUCT paint{};
        BeginPaint(window, &paint);
        EndPaint(window, &paint);
        return 0;
      }
      default:
        break;
    }
    return DefWindowProcW(window, message, wParam, lParam);
  }

  HWND parent{};
  HWND visual{};
  contracts::HitRegionSet currentSet{};
  contracts::PixelRect currentBounds{};
  std::vector<std::unique_ptr<IslandState>> islands;
  InputCallback callback{};
  std::uint64_t eventCount{};

  static std::once_flag classRegistrationOnce;
};

std::once_flag Win32InputIslandSet::Impl::classRegistrationOnce;

Win32InputIslandSet::Win32InputIslandSet() : impl_(std::make_unique<Impl>()) {}

Win32InputIslandSet::~Win32InputIslandSet() = default;

bool Win32InputIslandSet::initialize(const HWND parent, const HWND visual) {
  return impl_->initialize(parent, visual);
}

bool Win32InputIslandSet::publish(const contracts::HitRegionSet& regions,
                                  const contracts::PixelRect visualScreenBounds,
                                  const bool reveal) {
  return impl_->publish(regions, visualScreenBounds, reveal);
}

bool Win32InputIslandSet::relocate(const contracts::HitRegionSet& regions,
                                   const contracts::PixelRect visualScreenBounds) {
  return impl_->relocate(regions, visualScreenBounds);
}

void Win32InputIslandSet::setInputCallback(InputCallback callback) {
  impl_->callback = std::move(callback);
}

void Win32InputIslandSet::clear() noexcept {
  impl_->clear();
}

InputIslandObservation Win32InputIslandSet::observe() const noexcept {
  InputIslandObservation observation{};
  observation.hitRevision = impl_->currentSet.revision;
  observation.projectionRevision = impl_->currentSet.projectionRevision;
  observation.eventCount = impl_->eventCount;
  observation.inputEnabled = std::any_of(
      impl_->islands.begin(), impl_->islands.end(),
      [](const auto& island) { return island->region.enabled; });
  observation.windowHandles.reserve(impl_->islands.size());
  for (const auto& island : impl_->islands) {
    observation.windowHandles.push_back(handleValue(island->window));
  }
  return observation;
}

}  // namespace desktopup::adapters
