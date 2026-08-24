#include "desktop_host_contract.h"

#include <dwmapi.h>

#include <QCoreApplication>
#include <QEventLoop>
#include <QImage>
#include <QJsonArray>
#include <QJsonDocument>
#include <QQmlApplicationEngine>
#include <QQmlContext>
#include <QQuickWindow>
#include <QSGRendererInterface>
#include <QTimer>
#include <QUuid>
#include <QWindow>

#include <cstdio>
#include <utility>
#include <vector>

namespace desktopup::tsc01 {
namespace {

QString className(HWND window)
{
    wchar_t buffer[256] {};
    const int length = GetClassNameW(window, buffer, static_cast<int>(std::size(buffer)));
    return length > 0 ? QString::fromWCharArray(buffer, length) : QString();
}

bool isCloaked(HWND window)
{
    DWORD cloaked = 0;
    return SUCCEEDED(DwmGetWindowAttribute(window, DWMWA_CLOAKED, &cloaked, sizeof(cloaked))) && cloaked != 0;
}

BOOL CALLBACK collectVisibleDefViews(HWND topLevel, LPARAM parameter)
{
    auto* targets = reinterpret_cast<std::vector<HWND>*>(parameter);
    const HWND defView = FindWindowExW(topLevel, nullptr, L"SHELLDLL_DefView", nullptr);
    if (defView != nullptr && IsWindowVisible(defView) && !isCloaked(defView)) {
        targets->push_back(defView);
    }
    return TRUE;
}

QJsonArray rectArray(const RECT& rect)
{
    return {static_cast<int>(rect.left),
            static_cast<int>(rect.top),
            static_cast<int>(rect.right),
            static_cast<int>(rect.bottom)};
}

RECT clientScreenRect(HWND window)
{
    RECT rect {};
    GetClientRect(window, &rect);
    POINT points[2] {{rect.left, rect.top}, {rect.right, rect.bottom}};
    MapWindowPoints(window, nullptr, points, 2);
    return {points[0].x, points[0].y, points[1].x, points[1].y};
}

RECT extendedFrameRect(HWND window)
{
    RECT rect {};
    if (FAILED(DwmGetWindowAttribute(window, DWMWA_EXTENDED_FRAME_BOUNDS, &rect, sizeof(rect)))) {
        GetWindowRect(window, &rect);
    }
    return rect;
}

bool setWindowLongChecked(HWND window, int index, LONG_PTR value)
{
    SetLastError(ERROR_SUCCESS);
    const LONG_PTR previous = SetWindowLongPtrW(window, index, value);
    return previous != 0 || GetLastError() == ERROR_SUCCESS;
}

QString hostStateName(HostState state)
{
    switch (state) {
    case HostState::Detached: return QStringLiteral("DETACHED");
    case HostState::Attaching: return QStringLiteral("ATTACHING");
    case HostState::AttachedSafe: return QStringLiteral("ATTACHED_SAFE");
    case HostState::Faulted: return QStringLiteral("FAULTED");
    case HostState::Detaching: return QStringLiteral("DETACHING");
    case HostState::Exiting: return QStringLiteral("EXITING");
    }
    return QStringLiteral("UNKNOWN");
}

QString surfaceStateName(SurfaceState state)
{
    switch (state) {
    case SurfaceState::Unbound: return QStringLiteral("UNBOUND");
    case SurfaceState::Binding: return QStringLiteral("BINDING");
    case SurfaceState::ReadyNoFrame: return QStringLiteral("READY_NO_FRAME");
    case SurfaceState::Presented: return QStringLiteral("PRESENTED");
    case SurfaceState::Lost: return QStringLiteral("LOST");
    case SurfaceState::Releasing: return QStringLiteral("RELEASING");
    case SurfaceState::Released: return QStringLiteral("RELEASED");
    }
    return QStringLiteral("UNKNOWN");
}

QString transportName(SurfaceTransportKind transport)
{
    return transport == SurfaceTransportKind::OffscreenFrameSource
        ? QStringLiteral("OFFSCREEN_FRAME_SOURCE")
        : QStringLiteral("ADAPTER_OWNED_NATIVE_CHILD");
}

QString ownershipName(SurfaceOwnership ownership)
{
    return ownership == SurfaceOwnership::Host
        ? QStringLiteral("HOST")
        : QStringLiteral("ADAPTER");
}

}  // namespace

EventSink::EventSink(const QString& path)
{
    clock_.start();
    if (!path.isEmpty()) {
        file_.setFileName(path);
        stdoutOnly_ = !file_.open(QIODevice::WriteOnly | QIODevice::Truncate | QIODevice::Text);
    }
}

EventSink::~EventSink()
{
    if (file_.isOpen()) {
        file_.flush();
        file_.close();
    }
}

bool EventSink::isValid() const
{
    return file_.fileName().isEmpty() || file_.isOpen();
}

void EventSink::setIdentity(const QString& hostSessionId)
{
    hostSessionId_ = hostSessionId;
}

void EventSink::write(const QString& event, const QJsonObject& fields)
{
    QJsonObject record {
        {QStringLiteral("schema"), QStringLiteral("desktopup.desktop-host.event.v1")},
        {QStringLiteral("event"), event},
        {QStringLiteral("monotonic_ms"), static_cast<qint64>(clock_.elapsed())},
        {QStringLiteral("sequence"), static_cast<qint64>(++sequence_)},
        {QStringLiteral("host_session_id"), hostSessionId_},
        {QStringLiteral("candidate_id"), QString::fromLatin1(kCandidateId)},
        {QStringLiteral("build_id"), QString::fromLatin1(kBuildId)},
        {QStringLiteral("result"), QStringLiteral("OK")}
    };
    for (auto iterator = fields.constBegin(); iterator != fields.constEnd(); ++iterator) {
        record.insert(iterator.key(), iterator.value());
    }

    QByteArray line = QJsonDocument(record).toJson(QJsonDocument::Compact);
    line.append('\n');
    if (file_.isOpen()) {
        file_.write(line);
        file_.flush();
    } else if (stdoutOnly_) {
        std::fwrite(line.constData(), 1, static_cast<std::size_t>(line.size()), stdout);
        std::fflush(stdout);
    }
}

QtQuickSurfaceAdapter::QtQuickSurfaceAdapter(SurfaceFixtureKind fixtureKind)
    : fixtureKind_(fixtureKind)
{
}

QtQuickSurfaceAdapter::~QtQuickSurfaceAdapter()
{
    release();
}

bool QtQuickSurfaceAdapter::createSurface(HWND approvedParent, const RECT& parentPixelRect, QString* error)
{
    if (approvedParent == nullptr || !IsWindow(approvedParent)) {
        if (error != nullptr) *error = QStringLiteral("APPROVED_PARENT_INVALID");
        return false;
    }
    if (engine_ != nullptr || window_ != nullptr || nativeHandle_ != nullptr) {
        if (error != nullptr) *error = QStringLiteral("SURFACE_ALREADY_EXISTS");
        return false;
    }

    foreignParent_.reset(QWindow::fromWinId(reinterpret_cast<WId>(approvedParent)));
    if (!foreignParent_) {
        if (error != nullptr) *error = QStringLiteral("FOREIGN_PARENT_UNSUPPORTED");
        return false;
    }

    engine_ = std::make_unique<QQmlApplicationEngine>();
    const bool r2 = fixtureKind_ == SurfaceFixtureKind::R2NightOps;
    const QString module = r2 ? QStringLiteral("DesktopUp.TSC01.R2")
                              : QStringLiteral("DesktopUp.TSC01.R1");
    const QString typeName = r2 ? QStringLiteral("R2Surface")
                                : QStringLiteral("R1Surface");
    const char* markerName = r2 ? "r2SurfaceMarker" : "r1SurfaceMarker";
    const QString expectedMarker = r2
        ? QStringLiteral("desktopup.ts-c01.qt-quick.r2-surface.v1")
        : QStringLiteral("desktopup.ts-c01.qt-quick.r1-surface.v1");
    engine_->loadFromModule(module, typeName);
    if (engine_->rootObjects().size() != 1) {
        if (error != nullptr) *error = QStringLiteral("QML_ROOT_CREATION_FAILED");
        release();
        return false;
    }

    window_ = qobject_cast<QQuickWindow*>(engine_->rootObjects().constFirst());
    if (window_ == nullptr || window_->property(markerName).toString() != expectedMarker) {
        if (error != nullptr) *error = QStringLiteral("QML_ROOT_CONTRACT_MISMATCH");
        release();
        return false;
    }

    window_->setVisible(false);
    window_->setParent(foreignParent_.get());
    window_->setGeometry(QRect(parentPixelRect.left, parentPixelRect.top,
                               parentPixelRect.right - parentPixelRect.left,
                               parentPixelRect.bottom - parentPixelRect.top));
    nativeHandle_ = reinterpret_cast<HWND>(window_->winId());
    if (nativeHandle_ == nullptr || !IsWindow(nativeHandle_)) {
        if (error != nullptr) *error = QStringLiteral("QT_NATIVE_CHILD_CREATION_FAILED");
        release();
        return false;
    }
    return true;
}

bool QtQuickSurfaceAdapter::warmup(QString* error)
{
    if (window_ == nullptr) {
        if (error != nullptr) *error = QStringLiteral("SURFACE_NOT_CREATED");
        return false;
    }
    lastFrame_ = window_->grabWindow();
    if (lastFrame_.isNull() || lastFrame_.width() <= 0 || lastFrame_.height() <= 0) {
        if (error != nullptr) *error = QStringLiteral("QT_QUICK_WARMUP_EMPTY_FRAME");
        return false;
    }
    return true;
}

bool QtQuickSurfaceAdapter::presentAndWaitForFrame(int timeoutMs, QString* error)
{
    if (window_ == nullptr || nativeHandle_ == nullptr) {
        if (error != nullptr) *error = QStringLiteral("SURFACE_NOT_CREATED");
        return false;
    }
    window_->setVisible(true);
    return waitForNextFrame(timeoutMs, error);
}

bool QtQuickSurfaceAdapter::waitForNextFrame(int timeoutMs, QString* error)
{
    if (window_ == nullptr || nativeHandle_ == nullptr || !IsWindowVisible(nativeHandle_)) {
        if (error != nullptr) *error = QStringLiteral("SURFACE_NOT_VISIBLE");
        return false;
    }

    if (isOffscreenFrameSource()) {
        QCoreApplication::processEvents(QEventLoop::AllEvents, 50);
        lastFrame_ = window_->grabWindow();
        framePresented_ = !lastFrame_.isNull() &&
                          lastFrame_.width() > 0 && lastFrame_.height() > 0;
        if (!framePresented_ && error != nullptr) {
            *error = QStringLiteral("OFFSCREEN_FRAME_CAPTURE_FAILED");
        }
        return framePresented_;
    }

    framePresented_ = false;
    QString sceneGraphError;
    QEventLoop loop;
    QTimer timeout;
    timeout.setSingleShot(true);
    QObject::connect(&timeout, &QTimer::timeout, &loop, &QEventLoop::quit);
    const QMetaObject::Connection frameConnection = QObject::connect(
        window_, &QQuickWindow::frameSwapped, &loop, [&]() {
            framePresented_ = true;
            loop.quit();
        }, Qt::QueuedConnection);
    const QMetaObject::Connection errorConnection = QObject::connect(
        window_, &QQuickWindow::sceneGraphError, &loop,
        [&](QQuickWindow::SceneGraphError, const QString& message) {
            sceneGraphError = message;
            loop.quit();
        }, Qt::QueuedConnection);

    window_->requestUpdate();
    timeout.start(timeoutMs);
    loop.exec();
    QObject::disconnect(frameConnection);
    QObject::disconnect(errorConnection);

    if (!sceneGraphError.isEmpty()) {
        if (error != nullptr) *error = QStringLiteral("SCENE_GRAPH_ERROR: ") + sceneGraphError;
        return false;
    }
    if (!framePresented_) {
        if (error != nullptr) *error = QStringLiteral("FRAME_TIMEOUT");
        return false;
    }
    return true;
}

bool QtQuickSurfaceAdapter::setRootProperty(
    const char* name, const QVariant& value, QString* error)
{
    if (window_ == nullptr || name == nullptr || name[0] == '\0') {
        if (error != nullptr) *error = QStringLiteral("QML_ROOT_PROPERTY_INVALID");
        return false;
    }
    if (!window_->setProperty(name, value)) {
        if (error != nullptr) {
            *error = QStringLiteral("QML_ROOT_PROPERTY_REJECTED: ") + QString::fromLatin1(name);
        }
        return false;
    }
    return true;
}

void QtQuickSurfaceAdapter::hide()
{
    if (window_ != nullptr) {
        window_->setVisible(false);
    }
}

void QtQuickSurfaceAdapter::release()
{
    if (window_ != nullptr) {
        window_->setVisible(false);
        window_->releaseResources();
    }
    engine_.reset();
    window_ = nullptr;
    nativeHandle_ = nullptr;
    QCoreApplication::processEvents(QEventLoop::AllEvents, 50);
    foreignParent_.reset();
    framePresented_ = false;
    lastFrame_ = QImage();
}

HWND QtQuickSurfaceAdapter::nativeHandle() const
{
    return nativeHandle_;
}

const QImage& QtQuickSurfaceAdapter::lastFrame() const
{
    return lastFrame_;
}

bool QtQuickSurfaceAdapter::isOffscreenFrameSource() const
{
    return fixtureKind_ == SurfaceFixtureKind::R2NightOps;
}

int QtQuickSurfaceAdapter::liveResourceCount() const
{
    return (engine_ != nullptr ? 1 : 0) + (window_ != nullptr ? 1 : 0) +
           (foreignParent_ != nullptr ? 1 : 0) + (nativeHandle_ != nullptr ? 1 : 0);
}

QString QtQuickSurfaceAdapter::graphicsApiName() const
{
    switch (QQuickWindow::graphicsApi()) {
    case QSGRendererInterface::Direct3D11: return QStringLiteral("Direct3D11");
    case QSGRendererInterface::Direct3D12: return QStringLiteral("Direct3D12");
    case QSGRendererInterface::OpenGL: return QStringLiteral("OpenGL");
    case QSGRendererInterface::Vulkan: return QStringLiteral("Vulkan");
    case QSGRendererInterface::Software: return QStringLiteral("Software");
    default: return QStringLiteral("Unknown");
    }
}

DesktopHostCoordinator::DesktopHostCoordinator(EventSink& sink)
    : sink_(sink),
      hostSessionId_(QUuid::createUuid().toString(QUuid::WithoutBraces))
{
    identity_.hostSessionId = hostSessionId_;
    sink_.setIdentity(hostSessionId_);
    sink_.write(QStringLiteral("host_session_started"), {
        {QStringLiteral("surface_transport"), QStringLiteral("ADAPTER_OWNED_NATIVE_CHILD")},
        {QStringLiteral("surface_ownership"), QStringLiteral("ADAPTER")},
        {QStringLiteral("p0_result"), QStringLiteral("NOT_RUN")}
    });
}

QJsonObject DesktopHostCoordinator::queryCapabilities() const
{
    return {
        {QStringLiteral("adapter_owned_native_child"), true},
        {QStringLiteral("offscreen_frame_source"), true},
        {QStringLiteral("topmost_forbidden"), true},
        {QStringLiteral("ordinary_top_level_fallback_forbidden"), true},
        {QStringLiteral("host_owns_parent_z_input"), true},
        {QStringLiteral("evidence_disposition"), QStringLiteral("NOT_RUN")}
    };
}

std::optional<SurfaceBindingPlan> DesktopHostCoordinator::negotiateSurface(
    const SurfaceOffer& offer, QString* error) const
{
    if (exiting_) {
        if (error != nullptr) *error = QStringLiteral("HOST_EXITING");
        return std::nullopt;
    }
    const bool supportedRoute =
        (offer.transport == SurfaceTransportKind::AdapterOwnedNativeChild &&
         offer.ownership == SurfaceOwnership::Adapter) ||
        (offer.transport == SurfaceTransportKind::OffscreenFrameSource &&
         offer.ownership == SurfaceOwnership::Host);
    if (offer.candidateId != QString::fromLatin1(kCandidateId) ||
        !supportedRoute || offer.alphaMode != QStringLiteral("premultiplied")) {
        if (error != nullptr) *error = QStringLiteral("SURFACE_TRANSPORT_UNSUPPORTED");
        return std::nullopt;
    }
    SurfaceBindingPlan plan;
    plan.planId = offer.transport == SurfaceTransportKind::OffscreenFrameSource
        ? QStringLiteral("ts-c01-plan-offscreen-frame-host-layered-v1")
        : QStringLiteral("ts-c01-plan-adapter-owned-native-child-v1");
    plan.transport = offer.transport;
    plan.ownership = offer.ownership;
    plan.evidenceDisposition = QStringLiteral("NOT_RUN");
    plan.valid = true;
    return plan;
}

ShellTarget DesktopHostCoordinator::discoverShellTarget()
{
    ShellTarget target;
    target.progman = FindWindowW(L"Progman", nullptr);
    std::vector<HWND> visibleDefViews;
    EnumWindows(collectVisibleDefViews, reinterpret_cast<LPARAM>(&visibleDefViews));
    target.visibleDefViewCount = static_cast<int>(visibleDefViews.size());
    if (visibleDefViews.size() != 1) {
        target.topology = QStringLiteral("AMBIGUOUS_OR_MISSING");
        return target;
    }

    target.defView = visibleDefViews.front();
    target.iconHost = GetParent(target.defView);
    GetWindowThreadProcessId(target.defView, &target.explorerPid);
    if (target.iconHost == target.progman && className(target.iconHost) == QStringLiteral("Progman")) {
        target.topology = QStringLiteral("RAISED_PROGMAN");
    } else if (className(target.iconHost) == QStringLiteral("WorkerW")) {
        target.topology = QStringLiteral("TRADITIONAL_WORKERW");
    } else {
        target.topology = QStringLiteral("UNSUPPORTED");
    }

    target.iconView = FindWindowExW(target.defView, nullptr, L"SysListView32", nullptr);
    if (target.iconView == nullptr) {
        target.iconView = FindWindowExW(target.defView, nullptr, L"FolderView", nullptr);
    }
    return target;
}

bool DesktopHostCoordinator::attach(
    const SurfaceBindingPlan& plan, QtQuickSurfaceAdapter& adapter, QString* error)
{
    if (exiting_) {
        if (error != nullptr) *error = QStringLiteral("HOST_EXITING");
        return false;
    }
    const bool supportedRoute =
        (plan.transport == SurfaceTransportKind::AdapterOwnedNativeChild &&
         plan.ownership == SurfaceOwnership::Adapter) ||
        (plan.transport == SurfaceTransportKind::OffscreenFrameSource &&
         plan.ownership == SurfaceOwnership::Host);
    if (!plan.valid || !supportedRoute) {
        if (error != nullptr) *error = QStringLiteral("SURFACE_PLAN_INVALID");
        return false;
    }

    shell_ = discoverShellTarget();
    if (shell_.topology != QStringLiteral("RAISED_PROGMAN") ||
        shell_.iconHost == nullptr || shell_.defView == nullptr ||
        shell_.iconView == nullptr || shell_.visibleDefViewCount != 1) {
        if (error != nullptr) *error = QStringLiteral("NO_SAFE_HOST_RAISED_PROGMAN_REQUIRED");
        failed_ = true;
        return false;
    }

    MONITORINFO monitor {};
    monitor.cbSize = sizeof(monitor);
    if (!GetMonitorInfoW(MonitorFromPoint(POINT {0, 0}, MONITOR_DEFAULTTOPRIMARY), &monitor)) {
        if (error != nullptr) *error = QStringLiteral("PRIMARY_MONITOR_QUERY_FAILED");
        failed_ = true;
        return false;
    }
    UINT dpi = GetDpiForSystem();
    if (dpi == 0) dpi = 96;
    const int width = MulDiv(420, static_cast<int>(dpi), 96);
    const int height = MulDiv(620, static_cast<int>(dpi), 96);
    const int margin = MulDiv(40, static_cast<int>(dpi), 96);
    screenRect_ = {monitor.rcWork.right - width - margin, monitor.rcWork.top + margin,
                   monitor.rcWork.right - margin, monitor.rcWork.top + margin + height};
    POINT points[2] {{screenRect_.left, screenRect_.top}, {screenRect_.right, screenRect_.bottom}};
    MapWindowPoints(nullptr, shell_.iconHost, points, 2);
    parentRect_ = {points[0].x, points[0].y, points[1].x, points[1].y};

    plan_ = plan;
    adapter_ = &adapter;
    identity_.attachmentId = QUuid::createUuid().toString(QUuid::WithoutBraces);
    identity_.shellGeneration = shell_.explorerPid != 0 ? shell_.explorerPid : 1;
    identity_.lifecycleEpoch = 1;
    identity_.surfaceEpoch = 1;
    identity_.hostProjectionRevision = 1;
    identity_.sourceSnapshotRevision = 1;
    identity_.hitRegionRevision = 1;
    identity_.renderRevision = 0;
    hostState_ = HostState::Attaching;
    surfaceState_ = SurfaceState::Binding;
    detached_ = false;
    writeAttachmentState(QStringLiteral("ATTACHING"), QStringLiteral("BINDING"), QStringLiteral("OK"));

    if (!adapter.createSurface(shell_.iconHost, parentRect_, error) ||
        !bindNativeSurface(error) || !commitProjection(error) ||
        !adapter.warmup(error) || !notifySurfaceReady(error)) {
        failed_ = true;
        return false;
    }
    return true;
}

bool DesktopHostCoordinator::bindNativeSurface(QString* error)
{
    const HWND surface = adapter_ != nullptr ? adapter_->nativeHandle() : nullptr;
    if (surface == nullptr || !IsWindow(surface)) {
        if (error != nullptr) *error = QStringLiteral("SURFACE_HANDLE_INVALID");
        return false;
    }

    LONG_PTR style = GetWindowLongPtrW(surface, GWL_STYLE);
    style = (style | WS_CHILD | WS_CLIPSIBLINGS | WS_CLIPCHILDREN) &
            ~(static_cast<LONG_PTR>(WS_POPUP) | WS_CAPTION | WS_THICKFRAME);
    LONG_PTR exStyle = GetWindowLongPtrW(surface, GWL_EXSTYLE);
    exStyle = (exStyle | WS_EX_NOACTIVATE |
               WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT) &
              ~static_cast<LONG_PTR>(WS_EX_TOPMOST);

    const bool styleApplied = setWindowLongChecked(surface, GWL_STYLE, style);
    const DWORD styleError = GetLastError();
    const bool exStyleApplied = setWindowLongChecked(surface, GWL_EXSTYLE, exStyle);
    const DWORD exStyleError = GetLastError();
    const bool positioned = SetWindowPos(
        surface, plan_.transport == SurfaceTransportKind::OffscreenFrameSource
                     ? shell_.defView : HWND_TOP,
        parentRect_.left, parentRect_.top,
        parentRect_.right - parentRect_.left,
        parentRect_.bottom - parentRect_.top,
        SWP_NOACTIVATE | SWP_FRAMECHANGED) != FALSE;
    const DWORD positionError = GetLastError();
    const LONG_PTR appliedStyle = GetWindowLongPtrW(surface, GWL_STYLE);
    const LONG_PTR appliedExStyle = GetWindowLongPtrW(surface, GWL_EXSTYLE);
    const bool valid = IsWindow(surface) &&
                       positioned &&
                       GetParent(surface) == shell_.iconHost &&
                       (appliedStyle & WS_CHILD) != 0 &&
                       (appliedStyle & WS_POPUP) == 0 &&
                       (appliedExStyle & WS_EX_NOACTIVATE) != 0 &&
                       (appliedExStyle & WS_EX_TRANSPARENT) != 0 &&
                       (appliedExStyle & WS_EX_TOPMOST) == 0 &&
                       GetWindow(surface, GW_OWNER) == nullptr &&
                       !IsWindowVisible(surface);
    if (!valid) {
        if (error != nullptr) {
            *error = QStringLiteral("HOST_NATIVE_BIND_CONTRACT_FAILED style_set=%1 style_error=%2 ex_style_set=%3 ex_error=%4 position=%5 position_error=%6 parent_match=%7 actual_style=0x%8 actual_ex_style=0x%9")
                .arg(styleApplied).arg(styleError).arg(exStyleApplied).arg(exStyleError)
                .arg(positioned).arg(positionError).arg(GetParent(surface) == shell_.iconHost)
                .arg(QString::number(static_cast<qulonglong>(appliedStyle), 16))
                .arg(QString::number(static_cast<qulonglong>(appliedExStyle), 16));
        }
        return false;
    }
    return true;
}

bool DesktopHostCoordinator::commitProjection(QString* error)
{
    if (detached_ || exiting_ || adapter_ == nullptr) {
        if (error != nullptr) *error = QStringLiteral("COMMIT_NOT_ALLOWED");
        return false;
    }
    identity_.hostProjectionRevision = 2;
    identity_.sourceSnapshotRevision = 2;
    identity_.hitRegionRevision = 2;
    identity_.renderRevision = 2;
    QJsonObject fields = fencedFields();
    fields.insert(QStringLiteral("physical_bounds"), physicalBounds());
    fields.insert(QStringLiteral("surface_transport"), transportName(plan_.transport));
    fields.insert(QStringLiteral("surface_ownership"), ownershipName(plan_.ownership));
    fields.insert(QStringLiteral("hit_region_count"), 0);
    sink_.write(QStringLiteral("projection_committed"), fields);
    return true;
}

bool DesktopHostCoordinator::commitFixtureProjection(
    const QRect& screenPixelRect,
    const QString& presentationMode,
    const QString& interactionMode,
    bool locked,
    const QJsonArray& hitRegions,
    const QString& reason,
    QString* error)
{
    if (detached_ || exiting_ || adapter_ == nullptr ||
        surfaceState_ != SurfaceState::Presented) {
        if (error != nullptr) *error = QStringLiteral("FIXTURE_COMMIT_NOT_ALLOWED");
        return false;
    }
    if (!screenPixelRect.isValid() || screenPixelRect.width() < 240 ||
        screenPixelRect.height() < 56 ||
        (presentationMode != QStringLiteral("EXPANDED") &&
         presentationMode != QStringLiteral("COLLAPSED")) ||
        (interactionMode != QStringLiteral("USING") &&
         interactionMode != QStringLiteral("EDITING"))) {
        if (error != nullptr) *error = QStringLiteral("FIXTURE_PROJECTION_INVALID");
        return false;
    }

    const HWND surface = adapter_->nativeHandle();
    if (surface == nullptr || !IsWindow(surface)) {
        if (error != nullptr) *error = QStringLiteral("SURFACE_HANDLE_INVALID");
        return false;
    }

    POINT points[2] {
        {screenPixelRect.left(), screenPixelRect.top()},
        {screenPixelRect.right() + 1, screenPixelRect.bottom() + 1}
    };
    MapWindowPoints(nullptr, shell_.iconHost, points, 2);
    const RECT nextParentRect {points[0].x, points[0].y, points[1].x, points[1].y};
    if (!SetWindowPos(
            surface, plan_.transport == SurfaceTransportKind::OffscreenFrameSource
                         ? shell_.defView : HWND_TOP,
            nextParentRect.left, nextParentRect.top,
            nextParentRect.right - nextParentRect.left,
            nextParentRect.bottom - nextParentRect.top,
            SWP_NOACTIVATE)) {
        if (error != nullptr) *error = QStringLiteral("FIXTURE_WINDOW_POSITION_FAILED");
        return false;
    }

    RECT actual {};
    GetWindowRect(surface, &actual);
    const RECT expected {
        screenPixelRect.left(),
        screenPixelRect.top(),
        screenPixelRect.right() + 1,
        screenPixelRect.bottom() + 1
    };
    if (actual.left != expected.left || actual.top != expected.top ||
        actual.right != expected.right || actual.bottom != expected.bottom) {
        if (error != nullptr) *error = QStringLiteral("RENDER_BOUNDS_MISMATCH");
        return false;
    }

    screenRect_ = expected;
    parentRect_ = nextParentRect;
    ++identity_.hostProjectionRevision;
    ++identity_.sourceSnapshotRevision;
    ++identity_.hitRegionRevision;
    ++identity_.renderRevision;
    surfaceState_ = SurfaceState::ReadyNoFrame;

    QJsonObject fields = fencedFields();
    fields.insert(QStringLiteral("physical_bounds"), physicalBounds());
    fields.insert(QStringLiteral("logical_geometry"), QJsonObject {
        {QStringLiteral("x"), screenPixelRect.x()},
        {QStringLiteral("y"), screenPixelRect.y()},
        {QStringLiteral("width"), screenPixelRect.width()},
        {QStringLiteral("height"), screenPixelRect.height()}
    });
    fields.insert(QStringLiteral("presentation_mode"), presentationMode);
    fields.insert(QStringLiteral("interaction_mode"), interactionMode);
    fields.insert(QStringLiteral("is_locked"), locked);
    fields.insert(QStringLiteral("hit_regions"), hitRegions);
    fields.insert(QStringLiteral("hit_region_count"), hitRegions.size());
    fields.insert(QStringLiteral("projection_reason"), reason);
    fields.insert(QStringLiteral("surface_transport"), transportName(plan_.transport));
    fields.insert(QStringLiteral("surface_ownership"), ownershipName(plan_.ownership));
    sink_.write(QStringLiteral("projection_committed"), fields);
    return true;
}

bool DesktopHostCoordinator::notifySurfaceReady(QString* error)
{
    if (surfaceState_ != SurfaceState::Binding || adapter_ == nullptr ||
        adapter_->nativeHandle() == nullptr) {
        if (error != nullptr) *error = QStringLiteral("SURFACE_READY_STALE_OR_INVALID");
        return false;
    }
    surfaceState_ = SurfaceState::ReadyNoFrame;
    hostState_ = HostState::AttachedSafe;
    QJsonObject fields = fencedFields();
    fields.insert(QStringLiteral("physical_bounds"), QJsonObject {
        {QStringLiteral("render_target"), rectArray(screenRect_)}
    });
    fields.insert(QStringLiteral("graphics_api"), adapter_->graphicsApiName());
    sink_.write(QStringLiteral("surface_ready"), fields);
    return true;
}

bool DesktopHostCoordinator::notifyFramePresented(QString* error)
{
    if (surfaceState_ != SurfaceState::ReadyNoFrame || adapter_ == nullptr ||
        adapter_->nativeHandle() == nullptr) {
        if (error != nullptr) *error = QStringLiteral("FRAME_STALE_OR_INVALID");
        return false;
    }
    const HWND surface = adapter_->nativeHandle();
    const LONG_PTR exStyle = GetWindowLongPtrW(surface, GWL_EXSTYLE);
    if (GetParent(surface) != shell_.iconHost || (exStyle & WS_EX_TOPMOST) != 0 ||
        GetWindow(surface, GW_OWNER) != nullptr || !IsWindowVisible(surface)) {
        if (error != nullptr) *error = QStringLiteral("PRESENTED_SURFACE_CONTRACT_FAILED");
        return false;
    }
    surfaceState_ = SurfaceState::Presented;
    hostState_ = HostState::AttachedSafe;
    QJsonObject fields = fencedFields();
    fields.insert(QStringLiteral("physical_bounds"), QJsonObject {
        {QStringLiteral("render_target"), rectArray(screenRect_)}
    });
    sink_.write(QStringLiteral("frame_presented"), fields);
    return true;
}

bool DesktopHostCoordinator::notifyProjectionFramePresented(QString* error)
{
    if (surfaceState_ != SurfaceState::ReadyNoFrame || adapter_ == nullptr ||
        adapter_->nativeHandle() == nullptr) {
        if (error != nullptr) *error = QStringLiteral("FRAME_STALE_OR_INVALID");
        return false;
    }
    const HWND surface = adapter_->nativeHandle();
    const LONG_PTR exStyle = GetWindowLongPtrW(surface, GWL_EXSTYLE);
    if (GetParent(surface) != shell_.iconHost || (exStyle & WS_EX_TOPMOST) != 0 ||
        GetWindow(surface, GW_OWNER) != nullptr || !IsWindowVisible(surface)) {
        if (error != nullptr) *error = QStringLiteral("PRESENTED_SURFACE_CONTRACT_FAILED");
        return false;
    }
    surfaceState_ = SurfaceState::Presented;
    hostState_ = HostState::AttachedSafe;
    QJsonObject fields = fencedFields();
    fields.insert(QStringLiteral("physical_bounds"), QJsonObject {
        {QStringLiteral("render_target"), rectArray(screenRect_)}
    });
    fields.insert(QStringLiteral("projection_publish"), QStringLiteral("R2_FIXTURE"));
    sink_.write(QStringLiteral("frame_presented"), fields);
    return true;
}

bool DesktopHostCoordinator::notifyProjectionFramePresentedAtRevision(
    quint64 observedRevision, QString* error)
{
    if (detached_ || exiting_ || surfaceState_ == SurfaceState::Released ||
        surfaceState_ == SurfaceState::Unbound) {
        QJsonObject fields = fencedFields();
        fields.insert(QStringLiteral("observed_revision"),
                      static_cast<qint64>(observedRevision));
        fields.insert(QStringLiteral("expected_revision"),
                      static_cast<qint64>(identity_.hostProjectionRevision));
        fields.insert(QStringLiteral("rejection_reason"),
                      QStringLiteral("ATTACHMENT_NOT_ACCEPTING_FRAMES"));
        fields.insert(QStringLiteral("result"), QStringLiteral("PASS"));
        sink_.write(QStringLiteral("late_frame_rejected"), fields);
        if (error != nullptr) *error = QStringLiteral("LATE_FRAME_REJECTED");
        return false;
    }
    if (observedRevision != identity_.hostProjectionRevision) {
        QJsonObject fields = fencedFields();
        fields.insert(QStringLiteral("observed_revision"),
                      static_cast<qint64>(observedRevision));
        fields.insert(QStringLiteral("expected_revision"),
                      static_cast<qint64>(identity_.hostProjectionRevision));
        fields.insert(QStringLiteral("rejection_reason"),
                      QStringLiteral("HOST_PROJECTION_REVISION_MISMATCH"));
        fields.insert(QStringLiteral("result"), QStringLiteral("PASS"));
        sink_.write(QStringLiteral("stale_frame_rejected"), fields);
        if (error != nullptr) *error = QStringLiteral("STALE_FRAME_REJECTED");
        return false;
    }
    return notifyProjectionFramePresented(error);
}
bool DesktopHostCoordinator::notifySurfaceFault(const QString& reason, bool recoverable)
{
    if (detached_ || exiting_) return false;
    surfaceState_ = SurfaceState::Lost;
    hostState_ = HostState::Faulted;
    failed_ = true;
    QJsonObject fields = fencedFields();
    fields.insert(QStringLiteral("reason"), reason);
    fields.insert(QStringLiteral("recoverable"), recoverable);
    sink_.write(QStringLiteral("surface_fault"), fields);
    return true;
}

QJsonObject DesktopHostCoordinator::observeHostState() const
{
    QJsonObject observation = fencedFields();
    observation.insert(QStringLiteral("host_state"), hostStateName(hostState_));
    observation.insert(QStringLiteral("surface_state"), surfaceStateName(surfaceState_));
    observation.insert(QStringLiteral("topology"), shell_.topology);
    observation.insert(QStringLiteral("physical_bounds"), physicalBounds());
    observation.insert(QStringLiteral("resource_counts"), resourceCounts());
    return observation;
}

bool DesktopHostCoordinator::requestReconnect(
    quint64 observedShellGeneration, const QString& requestId, QString* error)
{
    Q_UNUSED(requestId);
    if (exiting_) {
        if (error != nullptr) *error = QStringLiteral("HOST_EXITING");
        return false;
    }
    if (observedShellGeneration == identity_.shellGeneration && !failed_) {
        if (error != nullptr) *error = QStringLiteral("RECONNECT_NOT_REQUIRED");
        return false;
    }
    if (error != nullptr) *error = QStringLiteral("R1_RECONNECT_HOOK_DECLARED_NOT_EXERCISED");
    return false;
}

bool DesktopHostCoordinator::detach(const QString& requestId, QString* error)
{
    if (detached_) {
        if (detachRequestId_.isEmpty() || detachRequestId_ == requestId) return true;
        if (error != nullptr) *error = QStringLiteral("REQUEST_ID_REUSE_MISMATCH");
        return false;
    }
    if (!detachRequestId_.isEmpty() && detachRequestId_ != requestId) {
        if (error != nullptr) *error = QStringLiteral("DETACH_ALREADY_IN_PROGRESS");
        return false;
    }
    detachRequestId_ = requestId;
    ++identity_.lifecycleEpoch;
    hostState_ = HostState::Detaching;
    surfaceState_ = SurfaceState::Releasing;
    writeAttachmentState(QStringLiteral("DETACHING"), QStringLiteral("RELEASING"), QStringLiteral("OK"));

    const HWND oldSurface = adapter_ != nullptr ? adapter_->nativeHandle() : nullptr;
    if (adapter_ != nullptr) {
        adapter_->hide();
        adapter_->release();
    }
    const bool released = oldSurface == nullptr || !IsWindow(oldSurface);
    surfaceState_ = SurfaceState::Released;
    hostState_ = HostState::Detached;
    detached_ = true;
    writeAttachmentState(QStringLiteral("DETACHED"), QStringLiteral("RELEASED"),
                         released ? QStringLiteral("OK") : QStringLiteral("CLEANUP_INCOMPLETE"));
    if (!released) {
        failed_ = true;
        if (error != nullptr) *error = QStringLiteral("ADAPTER_SURFACE_STILL_LIVE");
        return false;
    }
    return true;
}

bool DesktopHostCoordinator::beginExit(const QString& requestId, QString* error)
{
    if (exiting_) {
        if (exitRequestId_ == requestId) return !failed_;
        if (error != nullptr) *error = QStringLiteral("REQUEST_ID_REUSE_MISMATCH");
        return false;
    }
    exiting_ = true;
    exitRequestId_ = requestId;
    sink_.write(QStringLiteral("host_session_exiting"));
    if (!detached_ && !detach(QStringLiteral("begin-exit-detach"), error)) {
        failed_ = true;
    }
    hostState_ = HostState::Exiting;
    const QJsonObject counts = resourceCounts();
    sink_.write(QStringLiteral("cleanup_completed"), {{QStringLiteral("resource_counts"), counts}});
    sink_.write(QStringLiteral("host_session_ended"), {{QStringLiteral("resource_counts"), counts}});
    return !failed_;
}

bool DesktopHostCoordinator::holdVisibleFor(int durationMs)
{
    if (surfaceState_ != SurfaceState::Presented) return false;
    QEventLoop loop;
    QTimer::singleShot(durationMs, &loop, &QEventLoop::quit);
    loop.exec();
    return true;
}

void DesktopHostCoordinator::recordFixtureEvent(
    const QString& event, const QJsonObject& fields)
{
    QJsonObject record = fencedFields();
    for (auto iterator = fields.constBegin(); iterator != fields.constEnd(); ++iterator) {
        record.insert(iterator.key(), iterator.value());
    }
    sink_.write(event, record);
}

HWND DesktopHostCoordinator::approvedParent() const
{
    return shell_.iconHost;
}

HWND DesktopHostCoordinator::explorerDefView() const
{
    return shell_.defView;
}

HWND DesktopHostCoordinator::explorerIconView() const
{
    return shell_.iconView;
}

QRect DesktopHostCoordinator::screenPixelRect() const
{
    return QRect(screenRect_.left, screenRect_.top,
                 screenRect_.right - screenRect_.left,
                 screenRect_.bottom - screenRect_.top);
}

quint64 DesktopHostCoordinator::hostProjectionRevision() const
{
    return identity_.hostProjectionRevision;
}

int DesktopHostCoordinator::exitCode() const
{
    return failed_ ? 1 : 0;
}

QJsonObject DesktopHostCoordinator::fencedFields() const
{
    return {
        {QStringLiteral("attachment_id"), identity_.attachmentId},
        {QStringLiteral("shell_generation"), static_cast<qint64>(identity_.shellGeneration)},
        {QStringLiteral("lifecycle_epoch"), static_cast<qint64>(identity_.lifecycleEpoch)},
        {QStringLiteral("surface_epoch"), static_cast<qint64>(identity_.surfaceEpoch)},
        {QStringLiteral("host_projection_revision"), static_cast<qint64>(identity_.hostProjectionRevision)},
        {QStringLiteral("source_snapshot_revision"), static_cast<qint64>(identity_.sourceSnapshotRevision)},
        {QStringLiteral("hit_region_revision"), static_cast<qint64>(identity_.hitRegionRevision)},
        {QStringLiteral("render_revision"), static_cast<qint64>(identity_.renderRevision)},
        {QStringLiteral("display_environment_revision"), static_cast<qint64>(identity_.displayEnvironmentRevision)}
    };
}

QJsonObject DesktopHostCoordinator::physicalBounds() const
{
    if (adapter_ == nullptr || adapter_->nativeHandle() == nullptr ||
        !IsWindow(adapter_->nativeHandle())) {
        return {
            {QStringLiteral("window"), rectArray(screenRect_)},
            {QStringLiteral("client"), rectArray(screenRect_)},
            {QStringLiteral("extended_frame"), rectArray(screenRect_)},
            {QStringLiteral("render_target"), rectArray(screenRect_)}
        };
    }
    const HWND surface = adapter_->nativeHandle();
    RECT windowRect {};
    GetWindowRect(surface, &windowRect);
    return {
        {QStringLiteral("window"), rectArray(windowRect)},
        {QStringLiteral("client"), rectArray(clientScreenRect(surface))},
        {QStringLiteral("extended_frame"), rectArray(extendedFrameRect(surface))},
        {QStringLiteral("render_target"), rectArray(windowRect)}
    };
}

QJsonObject DesktopHostCoordinator::resourceCounts() const
{
    const int adapterCount = adapter_ != nullptr ? adapter_->liveResourceCount() : 0;
    const int hostCount = (!detached_ && adapter_ != nullptr && adapter_->nativeHandle() != nullptr) ? 1 : 0;
    return {
        {QStringLiteral("host"), hostCount},
        {QStringLiteral("adapter"), adapterCount},
        {QStringLiteral("quarantine"), 0}
    };
}

void DesktopHostCoordinator::writeAttachmentState(
    const QString& hostState, const QString& surfaceState, const QString& result)
{
    QJsonObject fields = fencedFields();
    fields.insert(QStringLiteral("host_state"), hostState);
    fields.insert(QStringLiteral("surface_state"), surfaceState);
    fields.insert(QStringLiteral("result"), result);
    fields.insert(QStringLiteral("topmost"), false);
    fields.insert(QStringLiteral("ordinary_top_level_fallback"), false);
    fields.insert(QStringLiteral("surface_transport"), transportName(plan_.transport));
    fields.insert(QStringLiteral("surface_ownership"), ownershipName(plan_.ownership));
    sink_.write(QStringLiteral("attachment_state"), fields);
}

}  // namespace desktopup::tsc01
