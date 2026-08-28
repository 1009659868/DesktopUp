#pragma once

#include <cstdint>
#include <filesystem>
#include <map>
#include <mutex>
#include <string>

namespace desktopup::adapters {

class ResourceLedger {
 public:
  void acquire(std::string name);
  void release(const std::string& name) noexcept;
  [[nodiscard]] std::uint64_t total() const noexcept;
  [[nodiscard]] std::map<std::string, std::uint64_t> snapshot() const;
  [[nodiscard]] bool writeJson(const std::filesystem::path& path,
                               std::uint64_t revision,
                               std::uint64_t surfaceEpoch,
                               std::string terminalState) const;

 private:
  mutable std::mutex mutex_;
  std::map<std::string, std::uint64_t> resources_;
};

std::string redactDiagnosticText(std::string value);

}  // namespace desktopup::adapters
