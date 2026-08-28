#include "adapters/runtime-ipc-win32/runtime_ipc.h"

#include "contracts/ipc_protocol.h"

#include <windows.h>
#include <sddl.h>

#include <atomic>
#include <cstring>
#include <mutex>
#include <thread>
#include <utility>
#include <vector>

namespace desktopup::adapters {
namespace {

using namespace contracts::ipc;

class LocalHandle {
 public:
  LocalHandle() = default;
  explicit LocalHandle(HANDLE value) : value_(value) {}
  ~LocalHandle() { reset(); }
  LocalHandle(const LocalHandle&) = delete;
  LocalHandle& operator=(const LocalHandle&) = delete;
  LocalHandle(LocalHandle&& other) noexcept : value_(std::exchange(other.value_, INVALID_HANDLE_VALUE)) {}
  LocalHandle& operator=(LocalHandle&& other) noexcept {
    if (this != &other) {
      reset();
      value_ = std::exchange(other.value_, INVALID_HANDLE_VALUE);
    }
    return *this;
  }
  [[nodiscard]] HANDLE get() const noexcept { return value_; }
  [[nodiscard]] bool valid() const noexcept {
    return value_ != nullptr && value_ != INVALID_HANDLE_VALUE;
  }
  void reset(HANDLE next = INVALID_HANDLE_VALUE) noexcept {
    if (valid()) {
      CloseHandle(value_);
    }
    value_ = next;
  }

 private:
  HANDLE value_{INVALID_HANDLE_VALUE};
};

std::vector<std::byte> currentUserSid() {
  LocalHandle token;
  HANDLE rawToken{};
  if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &rawToken)) {
    return {};
  }
  token.reset(rawToken);
  DWORD bytes{};
  GetTokenInformation(token.get(), TokenUser, nullptr, 0, &bytes);
  std::vector<std::byte> buffer(bytes);
  if (bytes == 0 || !GetTokenInformation(token.get(), TokenUser, buffer.data(), bytes, &bytes)) {
    return {};
  }
  const auto* user = reinterpret_cast<const TOKEN_USER*>(buffer.data());
  const DWORD sidBytes = GetLengthSid(user->User.Sid);
  std::vector<std::byte> sid(sidBytes);
  if (!CopySid(sidBytes, sid.data(), user->User.Sid)) {
    return {};
  }
  return sid;
}

struct PipeSecurity {
  SECURITY_ATTRIBUTES attributes{};
  PSECURITY_DESCRIPTOR descriptor{};

  ~PipeSecurity() {
    if (descriptor) {
      LocalFree(descriptor);
    }
  }

  bool initialize(const std::vector<std::byte>& sid) {
    if (sid.empty()) {
      return false;
    }
    LPWSTR sidString{};
    if (!ConvertSidToStringSidW(const_cast<void*>(static_cast<const void*>(sid.data())), &sidString)) {
      return false;
    }
    const std::wstring sddl = L"D:P(A;;GA;;;SY)(A;;GA;;;" + std::wstring(sidString) + L")";
    LocalFree(sidString);
    if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(
            sddl.c_str(), SDDL_REVISION_1, &descriptor, nullptr)) {
      return false;
    }
    attributes.nLength = sizeof(attributes);
    attributes.lpSecurityDescriptor = descriptor;
    attributes.bInheritHandle = FALSE;
    return true;
  }
};

bool peerMatchesCurrentUser(const HANDLE pipe, const std::vector<std::byte>& expectedSid) {
  ULONG clientProcessId{};
  if (!GetNamedPipeClientProcessId(pipe, &clientProcessId) || clientProcessId == 0) {
    return false;
  }
  LocalHandle process(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, clientProcessId));
  if (!process.valid()) {
    return false;
  }
  HANDLE rawToken{};
  const BOOL opened = OpenProcessToken(process.get(), TOKEN_QUERY, &rawToken);
  LocalHandle token(rawToken);
  if (!opened) {
    return false;
  }
  DWORD bytes{};
  GetTokenInformation(token.get(), TokenUser, nullptr, 0, &bytes);
  std::vector<std::byte> buffer(bytes);
  if (bytes == 0 || !GetTokenInformation(token.get(), TokenUser, buffer.data(), bytes, &bytes)) {
    return false;
  }
  const auto* user = reinterpret_cast<const TOKEN_USER*>(buffer.data());
  return EqualSid(user->User.Sid,
                  const_cast<void*>(static_cast<const void*>(expectedSid.data()))) != FALSE;
}

