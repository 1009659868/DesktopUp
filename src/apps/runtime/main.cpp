#include "adapters/desktop-host-win32/win32_desktop_host.h"
#include "adapters/diagnostics/resource_ledger.h"
#include "adapters/renderer-qtquick/qtquick_renderer.h"
#include "adapters/runtime-ipc-win32/runtime_ipc.h"
#include "adapters/state-store-sqlite/state_store_worker.h"
#include "core/display_geometry.h"
#include "core/geometry_gesture.h"
#include "core/runtime_core.h"

#include <QGuiApplication>
#include <QTimer>

#define NOMINMAX
#include <windows.h>
#include <algorithm>
#include <cmath>
#include <filesystem>
#include <iostream>
#include <optional>
#include <string>

namespace {

using desktopup::contracts::HitRegion;
using desktopup::contracts::HitRegionRole;
using desktopup::contracts::HitRegionSet;
using desktopup::contracts::PixelRect;
using desktopup::contracts::PixelSize;

std::optional<std::string> optionValue(const int argc, char** argv, const std::string& option) {
  for (int index = 1; index < argc; ++index) {
    const std::string argument = argv[index];
    if (argument == option && index + 1 < argc) {
      return std::string(argv[index + 1]);
    }
    const std::string prefix = option + "=";
    if (argument.rfind(prefix, 0) == 0) {
      return argument.substr(prefix.size());
    }
  }
  return std::nullopt;
}

bool hasOption(const int argc, char** argv, const std::string& option) {
  for (int index = 1; index < argc; ++index) {
    if (std::string(argv[index]) == option) return true;
  }
  return false;
}

std::filesystem::path defaultStatePath() {
  wchar_t* localAppData{};
  std::size_t length{};
  if (_wdupenv_s(&localAppData, &length, L"LOCALAPPDATA") == 0 && localAppData) {
    const std::filesystem::path result =
        std::filesystem::path(localAppData) / L"DesktopUpV2" / L"state-v1.sqlite";
    free(localAppData);
    return result;
  }
  return std::filesystem::temp_directory_path() / L"DesktopUpV2" / L"state-v1.sqlite";
}

PixelRect pixelRect(const QRectF& source, const PixelSize clip) {
  const auto left = std::clamp<std::int32_t>(
      static_cast<std::int32_t>(std::floor(source.left())), 0,
      static_cast<std::int32_t>(clip.width));
  const auto top = std::clamp<std::int32_t>(
      static_cast<std::int32_t>(std::floor(source.top())), 0,
      static_cast<std::int32_t>(clip.height));
  const auto right = std::clamp<std::int32_t>(
      static_cast<std::int32_t>(std::ceil(source.right())), left,
      static_cast<std::int32_t>(clip.width));
  const auto bottom = std::clamp<std::int32_t>(
      static_cast<std::int32_t>(std::ceil(source.bottom())), top,
      static_cast<std::int32_t>(clip.height));
  return PixelRect{left,
                   top,
                   static_cast<std::uint32_t>(right - left),
                   static_cast<std::uint32_t>(bottom - top)};
}

HitRegionSet inputProjection(const desktopup::adapters::NightOpsScene& scene,
                             const desktopup::adapters::NightOpsLayout& layout,
                             const PixelSize frameSize,
                             const bool editing,
                             const desktopup::contracts::HostProjectionRevision projectionRevision,
                             const desktopup::contracts::HitRegionRevision hitRevision) {
  HitRegionSet result{hitRevision, projectionRevision, frameSize, {}};
  if (!scene.locked) {
    result.regions.push_back(
        HitRegion{1, HitRegionRole::TitleDrag, pixelRect(layout.titleDragBounds, frameSize),
                  20, 0, true});
  }
  result.regions.push_back(
      HitRegion{2, HitRegionRole::LockToggle, pixelRect(layout.lockButtonBounds, frameSize),
                100, 0, true});
  result.regions.push_back(
      HitRegion{3, HitRegionRole::PresentationToggle,
                pixelRect(layout.collapseButtonBounds, frameSize), 110, 0, true});

  if (layout.contentVisible) {
    for (std::size_t index = 0; index < layout.cardBounds.size(); ++index) {
      result.regions.push_back(
          HitRegion{100 + static_cast<desktopup::contracts::RegionId>(index),
                    HitRegionRole::Item,
                    pixelRect(layout.itemHitBounds[index], frameSize),
                    50,
                    static_cast<std::uint32_t>(index),
                    layout.itemVisible[index]});
    }
    result.regions.push_back(
        HitRegion{150,
                  HitRegionRole::ItemScroll,
                  pixelRect(layout.itemScrollInputBounds, frameSize),
                  80,
                  0,
                  !layout.itemScrollInputBounds.isEmpty()});
    if (editing && !scene.locked) {
      const auto corner = pixelRect(layout.resizeCornerBounds, frameSize);
      constexpr std::uint32_t edge = 8;
      result.regions.push_back(
          HitRegion{201,
                    HitRegionRole::ResizeRight,
                    PixelRect{static_cast<std::int32_t>(frameSize.width - edge),
                              static_cast<std::int32_t>(layout.headerBounds.bottom()),
                              edge,
                              frameSize.height -
                                  static_cast<std::uint32_t>(layout.headerBounds.bottom()) -
                                  corner.height},
                    120,
                    0,
                    true});
      result.regions.push_back(
          HitRegion{202,
                    HitRegionRole::ResizeBottom,
                    PixelRect{static_cast<std::int32_t>(layout.posterBounds.left()),
                              static_cast<std::int32_t>(frameSize.height - edge),
                              frameSize.width - corner.width -
                                  static_cast<std::uint32_t>(layout.posterBounds.left()),
                              edge},
                    120,
                    0,
                    true});
      result.regions.push_back(
          HitRegion{203, HitRegionRole::ResizeBottomRight, corner, 130, 0, true});
    }
  }
  return result;
}

qreal deviceScale(const desktopup::contracts::DisplayEnvironment& environment) noexcept {
  return static_cast<qreal>(std::max<std::uint32_t>(96, environment.effectiveDpi)) /
         96.0;
}

PixelSize projectDipSize(const PixelSize dipSize,
                         const desktopup::contracts::DisplayEnvironment& environment) {
  const auto dpi = std::max<std::uint32_t>(96, environment.effectiveDpi);
  return PixelSize{
      static_cast<std::uint32_t>(std::max(
          1L, std::lround(static_cast<double>(dipSize.width) * dpi / 96.0))),
      static_cast<std::uint32_t>(std::max(
          1L, std::lround(static_cast<double>(dipSize.height) * dpi / 96.0)))};
}

desktopup::core::GeometryConstraintsPx primaryGeometryConstraints(
    const desktopup::contracts::DisplayEnvironment& environment,
    const PixelSize minimumDipSize) {
  return desktopup::core::GeometryConstraintsPx{
      environment.primaryWorkArea,
      projectDipSize(minimumDipSize, environment)};
}

}  // namespace

