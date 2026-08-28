#pragma once

#include "contracts/ports.h"

struct sqlite3;

namespace desktopup::adapters {

class SqliteStateStore final : public contracts::IStateStore {
 public:
  SqliteStateStore() = default;
  ~SqliteStateStore() override;

  SqliteStateStore(const SqliteStateStore&) = delete;
  SqliteStateStore& operator=(const SqliteStateStore&) = delete;

  contracts::StoreResult open(const std::filesystem::path& path) override;
  contracts::StoreResult load() override;
  contracts::StoreResult commit(contracts::Revision expectedRevision,
                                std::string payload,
                                contracts::RequestId requestId) override;
  void close() noexcept override;
  [[nodiscard]] bool isOpen() const noexcept;

 private:
  contracts::StoreResult failure(contracts::StoreStatus status,
                                 contracts::ErrorCode code,
                                 contracts::RequestId requestId,
                                 std::string message) const;
  bool execute(const char* sql);

  sqlite3* db_{};
};

}  // namespace desktopup::adapters
