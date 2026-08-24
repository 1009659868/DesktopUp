#include "desktop_host_contract.h"

#include <QGuiApplication>
#include <QQuickWindow>
#include <QSGRendererInterface>

#include <iostream>

namespace {

struct Options {
    bool runLifecycle = false;
    int durationMs = 1200;
    QString logPath;
};

bool parseOptions(const QStringList& arguments, Options* options)
{
    for (int index = 1; index < arguments.size(); ++index) {
        const QString argument = arguments.at(index);
        if (argument == QStringLiteral("--run-r1-lifecycle")) {
            options->runLifecycle = true;
        } else if (argument == QStringLiteral("--duration-ms") && index + 1 < arguments.size()) {
            bool valid = false;
            const int value = arguments.at(++index).toInt(&valid);
            if (!valid || value < 250 || value > 15000) return false;
            options->durationMs = value;
        } else if (argument == QStringLiteral("--log") && index + 1 < arguments.size()) {
            options->logPath = arguments.at(++index);
        } else {
            return false;
        }
    }
    return options->runLifecycle;
}

}  // namespace

int main(int argc, char* argv[])
{
    if (!SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2) &&
        !AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(),
                                      DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)) {
        std::cerr << "Failed to establish Per-Monitor v2 DPI awareness.\n";
        return 2;
    }

    QGuiApplication application(argc, argv);
    application.setApplicationName(QStringLiteral("DesktopUp TS-C01 Qt R1"));
    application.setOrganizationName(QStringLiteral("DesktopUp"));
    QQuickWindow::setDefaultAlphaBuffer(true);
    QQuickWindow::setGraphicsApi(QSGRendererInterface::Direct3D11);

    Options options;
    if (!parseOptions(QCoreApplication::arguments(), &options)) {
        std::cerr << "Usage: ts-c01-qt-r1 --run-r1-lifecycle "
                     "[--duration-ms 250..15000] [--log path]\n";
        return 2;
    }

    desktopup::tsc01::EventSink sink(options.logPath);
    if (!sink.isValid()) {
        std::cerr << "Failed to open R1 JSONL log.\n";
        return 2;
    }

    desktopup::tsc01::QtQuickSurfaceAdapter adapter;
    desktopup::tsc01::DesktopHostCoordinator host(sink);
    QString error;
    const QJsonObject capabilities = host.queryCapabilities();
    if (!capabilities.value(QStringLiteral("adapter_owned_native_child")).toBool()) {
        error = QStringLiteral("HOST_CAPABILITY_MISSING");
    }

    desktopup::tsc01::SurfaceOffer offer;
    offer.candidateId = QString::fromLatin1(desktopup::tsc01::kCandidateId);
    const auto plan = error.isEmpty() ? host.negotiateSurface(offer, &error) : std::nullopt;
    bool succeeded = plan.has_value();
    if (succeeded) succeeded = host.attach(*plan, adapter, &error);
    if (succeeded) succeeded = adapter.presentAndWaitForFrame(5000, &error);
    if (succeeded) succeeded = host.notifyFramePresented(&error);
    if (succeeded) succeeded = host.holdVisibleFor(options.durationMs);

    QString detachError;
    const bool detached = host.detach(QStringLiteral("r1-detach-1"), &detachError);
    const bool duplicateDetach = host.detach(QStringLiteral("r1-detach-1"), &detachError);
    QString exitError;
    const bool exited = host.beginExit(QStringLiteral("r1-exit-1"), &exitError);

    if (!succeeded || !detached || !duplicateDetach || !exited || host.exitCode() != 0) {
        if (error.isEmpty()) error = !detachError.isEmpty() ? detachError : exitError;
        std::cerr << "TS-C01 R1 lifecycle preflight failed: "
                  << error.toStdString() << "\n";
        return 1;
    }

    std::cout << "TS-C01 R1 lifecycle preflight PASS; P0 remains NOT RUN.\n";
    return 0;
}
