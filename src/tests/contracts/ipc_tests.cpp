#include "adapters/runtime-ipc-win32/runtime_ipc.h"
#include "contracts/ipc_protocol.h"

#include <windows.h>

#include <atomic>
#include <iostream>
#include <string>

namespace {

int failures{};

void expect(const bool condition, const char* message) {
  if (!condition) {
    std::cerr << "FAIL: " << message << '\n';
    ++failures;
  }
}

bool sendOversizedHello(const std::wstring& name) {
  const auto pipeName = desktopup::adapters::normalizePipeName(name);
  HANDLE pipe = INVALID_HANDLE_VALUE;
  const ULONGLONG deadline = GetTickCount64() + 2000;
  while (GetTickCount64() < deadline) {
    pipe = CreateFileW(pipeName.c_str(),
                       GENERIC_READ | GENERIC_WRITE,
                       0,
                       nullptr,
                       OPEN_EXISTING,
                       0,
                       nullptr);
    if (pipe != INVALID_HANDLE_VALUE) {
      break;
    }
    if (GetLastError() == ERROR_PIPE_BUSY) {
      WaitNamedPipeW(pipeName.c_str(), 20);
    } else {
      Sleep(1);
    }
  }
  if (pipe == INVALID_HANDLE_VALUE) {
    return false;
  }
  desktopup::contracts::ipc::Header header;
  header.kind = static_cast<std::uint16_t>(desktopup::contracts::ipc::MessageKind::Hello);
  header.payloadBytes = desktopup::contracts::ipc::kMaxPayloadBytes + 1;
  DWORD written{};
  const bool sent = WriteFile(pipe, &header, sizeof(header), &written, nullptr) != FALSE &&
                    written == sizeof(header);
  char ignored{};
  DWORD ignoredBytes{};
  ReadFile(pipe, &ignored, sizeof(ignored), &ignoredBytes, nullptr);
  CloseHandle(pipe);
  return sent;
}

}  // namespace

int main() {
  using namespace desktopup;
  const std::wstring pipeName = L"DesktopUp.QTS03.IPC." + std::to_wstring(GetCurrentProcessId());
  std::atomic_uint64_t ordinary{};
  std::atomic_uint64_t autostart{};
  std::atomic_uint64_t controlCenter{};

  const auto instanceName =
      adapters::currentSessionObjectName(L"DesktopUp.QTS03.Instance." + std::to_wstring(GetCurrentProcessId()));
  adapters::RuntimeInstanceLock primaryLock;
  adapters::RuntimeInstanceLock secondaryLock;
  expect(primaryLock.acquire(instanceName) && primaryLock.primary(), "first same-session instance must be primary");
  expect(secondaryLock.acquire(instanceName) && !secondaryLock.primary(),
         "ordinary/autostart second instance must become forwarding broker");

  adapters::RuntimeIpcServer server;
  expect(server.start(pipeName, [&](const contracts::LaunchIntent intent, const contracts::RequestId requestId) {
           if (intent == contracts::LaunchIntent::Ordinary) {
             ++ordinary;
           } else if (intent == contracts::LaunchIntent::Autostart) {
             ++autostart;
           } else if (intent == contracts::LaunchIntent::OpenControlCenter) {
             ++controlCenter;
           }
           return adapters::IpcIntentResult{true, contracts::ErrorCode::None, requestId};
         }),
         "per-user named pipe server must start");

  const auto primary = adapters::RuntimeIpcClient::sendIntent(pipeName, contracts::LaunchIntent::Ordinary, 101);
  const auto autoStart = adapters::RuntimeIpcClient::sendIntent(pipeName, contracts::LaunchIntent::Autostart, 102);
  const auto control =
      adapters::RuntimeIpcClient::sendIntent(pipeName, contracts::LaunchIntent::OpenControlCenter, 103);
  expect(primary.accepted && primary.requestId == 101, "ordinary secondary intent must forward");
  expect(autoStart.accepted && autoStart.requestId == 102, "autostart secondary intent must forward");
  expect(control.accepted && control.requestId == 103, "control-center intent must forward");
  expect(ordinary == 1 && autostart == 1 && controlCenter == 1, "intent routing must stay distinct");

  const auto unknown = adapters::RuntimeIpcClient::sendIntent(
      pipeName, static_cast<contracts::LaunchIntent>(999), 104);
  expect(!unknown.accepted && unknown.error == contracts::ErrorCode::UnknownRequiredMessage,
         "unknown required intent must fail closed");
  expect(sendOversizedHello(pipeName), "oversized message fixture must reach server");
  for (int attempt = 0; attempt < 200 && server.rejectedMessages() < 1; ++attempt) {
    Sleep(5);
  }
  expect(server.rejectedMessages() >= 1, "oversized message must be rejected");

  server.stop();
  expect(!server.running(), "IPC worker must reach terminal state");
  const auto afterStop = adapters::RuntimeIpcClient::sendIntent(
      pipeName, contracts::LaunchIntent::Ordinary, 105, 20);
  expect(!afterStop.accepted, "pipe must not survive server exit");
  secondaryLock.release();
  primaryLock.release();

  if (failures == 0) {
    std::cout << "QTS03_IPC_PASS\n";
  }
  return failures == 0 ? 0 : 1;
}
