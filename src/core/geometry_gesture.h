#pragma once

#include "contracts/types.h"

#include <optional>

namespace desktopup::core {

struct GeometryConstraintsPx {
  contracts::PixelRect workArea{};
  contracts::PixelSize minimumSize{};
};

[[nodiscard]] contracts::PixelRect clampGeometryToWorkArea(
    contracts::PixelRect bounds,
    const GeometryConstraintsPx& constraints) noexcept;

class GeometryGestureSession {
 public:
  [[nodiscard]] bool begin(contracts::HitRegionRole role,
                           std::int32_t screenX,
                           std::int32_t screenY,
                           contracts::PixelRect startBounds) noexcept;

  [[nodiscard]] std::optional<contracts::PixelRect> update(
      std::int32_t screenX,
      std::int32_t screenY,
      const GeometryConstraintsPx& constraints) noexcept;

  [[nodiscard]] bool end() noexcept;
  void cancel() noexcept;

  [[nodiscard]] bool active() const noexcept;
  [[nodiscard]] bool thresholdCrossed() const noexcept;
  [[nodiscard]] contracts::HitRegionRole role() const noexcept;

 private:
  bool active_{false};
  bool thresholdCrossed_{false};
  contracts::HitRegionRole role_{contracts::HitRegionRole::TitleDrag};
  std::int32_t startScreenX_{};
  std::int32_t startScreenY_{};
  contracts::PixelRect startBounds_{};
};

}  // namespace desktopup::core
