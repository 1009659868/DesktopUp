#include "adapters/diagnostics/resource_ledger.h"

#include <fstream>
#include <regex>
#include <utility>

namespace desktopup::adapters {

void ResourceLedger::acquire(std::string name) {
  std::scoped_lock lock(mutex_);
  ++resources_[std::move(name)];
}

void ResourceLedger::release(const std::string& name) noexcept {
  std::scoped_lock lock(mutex_);
  const auto found = resources_.find(name);
  if (found == resources_.end()) {
    return;
  }
  if (found->second > 1) {
    --found->second;
  } else {
    resources_.erase(found);
  }
}

std::uint64_t ResourceLedger::total() const noexcept {
  std::scoped_lock lock(mutex_);
  std::uint64_t sum{};
  for (const auto& [_, count] : resources_) {
    sum += count;
  }
  return sum;
}

std::map<std::string, std::uint64_t> ResourceLedger::snapshot() const {
  std::scoped_lock lock(mutex_);
  return resources_;
}

bool ResourceLedger::writeJson(const std::filesystem::path& path,
                               const std::uint64_t revision,
                               const std::uint64_t surfaceEpoch,
                               std::string terminalState) const {
  std::error_code error;
  std::filesystem::create_directories(path.parent_path(), error);
  std::ofstream output(path, std::ios::binary | std::ios::trunc);
  if (!output) {
    return false;
  }
  output << "{\n"
         << "  \"terminalState\": \"" << redactDiagnosticText(std::move(terminalState)) << "\",\n"
         << "  \"revision\": " << revision << ",\n"
         << "  \"surfaceEpoch\": " << surfaceEpoch << ",\n"
         << "  \"ownedResourceCount\": " << total() << "\n"
         << "}\n";
  return output.good();
}

std::string redactDiagnosticText(std::string value) {
  static const std::regex windowsPath(R"([A-Za-z]:\\[^\r\n\"]+)");
  return std::regex_replace(value, windowsPath, "<redacted-path>");
}

}  // namespace desktopup::adapters
