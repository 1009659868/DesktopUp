#include "core/runtime_core.h"

namespace desktopup::core {

bool AcceptedState::acceptStoreResult(const contracts::StoreResult& result) {
  if ((result.status != contracts::StoreStatus::Loaded &&
       result.status != contracts::StoreStatus::CommitAccepted) ||
      !result.snapshot.has_value()) {
    lastError_ = result.error.code;
    return false;
  }

  if (accepted_.has_value() && result.snapshot->revision < accepted_->revision) {
    lastError_ = contracts::ErrorCode::StaleResult;
    return false;
  }

  accepted_ = result.snapshot;
  lastError_ = contracts::ErrorCode::None;
  return true;
}

std::optional<contracts::Snapshot> AcceptedState::snapshot() const {
  return accepted_;
}

contracts::ErrorCode AcceptedState::lastError() const noexcept {
  return lastError_;
}

void FrameGate::configure(const contracts::Revision revision,
                          const contracts::SurfaceEpoch epoch,
                          const contracts::PixelSize bounds) noexcept {
  if (exiting_) {
    return;
  }
  revision_ = revision;
  epoch_ = epoch;
  bounds_ = bounds;
  configured_ = true;
  firstFramePublished_ = false;
}

bool FrameGate::accept(const contracts::FrameDescriptor& frame) noexcept {
  if (exiting_ || !configured_ || frame.buffer == 0 || frame.revision != revision_ ||
      frame.surfaceEpoch != epoch_ || frame.bounds != bounds_) {
    return false;
  }
  firstFramePublished_ = true;
  return true;
}

void FrameGate::beginExit() noexcept {
  exiting_ = true;
}

bool FrameGate::exiting() const noexcept {
  return exiting_;
}

bool FrameGate::firstFramePublished() const noexcept {
  return firstFramePublished_;
}

bool SnapshotCursor::observe(const contracts::Revision revision) noexcept {
  if (revision == 0 || revision < revision_) {
    return false;
  }
  revision_ = revision;
  return true;
}

contracts::Revision SnapshotCursor::revision() const noexcept {
  return revision_;
}

}  // namespace desktopup::core
