#include "core/display_geometry.h"

#include <algorithm>
#include <cmath>

namespace desktopup::core {

namespace {

constexpr double kDipBase = 96.0;
constexpr double kCollapsedHeightDip = 60.0;

double pxToDip(const std::uint32_t pixels, const std::uint32_t dpi) noexcept {
  return static_cast<double>(pixels) * kDipBase /
         static_cast<double>(dpi == 0 ? 96 : dpi);
}

std::uint32_t dipToPx(const double dips, const std::uint32_t dpi) noexcept {
  return static_cast<std::uint32_t>(std::max<long long>(
      1, std::llround(dips * static_cast<double>(dpi == 0 ? 96 : dpi) /
                      kDipBase)));
}

double normalizedPosition(const std::int64_t offset,
                          const std::uint32_t available) noexcept {
  if (available == 0) return 0.0;
  return std::clamp(static_cast<double>(offset) /
                        static_cast<double>(available),
                    0.0,
                    1.0);
}

}  // namespace

LogicalPosterGeometryDip makeCenteredLogicalPosterGeometry(
    const contracts::PixelSize logicalExpandedSize) noexcept {
  return LogicalPosterGeometryDip{
      static_cast<double>(logicalExpandedSize.width),
      static_cast<double>(logicalExpandedSize.height),
      0.5,
      0.5};
}

LogicalPosterGeometryDip captureLogicalPosterGeometry(
    const contracts::PixelRect currentBounds,
    const std::uint32_t expandedHeightPx,
    const contracts::DisplayEnvironment& environment) noexcept {
  if (!environment.primaryTargetValid || environment.effectiveDpi == 0 ||
      environment.primaryWorkArea.width == 0 ||
      environment.primaryWorkArea.height == 0 || currentBounds.width == 0 ||
      currentBounds.height == 0 || expandedHeightPx == 0) {
    return {};
  }

  const auto availableX = environment.primaryWorkArea.width > currentBounds.width
                              ? environment.primaryWorkArea.width - currentBounds.width
                              : 0;
  const auto availableY = environment.primaryWorkArea.height > currentBounds.height
                              ? environment.primaryWorkArea.height - currentBounds.height
                              : 0;
  return LogicalPosterGeometryDip{
      pxToDip(currentBounds.width, environment.effectiveDpi),
      pxToDip(expandedHeightPx, environment.effectiveDpi),
      normalizedPosition(
          static_cast<std::int64_t>(currentBounds.left) -
              environment.primaryWorkArea.left,
          availableX),
      normalizedPosition(
          static_cast<std::int64_t>(currentBounds.top) -
              environment.primaryWorkArea.top,
          availableY)};
}

ProjectedPosterGeometry projectLogicalPosterGeometry(
    const LogicalPosterGeometryDip& logical,
    const contracts::DisplayEnvironment& environment,
    const bool collapsed,
    const contracts::PixelSize minimumExpandedDip) noexcept {
  if (!environment.primaryTargetValid || environment.effectiveDpi == 0 ||
      environment.primaryWorkArea.width == 0 ||
      environment.primaryWorkArea.height == 0 || logical.width <= 0.0 ||
      logical.expandedHeight <= 0.0) {
    return {};
  }

  const double workWidthDip =
      pxToDip(environment.primaryWorkArea.width, environment.effectiveDpi);
  const double workHeightDip =
      pxToDip(environment.primaryWorkArea.height, environment.effectiveDpi);
  const double minimumWidthDip =
      std::min(workWidthDip, std::max(1.0, static_cast<double>(minimumExpandedDip.width)));
  const double minimumHeightDip =
      std::min(workHeightDip, std::max(1.0, static_cast<double>(minimumExpandedDip.height)));
  const double widthDip = std::clamp(logical.width, minimumWidthDip, workWidthDip);
  const double expandedHeightDip =
      std::clamp(logical.expandedHeight, minimumHeightDip, workHeightDip);

  const std::uint32_t widthPx =
      std::min(environment.primaryWorkArea.width,
               dipToPx(widthDip, environment.effectiveDpi));
  const std::uint32_t expandedHeightPx =
      std::min(environment.primaryWorkArea.height,
               dipToPx(expandedHeightDip, environment.effectiveDpi));
  const double currentHeightDip =
      collapsed ? std::min(workHeightDip, kCollapsedHeightDip)
                : expandedHeightDip;
  const std::uint32_t currentHeightPx =
      std::min(environment.primaryWorkArea.height,
               dipToPx(currentHeightDip, environment.effectiveDpi));

  const std::uint32_t availableX =
      environment.primaryWorkArea.width - widthPx;
  const std::uint32_t availableY =
      environment.primaryWorkArea.height - currentHeightPx;
  const auto leftOffset = static_cast<std::int64_t>(std::llround(
      std::clamp(logical.anchorX, 0.0, 1.0) *
      static_cast<double>(availableX)));
  const auto topOffset = static_cast<std::int64_t>(std::llround(
      std::clamp(logical.anchorY, 0.0, 1.0) *
      static_cast<double>(availableY)));

  return ProjectedPosterGeometry{
      contracts::PixelRect{
          static_cast<std::int32_t>(
              static_cast<std::int64_t>(environment.primaryWorkArea.left) +
              leftOffset),
          static_cast<std::int32_t>(
              static_cast<std::int64_t>(environment.primaryWorkArea.top) +
              topOffset),
          widthPx,
          currentHeightPx},
      expandedHeightPx,
      true};
}

}  // namespace desktopup::core
