#include "adapters/desktop-host-win32/win32_desktop_host.h"
#include "adapters/renderer-qpainter/qpainter_renderer.h"

#include <windows.h>
#include <windowsx.h>

#include <QGuiApplication>

#include <algorithm>
#include <cstdint>
#include <iostream>
#include <string>
#include <vector>

namespace {

int failures{};

void expect(const bool condition, const char* message) {
  if (!condition) {
    std::cerr << "FAIL: " << message << '\n';    ++failures;
  }
}

HWND asWindow(const std::uint64_t value) {
  return reinterpret_cast<HWND>(static_cast<std::uintptr_t>(value));
}

std::wstring className(const HWND window) {
  wchar_t value[128]{};
  const int length = GetClassNameW(window, value, static_cast<int>(std::size(value)));
  return length > 0 ? std::wstring(value, static_cast<std::size_t>(length)) : L"";
}

std::wstring windowText(const HWND window) {
  wchar_t value[128]{};
  const int length = GetWindowTextW(window, value, static_cast<int>(std::size(value)));
  return length > 0 ? std::wstring(value, static_cast<std::size_t>(length)) : L"";
}

std::vector<HWND> islandWindows(const HWND parent) {
  std::vector<HWND> result;
  for (HWND child = GetWindow(parent, GW_CHILD); child != nullptr;
       child = GetWindow(child, GW_HWNDNEXT)) {
    if (className(child) == L"DesktopUpV2QTS04InputIsland") {
      result.push_back(child);
    }
  }
  return result;
}

HWND islandById(const HWND parent, const desktopup::contracts::RegionId id) {
  const std::wstring suffix = std::to_wstring(id);
  for (const HWND child : islandWindows(parent)) {
    const auto title = windowText(child);
    if (title.size() >= suffix.size() &&
        title.compare(title.size() - suffix.size(), suffix.size(), suffix) == 0) {
      return child;
    }
  }
  return nullptr;
}

POINT parentPoint(const HWND parent,
                  const desktopup::contracts::PixelRect screenBounds,
                  const int localX,
                  const int localY) {
  POINT point{screenBounds.left + localX, screenBounds.top + localY};
  MapWindowPoints(nullptr, parent, &point, 1);
  return point;
}

bool nativeDesktopTarget(const HWND window,
                         const desktopup::contracts::HostObservation& observation) {
  const HWND defView = asWindow(observation.defViewHandle);
  const HWND iconView = asWindow(observation.iconViewHandle);
  return window == defView || window == iconView ||
         (defView != nullptr && IsChild(defView, window) != FALSE);
}

desktopup::contracts::HitRegionSet hitSet(
    const desktopup::contracts::HitRegionRevision revision,
    const desktopup::contracts::PixelSize size,
    std::vector<desktopup::contracts::HitRegion> regions) {
  return desktopup::contracts::HitRegionSet{revision, revision, size, std::move(regions)};
}

}  // namespace

