#pragma once

#include "contracts/types.h"

#include <filesystem>
#include <functional>

namespace desktopup::contracts {

class IStateStore {
 public:
  virtual ~IStateStore() = default;
  virtual StoreResult open(const std::filesystem::path& path) = 0;
  virtual StoreResult load() = 0;
  virtual StoreResult commit(Revision expectedRevision, std::string payload, RequestId requestId) = 0;
  virtual void close() noexcept = 0;
};

class IRenderer {
 public:
  virtual ~IRenderer() = default;
  virtual FrameDescriptor render(const RenderRequest& request) = 0;
  virtual PremultipliedFrameView frameView(BufferHandle buffer) const noexcept = 0;
  virtual void release(BufferHandle buffer) noexcept = 0;
  virtual void beginExit() noexcept = 0;
};

class IDesktopHost {
 public:
  using InputCallback = std::function<void(const HostInputEvent&)>;

  virtual ~IDesktopHost() = default;
  virtual bool attach(SurfaceEpoch epoch, PixelRect screenBounds) = 0;
  virtual bool present(PixelRect screenBounds, const FrameDescriptor& frame,
                       const PremultipliedFrameView& pixels,
                       const HitRegionSet& hitRegions) = 0;
  virtual bool relocate(PixelRect screenBounds,
                        const HitRegionSet& hitRegions) = 0;
  virtual void setInputCallback(InputCallback callback) = 0;
  virtual HostMaintenanceResult requestReconnect() = 0;
  virtual HostMaintenanceResult refreshDisplayEnvironment() = 0;
  virtual HostObservation observe() const noexcept = 0;
  virtual void beginExit() noexcept = 0;
  virtual void detach() noexcept = 0;
};

}  // namespace desktopup::contracts