bool readExact(const HANDLE pipe, void* destination, const DWORD bytes) {
  LocalHandle event(CreateEventW(nullptr, TRUE, FALSE, nullptr));
  if (!event.valid()) {
    return false;
  }
  OVERLAPPED overlapped{};
  overlapped.hEvent = event.get();
  DWORD transferred{};
  const BOOL started = ReadFile(pipe, destination, bytes, &transferred, &overlapped);
  if (!started) {
    if (GetLastError() != ERROR_IO_PENDING || WaitForSingleObject(event.get(), 2000) != WAIT_OBJECT_0 ||
        !GetOverlappedResult(pipe, &overlapped, &transferred, FALSE)) {
      CancelIoEx(pipe, &overlapped);
      return false;
    }
  }
  return transferred == bytes;
}

bool writeExact(const HANDLE pipe, const void* source, const DWORD bytes) {
  LocalHandle event(CreateEventW(nullptr, TRUE, FALSE, nullptr));
  if (!event.valid()) {
    return false;
  }
  OVERLAPPED overlapped{};
  overlapped.hEvent = event.get();
  DWORD transferred{};
  const BOOL started = WriteFile(pipe, source, bytes, &transferred, &overlapped);
  if (!started) {
    if (GetLastError() != ERROR_IO_PENDING || WaitForSingleObject(event.get(), 2000) != WAIT_OBJECT_0 ||
        !GetOverlappedResult(pipe, &overlapped, &transferred, FALSE)) {
      CancelIoEx(pipe, &overlapped);
      return false;
    }
  }
  return transferred == bytes;
}

template <typename Payload>
bool writeMessage(const HANDLE pipe, const MessageKind kind, const Payload& payload) {
  Header header;
  header.kind = static_cast<std::uint16_t>(kind);
  header.payloadBytes = sizeof(Payload);
  return writeExact(pipe, &header, sizeof(header)) && writeExact(pipe, &payload, sizeof(payload));
}

template <typename Payload>
bool readPayload(const HANDLE pipe, const Header& header, Payload& payload) {
  return header.payloadBytes == sizeof(Payload) && readExact(pipe, &payload, sizeof(payload));
}

}  // namespace

struct RuntimeInstanceLock::Impl {
  LocalHandle mutex;
  bool primary{false};
};

RuntimeInstanceLock::RuntimeInstanceLock() : impl_(std::make_unique<Impl>()) {}

RuntimeInstanceLock::~RuntimeInstanceLock() {
  release();
}

bool RuntimeInstanceLock::acquire(std::wstring objectName) {
  release();
  const auto sid = currentUserSid();
  PipeSecurity security;
  if (objectName.empty() || !security.initialize(sid)) {
    return false;
  }
  SetLastError(ERROR_SUCCESS);
  impl_->mutex.reset(CreateMutexW(&security.attributes, FALSE, objectName.c_str()));
  if (!impl_->mutex.valid()) {
    return false;
  }
  impl_->primary = GetLastError() != ERROR_ALREADY_EXISTS;
  return true;
}

void RuntimeInstanceLock::release() noexcept {
  impl_->primary = false;
  impl_->mutex.reset();
}

bool RuntimeInstanceLock::primary() const noexcept {
  return impl_->primary;
}

struct RuntimeIpcServer::Impl {
  std::wstring pipeName;
  IntentHandler handler;
  std::vector<std::byte> userSid;
  LocalHandle stopEvent;
  std::jthread worker;
  std::atomic_bool running{false};
  std::atomic_uint64_t rejectedPeers{0};
  std::atomic_uint64_t rejectedMessages{0};

