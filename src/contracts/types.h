#pragma once

#include <cstdint>
#include <optional>
#include <string>
#include <vector>

namespace desktopup::contracts {

using Revision = std::uint64_t;
using SurfaceEpoch = std::uint64_t;
using RequestId = std::uint64_t;
using BufferHandle = std::uint64_t;
using HostProjectionRevision = std::uint64_t;
using HitRegionRevision = std::uint64_t;
using DisplayEnvironmentRevision = std::uint64_t;
using RegionId = std::uint64_t;

struct PixelSize {
  std::uint32_t width{};
  std::uint32_t height{};

  friend bool operator==(const PixelSize&, const PixelSize&) = default;
};

struct PixelRect {
  std::int32_t left{};
  std::int32_t top{};
  std::uint32_t width{};
  std::uint32_t height{};

  [[nodiscard]] PixelSize size() const noexcept { return {width, height}; }
  friend bool operator==(const PixelRect&, const PixelRect&) = default;
};
struct DisplayEnvironment {
  DisplayEnvironmentRevision revision{};
  PixelRect primaryMonitor{};
  PixelRect primaryWorkArea{};
  std::uint32_t effectiveDpi{96};
  bool primaryTargetValid{false};

  friend bool operator==(const DisplayEnvironment&, const DisplayEnvironment&) = default;
};



enum class HitRegionRole : std::uint8_t {
  TitleDrag,
  Item,
  ItemScroll,
  LockToggle,
  PresentationToggle,
  ResizeRight,
  ResizeBottom,
  ResizeBottomRight,
};

struct HitRegion {
  RegionId id{};
  HitRegionRole role{HitRegionRole::Item};
  PixelRect bounds{};
  std::uint32_t priority{};
  std::uint32_t actionIndex{};
  bool enabled{true};
};

struct HitRegionSet {
  HitRegionRevision revision{};
  HostProjectionRevision projectionRevision{};
  PixelSize visualBounds{};
  std::vector<HitRegion> regions;
};

enum class PointerPhase : std::uint8_t {
  Pressed,
  Moved,
  Released,
  DoubleClicked,
  CaptureLost,
  Wheel,
};

enum class PointerButton : std::uint8_t {
  None,
  Left,
  Right,
};

struct HostInputEvent {
  HitRegionRevision hitRevision{};
  HostProjectionRevision projectionRevision{};
  RegionId regionId{};
  HitRegionRole role{HitRegionRole::Item};
  PointerPhase phase{PointerPhase::Pressed};
  PointerButton button{PointerButton::None};
  std::int32_t posterX{};
  std::int32_t posterY{};
  std::int32_t screenX{};
  std::int32_t screenY{};
  std::uint32_t actionIndex{};
  std::int32_t wheelDelta{};
};
struct Snapshot {
  Revision revision{};
  std::string payload;
};

enum class ErrorCode : std::uint32_t {
  None = 0,
  InvalidArgument,
  NoData,
  CorruptData,
  IoFailure,
  RevisionConflict,
  StaleResult,
  ProtocolMismatch,
  AccessDenied,
  MessageTooLarge,
  UnknownRequiredMessage,
  Exiting,
};

struct Error {
  ErrorCode code{ErrorCode::None};
  RequestId requestId{};
  std::string safeMessage;
};

enum class StoreStatus : std::uint8_t {
  Loaded,
  NoData,
  CommitAccepted,
  Conflict,
  Corrupt,
  IoError,
  Closed,
};

struct StoreResult {
  StoreStatus status{StoreStatus::IoError};
  std::optional<Snapshot> snapshot;
  Error error;
};

struct RenderRequest {
  Revision revision{};
  SurfaceEpoch surfaceEpoch{};
  PixelSize bounds{};
};

struct FrameDescriptor {
  Revision revision{};
  SurfaceEpoch surfaceEpoch{};
  PixelSize bounds{};
  BufferHandle buffer{};
};

struct PremultipliedFrameView {
  BufferHandle buffer{};
  PixelSize bounds{};
  std::uint32_t strideBytes{};
  const std::uint8_t* pixels{};

  [[nodiscard]] bool valid() const noexcept {
    return buffer != 0 && bounds.width != 0 && bounds.height != 0 && pixels != nullptr &&
           strideBytes >= bounds.width * 4;
  }
};

enum class HostTopology : std::uint8_t {
  Unknown,
  RaisedProgman,
  TraditionalWorkerW,
  NoSafeHost,
};

enum class HostState : std::uint8_t {
  Detached,
  Discovering,
  Attaching,
  AttachedSafe,
  ShellGenerationLost,
  DegradedNoDesktopHost,
  Recovering,
  Detaching,
  Exiting,
  Faulted,
};

enum class HostMaintenanceResult : std::uint8_t {
  Unchanged,
  ReattachedNeedsFrame,
  DisplayEnvironmentChangedNeedsProjection,
  DegradedNoDesktopHost,
  Exiting,
  Failed,
};

struct HostObservation {
  HostTopology topology{HostTopology::Unknown};
  HostState state{HostState::Detached};
  std::uint64_t shellGeneration{};
  std::uint64_t lifecycleEpoch{};
  SurfaceEpoch surfaceEpoch{};
  PixelRect requestedBounds{};
  PixelRect windowBounds{};
  PixelRect clientScreenBounds{};
  PixelRect extendedFrameBounds{};
  std::uint32_t explorerProcessId{};
  std::uint64_t visualHandle{};
  std::uint64_t parentHandle{};
  std::uint64_t defViewHandle{};
  std::uint64_t iconViewHandle{};
  std::uint32_t ownedResourceCount{};
  std::uint32_t nativeError{};
  HostProjectionRevision hostProjectionRevision{};
  HitRegionRevision hitRegionRevision{};
  DisplayEnvironment displayEnvironment{};
  std::uint64_t relevantShellEventCount{};
  std::uint64_t ignoredShellEventCount{};
  std::uint64_t topologyValidationCount{};
  std::uint64_t shellRecoveryCount{};
  std::uint64_t displayChangeCount{};
  bool safeTargetUnique{false};
  std::uint32_t inputIslandCount{};
  bool attached{false};
  std::uint64_t inputEventCount{};
  bool visible{false};
  bool topmost{false};
  bool ordinaryTopLevelFallback{false};
  bool inputEnabled{false};
  bool visualPointerTransparent{false};
  bool hostAboveDefView{false};
  bool winEventHooksActive{false};
  bool layeredChildCompatibilityDeclared{false};
};

enum class LaunchIntent : std::uint32_t {
  Ordinary = 1,
  Autostart = 2,
  OpenControlCenter = 3,
};

}  // namespace desktopup::contracts
