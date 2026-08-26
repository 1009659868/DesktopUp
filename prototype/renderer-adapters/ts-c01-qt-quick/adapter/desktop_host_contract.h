#pragma once

#include <windows.h>

#include <QElapsedTimer>
#include <QFile>
#include <QImage>
#include <QJsonArray>
#include <QJsonObject>
#include <QRect>
#include <QString>
#include <QVariant>

#include <memory>
#include <optional>

class QQmlApplicationEngine;
class QQuickWindow;
class QWindow;

namespace desktopup::tsc01 {

inline constexpr char kCandidateId[] = "TS-C01";
#ifndef DESKTOPUP_TS_C01_BUILD_ID
#define DESKTOPUP_TS_C01_BUILD_ID "ts-c01-qt-6.8.3-r1-20260824-01"
#endif
inline constexpr char kBuildId[] = DESKTOPUP_TS_C01_BUILD_ID;

enum class SurfaceFixtureKind { R1Boundary, R2NightOps };
enum class SurfaceTransportKind { AdapterOwnedNativeChild, OffscreenFrameSource };
enum class SurfaceOwnership { Adapter, Host };
enum class HostState { Detached, Attaching, AttachedSafe, DegradedNoDesktopHost, Faulted, Detaching, Exiting };
enum class SurfaceState { Unbound, Binding, ReadyNoFrame, Presented, Lost, Releasing, Released };

struct SurfaceOffer {
    QString candidateId;
    SurfaceTransportKind transport = SurfaceTransportKind::AdapterOwnedNativeChild;
    SurfaceOwnership ownership = SurfaceOwnership::Adapter;
    QString alphaMode = QStringLiteral("premultiplied");
    QString sizeUpdateModel = QStringLiteral("host-atomic-pixel-bounds");
};

struct SurfaceBindingPlan {
    QString planId;
    SurfaceTransportKind transport = SurfaceTransportKind::AdapterOwnedNativeChild;
    SurfaceOwnership ownership = SurfaceOwnership::Adapter;
    QString evidenceDisposition = QStringLiteral("NOT_RUN");
    bool valid = false;
};

struct ShellTarget {
    HWND progman = nullptr;
    HWND iconHost = nullptr;
    HWND defView = nullptr;
    HWND iconView = nullptr;
    DWORD explorerPid = 0;
    QString topology;
    int visibleDefViewCount = 0;
};

struct AttachmentIdentity {
    QString hostSessionId;
    QString attachmentId;
    quint64 shellGeneration = 0;
    quint64 lifecycleEpoch = 0;
    quint64 surfaceEpoch = 0;
    quint64 hostProjectionRevision = 0;
    quint64 sourceSnapshotRevision = 0;
    quint64 hitRegionRevision = 0;
    quint64 renderRevision = 0;
    quint64 displayEnvironmentRevision = 1;
};

class EventSink final {
public:
    explicit EventSink(const QString& path);
    ~EventSink();

    bool isValid() const;
    void setIdentity(const QString& hostSessionId);
    void write(const QString& event, const QJsonObject& fields = {});

private:
    QFile file_;
    bool stdoutOnly_ = true;
    QElapsedTimer clock_;
    qint64 lastFlushMs_ = 0;
    quint64 bufferedLineCount_ = 0;
    quint64 sequence_ = 0;
    QString hostSessionId_;
};

class QtQuickSurfaceAdapter final {
public:
    explicit QtQuickSurfaceAdapter(SurfaceFixtureKind fixtureKind = SurfaceFixtureKind::R1Boundary);
    ~QtQuickSurfaceAdapter();

    bool createSurface(HWND approvedParent, const RECT& parentPixelRect, QString* error);
    bool warmup(QString* error);
    bool presentAndWaitForFrame(int timeoutMs, QString* error);
    bool waitForNextFrame(int timeoutMs, QString* error);
    bool setRootProperty(const char* name, const QVariant& value, QString* error);
    void hide();
    void release();

    HWND nativeHandle() const;
    const QImage& lastFrame() const;
    bool isOffscreenFrameSource() const;
    int liveResourceCount() const;
    QString graphicsApiName() const;

private:
    bool captureOffscreenFrame(int timeoutMs, QString* error);

    SurfaceFixtureKind fixtureKind_ = SurfaceFixtureKind::R1Boundary;
    std::unique_ptr<QQmlApplicationEngine> engine_;
    std::unique_ptr<QWindow> foreignParent_;
    QQuickWindow* window_ = nullptr;
    HWND nativeHandle_ = nullptr;
    bool framePresented_ = false;
    QImage lastFrame_;
};

class DesktopHostCoordinator final {
public:
    explicit DesktopHostCoordinator(EventSink& sink);

    QJsonObject queryCapabilities() const;
    std::optional<SurfaceBindingPlan> negotiateSurface(const SurfaceOffer& offer, QString* error) const;
    bool attach(const SurfaceBindingPlan& plan, QtQuickSurfaceAdapter& adapter, QString* error);
    bool commitProjection(QString* error);
    bool commitFixtureProjection(
        const QRect& screenPixelRect,
        const QString& presentationMode,
        const QString& interactionMode,
        bool locked,
        const QJsonArray& hitRegions,
        const QString& reason,
        QString* error);
    bool notifySurfaceReady(QString* error);
    bool notifyFramePresented(QString* error);
    bool notifyProjectionFramePresented(QString* error);
    bool notifyProjectionFramePresentedAtRevision(quint64 observedRevision, QString* error);
    bool notifyProjectionFramePresentedAtFence(
        quint64 observedSurfaceEpoch,
        quint64 observedRevision,
        QString* error);
    bool notifySurfaceFault(const QString& reason, bool recoverable);
    bool recoverSurfaceAfterFault(QString* error);
    QJsonObject observeHostState() const;
    bool requestReconnect(quint64 observedShellGeneration, const QString& requestId, QString* error);
    bool shellAttachmentStillValid() const;
    QString discoverSafeShellSignature() const;
    bool enterShellDegraded(const QString& reason, QString* error);
    bool recoverShellAttachment(QString* error);
    bool detach(const QString& requestId, QString* error);
    bool beginExit(const QString& requestId, QString* error);

    bool holdVisibleFor(int durationMs);
    void recordFixtureEvent(const QString& event, const QJsonObject& fields = {});
    HWND approvedParent() const;
    HWND explorerDefView() const;
    HWND explorerIconView() const;
    QRect screenPixelRect() const;
    quint64 hostProjectionRevision() const;
    bool advanceDisplayEnvironmentRevision(
        const QJsonObject& fields, QString* error);
    int exitCode() const;

private:
    static ShellTarget discoverShellTarget();
    bool bindNativeSurface(QString* error);
    QJsonObject fencedFields() const;
    QJsonObject physicalBounds() const;
    QJsonObject resourceCounts() const;
    void writeAttachmentState(const QString& hostState, const QString& surfaceState, const QString& result);

    EventSink& sink_;
    QString hostSessionId_;
    AttachmentIdentity identity_;
    ShellTarget shell_;
    SurfaceBindingPlan plan_;
    QtQuickSurfaceAdapter* adapter_ = nullptr;
    RECT screenRect_{};
    RECT parentRect_{};
    HostState hostState_ = HostState::Detached;
    SurfaceState surfaceState_ = SurfaceState::Unbound;
    QString detachRequestId_;
    QString exitRequestId_;
    bool detached_ = true;
    bool exiting_ = false;
    bool failed_ = false;
    bool recoveryPending_ = false;
    bool surfaceRecoveryPending_ = false;
    QString lastShellSignature_;
};

}  // namespace desktopup::tsc01
