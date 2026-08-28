#pragma once

#include "contracts/ports.h"

#include <functional>
#include <memory>

namespace desktopup::adapters {

class Win32DesktopHost final : public contracts::IDesktopHost {
 public:
  using RecoveryCallback = std::function<void(contracts::HostMaintenanceResult)>;

  Win32DesktopHost();
  ~Win32DesktopHost() override;

  Win32DesktopHost(const Win32DesktopHost&) = delete;
  Win32DesktopHost& operator=(const Win32DesktopHost&) = delete;

  [[nodiscard]] bool attach(contracts::SurfaceEpoch epoch,
                            contracts::PixelRect screenBounds) override;
  [[nodiscard]] bool present(contracts::PixelRect screenBounds,
                             const contracts::FrameDescriptor& frame,
                             const contracts::PremultipliedFrameView& pixels,
                             const contracts::HitRegionSet& hitRegions) override;
  [[nodiscard]] bool relocate(contracts::PixelRect screenBounds,
                              const contracts::HitRegionSet& hitRegions) override;
  void setInputCallback(InputCallback callback) override;
  [[nodiscard]] contracts::HostMaintenanceResult requestReconnect() override;
  [[nodiscard]] contracts::HostMaintenanceResult refreshDisplayEnvironment() override;
  [[nodiscard]] contracts::HostObservation observe() const noexcept override;
  void beginExit() noexcept override;
  void detach() noexcept override;

  void setRecoveryCallback(RecoveryCallback callback);

  [[nodiscard]] static contracts::PixelRect centeredPrimaryWorkArea(
      contracts::PixelSize size) noexcept;
  [[nodiscard]] static contracts::DisplayEnvironment primaryDisplayEnvironment() noexcept;

 private:
  struct Impl;
  std::unique_ptr<Impl> impl_;
};

}  // namespace desktopup::adapters
