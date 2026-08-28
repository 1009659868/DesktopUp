#include "baseline_common.h"

#include <QEventLoop>
#include <QGuiApplication>
#include <QImage>
#include <QPointer>
#include <QQmlComponent>
#include <QQmlEngine>
#include <QQuickItem>
#include <QQuickItemGrabResult>
#include <QQuickWindow>
#include <QSGRendererInterface>
#include <QTimer>

#include <windows.h>

namespace {

constexpr char kOffscreenQml[] = R"QML(
import QtQuick
import QtQuick.Window
Window {
    id: root
    width: 420
    height: 620
    visible: false
    color: "transparent"
    Rectangle {
        anchors.fill: parent
        radius: 22
        color: "#e30a1017"
        border.color: "#344653"
        border.width: 1
        Text {
            x: 28; y: 28
            text: "DESKTOP COLLECTION"
            color: "#a7afb5"
            font.pixelSize: 11
            font.bold: true
        }
        Text {
            x: 28; y: 86
            text: "NIGHT OPS"
            color: "#f5f7f8"
            font.pixelSize: 38
            font.bold: true
        }
        Grid {
            x: 28; y: 150
            columns: 3
            spacing: 12
            Repeater {
                model: ["AL", "GS", "IV", "LO", "SR", "HC"]
                Rectangle {
                    required property string modelData
                    width: 112
                    height: 90
                    radius: 6
                    color: "#d4101822"
                    border.color: "#344451"
                    Text {
                        anchors.centerIn: parent
                        text: parent.modelData
                        color: "#f5f7f8"
                        font.pixelSize: 22
                        font.bold: true
                    }
                }
            }
        }
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

bool captureOffscreen(QQuickWindow* window, QImage* frame, int timeoutMs)
{
    if (window == nullptr || window->contentItem() == nullptr || frame == nullptr) {
        return false;
    }
    const QSharedPointer<QQuickItemGrabResult> grab =
        window->contentItem()->grabToImage(window->size());
    if (grab.isNull()) return false;

    bool captured = false;
    QEventLoop loop;
    QTimer timeout;
    timeout.setSingleShot(true);
    QObject::connect(&timeout, &QTimer::timeout, &loop, &QEventLoop::quit);
    QObject::connect(grab.data(), &QQuickItemGrabResult::ready, &loop, [&]() {
        *frame = grab->image();
        captured = !frame->isNull();
        loop.quit();
    }, Qt::QueuedConnection);
    window->requestUpdate();
    timeout.start(timeoutMs);
    loop.exec();
    return captured;
}

}  // namespace

int main(int argc, char** argv)
{
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    QGuiApplication application(argc, argv);
    application.setApplicationName(QStringLiteral("DesktopUp QTS-01-01 Offscreen Renderer Baseline"));
    application.setQuitOnLastWindowClosed(false);
    QQuickWindow::setGraphicsApi(QSGRendererInterface::Direct3D11);

    desktopup::qts0101::Options options;
    if (!desktopup::qts0101::parseOptions(QCoreApplication::arguments(), &options)) {
        return desktopup::qts0101::usageError(argv[0]);
    }
    desktopup::qts0101::EventLog log(
        QStringLiteral("offscreen_renderer"), options.logPath);
    if (!log.open()) return 3;

    QQmlEngine engine;
    QQmlComponent component(&engine);
    component.setData(kOffscreenQml, QUrl());
    QPointer<QObject> root(component.create());
    QQuickWindow* window = qobject_cast<QQuickWindow*>(root.data());
    if (window == nullptr || component.isError()) {
        log.write(QStringLiteral("baseline_fault"), {
            {QStringLiteral("reason"), QStringLiteral("OFFSCREEN_QML_CREATE_FAILED")},
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
    QImage lastFrame;
    if (!waitForFirstFrame(window, 5000) ||
        !captureOffscreen(window, &lastFrame, 5000)) {
        log.write(QStringLiteral("baseline_fault"), {
            {QStringLiteral("reason"), QStringLiteral("OFFSCREEN_CAPTURE_FAILED")}
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
            {QStringLiteral("transport"), QStringLiteral("QQuickItem::grabToImage")},
            {QStringLiteral("frame_width"), lastFrame.width()},
            {QStringLiteral("frame_height"), lastFrame.height()},
            {QStringLiteral("cpu_frame_bytes"), static_cast<qint64>(lastFrame.sizeInBytes())},
            {QStringLiteral("window_visible_during_sample"), false}
        }) ||
        !desktopup::qts0101::publishReadyMarker(
            options.readyMarkerPath, QStringLiteral("offscreen_renderer"))) {
        delete root;
        return 5;
    }

    QTimer::singleShot(options.durationMs, &application, &QCoreApplication::quit);
    const int exitCode = application.exec();
    log.write(QStringLiteral("baseline_finished"), {
        {QStringLiteral("static_frame_count"), static_cast<qint64>(staticFrames)},
        {QStringLiteral("retained_cpu_frame_bytes"),
         static_cast<qint64>(lastFrame.sizeInBytes())},
        {QStringLiteral("exit_code"), exitCode}
    });
    delete root;
    return exitCode;
}