int main(int argc, char** argv) {
  if (!SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)) {
    const auto current = GetThreadDpiAwarenessContext();
    if (!AreDpiAwarenessContextsEqual(
            current, DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)) {
      std::cerr << "QTS04_RUNTIME_DPI_AWARENESS_FAILED\n";
      return 16;
    }
  }
  QGuiApplication application(argc, argv);
  QGuiApplication::setQuitOnLastWindowClosed(false);
  QCoreApplication::setApplicationName(QStringLiteral("DesktopUp V2 Runtime"));
  QCoreApplication::setOrganizationName(QStringLiteral("DesktopUp"));

  const auto stateOption = optionValue(argc, argv, "--state-db");
  const auto statePath =
      stateOption.has_value() ? std::filesystem::u8path(*stateOption) : defaultStatePath();
  const auto ledgerOption = optionValue(argc, argv, "--ledger");
  const auto ledgerPath = ledgerOption.has_value()
                              ? std::optional<std::filesystem::path>(
                                    std::filesystem::u8path(*ledgerOption))
                              : std::nullopt;
  const auto pipeOption = optionValue(argc, argv, "--pipe-name");
  const std::wstring pipeName = pipeOption.has_value()
                                    ? std::wstring(pipeOption->begin(), pipeOption->end())
                                    : desktopup::adapters::currentSessionPipeName(
                                          L"DesktopUp.V2.Runtime");
  const auto instanceOption = optionValue(argc, argv, "--instance-name");
  const std::wstring instanceBase = instanceOption.has_value()
                                        ? std::wstring(instanceOption->begin(), instanceOption->end())
                                        : L"DesktopUp.V2.Runtime";
  const auto exitOption = optionValue(argc, argv, "--exit-after-ms");
  const int exitAfterMs = exitOption.has_value() ? std::stoi(*exitOption) : 0;
  const bool editing = hasOption(argc, argv, "--editing");

  std::error_code directoryError;
  std::filesystem::create_directories(statePath.parent_path(), directoryError);
  if (directoryError) {
    std::cerr << "QTS04_RUNTIME_STATE_DIRECTORY_FAILED\n";
    return 10;
  }

  desktopup::adapters::ResourceLedger ledger;
  desktopup::adapters::RuntimeInstanceLock instanceLock;
  if (!instanceLock.acquire(desktopup::adapters::currentSessionObjectName(instanceBase))) {
    return 9;
  }
  if (!instanceLock.primary()) {
    const auto forwarded = desktopup::adapters::RuntimeIpcClient::sendIntent(
        pipeName, desktopup::contracts::LaunchIntent::Ordinary, 1);
    return forwarded.accepted ? 0 : 15;
  }
  ledger.acquire("runtime-instance-lock");

  desktopup::core::AcceptedState acceptedState;
  desktopup::adapters::StateStoreWorker store(statePath);
  ledger.acquire("state-store-worker");
  auto storeResult = store.start().get();
  if (storeResult.status == desktopup::contracts::StoreStatus::NoData) {
    storeResult = store.commit(0, R"({"schemaVersion":1,"posters":[]})", 1).get();
  }
  if (!acceptedState.acceptStoreResult(storeResult)) {
    store.stop();
    ledger.release("state-store-worker");
    std::cerr << "QTS04_RUNTIME_STATE_LOAD_FAILED\n";
    return 11;
  }

  const auto snapshot = acceptedState.snapshot().value();
  constexpr desktopup::contracts::SurfaceEpoch initialSurfaceEpoch = 1;
  constexpr auto defaultFrameSize =
      desktopup::adapters::QtQuickRenderer::defaultExpandedSize();
  auto displayEnvironment =
      desktopup::adapters::Win32DesktopHost::primaryDisplayEnvironment();
  auto logicalGeometry =
      desktopup::core::makeCenteredLogicalPosterGeometry(defaultFrameSize);
  const auto initialProjection = desktopup::core::projectLogicalPosterGeometry(
      logicalGeometry,
      displayEnvironment,
      false,
      desktopup::adapters::QtQuickRenderer::minimumExpandedSize());
  PixelRect posterBounds =
      initialProjection.valid
          ? initialProjection.bounds
          : desktopup::adapters::Win32DesktopHost::centeredPrimaryWorkArea(
                defaultFrameSize);
  std::uint32_t expandedHeight =
      initialProjection.valid ? initialProjection.expandedHeight
                              : posterBounds.height;
  desktopup::contracts::HostProjectionRevision projectionRevision = 1;
  desktopup::contracts::HitRegionRevision hitRevision = 1;

  auto scene = desktopup::adapters::NightOpsScene::fixture();
  scene.deviceScale = deviceScale(displayEnvironment);
  desktopup::adapters::QtQuickRenderer renderer(scene);
  if (!renderer.ready()) {
    std::cerr << "QTS04_RUNTIME_QTQUICK_RENDERER_INIT_FAILED "
              << renderer.errorString().toStdString() << "\n";
    return 17;
  }
  desktopup::adapters::Win32DesktopHost host;
  desktopup::core::FrameGate frameGate;
  desktopup::contracts::BufferHandle activeBuffer{};
  std::uint64_t geometryRelocationCount{};
  desktopup::core::GeometryGestureSession geometryGesture;

  const auto publishCurrentSurface = [&]() -> bool {
    const auto observation = host.observe();
    if (!observation.attached || observation.surfaceEpoch == 0) {
      return false;
    }
    const PixelSize frameSize = posterBounds.size();
    renderer.setScene(scene);
    frameGate.configure(snapshot.revision, observation.surfaceEpoch, frameSize);
    const auto frame =
        renderer.render({snapshot.revision, observation.surfaceEpoch, frameSize});
    const auto pixels = renderer.frameView(frame.buffer);
    const auto hits =
        inputProjection(scene, renderer.layout(), frameSize, editing,
                        projectionRevision, hitRevision);
    if (!frameGate.accept(frame) ||
        !host.present(posterBounds, frame, pixels, hits)) {
      renderer.release(frame.buffer);
      return false;
    }
    const auto& acceptedLayout = renderer.layout();
    scene.itemScrollOffsetDip =
        acceptedLayout.unit > 0.0
            ? acceptedLayout.itemScrollOffset / acceptedLayout.unit
            : 0.0;
    if (activeBuffer != 0) renderer.release(activeBuffer);
    activeBuffer = frame.buffer;
    return true;
  };

  const auto relocateCurrentSurface = [&]() -> bool {
    const PixelSize frameSize = posterBounds.size();
    const auto hits =
        inputProjection(scene, renderer.layout(), frameSize, editing,
                        projectionRevision, hitRevision);
    if (!host.relocate(posterBounds, hits)) return false;
    ++geometryRelocationCount;
    return true;
  };

  const auto captureAcceptedGeometry = [&] {
    const auto captured = desktopup::core::captureLogicalPosterGeometry(
        posterBounds, expandedHeight, displayEnvironment);
    if (captured.width > 0.0 && captured.expandedHeight > 0.0) {
      logicalGeometry = captured;
    }
  };

  host.setInputCallback([&](const desktopup::contracts::HostInputEvent& event) {
    if (event.hitRevision != hitRevision ||
        event.projectionRevision != projectionRevision) {
      return;
    }

    const bool geometryRole =
        event.role == HitRegionRole::TitleDrag ||
        event.role == HitRegionRole::ResizeRight ||
        event.role == HitRegionRole::ResizeBottom ||
        event.role == HitRegionRole::ResizeBottomRight;

    if ((event.role == HitRegionRole::Item ||
         event.role == HitRegionRole::ItemScroll) &&
        event.phase == desktopup::contracts::PointerPhase::Wheel &&
        event.wheelDelta != 0) {
      QTimer::singleShot(0, &application, [&, delta = event.wheelDelta] {
        const auto previousScene = scene;
        const auto& layout = renderer.layout();
        if (layout.maxItemScrollOffset <= 0.0 || layout.unit <= 0.0) return;
        const qreal currentDip = layout.itemScrollOffset / layout.unit;
        const qreal maximumDip = layout.maxItemScrollOffset / layout.unit;
        const qreal wheelSteps = static_cast<qreal>(delta) / WHEEL_DELTA;
        constexpr qreal scrollStepDip = 48.0;
        scene.itemScrollOffsetDip =
            std::clamp(currentDip - wheelSteps * scrollStepDip,
                       0.0,
                       maximumDip);
        if (std::abs(scene.itemScrollOffsetDip - currentDip) < 0.01) return;
        ++projectionRevision;
        ++hitRevision;
        if (!publishCurrentSurface()) {
          scene = previousScene;
          ++projectionRevision;
          ++hitRevision;
          if (!publishCurrentSurface()) {
            std::cerr << "QTS04_RUNTIME_ITEM_SCROLL_ROLLBACK_FAILED\n";
          }
        }
      });
      return;
    }

    if (geometryRole && event.phase == desktopup::contracts::PointerPhase::Pressed &&
        !scene.locked) {
      static_cast<void>(geometryGesture.begin(
          event.role, event.screenX, event.screenY, posterBounds));
      return;
    }

    if (geometryRole && event.phase == desktopup::contracts::PointerPhase::Moved &&
        geometryGesture.active() && !scene.locked) {
      const auto minimumExpanded =
          desktopup::adapters::QtQuickRenderer::minimumExpandedSize();
      const PixelSize minimumSize =
          scene.presentation == desktopup::adapters::PosterPresentation::Expanded
              ? minimumExpanded
              : PixelSize{minimumExpanded.width, 1};
      const auto next = geometryGesture.update(
          event.screenX,
          event.screenY,
          primaryGeometryConstraints(displayEnvironment, minimumSize));
      if (!next.has_value() || *next == posterBounds) return;

      const PixelRect previousBounds = posterBounds;
      const std::uint32_t previousExpandedHeight = expandedHeight;
      posterBounds = *next;
      if (scene.presentation == desktopup::adapters::PosterPresentation::Expanded) {
        expandedHeight = posterBounds.height;
      }
      ++projectionRevision;
      ++hitRevision;
      const bool sameSizeTitleMove =
          event.role == HitRegionRole::TitleDrag &&
          previousBounds.size() == posterBounds.size();
      if (!(sameSizeTitleMove ? relocateCurrentSurface() : publishCurrentSurface())) {
        std::cerr << "QTS04_RUNTIME_GEOMETRY_PREVIEW_FAILED\n";
        posterBounds = previousBounds;
        expandedHeight = previousExpandedHeight;
        ++projectionRevision;
        ++hitRevision;
        if (!publishCurrentSurface()) {
          std::cerr << "QTS04_RUNTIME_GEOMETRY_ROLLBACK_FAILED\n";
        }
        geometryGesture.cancel();
      } else {
        captureAcceptedGeometry();
      }
      return;
    }

    if (geometryRole &&
        (event.phase == desktopup::contracts::PointerPhase::Released ||
         event.phase == desktopup::contracts::PointerPhase::CaptureLost)) {
      if (event.phase == desktopup::contracts::PointerPhase::CaptureLost) {
        geometryGesture.cancel();
      } else {
        static_cast<void>(geometryGesture.end());
      }
      return;
    }

    if (event.phase != desktopup::contracts::PointerPhase::Released ||
        event.button != desktopup::contracts::PointerButton::Left) {
      return;
    }

    QTimer::singleShot(
        0, &application,
        [&, role = event.role, index = event.actionIndex] {
          const auto previousScene = scene;
          const PixelRect previousBounds = posterBounds;
          const std::uint32_t previousExpandedHeight = expandedHeight;
          if (role == HitRegionRole::LockToggle) {
            scene.locked = !scene.locked;
          } else if (role == HitRegionRole::PresentationToggle) {
            if (scene.presentation ==
                desktopup::adapters::PosterPresentation::Expanded) {
              expandedHeight = posterBounds.height;
              scene.presentation =
                  desktopup::adapters::PosterPresentation::Collapsed;
              posterBounds.height =
                  desktopup::adapters::QtQuickRenderer::
                      collapsedHeightForScale(scene.deviceScale * scene.contentScale);
            } else {
              scene.presentation =
                  desktopup::adapters::PosterPresentation::Expanded;
              posterBounds.height = expandedHeight;
              posterBounds = desktopup::core::clampGeometryToWorkArea(
                  posterBounds,
                  primaryGeometryConstraints(
                      displayEnvironment,
                      desktopup::adapters::QtQuickRenderer::
                          minimumExpandedSize()));
            }
          } else if (role == HitRegionRole::Item &&
                     index < scene.items.size()) {
            scene.selectedItem = static_cast<std::int32_t>(index);
          } else {
            return;
          }

          ++projectionRevision;
          ++hitRevision;
          if (!publishCurrentSurface()) {
            std::cerr << "QTS04_RUNTIME_INTERACTION_FRAME_FAILED\n";
            scene = previousScene;
            posterBounds = previousBounds;
            expandedHeight = previousExpandedHeight;
            ++projectionRevision;
            ++hitRevision;
            if (!publishCurrentSurface()) {
              std::cerr << "QTS04_RUNTIME_INTERACTION_ROLLBACK_FAILED\n";
            }
          } else {
            captureAcceptedGeometry();
          }
        });
  });

  const auto applyDisplayEnvironment = [&]() -> bool {
    const auto observation = host.observe();
    const auto nextEnvironment = observation.displayEnvironment;
    if (!nextEnvironment.primaryTargetValid) {
      return false;
    }
    const auto projected = desktopup::core::projectLogicalPosterGeometry(
        logicalGeometry,
        nextEnvironment,
        scene.presentation == desktopup::adapters::PosterPresentation::Collapsed,
        desktopup::adapters::QtQuickRenderer::minimumExpandedSize());
    if (!projected.valid) {
      return false;
    }

    geometryGesture.cancel();
    posterBounds = projected.bounds;
    expandedHeight = projected.expandedHeight;
    scene.deviceScale = deviceScale(nextEnvironment);
    displayEnvironment = nextEnvironment;
    ++projectionRevision;
    ++hitRevision;
    if (observation.attached && !publishCurrentSurface()) {
      return false;
    }
    std::cout << "QTS04_RUNTIME_DISPLAY_REPROJECTED displayRevision="
              << displayEnvironment.revision
              << " dpi=" << displayEnvironment.effectiveDpi
              << " bounds=" << posterBounds.left << "," << posterBounds.top
              << "," << posterBounds.width << "," << posterBounds.height << "\n";
    return true;
  };

  host.setRecoveryCallback([&](const desktopup::contracts::HostMaintenanceResult result) {
    if (result == desktopup::contracts::HostMaintenanceResult::ReattachedNeedsFrame) {
      const auto recovered = host.observe();
      if (recovered.displayEnvironment.revision != displayEnvironment.revision) {
        if (!applyDisplayEnvironment()) {
          std::cerr << "QTS04_RUNTIME_RECOVERY_REPROJECT_FAILED\n";
        }
      } else {
        ++projectionRevision;
        ++hitRevision;
        if (!publishCurrentSurface()) {
          std::cerr << "QTS04_RUNTIME_RECOVERY_FRAME_FAILED\n";
        }
      }
      const auto completed = host.observe();
      std::cout << "QTS04_RUNTIME_SHELL_RECOVERED shellGeneration="
                << completed.shellGeneration
                << " surfaceEpoch=" << completed.surfaceEpoch
                << " recoveries=" << completed.shellRecoveryCount << "\n";
    } else if (
        result ==
        desktopup::contracts::HostMaintenanceResult::
            DisplayEnvironmentChangedNeedsProjection) {
      if (!applyDisplayEnvironment()) {
        std::cerr << "QTS04_RUNTIME_DISPLAY_REPROJECT_FAILED\n";
      }
    } else if (result == desktopup::contracts::HostMaintenanceResult::DegradedNoDesktopHost &&
               activeBuffer != 0) {
      renderer.release(activeBuffer);
      activeBuffer = 0;
      std::cout << "QTS04_RUNTIME_SHELL_DEGRADED\n";
    }
  });
  if (!host.attach(initialSurfaceEpoch, posterBounds)) {
    store.stop();
    ledger.release("state-store-worker");
    std::cerr << "QTS04_RUNTIME_HOST_ATTACH_FAILED\n";
    return 12;
  }
  ledger.acquire("desktop-host-attachment");

  displayEnvironment = host.observe().displayEnvironment;
  scene.deviceScale = deviceScale(displayEnvironment);
  const auto attachedProjection =
      desktopup::core::projectLogicalPosterGeometry(
          logicalGeometry,
          displayEnvironment,
          false,
          desktopup::adapters::QtQuickRenderer::minimumExpandedSize());
  if (attachedProjection.valid) {
    posterBounds = attachedProjection.bounds;
    expandedHeight = attachedProjection.expandedHeight;
  }

  if (!publishCurrentSurface()) {
    host.beginExit();
    store.stop();
    ledger.release("desktop-host-attachment");
    ledger.release("state-store-worker");
    std::cerr << "QTS04_RUNTIME_FIRST_FRAME_FAILED\n";
    return 13;
  }
  ledger.acquire("renderer-buffer");

  desktopup::adapters::RuntimeIpcServer ipcServer;
  if (!ipcServer.start(pipeName,
                       [](const desktopup::contracts::LaunchIntent intent,
                          const desktopup::contracts::RequestId requestId) {
                         const bool known =
                             intent == desktopup::contracts::LaunchIntent::Ordinary ||
                             intent == desktopup::contracts::LaunchIntent::Autostart ||
                             intent == desktopup::contracts::LaunchIntent::OpenControlCenter;
                         return desktopup::adapters::IpcIntentResult{
                             known,
                             known ? desktopup::contracts::ErrorCode::None
                                   : desktopup::contracts::ErrorCode::UnknownRequiredMessage,
                             requestId};
                       })) {
    renderer.release(activeBuffer);
    activeBuffer = 0;
    host.beginExit();
    store.stop();
    ledger.release("renderer-buffer");
    ledger.release("desktop-host-attachment");
    ledger.release("state-store-worker");
    return 14;
  }
  ledger.acquire("runtime-ipc-pipe");

  QTimer displayEnvironmentTimer;
  displayEnvironmentTimer.setInterval(250);
  displayEnvironmentTimer.setTimerType(Qt::PreciseTimer);
  QObject::connect(&displayEnvironmentTimer, &QTimer::timeout,
                   &application, [&] {
                     static_cast<void>(host.refreshDisplayEnvironment());
                   });
  displayEnvironmentTimer.start();

  bool finishing = false;
  const auto finish = [&] {
    if (finishing) {
      return;
    }
    finishing = true;
    displayEnvironmentTimer.stop();
    const auto finalObservation = host.observe();
    std::cout << "QTS04_RUNTIME_LIFECYCLE_FINAL shellGeneration="
              << finalObservation.shellGeneration
              << " lifecycleEpoch=" << finalObservation.lifecycleEpoch
              << " displayRevision="
              << finalObservation.displayEnvironment.revision
              << " relevantShellEvents="
              << finalObservation.relevantShellEventCount
              << " ignoredShellEvents="
              << finalObservation.ignoredShellEventCount
              << " topologyValidations="
              << finalObservation.topologyValidationCount
              << " shellRecoveries="
              << finalObservation.shellRecoveryCount
              << " displayChanges="
              << finalObservation.displayChangeCount
              << " geometryRelocations=" << geometryRelocationCount << "\n";
    host.setInputCallback({});
    host.setRecoveryCallback({});
    frameGate.beginExit();
    host.beginExit();
    renderer.beginExit();
    ipcServer.stop();
    ledger.release("runtime-ipc-pipe");
    renderer.release(activeBuffer);
    activeBuffer = 0;
    ledger.release("renderer-buffer");
    host.detach();
    ledger.release("desktop-host-attachment");
    store.stop();
    ledger.release("state-store-worker");
    instanceLock.release();
    ledger.release("runtime-instance-lock");
    if (ledgerPath.has_value() &&
        !ledger.writeJson(*ledgerPath,
                          snapshot.revision,
                          initialSurfaceEpoch,
                          "exited")) {
      std::cerr << "QTS04_RUNTIME_LEDGER_WRITE_FAILED\n";
    }
    application.quit();
  };

  if (exitAfterMs > 0) {
    QTimer::singleShot(exitAfterMs, &application, finish);
  }

  const auto observation = host.observe();
  std::cout << "QTS04_RUNTIME_READY revision=" << snapshot.revision
            << " shellGeneration=" << observation.shellGeneration
            << " surfaceEpoch=" << observation.surfaceEpoch
            << " displayRevision="
            << observation.displayEnvironment.revision
            << " dpi=" << observation.displayEnvironment.effectiveDpi
            << " inputIslands=" << observation.inputIslandCount
            << " editing=" << (editing ? "true" : "false") << "\n";
  return application.exec();
}
