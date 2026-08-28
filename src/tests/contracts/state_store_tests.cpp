#include "adapters/state-store-sqlite/sqlite_state_store.h"
#include "adapters/state-store-sqlite/state_store_worker.h"

#include <windows.h>

#include <chrono>
#include <filesystem>
#include <fstream>
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
  const auto root = std::filesystem::temp_directory_path() /
                    (L"DesktopUp-QTS03-State-" + std::to_wstring(GetCurrentProcessId()));
  std::error_code error;
  std::filesystem::remove_all(root, error);
  std::filesystem::create_directories(root, error);
  const auto database = root / L"state.sqlite";

  adapters::SqliteStateStore store;
  auto result = store.open(database);
  expect(result.status == contracts::StoreStatus::NoData, "new database must report NoData");

  result = store.commit(0, R"({"schemaVersion":1})", 10);
  expect(result.status == contracts::StoreStatus::CommitAccepted, "InitializeState commit must pass CAS");
  expect(result.snapshot.has_value() && result.snapshot->revision == 1, "InitializeState must create revision 1");

  result = store.commit(0, "stale", 11);
  expect(result.status == contracts::StoreStatus::Conflict, "stale expected revision must conflict");
  expect(result.snapshot.has_value() && result.snapshot->revision == 1, "conflict returns current accepted revision");

  result = store.commit(1, R"({"schemaVersion":1,"value":2})", 12);
  expect(result.status == contracts::StoreStatus::CommitAccepted && result.snapshot->revision == 2,
         "matching expected revision must commit");
  store.close();

  adapters::StateStoreWorker worker(database);
  const auto startedAt = std::chrono::steady_clock::now();
  auto started = worker.start();
  const auto returnedAfter = std::chrono::steady_clock::now() - startedAt;
  expect(returnedAfter < std::chrono::milliseconds(100), "worker API must return without blocking coordinator");
  expect(started.get().status == contracts::StoreStatus::Loaded, "worker must load accepted state");
  worker.stop();
  expect(worker.stopped(), "worker must reach terminal state");

  const auto corruptPath = root / L"corrupt.sqlite";
  {
    std::ofstream corrupt(corruptPath, std::ios::binary);
    corrupt << "not-a-sqlite-database";
  }
  adapters::SqliteStateStore corruptStore;
  result = corruptStore.open(corruptPath);
  expect(result.status == contracts::StoreStatus::Corrupt, "Corrupt must stay distinct from NoData");
  corruptStore.close();

  std::filesystem::remove_all(root, error);
  if (failures == 0) {
    std::cout << "QTS03_STATE_STORE_PASS\n";
  }
  return failures == 0 ? 0 : 1;
}
