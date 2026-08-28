#pragma once

#include <cstddef>
#include <cstdint>

namespace desktopup::contracts::ipc {

inline constexpr std::uint32_t kMagic = 0x32505544;  // "DUP2"
inline constexpr std::uint16_t kProtocolVersion = 1;
inline constexpr std::uint32_t kMaxPayloadBytes = 4096;
inline constexpr std::uint64_t kCapabilityIntentForwarding = 1ull << 0;
inline constexpr std::uint64_t kCapabilitySnapshotSubscription = 1ull << 1;

enum class MessageKind : std::uint16_t {
  Hello = 1,
  HelloAck = 2,
  ForwardIntent = 3,
  IntentAck = 4,
  SnapshotAccepted = 5,
};

inline constexpr std::uint16_t kFlagRequired = 1u << 0;

#pragma pack(push, 1)
struct Header {
  std::uint32_t magic{kMagic};
  std::uint16_t version{kProtocolVersion};
  std::uint16_t kind{};
  std::uint16_t flags{};
  std::uint16_t reserved{};
  std::uint32_t payloadBytes{};
};

struct HelloPayload {
  std::uint16_t minimumVersion{kProtocolVersion};
  std::uint16_t maximumVersion{kProtocolVersion};
  std::uint32_t reserved{};
  std::uint64_t capabilities{kCapabilityIntentForwarding | kCapabilitySnapshotSubscription};
};

struct HelloAckPayload {
  std::uint16_t selectedVersion{kProtocolVersion};
  std::uint16_t accepted{};
  std::uint32_t errorCode{};
  std::uint64_t capabilities{};
};

struct IntentPayload {
  std::uint32_t intent{};
  std::uint32_t reserved{};
  std::uint64_t requestId{};
};

struct IntentAckPayload {
  std::uint32_t accepted{};
  std::uint32_t errorCode{};
  std::uint64_t requestId{};
};

struct SnapshotAcceptedPayload {
  std::uint64_t revision{};
  std::uint64_t subscriptionSequence{};
};
#pragma pack(pop)

static_assert(sizeof(Header) == 16);
static_assert(sizeof(HelloPayload) == 16);
static_assert(sizeof(IntentPayload) == 16);

}  // namespace desktopup::contracts::ipc
