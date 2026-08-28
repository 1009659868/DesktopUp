#include "baseline_common.h"

#include <QGuiApplication>
#include <QTimer>
#include <QWindow>

#include <windows.h>

int main(int argc, char** argv)
{
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    QGuiApplication application(argc, argv);
    application.setApplicationName(QStringLiteral("DesktopUp QTS-01-01 Qt GUI Baseline"));
    application.setQuitOnLastWindowClosed(false);

    desktopup::qts0101::Options options;
    if (!desktopup::qts0101::parseOptions(QCoreApplication::arguments(), &options)) {
        return desktopup::qts0101::usageError(argv[0]);
    }

    desktopup::qts0101::EventLog log(QStringLiteral("qt_gui_no_quick"), options.logPath);
    if (!log.open()) return 3;

    QWindow platformWindow;
    platformWindow.setTitle(QStringLiteral("DesktopUp QTS-01-01 Qt GUI Platform Window"));
    platformWindow.setFlags(Qt::Tool | Qt::FramelessWindowHint |
                            Qt::WindowDoesNotAcceptFocus);
    platformWindow.setSurfaceType(QSurface::RasterSurface);
    platformWindow.resize(1, 1);
    platformWindow.create();
    if (platformWindow.winId() == 0) {
        log.write(QStringLiteral("baseline_fault"), {
            {QStringLiteral("reason"), QStringLiteral("QT_PLATFORM_WINDOW_CREATE_FAILED")}
        });
        return 4;
    }

    if (!log.write(QStringLiteral("baseline_ready"), {
            {QStringLiteral("qt_quick_linked"), false},
            {QStringLiteral("native_platform_window"), true},
            {QStringLiteral("window_visible"), false}
        }) ||
        !desktopup::qts0101::publishReadyMarker(
            options.readyMarkerPath, QStringLiteral("qt_gui_no_quick"))) {
        return 5;
    }

    QTimer::singleShot(options.durationMs, &application, &QCoreApplication::quit);
    const int exitCode = application.exec();
    log.write(QStringLiteral("baseline_finished"), {
        {QStringLiteral("static_frame_count"), 0},
        {QStringLiteral("exit_code"), exitCode}
    });
    return exitCode;
}
