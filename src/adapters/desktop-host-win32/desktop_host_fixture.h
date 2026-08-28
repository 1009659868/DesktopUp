#pragma once

#include "contracts/ports.h"

namespace desktopup::adapters {

class Win32DesktopHostFixture final : public contracts::IDesktopHost {
 public:
  bool attach(contracts::SurfaceEpoch epoch, contracts::PixelRect bounds) override;
  bool present(contracts::PixelRect screenBounds,
               const contracts::FrameDescriptor& frame,
               const contracts::PremultipliedFrameView& pixels,
               const contracts::HitRegionSet& hitRegions) override;
  bool relocate(contracts::PixelRect screenBounds,
                const contracts::HitRegionSet& hitRegions) override;
  void setInputCallback(InputCallback callback) override;
  contracts::HostMaintenanceResult requestReconnect() override;
  contracts::HostMaintenanceResult refreshDisplayEnvironment() override;
  contracts::HostObservation observe() const noexcept override;
  void beginExit() noexcept override;
  void detach() noexcept override;

  [[nodiscard]] bool attached() const noexcept;
  [[nodiscard]] bool exiting() const noexcept;
  [[nodiscard]] std::uint64_t presentCount() const noexcept;

 private:
  contracts::SurfaceEpoch epoch_{};
  contracts::PixelRect bounds_{};
  std::uint64_t shellGeneration_{};
  std::uint64_t lifecycleEpoch_{};
  contracts::HitRegionRevision hitRevision_{};
  contracts::HostProjectionRevision projectionRevision_{};
  contracts::DisplayEnvironment displayEnvironment_{
      1, {0, 0, 1920, 1080}, {0, 0, 1920, 1040}, 96, true};
  InputCallback inputCallback_{};
  std::uint64_t presentCount_{};
  bool attached_{false};
  bool visible_{false};
  bool exiting_{false};
};

}  // namespace desktopup::adapters
