#include "core/geometry_gesture.h"

#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <limits>

namespace desktopup::core {
namespace {

constexpr std::int64_t kGestureThresholdPx = 5;

[[nodiscard]] bool supportedRole(const contracts::HitRegionRole role) noexcept {
  return role == contracts::HitRegionRole::TitleDrag ||
         role == contracts::HitRegionRole::ResizeRight ||
         role == contracts::HitRegionRole::ResizeBottom ||
         role == contracts::HitRegionRole::ResizeBottomRight;
}

[[nodiscard]] std::uint32_t clampExtent(const std::int64_t value,
                                        const std::uint32_t minimum,
                                        const std::uint32_t maximum) noexcept {
  const auto clamped = std::clamp<std::int64_t>(
      value, static_cast<std::int64_t>(minimum), static_cast<std::int64_t>(maximum));
  return static_cast<std::uint32_t>(clamped);
}

[[nodiscard]] std::int32_t clampOrigin(const std::int64_t value,
                                       const std::int64_t minimum,
                                       const std::int64_t maximum) noexcept {
  const auto clamped = std::clamp(value, minimum, maximum);
  return static_cast<std::int32_t>(std::clamp<std::int64_t>(
      clamped,
      std::numeric_limits<std::int32_t>::min(),
      std::numeric_limits<std::int32_t>::max()));
}

}  // namespace

contracts::PixelRect clampGeometryToWorkArea(
    contracts::PixelRect bounds,
    const GeometryConstraintsPx& constraints) noexcept {
  const auto workWidth = constraints.workArea.width;
  const auto workHeight = constraints.workArea.height;
  if (workWidth == 0 || workHeight == 0) {
    return bounds;
  }

  const auto minimumWidth =
      std::clamp<std::uint32_t>(constraints.minimumSize.width, 1, workWidth);
  const auto minimumHeight =
      std::clamp<std::uint32_t>(constraints.minimumSize.height, 1, workHeight);
  bounds.width = std::clamp(bounds.width, minimumWidth, workWidth);
  bounds.height = std::clamp(bounds.height, minimumHeight, workHeight);

  const auto minimumLeft = static_cast<std::int64_t>(constraints.workArea.left);
  const auto minimumTop = static_cast<std::int64_t>(constraints.workArea.top);
  const auto maximumLeft =
      minimumLeft + static_cast<std::int64_t>(workWidth) - bounds.width;
  const auto maximumTop =
      minimumTop + static_cast<std::int64_t>(workHeight) - bounds.height;
  bounds.left = clampOrigin(bounds.left, minimumLeft, maximumLeft);
  bounds.top = clampOrigin(bounds.top, minimumTop, maximumTop);
  return bounds;
}

bool GeometryGestureSession::begin(const contracts::HitRegionRole role,
                                   const std::int32_t screenX,
                                   const std::int32_t screenY,
                                   const contracts::PixelRect startBounds) noexcept {
  if (active_ || !supportedRole(role) || startBounds.width == 0 ||
      startBounds.height == 0) {
    return false;
  }
  active_ = true;
  thresholdCrossed_ = false;
  role_ = role;
  startScreenX_ = screenX;
  startScreenY_ = screenY;
  startBounds_ = startBounds;
  return true;
}

std::optional<contracts::PixelRect> GeometryGestureSession::update(
    const std::int32_t screenX,
    const std::int32_t screenY,
    const GeometryConstraintsPx& constraints) noexcept {
  if (!active_ || constraints.workArea.width == 0 ||
      constraints.workArea.height == 0) {
    return std::nullopt;
  }

  const auto deltaX =
      static_cast<std::int64_t>(screenX) - static_cast<std::int64_t>(startScreenX_);
  const auto deltaY =
      static_cast<std::int64_t>(screenY) - static_cast<std::int64_t>(startScreenY_);
  if (!thresholdCrossed_ &&
      std::max(std::llabs(deltaX), std::llabs(deltaY)) < kGestureThresholdPx) {
    return std::nullopt;
  }
  thresholdCrossed_ = true;

  contracts::PixelRect candidate = startBounds_;
  if (role_ == contracts::HitRegionRole::TitleDrag) {
    candidate.left = clampOrigin(
        static_cast<std::int64_t>(startBounds_.left) + deltaX,
        std::numeric_limits<std::int32_t>::min(),
        std::numeric_limits<std::int32_t>::max());
    candidate.top = clampOrigin(
        static_cast<std::int64_t>(startBounds_.top) + deltaY,
        std::numeric_limits<std::int32_t>::min(),
        std::numeric_limits<std::int32_t>::max());
    return clampGeometryToWorkArea(candidate, constraints);
  }

  const auto workRight = static_cast<std::int64_t>(constraints.workArea.left) +
                         constraints.workArea.width;
  const auto workBottom = static_cast<std::int64_t>(constraints.workArea.top) +
                          constraints.workArea.height;
  const auto availableWidth = static_cast<std::uint32_t>(std::max<std::int64_t>(
      1, workRight - static_cast<std::int64_t>(startBounds_.left)));
  const auto availableHeight = static_cast<std::uint32_t>(std::max<std::int64_t>(
      1, workBottom - static_cast<std::int64_t>(startBounds_.top)));
  const auto minimumWidth =
      std::clamp<std::uint32_t>(constraints.minimumSize.width, 1, availableWidth);
  const auto minimumHeight =
      std::clamp<std::uint32_t>(constraints.minimumSize.height, 1, availableHeight);

  if (role_ == contracts::HitRegionRole::ResizeRight ||
      role_ == contracts::HitRegionRole::ResizeBottomRight) {
    candidate.width = clampExtent(
        static_cast<std::int64_t>(startBounds_.width) + deltaX,
        minimumWidth,
        availableWidth);
  }
  if (role_ == contracts::HitRegionRole::ResizeBottom ||
      role_ == contracts::HitRegionRole::ResizeBottomRight) {
    candidate.height = clampExtent(
        static_cast<std::int64_t>(startBounds_.height) + deltaY,
        minimumHeight,
        availableHeight);
  }
  return clampGeometryToWorkArea(candidate, constraints);
}

bool GeometryGestureSession::end() noexcept {
  const bool wasActive = active_;
  active_ = false;
  return wasActive;
}

void GeometryGestureSession::cancel() noexcept {
  active_ = false;
}

bool GeometryGestureSession::active() const noexcept {
  return active_;
}

bool GeometryGestureSession::thresholdCrossed() const noexcept {
  return thresholdCrossed_;
}

contracts::HitRegionRole GeometryGestureSession::role() const noexcept {
  return role_;
}

}  // namespace desktopup::core
