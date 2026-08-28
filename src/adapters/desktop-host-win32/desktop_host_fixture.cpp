#include "adapters/desktop-host-win32/desktop_host_fixture.h"

#include <utility>

namespace desktopup::adapters {

bool Win32DesktopHostFixture::attach(const contracts::SurfaceEpoch epoch,
                                     const contracts::PixelRect bounds) {
  if (exiting_ || epoch == 0 || bounds.width == 0 || bounds.height == 0) {
    return false;
  }
  epoch_ = epoch;
  bounds_ = bounds;
  ++shellGeneration_;
  ++lifecycleEpoch_;
  attached_ = true;
  visible_ = false;
  return true;
}

bool Win32DesktopHostFixture::present(const contracts::PixelRect screenBounds,
                                      const contracts::FrameDescriptor& frame,
                                      const contracts::PremultipliedFrameView& pixels,
                                      const contracts::HitRegionSet& hitRegions) {
  if (exiting_ || !attached_ || frame.surfaceEpoch != epoch_ ||
      screenBounds.width == 0 || screenBounds.height == 0 ||
      frame.bounds != screenBounds.size() || frame.buffer == 0 || !pixels.valid() ||
      pixels.buffer != frame.buffer || pixels.bounds != frame.bounds ||
      hitRegions.revision == 0 || hitRegions.projectionRevision == 0 ||
      hitRegions.visualBounds != frame.bounds) {
    return false;
  }
  bounds_ = screenBounds;

  ++presentCount_;
  hitRevision_ = hitRegions.revision;
  projectionRevision_ = hitRegions.projectionRevision;
  visible_ = true;
  return true;
}

bool Win32DesktopHostFixture::relocate(
    const contracts::PixelRect screenBounds,
    const contracts::HitRegionSet& hitRegions) {
  if (exiting_ || !attached_ || !visible_ ||
      screenBounds.size() != bounds_.size() ||
      hitRegions.revision == 0 || hitRegions.projectionRevision == 0 ||
      hitRegions.visualBounds != screenBounds.size()) {
    return false;
  }
  bounds_ = screenBounds;
  hitRevision_ = hitRegions.revision;
  projectionRevision_ = hitRegions.projectionRevision;
  return true;
  }


void Win32DesktopHostFixture::setInputCallback(InputCallback callback) {
  inputCallback_ = std::move(callback);
}
contracts::HostMaintenanceResult Win32DesktopHostFixture::requestReconnect() {

  if (exiting_) {
    return contracts::HostMaintenanceResult::Exiting;
  }
  if (!attached_) {
    return contracts::HostMaintenanceResult::Failed;
  }
  ++epoch_;
  ++shellGeneration_;
  ++lifecycleEpoch_;
  visible_ = false;
  return contracts::HostMaintenanceResult::ReattachedNeedsFrame;
}
contracts::HostMaintenanceResult Win32DesktopHostFixture::refreshDisplayEnvironment() {
  return exiting_ ? contracts::HostMaintenanceResult::Exiting
                  : contracts::HostMaintenanceResult::Unchanged;
}


contracts::HostObservation Win32DesktopHostFixture::observe() const noexcept {
  contracts::HostObservation observation{};
  observation.topology = contracts::HostTopology::RaisedProgman;
  observation.state = exiting_ ? contracts::HostState::Exiting
                               : attached_ ? contracts::HostState::AttachedSafe
                                           : contracts::HostState::Detached;
  observation.shellGeneration = shellGeneration_;
  observation.lifecycleEpoch = lifecycleEpoch_;
  observation.surfaceEpoch = epoch_;
  observation.requestedBounds = bounds_;
  observation.safeTargetUnique = attached_;
  observation.attached = attached_;
  observation.hitRegionRevision = hitRevision_;
  observation.hostProjectionRevision = projectionRevision_;
  observation.displayEnvironment = displayEnvironment_;
  observation.inputEnabled = hitRevision_ != 0;
  observation.inputIslandCount = 0;
  observation.visible = visible_;
  observation.hostAboveDefView = attached_;
  return observation;
}

void Win32DesktopHostFixture::beginExit() noexcept {
  exiting_ = true;
  visible_ = false;
}

void Win32DesktopHostFixture::detach() noexcept {
  attached_ = false;
  visible_ = false;
  epoch_ = 0;
  bounds_ = {};
  hitRevision_ = 0;
  projectionRevision_ = 0;
}
bool Win32DesktopHostFixture::attached() const noexcept {
  return attached_;
}

bool Win32DesktopHostFixture::exiting() const noexcept {
  return exiting_;
}

std::uint64_t Win32DesktopHostFixture::presentCount() const noexcept {
  return presentCount_;
}

}  // namespace desktopup::adapters
