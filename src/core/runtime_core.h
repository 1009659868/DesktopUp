#pragma once

#include "contracts/types.h"

#include <optional>

namespace desktopup::core {

class AcceptedState {
 public:
  bool acceptStoreResult(const contracts::StoreResult& result);
  [[nodiscard]] std::optional<contracts::Snapshot> snapshot() const;
  [[nodiscard]] contracts::ErrorCode lastError() const noexcept;

 private:
  std::optional<contracts::Snapshot> accepted_;
  contracts::ErrorCode lastError_{contracts::ErrorCode::None};
};

class FrameGate {
 public:
  void configure(contracts::Revision revision,
                 contracts::SurfaceEpoch epoch,
                 contracts::PixelSize bounds) noexcept;
  [[nodiscard]] bool accept(const contracts::FrameDescriptor& frame) noexcept;
  void beginExit() noexcept;
  [[nodiscard]] bool exiting() const noexcept;
  [[nodiscard]] bool firstFramePublished() const noexcept;

 private:
  contracts::Revision revision_{};
  contracts::SurfaceEpoch epoch_{};
  contracts::PixelSize bounds_{};
  bool configured_{false};
  bool firstFramePublished_{false};
  bool exiting_{false};
};

class SnapshotCursor {
 public:
  [[nodiscard]] bool observe(contracts::Revision revision) noexcept;
  [[nodiscard]] contracts::Revision revision() const noexcept;

 private:
  contracts::Revision revision_{};
};

}  // namespace desktopup::core
