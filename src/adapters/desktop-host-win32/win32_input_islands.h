#pragma once

#include "contracts/types.h"

#include <windows.h>

#include <functional>
#include <memory>
#include <vector>

namespace desktopup::adapters {

struct InputIslandObservation {
  contracts::HitRegionRevision hitRevision{};
  contracts::HostProjectionRevision projectionRevision{};
  std::vector<std::uint64_t> windowHandles;
  std::uint64_t eventCount{};
  bool inputEnabled{false};
};

class Win32InputIslandSet final {
 public:
  using InputCallback = std::function<void(const contracts::HostInputEvent&)>;

  Win32InputIslandSet();
  ~Win32InputIslandSet();

  Win32InputIslandSet(const Win32InputIslandSet&) = delete;
  Win32InputIslandSet& operator=(const Win32InputIslandSet&) = delete;

  [[nodiscard]] bool initialize(HWND parent, HWND visual);
  [[nodiscard]] bool publish(const contracts::HitRegionSet& regions,
                             contracts::PixelRect visualScreenBounds,
                             bool reveal);
  [[nodiscard]] bool relocate(const contracts::HitRegionSet& regions,
                              contracts::PixelRect visualScreenBounds);
  void setInputCallback(InputCallback callback);
  void clear() noexcept;

  [[nodiscard]] InputIslandObservation observe() const noexcept;

 private:
  struct Impl;
  std::unique_ptr<Impl> impl_;
};

}  // namespace desktopup::adapters