  void run(const std::stop_token stopToken) {
    PipeSecurity security;
    if (!security.initialize(userSid)) {
      running = false;
      return;
    }
    running = true;
    while (!stopToken.stop_requested()) {
      LocalHandle pipe(CreateNamedPipeW(pipeName.c_str(),
                                       PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED,
                                       PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
                                       1,
                                       kMaxPayloadBytes,
                                       kMaxPayloadBytes,
                                       0,
                                       &security.attributes));
      if (!pipe.valid()) {
        running = false;
        return;
      }

      LocalHandle connectedEvent(CreateEventW(nullptr, TRUE, FALSE, nullptr));
      OVERLAPPED overlapped{};
      overlapped.hEvent = connectedEvent.get();
      BOOL connected = ConnectNamedPipe(pipe.get(), &overlapped);
      DWORD connectError = connected ? ERROR_SUCCESS : GetLastError();
      if (!connected && connectError == ERROR_IO_PENDING) {
        HANDLE waits[] = {stopEvent.get(), connectedEvent.get()};
        const DWORD wait = WaitForMultipleObjects(2, waits, FALSE, INFINITE);
        if (wait == WAIT_OBJECT_0) {
          CancelIoEx(pipe.get(), &overlapped);
          break;
        }
        DWORD transferred{};
        connected = GetOverlappedResult(pipe.get(), &overlapped, &transferred, FALSE);
      } else if (!connected && connectError == ERROR_PIPE_CONNECTED) {
        connected = TRUE;
      }

      if (!connected) {
        continue;
      }
      if (!peerMatchesCurrentUser(pipe.get(), userSid)) {
        ++rejectedPeers;
        DisconnectNamedPipe(pipe.get());
        continue;
      }

      Header helloHeader{};
      HelloPayload hello{};
      if (!readExact(pipe.get(), &helloHeader, sizeof(helloHeader)) ||
          helloHeader.magic != kMagic || helloHeader.version != kProtocolVersion ||
          helloHeader.kind != static_cast<std::uint16_t>(MessageKind::Hello) ||
          helloHeader.payloadBytes > kMaxPayloadBytes || !readPayload(pipe.get(), helloHeader, hello) ||
          hello.minimumVersion > kProtocolVersion || hello.maximumVersion < kProtocolVersion) {
        ++rejectedMessages;
        DisconnectNamedPipe(pipe.get());
        continue;
      }

      HelloAckPayload helloAck;
      helloAck.accepted = 1;
      helloAck.capabilities = kCapabilityIntentForwarding | kCapabilitySnapshotSubscription;
      if (!writeMessage(pipe.get(), MessageKind::HelloAck, helloAck)) {
        DisconnectNamedPipe(pipe.get());
        continue;
      }

      Header intentHeader{};
      IntentPayload intent{};
      if (!readExact(pipe.get(), &intentHeader, sizeof(intentHeader)) ||
          intentHeader.magic != kMagic || intentHeader.version != kProtocolVersion ||
          intentHeader.payloadBytes > kMaxPayloadBytes ||
          intentHeader.kind != static_cast<std::uint16_t>(MessageKind::ForwardIntent) ||
          !readPayload(pipe.get(), intentHeader, intent)) {
        ++rejectedMessages;
        DisconnectNamedPipe(pipe.get());
        continue;
      }

      const auto launchIntent = static_cast<contracts::LaunchIntent>(intent.intent);
      const bool known = launchIntent == contracts::LaunchIntent::Ordinary ||
                         launchIntent == contracts::LaunchIntent::Autostart ||
                         launchIntent == contracts::LaunchIntent::OpenControlCenter;
      const auto result = known && handler
                              ? handler(launchIntent, intent.requestId)
                              : IpcIntentResult{false,
                                                contracts::ErrorCode::UnknownRequiredMessage,
                                                intent.requestId};
      IntentAckPayload ack;
      ack.accepted = result.accepted ? 1u : 0u;
      ack.errorCode = static_cast<std::uint32_t>(result.error);
      ack.requestId = result.requestId;
      writeMessage(pipe.get(), MessageKind::IntentAck, ack);
      FlushFileBuffers(pipe.get());
      DisconnectNamedPipe(pipe.get());
    }
    running = false;
  }
};

RuntimeIpcServer::RuntimeIpcServer() : impl_(std::make_unique<Impl>()) {}

RuntimeIpcServer::~RuntimeIpcServer() {
  stop();
}

bool RuntimeIpcServer::start(std::wstring pipeName, IntentHandler handler) {
  stop();
  impl_->pipeName = normalizePipeName(pipeName);
  impl_->handler = std::move(handler);
  impl_->userSid = currentUserSid();
  impl_->stopEvent.reset(CreateEventW(nullptr, TRUE, FALSE, nullptr));
  if (impl_->pipeName.empty() || impl_->userSid.empty() || !impl_->stopEvent.valid()) {
    return false;
  }
  impl_->worker = std::jthread([this](const std::stop_token token) { impl_->run(token); });
  for (int attempt = 0; attempt < 100 && !impl_->running; ++attempt) {
    Sleep(2);
  }
  return impl_->running;
}

void RuntimeIpcServer::stop() noexcept {
  if (!impl_->worker.joinable()) {
    return;
  }
  impl_->worker.request_stop();
  SetEvent(impl_->stopEvent.get());
  impl_->worker.join();
  impl_->stopEvent.reset();
}

bool RuntimeIpcServer::running() const noexcept {
  return impl_->running;
}