int main(int argc, char** argv) {
  SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
  QGuiApplication application(argc, argv);
  using namespace desktopup;

  constexpr contracts::PixelSize frameSize{420, 620};
  const auto bounds = adapters::Win32DesktopHost::centeredPrimaryWorkArea(frameSize);
  adapters::Win32DesktopHost host;
  adapters::QPainterRenderer renderer;
  std::vector<contracts::HostInputEvent> events;
  host.setInputCallback([&events](const auto& event) { events.push_back(event); });

  expect(host.attach(61, bounds), "host must attach before split-input publication");
  auto observation = host.observe();
  const HWND parent = asWindow(observation.parentHandle);
  const HWND visual = asWindow(observation.visualHandle);
  expect(parent != nullptr && visual != nullptr, "host observation must expose parent and visual");

  const auto frame = renderer.render({1, observation.surfaceEpoch, frameSize});
  const auto pixels = renderer.frameView(frame.buffer);

  expect(host.present(bounds, frame, pixels, hitSet(1, frameSize, {})),
         "zero-region projection must publish");
  observation = host.observe();
  expect(observation.inputIslandCount == 0 && !observation.inputEnabled,
         "zero regions must leave the entire visual pointer-transparent");
  expect(observation.visualPointerTransparent && observation.ownedResourceCount == 3,
         "visual must retain WS_EX_TRANSPARENT without hidden input resources");

  std::vector<contracts::HitRegion> one{{
      3,
      contracts::HitRegionRole::PresentationToggle,
      contracts::PixelRect{360, 14, 34, 34},
      110,
      0,
      true,
  }};
  expect(host.present(bounds, frame, pixels, hitSet(2, frameSize, one)),
         "one-region projection must publish");
  observation = host.observe();
  expect(observation.inputIslandCount == 1 && observation.inputEnabled,
         "one enabled region must create exactly one input island");

  const auto flags = CWP_SKIPINVISIBLE | CWP_SKIPDISABLED | CWP_SKIPTRANSPARENT;
  const POINT actionPoint = parentPoint(parent, bounds, 377, 31);
  expect(ChildWindowFromPointEx(parent, actionPoint, flags) == islandById(parent, 3),
         "one registered region must route to its island window");

  std::vector<contracts::HitRegion> multi{
      {1, contracts::HitRegionRole::TitleDrag,
       contracts::PixelRect{22, 10, 250, 40}, 20, 0, true},
      {101, contracts::HitRegionRole::Item,
       contracts::PixelRect{28, 183, 112, 92}, 50, 1, true},
      {150, contracts::HitRegionRole::ItemScroll,
       contracts::PixelRect{402, 183, 12, 200}, 80, 0, true},
      {3, contracts::HitRegionRole::PresentationToggle,
       contracts::PixelRect{360, 14, 34, 34}, 110, 0, true},
  };
  const bool multiPublished = host.present(bounds, frame, pixels, hitSet(3, frameSize, multi));
  if (!multiPublished) {
    std::cerr << "MULTI_PRESENT_NATIVE_ERROR=0x" << std::hex
              << host.observe().nativeError << std::dec << '\n';
  }
  expect(multiPublished, "multi-region projection must publish");
  observation = host.observe();
  expect(observation.inputIslandCount == 4,
         "multi projection must retain item, scrollbar and action islands without an envelope");
  const HWND itemIsland = islandById(parent, 101);
  const POINT itemScreen{bounds.left + 84, bounds.top + 220};
  SendMessageW(itemIsland,
               WM_MOUSEWHEEL,
               MAKEWPARAM(0, WHEEL_DELTA),
               MAKELPARAM(itemScreen.x, itemScreen.y));
  const bool wheelDispatched = std::any_of(
      events.begin(), events.end(), [](const contracts::HostInputEvent& event) {
        return event.hitRevision == 3 && event.regionId == 101 &&
               event.role == contracts::HitRegionRole::Item &&
               event.phase == contracts::PointerPhase::Wheel &&
               event.wheelDelta == WHEEL_DELTA;
      });
  expect(wheelDispatched,
         "visible item islands must dispatch V1 overflow wheel input");

  const HWND scrollIsland = islandById(parent, 150);
  expect(scrollIsland != nullptr,
         "overflow must expose a dedicated scrollbar input island");
  if (scrollIsland != nullptr) {
    const POINT scrollScreen{bounds.left + 408, bounds.top + 260};
    SendMessageW(scrollIsland,
                 WM_MOUSEWHEEL,
                 MAKEWPARAM(0, WHEEL_DELTA),
                 MAKELPARAM(scrollScreen.x, scrollScreen.y));
  }
  const bool scrollbarWheelDispatched = std::any_of(
      events.begin(), events.end(), [](const contracts::HostInputEvent& event) {
        return event.hitRevision == 3 && event.regionId == 150 &&
               event.role == contracts::HitRegionRole::ItemScroll &&
               event.phase == contracts::PointerPhase::Wheel &&
               event.wheelDelta == WHEEL_DELTA;
      });
  expect(scrollbarWheelDispatched,
         "scrollbar island must keep wheel input reachable without a visible item");

  const POINT blankPoint = parentPoint(parent, bounds, 210, 500);
  const HWND blankTarget = ChildWindowFromPointEx(parent, blankPoint, flags);
  expect(nativeDesktopTarget(blankTarget, observation),
         "unregistered poster blank must route directly to Explorer");

  std::vector<contracts::HitRegion> overlap{
      {20, contracts::HitRegionRole::TitleDrag,
       contracts::PixelRect{40, 20, 160, 50}, 10, 0, true},
      {21, contracts::HitRegionRole::PresentationToggle,
       contracts::PixelRect{90, 25, 80, 36}, 100, 0, true},
  };
  expect(host.present(bounds, frame, pixels, hitSet(4, frameSize, overlap)),
         "overlap projection must publish");
  const POINT overlapPoint = parentPoint(parent, bounds, 110, 40);
  const HWND highPriority = islandById(parent, 21);
  expect(highPriority != nullptr &&
             ChildWindowFromPointEx(parent, overlapPoint, flags) == highPriority,
         "overlap must route to the explicitly higher-priority island");

  SendMessageW(highPriority, WM_LBUTTONDOWN, MK_LBUTTON, MAKELPARAM(10, 10));
  SendMessageW(highPriority, WM_LBUTTONUP, 0, MAKELPARAM(10, 10));
  const bool released = std::any_of(
      events.begin(), events.end(), [](const contracts::HostInputEvent& event) {
        return event.hitRevision == 4 && event.regionId == 21 &&
               event.role == contracts::HitRegionRole::PresentationToggle &&
               event.phase == contracts::PointerPhase::Released &&
               event.button == contracts::PointerButton::Left;
      });
  expect(released, "island click must emit a revision-fenced semantic pointer event");
  observation = host.observe();
  expect(observation.inputEventCount >= 2,
         "host observation must expose bounded input event diagnostics");
  const HWND titleIsland = islandById(parent, 20);
  const auto relocationBefore = host.observe();
  expect(titleIsland != nullptr, "title drag island must exist before relocation");
  SendMessageW(titleIsland, WM_LBUTTONDOWN, MK_LBUTTON, MAKELPARAM(10, 10));
  expect(GetCapture() == titleIsland,
         "title drag island must retain capture across low-latency relocation");

  const contracts::PixelRect movedBounds{
      bounds.left + 80, bounds.top + 60, bounds.width, bounds.height};
  expect(host.relocate(movedBounds, hitSet(5, frameSize, overlap)),
         "same-size relocation must atomically move visual and input islands");
  observation = host.observe();
  expect(observation.requestedBounds == movedBounds &&
             observation.windowBounds == movedBounds,
         "Host observation and the real visual HWND must commit the moved screen bounds");
  const HWND movedHighPriority = islandById(parent, 21);
  RECT movedIslandRect{};
  expect(movedHighPriority == highPriority &&
             GetWindowRect(movedHighPriority, &movedIslandRect) != FALSE &&
             movedIslandRect.left == movedBounds.left + 90 &&
             movedIslandRect.top == movedBounds.top + 25,
         "same-identity islands must retain HWND identity and move with the visual");
  for (contracts::HitRegionRevision step = 0; step < 24; ++step) {
    const contracts::PixelRect intermediate{
        movedBounds.left + static_cast<std::int32_t>(step % 3),
        movedBounds.top + static_cast<std::int32_t>((step / 3) % 3),
        movedBounds.width,
        movedBounds.height};
    expect(host.relocate(intermediate, hitSet(6 + step, frameSize, overlap)),
           "continuous same-size relocation must stay publishable");
    expect(islandById(parent, 20) == titleIsland && GetCapture() == titleIsland,
           "continuous relocation must preserve the captured title island HWND");
  }
  expect(host.relocate(movedBounds, hitSet(30, frameSize, overlap)),
         "continuous relocation test must return to the accepted final bounds");
  observation = host.observe();
  expect(observation.visualHandle == relocationBefore.visualHandle &&
             observation.shellGeneration == relocationBefore.shellGeneration &&
             observation.lifecycleEpoch == relocationBefore.lifecycleEpoch &&
             observation.surfaceEpoch == relocationBefore.surfaceEpoch &&
             observation.requestedBounds == movedBounds && observation.visible,
         "relocation must preserve visual and lifecycle identity while committing geometry");
  SendMessageW(titleIsland, WM_LBUTTONUP, 0, MAKELPARAM(10, 10));
  expect(GetCapture() != titleIsland,
         "title drag capture must release normally after continuous relocation");


  const auto minimumSize = adapters::QPainterRenderer::minimumExpandedSize();
  const contracts::PixelRect minimumBounds{
      movedBounds.left, movedBounds.top, minimumSize.width, minimumSize.height};
  const auto minimumFrame =
      renderer.render({2, observation.surfaceEpoch, minimumSize});
  const auto minimumPixels = renderer.frameView(minimumFrame.buffer);
  std::vector<contracts::HitRegion> resizeProjection{
      {1, contracts::HitRegionRole::TitleDrag,
       contracts::PixelRect{22, 10, 220, 40}, 20, 0, true},
      {100, contracts::HitRegionRole::Item,
       contracts::PixelRect{}, 50, 0, false},
      {203, contracts::HitRegionRole::ResizeBottomRight,
       contracts::PixelRect{300, 400, 18, 18}, 130, 0, true},
  };
  expect(host.present(
             minimumBounds,
             minimumFrame,
             minimumPixels,
             hitSet(31, minimumSize, resizeProjection)),
         "the frozen 320x420 minimum projection must remain publishable");
  const HWND resizeIsland = islandById(parent, 203);
  expect(resizeIsland != nullptr, "minimum expanded geometry must retain its resize island");
  SendMessageW(resizeIsland, WM_LBUTTONDOWN, MK_LBUTTON, MAKELPARAM(8, 8));
  expect(GetCapture() == resizeIsland, "resize island must own pointer capture during gesture");
  auto reflowProjection = resizeProjection;
  reflowProjection[1].bounds = contracts::PixelRect{24, 180, 90, 80};
  reflowProjection[1].enabled = true;
  expect(host.present(
             minimumBounds,
             minimumFrame,
             minimumPixels,
             hitSet(32, minimumSize, reflowProjection)),
         "item visibility reflow must publish while resize capture is active");
  expect(islandById(parent, 203) == resizeIsland && GetCapture() == resizeIsland,
         "item row or column changes must not replace the captured resize HWND");

  const auto visualBeforeResizeTransaction = host.observe().visualHandle;
  constexpr contracts::PixelSize thresholdSize{570, 500};
  const contracts::PixelRect thresholdBounds{
      minimumBounds.left,
      minimumBounds.top,
      thresholdSize.width,
      thresholdSize.height};
  const auto thresholdFrame =
      renderer.render({3, observation.surfaceEpoch, thresholdSize});
  const auto thresholdPixels = renderer.frameView(thresholdFrame.buffer);
  auto thresholdProjection = reflowProjection;
  thresholdProjection[0].bounds = contracts::PixelRect{22, 10, 360, 40};
  thresholdProjection[1].bounds = contracts::PixelRect{28, 183, 113, 92};
  thresholdProjection[2].bounds = contracts::PixelRect{550, 480, 18, 18};
  expect(host.present(
             thresholdBounds,
             thresholdFrame,
             thresholdPixels,
             hitSet(33, thresholdSize, thresholdProjection)),
         "staged resize transaction must publish a different visual size while capture is active");
  observation = host.observe();
  expect(observation.visualHandle == visualBeforeResizeTransaction &&
             observation.windowBounds == thresholdBounds &&
             observation.requestedBounds == thresholdBounds &&
             observation.visible,
         "staged resize transaction must preserve visual identity and synchronously commit bounds");
  expect(islandById(parent, 203) == resizeIsland && GetCapture() == resizeIsland,
         "visual resize and input-island geometry batch must preserve the captured resize HWND");
  SendMessageW(resizeIsland, WM_LBUTTONUP, 0, MAKELPARAM(8, 8));
  expect(GetCapture() != resizeIsland, "normal resize release must end pointer capture");

  const auto oldIslands = islandWindows(parent);
  const auto finalMinimumFrame =
      renderer.render({4, observation.surfaceEpoch, minimumSize});
  const auto finalMinimumPixels =
      renderer.frameView(finalMinimumFrame.buffer);
  expect(host.present(
             minimumBounds,
             finalMinimumFrame,
             finalMinimumPixels,
             hitSet(34, minimumSize, {})),
         "returning to zero regions must publish");
  observation = host.observe();
  expect(observation.inputIslandCount == 0 && !observation.inputEnabled,
         "zero-region replacement must remove all old islands");
  expect(std::all_of(oldIslands.begin(), oldIslands.end(),
                     [](const HWND window) { return IsWindow(window) == FALSE; }),
         "old island HWNDs must be destroyed after atomic replacement");

  renderer.release(minimumFrame.buffer);
  renderer.release(thresholdFrame.buffer);
  renderer.release(finalMinimumFrame.buffer);
  host.setInputCallback({});
  host.beginExit();
  renderer.beginExit();
  observation = host.observe();
  expect(observation.visualHandle == 0 && observation.inputIslandCount == 0 &&
             observation.ownedResourceCount == 0,
         "BeginExit must synchronously remove visual and every input island");
  host.detach();

  if (failures == 0) {
    std::cout << "QTS04_SPLIT_INPUT_ISLANDS_PASS\n";
  }
  return failures == 0 ? 0 : 1;
}
