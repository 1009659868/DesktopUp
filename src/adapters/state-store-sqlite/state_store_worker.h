#pragma once

#include "adapters/state-store-sqlite/sqlite_state_store.h"

#include <condition_variable>
#include <deque>
#include <filesystem>
#include <future>
#include <functional>
#include <mutex>
#include <thread>

namespace desktopup::adapters {

class StateStoreWorker {
 public:
  explicit StateStoreWorker(std::filesystem::path path);
  ~StateStoreWorker();

  StateStoreWorker(const StateStoreWorker&) = delete;
  StateStoreWorker& operator=(const StateStoreWorker&) = delete;

  std::future<contracts::StoreResult> start();
  std::future<contracts::StoreResult> load();
  std::future<contracts::StoreResult> commit(contracts::Revision expectedRevision,
                                             std::string payload,
                                             contracts::RequestId requestId);
  void stop() noexcept;
  [[nodiscard]] bool stopped() const noexcept;

 private:
  using Task = std::function<void()>;
  std::future<contracts::StoreResult> enqueue(std::function<contracts::StoreResult()> action);
  void run(std::stop_token stopToken);

  std::filesystem::path path_;
  SqliteStateStore store_;
  std::jthread thread_;
  mutable std::mutex mutex_;
  std::condition_variable_any changed_;
  std::deque<Task> tasks_;
  bool stopping_{false};
  bool stopped_{false};
};

}  // namespace desktopup::adapters
