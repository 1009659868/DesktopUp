#include "core/runtime_core.h"

#include <iostream>

namespace {

int failures{};

void expect(const bool condition, const char* message) {
  if (!condition) {
    std::cerr << "FAIL: " << message << '\n';
    ++failures;
  }
}

}  // namespace

int main() {
  using namespace desktopup;

  core::AcceptedState state;
  expect(!state.acceptStoreResult({contracts::StoreStatus::NoData,
                                   std::nullopt,
                                   {contracts::ErrorCode::NoData, 1, "no data"}}),
         "NoData must not become accepted state");
  expect(state.acceptStoreResult({contracts::StoreStatus::CommitAccepted,
                                  contracts::Snapshot{1, "v1"},
                                  {contracts::ErrorCode::None, 2, {}}}),
         "InitializeState commit should be accepted");
  expect(state.acceptStoreResult({contracts::StoreStatus::CommitAccepted,
                                  contracts::Snapshot{2, "v2"},
                                  {contracts::ErrorCode::None, 3, {}}}),
         "newer commit should be accepted");
  expect(!state.acceptStoreResult({contracts::StoreStatus::Loaded,
                                   contracts::Snapshot{1, "stale"},
                                   {}}),
         "stale store result must be rejected");
  expect(state.snapshot()->revision == 2, "stale result must not roll revision backward");
  expect(state.lastError() == contracts::ErrorCode::StaleResult, "stale result needs stable error code");

  core::FrameGate gate;
  constexpr contracts::PixelSize bounds{320, 480};
  gate.configure(4, 7, bounds);
  expect(!gate.firstFramePublished(), "first-frame gate starts closed");
  expect(!gate.accept({3, 7, bounds, 1}), "stale revision frame must be rejected");
  expect(!gate.accept({4, 6, bounds, 1}), "stale epoch frame must be rejected");
  expect(!gate.accept({4, 7, {321, 480}, 1}), "wrong bounds frame must be rejected");
  expect(gate.accept({4, 7, bounds, 1}), "matching first frame should open publish gate");
  expect(gate.firstFramePublished(), "first frame should be recorded");
  gate.beginExit();
  gate.configure(5, 8, bounds);
  expect(gate.exiting(), "exit is sticky");
  expect(!gate.accept({4, 7, bounds, 2}), "late frame must be rejected after BeginExit");

  core::SnapshotCursor cursor;
  expect(cursor.observe(8), "first accepted snapshot event must advance subscription");
  expect(cursor.observe(8), "duplicate accepted snapshot event must be idempotent");
  expect(!cursor.observe(7), "accepted snapshot subscription must reject revision rollback");
  expect(cursor.revision() == 8, "subscription cursor must stay monotonic");

  if (failures == 0) {
    std::cout << "QTS03_CONTRACTS_PASS\n";
  }
  return failures == 0 ? 0 : 1;
}