std::uint64_t RuntimeIpcServer::rejectedPeers() const noexcept {
  return impl_->rejectedPeers;
}

std::uint64_t RuntimeIpcServer::rejectedMessages() const noexcept {
  return impl_->rejectedMessages;
}

IpcIntentResult RuntimeIpcClient::sendIntent(const std::wstring& pipeName,
                                             const contracts::LaunchIntent intent,
                                             const contracts::RequestId requestId,
                                             const std::uint32_t timeoutMs) {
  const auto normalized = normalizePipeName(pipeName);
  const ULONGLONG deadline = GetTickCount64() + timeoutMs;
  LocalHandle pipe;
  for (;;) {
    pipe.reset(CreateFileW(normalized.c_str(),
                           GENERIC_READ | GENERIC_WRITE,
                           0,
                           nullptr,
                           OPEN_EXISTING,
                           FILE_FLAG_OVERLAPPED,
                           nullptr));
    if (pipe.valid()) {
      break;
    }
    const DWORD error = GetLastError();
    const ULONGLONG now = GetTickCount64();
    if (now >= deadline) {
      return {false, contracts::ErrorCode::IoFailure, requestId};
    }
    const DWORD remaining = static_cast<DWORD>(deadline - now);
    if (error == ERROR_PIPE_BUSY) {
      WaitNamedPipeW(normalized.c_str(), remaining);
    } else if (error == ERROR_FILE_NOT_FOUND) {
      Sleep(1);
    } else {
      return {false, contracts::ErrorCode::AccessDenied, requestId};
    }
  }
  DWORD mode = PIPE_READMODE_MESSAGE;
  if (!SetNamedPipeHandleState(pipe.get(), &mode, nullptr, nullptr)) {
    return {false, contracts::ErrorCode::IoFailure, requestId};
  }

  const HelloPayload hello{};
  if (!writeMessage(pipe.get(), MessageKind::Hello, hello)) {
    return {false, contracts::ErrorCode::IoFailure, requestId};
  }
  Header helloAckHeader{};
  HelloAckPayload helloAck{};
  if (!readExact(pipe.get(), &helloAckHeader, sizeof(helloAckHeader)) ||
      helloAckHeader.magic != kMagic || helloAckHeader.version != kProtocolVersion ||
      helloAckHeader.kind != static_cast<std::uint16_t>(MessageKind::HelloAck) ||
      !readPayload(pipe.get(), helloAckHeader, helloAck) || !helloAck.accepted) {
    return {false, contracts::ErrorCode::ProtocolMismatch, requestId};
  }

  IntentPayload intentPayload;
  intentPayload.intent = static_cast<std::uint32_t>(intent);
  intentPayload.requestId = requestId;
  if (!writeMessage(pipe.get(), MessageKind::ForwardIntent, intentPayload)) {
    return {false, contracts::ErrorCode::IoFailure, requestId};
  }
  Header ackHeader{};
  IntentAckPayload ack{};
  if (!readExact(pipe.get(), &ackHeader, sizeof(ackHeader)) || ackHeader.magic != kMagic ||
      ackHeader.version != kProtocolVersion ||
      ackHeader.kind != static_cast<std::uint16_t>(MessageKind::IntentAck) ||
      !readPayload(pipe.get(), ackHeader, ack)) {
    return {false, contracts::ErrorCode::ProtocolMismatch, requestId};
  }
  return {ack.accepted != 0, static_cast<contracts::ErrorCode>(ack.errorCode), ack.requestId};
}

std::wstring normalizePipeName(const std::wstring& pipeName) {
  if (pipeName.empty()) {
    return {};
  }
  constexpr wchar_t prefix[] = L"\\\\.\\pipe\\";
  if (pipeName.rfind(prefix, 0) == 0) {
    return pipeName;
  }
  return std::wstring(prefix) + pipeName;
}

std::wstring currentSessionObjectName(const std::wstring& baseName) {
  DWORD sessionId{};
  if (baseName.empty() || !ProcessIdToSessionId(GetCurrentProcessId(), &sessionId)) {
    return {};
  }
  return L"Local\\" + baseName + L".Session." + std::to_wstring(sessionId);
}

std::wstring currentSessionPipeName(const std::wstring& baseName) {
  DWORD sessionId{};
  if (baseName.empty() || !ProcessIdToSessionId(GetCurrentProcessId(), &sessionId)) {
    return {};
  }
  return normalizePipeName(baseName + L".Session." + std::to_wstring(sessionId));
}

}  // namespace desktopup::adapters
