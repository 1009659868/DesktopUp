#pragma once

#include "contracts/types.h"

namespace desktopup::core {

struct LogicalPosterGeometryDip {
  double width{};
  double expandedHeight{};
  double anchorX{0.5};
  double anchorY{0.5};
};

struct ProjectedPosterGeometry {
  contracts::PixelRect bounds{};
  std::uint32_t expandedHeight{};
  bool valid{false};
};

[[nodiscard]] LogicalPosterGeometryDip makeCenteredLogicalPosterGeometry(
    contracts::PixelSize logicalExpandedSize) noexcept;

[[nodiscard]] LogicalPosterGeometryDip captureLogicalPosterGeometry(
    contracts::PixelRect currentBounds,
    std::uint32_t expandedHeightPx,
    const contracts::DisplayEnvironment& environment) noexcept;

[[nodiscard]] ProjectedPosterGeometry projectLogicalPosterGeometry(
    const LogicalPosterGeometryDip& logical,
    const contracts::DisplayEnvironment& environment,
    bool collapsed,
    contracts::PixelSize minimumExpandedDip) noexcept;

}  // namespace desktopup::core
