#include <QCoreApplication>
#include <QDebug>
#include <QGuiApplication>
#include <QJsonDocument>
#include <QJsonObject>
#include <QQmlApplicationEngine>
#include <QQmlContext>
#include <QVariant>

namespace {
constexpr auto kEntryMarker = "desktopup.ts-c01.qt-quick.entry.v1";
}

int main(int argc, char* argv[])
{
    QGuiApplication application(argc, argv);
    application.setApplicationName(QStringLiteral("DesktopUp TS-C01 Entry"));
    application.setOrganizationName(QStringLiteral("DesktopUp"));

    const bool selfCheck = QCoreApplication::arguments().contains(QStringLiteral("--self-check"));

    QQmlApplicationEngine engine;
    engine.rootContext()->setContextProperty(QStringLiteral("entrySelfCheck"), selfCheck);

    bool objectCreationFailed = false;
    QObject::connect(
        &engine,
        &QQmlApplicationEngine::objectCreationFailed,
        &application,
        [&objectCreationFailed]() { objectCreationFailed = true; },
        Qt::DirectConnection);

    engine.loadFromModule(QStringLiteral("DesktopUp.TSC01.Entry"), QStringLiteral("Main"));

    if (objectCreationFailed || engine.rootObjects().isEmpty()) {
        return 20;
    }

    const QObject* root = engine.rootObjects().constFirst();
    const QString marker = root->property("entryMarker").toString();
    if (marker != QString::fromLatin1(kEntryMarker)) {
        return 21;
    }

    if (selfCheck) {
        const QJsonObject record {
            {QStringLiteral("schema"), QStringLiteral("desktopup.ts-c01.entry-self-check.v1")},
            {QStringLiteral("candidateId"), QStringLiteral("TS-C01")},
            {QStringLiteral("framework"), QStringLiteral("Qt Quick")},
            {QStringLiteral("qtVersion"), QString::fromLatin1(qVersion())},
            {QStringLiteral("qmlRootMarker"), marker},
            {QStringLiteral("result"), QStringLiteral("PASS")},
            {QStringLiteral("scope"), QStringLiteral("R0_ENTRY_ONLY_P0_NOT_RUN")}
        };
        qInfo().noquote() << QJsonDocument(record).toJson(QJsonDocument::Compact);
        return 0;
    }

    return application.exec();
}
