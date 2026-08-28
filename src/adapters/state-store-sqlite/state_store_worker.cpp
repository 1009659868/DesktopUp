#include "adapters/state-store-sqlite/state_store_worker.h"

#include <utility>

namespace desktopup::adapters {

StateStoreWorker::StateStoreWorker(std::filesystem::path path) : path_(std::move(path)) {}

StateStoreWorker::~StateStoreWorker() {
  stop();
}

std::future<contracts::StoreResult> StateStoreWorker::start() {
  {
    std::scoped_lock lock(mutex_);
    if (!thread_.joinable()) {
      stopped_ = false;
      stopping_ = false;
      thread_ = std::jthread([this](const std::stop_token token) { run(token); });
    }
  }
  return enqueue([this] { return store_.open(path_); });
}

std::future<contracts::StoreResult> StateStoreWorker::load() {
  return enqueue([this] { return store_.load(); });
}

std::future<contracts::StoreResult> StateStoreWorker::commit(const contracts::Revision expectedRevision,
                                                             std::string payload,
                                                             const contracts::RequestId requestId) {
  return enqueue([this, expectedRevision, payload = std::move(payload), requestId]() mutable {
    return store_.commit(expectedRevision, std::move(payload), requestId);
  });
}

void StateStoreWorker::stop() noexcept {
  {
    std::scoped_lock lock(mutex_);
    if (!thread_.joinable()) {
      stopped_ = true;
      return;
    }
    stopping_ = true;
  }
  thread_.request_stop();
  changed_.notify_all();
  thread_.join();
}

bool StateStoreWorker::stopped() const noexcept {
  std::scoped_lock lock(mutex_);
  return stopped_;
}

std::future<contracts::StoreResult> StateStoreWorker::enqueue(
    std::function<contracts::StoreResult()> action) {
  auto task = std::make_shared<std::packaged_task<contracts::StoreResult()>>(std::move(action));
  auto future = task->get_future();
  {
    std::scoped_lock lock(mutex_);
    if (stopping_ || !thread_.joinable()) {
      std::promise<contracts::StoreResult> rejected;
      rejected.set_value({contracts::StoreStatus::Closed,
                          std::nullopt,
                          {contracts::ErrorCode::Exiting, 0, "state worker is stopped"}});
      return rejected.get_future();
    }
    tasks_.emplace_back([task] { (*task)(); });
  }
  changed_.notify_one();
  return future;
}

void StateStoreWorker::run(const std::stop_token stopToken) {
  for (;;) {
    Task task;
    {
      std::unique_lock lock(mutex_);
      changed_.wait(lock, stopToken, [this] { return !tasks_.empty() || stopping_; });
      if (tasks_.empty() && (stopping_ || stopToken.stop_requested())) {
        break;
      }
      if (!tasks_.empty()) {
        task = std::move(tasks_.front());
        tasks_.pop_front();
      }
    }
    if (task) {
      task();
    }
  }
  store_.close();
  std::scoped_lock lock(mutex_);
  tasks_.clear();
  stopped_ = true;
}

}  // namespace desktopup::adapters
