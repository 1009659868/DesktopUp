#include "core/geometry_gesture.h"
#include "core/display_geometry.h"

#include <cmath>
#include <iostream>

namespace {

int failures{};

void expect(const bool condition, const char* message) {
  if (!condition) {
    std::cerr << "FAIL: " << message << '\n';
    ++failures;
  }
}

}  // namespace

int main() {
  using desktopup::contracts::HitRegionRole;
  using desktopup::contracts::PixelRect;
  using desktopup::contracts::PixelSize;
  using desktopup::core::GeometryConstraintsPx;
  using desktopup::core::GeometryGestureSession;

  constexpr GeometryConstraintsPx constraints{
      PixelRect{0, 0, 1920, 1040},
      PixelSize{320, 420}};
  constexpr PixelRect start{500, 300, 420, 620};

  GeometryGestureSession gesture;
  expect(!gesture.begin(HitRegionRole::Item, 100, 100, start),
         "non-geometry roles must not start a geometry session");
  expect(gesture.begin(HitRegionRole::TitleDrag, 100, 100, start),
         "title drag must start a geometry session");
  expect(!gesture.update(104, 103, constraints).has_value(),
         "movement below the frozen 5px threshold must not publish geometry");
  const auto moved = gesture.update(300, 300, constraints);
  expect(moved.has_value() && moved->left == 700 && moved->top == 420 &&
             moved->width == start.width && moved->height == start.height,
         "title drag must preserve size and clamp the complete poster to the work area");
  expect(gesture.thresholdCrossed(), "accepted movement must cross the gesture threshold");
  expect(gesture.end() && !gesture.active(),
         "normal release must close the active geometry session");

  expect(gesture.begin(HitRegionRole::ResizeBottomRight, 920, 920, start),
         "bottom-right resize must start from the accepted bounds");
  const auto minimum = gesture.update(-1000, -1000, constraints);
  expect(minimum.has_value() && minimum->left == start.left &&
             minimum->top == start.top && minimum->width == 320 &&
             minimum->height == 420,
         "dragging above the poster top must clamp to 320x420 without crossing the origin");
  expect(gesture.end(), "minimum-size resize must end cleanly");

  expect(gesture.begin(HitRegionRole::ResizeBottomRight, 920, 920, start),
         "a completed resize must not disable the next resize session");
  const auto maximum = gesture.update(10000, 10000, constraints);
  expect(maximum.has_value() && maximum->left == start.left &&
             maximum->top == start.top && maximum->width == 1420 &&
             maximum->height == 740,
         "resize must keep left/top fixed and clamp right/bottom to the work area");
  gesture.cancel();
  expect(!gesture.active(), "capture loss must cancel the session");

  constexpr GeometryConstraintsPx smallWorkArea{
      PixelRect{-640, 0, 300, 360},
      PixelSize{320, 420}};
  const auto clamped = desktopup::core::clampGeometryToWorkArea(
      PixelRect{-900, -100, 420, 620}, smallWorkArea);
  expect(clamped == PixelRect{-640, 0, 300, 360},
         "constraints larger than the work area must produce a visible non-negative extent");
  constexpr desktopup::contracts::DisplayEnvironment display96{
      1, PixelRect{0, 0, 1920, 1080}, PixelRect{0, 0, 1920, 1040}, 96, true};
  const auto logical = desktopup::core::makeCenteredLogicalPosterGeometry(
      PixelSize{420, 620});
  const auto projected96 = desktopup::core::projectLogicalPosterGeometry(
      logical, display96, false, PixelSize{320, 420});
  expect(projected96.valid &&
             projected96.bounds == PixelRect{750, 210, 420, 620} &&
             projected96.expandedHeight == 620,
         "96 DPI projection must preserve logical size and centered anchor");

  constexpr desktopup::contracts::DisplayEnvironment display120{
      2, PixelRect{0, 0, 2560, 1440}, PixelRect{0, 0, 2560, 1400}, 120, true};
  const auto projected120 = desktopup::core::projectLogicalPosterGeometry(
      logical, display120, false, PixelSize{320, 420});
  expect(projected120.valid &&
             projected120.bounds == PixelRect{1018, 313, 525, 775} &&
             projected120.expandedHeight == 775,
         "120 DPI projection must derive pixels from the same DIP authority");
  const auto collapsed120 = desktopup::core::projectLogicalPosterGeometry(
      logical, display120, true, PixelSize{320, 420});
  expect(collapsed120.valid &&
             collapsed120.bounds == PixelRect{1018, 663, 525, 75} &&
             collapsed120.expandedHeight == 775,
         "collapsed projection must keep logical width and the scaled title height");

  auto wideLogical = logical;
  wideLogical.width = 700.0;
  const auto collapsedWide120 = desktopup::core::projectLogicalPosterGeometry(
      wideLogical, display120, true, PixelSize{320, 420});
  expect(collapsedWide120.valid && collapsedWide120.bounds.width == 875 &&
             collapsedWide120.bounds.height == 75,
         "collapsed height must remain 60 DIP when expanded frame width changes");

  const auto captured = desktopup::core::captureLogicalPosterGeometry(
      PixelRect{1525, 225, 525, 775}, 775, display120);
  expect(std::abs(captured.width - 420.0) < 0.001 &&
             std::abs(captured.expandedHeight - 620.0) < 0.001 &&
             captured.anchorX > 0.74 && captured.anchorX < 0.76,
         "accepted physical gestures must update DIP size and normalized anchor once");

  constexpr desktopup::contracts::DisplayEnvironment negativeWorkArea{
      3, PixelRect{-1280, 0, 1280, 1024}, PixelRect{-1280, 0, 1280, 984}, 96, true};
  const auto projectedNegative = desktopup::core::projectLogicalPosterGeometry(
      logical, negativeWorkArea, false, PixelSize{320, 420});
  expect(projectedNegative.valid &&
             projectedNegative.bounds == PixelRect{-850, 182, 420, 620},
         "projection must preserve signed screen origins without physical accumulation");

  if (failures == 0) {
    std::cout << "QTS04_GEOMETRY_GESTURE_PASS\n";
  }
  return failures == 0 ? 0 : 1;
}
