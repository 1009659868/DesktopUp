#pragma once

#include "contracts/types.h"

#include <functional>
#include <memory>
#include <string>

namespace desktopup::adapters {

struct IpcIntentResult {
  bool accepted{false};
  contracts::ErrorCode error{contracts::ErrorCode::None};
  contracts::RequestId requestId{};
};

class RuntimeInstanceLock {
 public:
  RuntimeInstanceLock();
  ~RuntimeInstanceLock();
  RuntimeInstanceLock(const RuntimeInstanceLock&) = delete;
  RuntimeInstanceLock& operator=(const RuntimeInstanceLock&) = delete;

  bool acquire(std::wstring objectName);
  void release() noexcept;
  [[nodiscard]] bool primary() const noexcept;

 private:
  struct Impl;
  std::unique_ptr<Impl> impl_;
};

class RuntimeIpcServer {
 public:
  using IntentHandler = std::function<IpcIntentResult(contracts::LaunchIntent, contracts::RequestId)>;

  RuntimeIpcServer();
  ~RuntimeIpcServer();
  RuntimeIpcServer(const RuntimeIpcServer&) = delete;
  RuntimeIpcServer& operator=(const RuntimeIpcServer&) = delete;

  bool start(std::wstring pipeName, IntentHandler handler);
  void stop() noexcept;
  [[nodiscard]] bool running() const noexcept;
  [[nodiscard]] std::uint64_t rejectedPeers() const noexcept;
  [[nodiscard]] std::uint64_t rejectedMessages() const noexcept;

 private:
  struct Impl;
  std::unique_ptr<Impl> impl_;
};

class RuntimeIpcClient {
 public:
  static IpcIntentResult sendIntent(const std::wstring& pipeName,
                                    contracts::LaunchIntent intent,
                                    contracts::RequestId requestId,
                                    std::uint32_t timeoutMs = 2000);
};

std::wstring normalizePipeName(const std::wstring& pipeName);
std::wstring currentSessionObjectName(const std::wstring& baseName);
std::wstring currentSessionPipeName(const std::wstring& baseName);

}  // namespace desktopup::adapters
