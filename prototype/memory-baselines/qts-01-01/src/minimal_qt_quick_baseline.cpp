#include "baseline_common.h"

#include <QEventLoop>
#include <QGuiApplication>
#include <QPointer>
#include <QQmlComponent>
#include <QQmlEngine>
#include <QQuickWindow>
#include <QSGRendererInterface>
#include <QTimer>

#include <windows.h>

namespace {

constexpr char kMinimalQml[] = R"QML(
import QtQuick
import QtQuick.Window
Window {
    width: 96
    height: 96
    visible: false
    color: "transparent"
    Rectangle {
        anchors.fill: parent
        color: "#10212b"
        border.color: "#42c5d6"
        border.width: 1
    }
}
)QML";

bool waitForFirstFrame(QQuickWindow* window, int timeoutMs)
{
    bool presented = false;
    QEventLoop loop;
    QTimer timeout;
    timeout.setSingleShot(true);
    QObject::connect(&timeout, &QTimer::timeout, &loop, &QEventLoop::quit);
    const auto connection = QObject::connect(
        window, &QQuickWindow::frameSwapped, &loop, [&]() {
            presented = true;
            loop.quit();
        }, Qt::QueuedConnection);
    window->show();
    window->requestUpdate();
    timeout.start(timeoutMs);
    loop.exec();
    QObject::disconnect(connection);
    return presented;
}

}  // namespace

int main(int argc, char** argv)
{
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    QGuiApplication application(argc, argv);
    application.setApplicationName(QStringLiteral("DesktopUp QTS-01-01 Minimal Qt Quick Baseline"));
    application.setQuitOnLastWindowClosed(false);
    QQuickWindow::setGraphicsApi(QSGRendererInterface::Direct3D11);

    desktopup::qts0101::Options options;
    if (!desktopup::qts0101::parseOptions(QCoreApplication::arguments(), &options)) {
        return desktopup::qts0101::usageError(argv[0]);
    }
    desktopup::qts0101::EventLog log(
        QStringLiteral("minimal_qt_quick"), options.logPath);
    if (!log.open()) return 3;

    QQmlEngine engine;
    QQmlComponent component(&engine);
    component.setData(kMinimalQml, QUrl());
    QPointer<QObject> root(component.create());
    QQuickWindow* window = qobject_cast<QQuickWindow*>(root.data());
    if (window == nullptr || component.isError()) {
        log.write(QStringLiteral("baseline_fault"), {
            {QStringLiteral("reason"), QStringLiteral("MINIMAL_QML_CREATE_FAILED")},
            {QStringLiteral("detail"), component.errorString()}
        });
        delete root;
        return 4;
    }

    window->setPersistentGraphics(true);
    window->setPersistentSceneGraph(true);
    window->setFlags(Qt::Tool | Qt::FramelessWindowHint |
                     Qt::WindowDoesNotAcceptFocus);
    window->setPosition(-32000, -32000);
    if (!waitForFirstFrame(window, 5000)) {
        log.write(QStringLiteral("baseline_fault"), {
            {QStringLiteral("reason"), QStringLiteral("FIRST_FRAME_TIMEOUT")}
        });
        delete root;
        return 4;
    }
    window->hide();

    quint64 staticFrames = 0;
    QObject::connect(window, &QQuickWindow::frameSwapped, &application,
                     [&]() { ++staticFrames; });
    if (!log.write(QStringLiteral("baseline_ready"), {
            {QStringLiteral("graphics_api"), QStringLiteral("Direct3D11")},
            {QStringLiteral("scene"), QStringLiteral("one_rectangle")},
            {QStringLiteral("window_visible_during_sample"), false}
        }) ||
        !desktopup::qts0101::publishReadyMarker(
            options.readyMarkerPath, QStringLiteral("minimal_qt_quick"))) {
        delete root;
        return 5;
    }

    QTimer::singleShot(options.durationMs, &application, &QCoreApplication::quit);
    const int exitCode = application.exec();
    log.write(QStringLiteral("baseline_finished"), {
        {QStringLiteral("static_frame_count"), static_cast<qint64>(staticFrames)},
        {QStringLiteral("exit_code"), exitCode}
    });
    delete root;
    return exitCode;
}
