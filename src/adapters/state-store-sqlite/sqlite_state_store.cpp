#include "adapters/state-store-sqlite/sqlite_state_store.h"

#include <winsqlite/winsqlite3.h>

#include <limits>
#include <utility>

namespace desktopup::adapters {
namespace {

constexpr char kCreateSchema[] =
    "CREATE TABLE IF NOT EXISTS state_snapshot ("
    "singleton INTEGER PRIMARY KEY CHECK(singleton = 1),"
    "revision INTEGER NOT NULL CHECK(revision >= 1),"
    "payload TEXT NOT NULL);";

contracts::ErrorCode classifySqliteError(const int code) {
  return code == SQLITE_CORRUPT || code == SQLITE_NOTADB ? contracts::ErrorCode::CorruptData
                                                         : contracts::ErrorCode::IoFailure;
}

}  // namespace

SqliteStateStore::~SqliteStateStore() {
  close();
}

contracts::StoreResult SqliteStateStore::open(const std::filesystem::path& path) {
  close();
  const auto wide = path.wstring();
  const int rc = sqlite3_open16(wide.c_str(), &db_);
  if (rc != SQLITE_OK) {
    const auto code = classifySqliteError(rc);
    close();
    return failure(code == contracts::ErrorCode::CorruptData ? contracts::StoreStatus::Corrupt
                                                              : contracts::StoreStatus::IoError,
                   code,
                   0,
                   "state database open failed");
  }

  sqlite3_busy_timeout(db_, 2000);
  if (!execute("PRAGMA journal_mode=WAL;") || !execute("PRAGMA synchronous=FULL;") ||
      !execute(kCreateSchema)) {
    const int error = sqlite3_errcode(db_);
    const auto code = classifySqliteError(error);
    close();
    return failure(code == contracts::ErrorCode::CorruptData ? contracts::StoreStatus::Corrupt
                                                              : contracts::StoreStatus::IoError,
                   code,
                   0,
                   "state database schema validation failed");
  }
  return load();
}

contracts::StoreResult SqliteStateStore::load() {
  if (!db_) {
    return failure(contracts::StoreStatus::Closed,
                   contracts::ErrorCode::IoFailure,
                   0,
                   "state database is closed");
  }

  sqlite3_stmt* statement{};
  const int prepare = sqlite3_prepare_v2(
      db_, "SELECT revision, payload FROM state_snapshot WHERE singleton = 1;", -1, &statement, nullptr);
  if (prepare != SQLITE_OK) {
    const auto code = classifySqliteError(prepare);
    return failure(code == contracts::ErrorCode::CorruptData ? contracts::StoreStatus::Corrupt
                                                              : contracts::StoreStatus::IoError,
                   code,
                   0,
                   "state snapshot query failed");
  }

  const int step = sqlite3_step(statement);
  if (step == SQLITE_DONE) {
    sqlite3_finalize(statement);
    return {contracts::StoreStatus::NoData,
            std::nullopt,
            {contracts::ErrorCode::NoData, 0, "state snapshot is not initialized"}};
  }
  if (step != SQLITE_ROW) {
    const auto code = classifySqliteError(step);
    sqlite3_finalize(statement);
    return failure(code == contracts::ErrorCode::CorruptData ? contracts::StoreStatus::Corrupt
                                                              : contracts::StoreStatus::IoError,
                   code,
                   0,
                   "state snapshot read failed");
  }

  const auto rawRevision = sqlite3_column_int64(statement, 0);
  const auto* rawPayload = reinterpret_cast<const char*>(sqlite3_column_text(statement, 1));
  const int payloadBytes = sqlite3_column_bytes(statement, 1);
  if (rawRevision < 1 || rawPayload == nullptr || payloadBytes < 0) {
    sqlite3_finalize(statement);
    return failure(contracts::StoreStatus::Corrupt,
                   contracts::ErrorCode::CorruptData,
                   0,
                   "state snapshot row is invalid");
  }

  contracts::Snapshot snapshot{static_cast<contracts::Revision>(rawRevision),
                               std::string(rawPayload, static_cast<std::size_t>(payloadBytes))};
  sqlite3_finalize(statement);
  return {contracts::StoreStatus::Loaded, std::move(snapshot), {}};
}

contracts::StoreResult SqliteStateStore::commit(const contracts::Revision expectedRevision,
                                                 std::string payload,
                                                 const contracts::RequestId requestId) {
  if (!db_) {
    return failure(contracts::StoreStatus::Closed,
                   contracts::ErrorCode::IoFailure,
                   requestId,
                   "state database is closed");
  }
  if (expectedRevision == std::numeric_limits<contracts::Revision>::max()) {
    return failure(contracts::StoreStatus::IoError,
                   contracts::ErrorCode::InvalidArgument,
                   requestId,
                   "revision overflow");
  }

  if (!execute("BEGIN IMMEDIATE TRANSACTION;")) {
    return failure(contracts::StoreStatus::IoError,
                   contracts::ErrorCode::IoFailure,
                   requestId,
                   "state transaction begin failed");
  }

  const auto current = load();
  const contracts::Revision currentRevision =
      current.status == contracts::StoreStatus::NoData ? 0 : current.snapshot.value_or(contracts::Snapshot{}).revision;
  if ((current.status != contracts::StoreStatus::NoData && current.status != contracts::StoreStatus::Loaded) ||
      currentRevision != expectedRevision) {
    execute("ROLLBACK;");
    if (current.status == contracts::StoreStatus::Loaded || current.status == contracts::StoreStatus::NoData) {
      return {contracts::StoreStatus::Conflict,
              current.snapshot,
              {contracts::ErrorCode::RevisionConflict, requestId, "expected revision conflict"}};
    }
    return current;
  }

  sqlite3_stmt* statement{};
  constexpr char sql[] =
      "INSERT INTO state_snapshot(singleton, revision, payload) VALUES(1, ?, ?) "
      "ON CONFLICT(singleton) DO UPDATE SET revision = excluded.revision, payload = excluded.payload;";
  if (sqlite3_prepare_v2(db_, sql, -1, &statement, nullptr) != SQLITE_OK) {
    execute("ROLLBACK;");
    return failure(contracts::StoreStatus::IoError,
                   contracts::ErrorCode::IoFailure,
                   requestId,
                   "state commit prepare failed");
  }

  const auto nextRevision = expectedRevision + 1;
  sqlite3_bind_int64(statement, 1, static_cast<sqlite3_int64>(nextRevision));
  sqlite3_bind_text(statement,
                    2,
                    payload.data(),
                    static_cast<int>(payload.size()),
                    SQLITE_TRANSIENT);
  const int step = sqlite3_step(statement);
  sqlite3_finalize(statement);
  if (step != SQLITE_DONE || !execute("COMMIT;")) {
    execute("ROLLBACK;");
    return failure(contracts::StoreStatus::IoError,
                   contracts::ErrorCode::IoFailure,
                   requestId,
                   "state commit failed");
  }

  return {contracts::StoreStatus::CommitAccepted,
          contracts::Snapshot{nextRevision, std::move(payload)},
          {contracts::ErrorCode::None, requestId, {}}};
}

void SqliteStateStore::close() noexcept {
  if (db_) {
    sqlite3_close(db_);
    db_ = nullptr;
  }
}

bool SqliteStateStore::isOpen() const noexcept {
  return db_ != nullptr;
}

contracts::StoreResult SqliteStateStore::failure(const contracts::StoreStatus status,
                                                  const contracts::ErrorCode code,
                                                  const contracts::RequestId requestId,
                                                  std::string message) const {
  return {status, std::nullopt, {code, requestId, std::move(message)}};
}

bool SqliteStateStore::execute(const char* sql) {
  char* error{};
  const int rc = sqlite3_exec(db_, sql, nullptr, nullptr, &error);
  if (error) {
    sqlite3_free(error);
  }
  return rc == SQLITE_OK;
}

}  // namespace desktopup::adapters
