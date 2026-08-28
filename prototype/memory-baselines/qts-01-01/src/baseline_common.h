#pragma once

#include <QCoreApplication>
#include <QDateTime>
#include <QFile>
#include <QJsonDocument>
#include <QJsonObject>
#include <QString>
#include <QStringList>

#include <cstdio>
#include <utility>

namespace desktopup::qts0101 {

struct Options {
    int durationMs = 15000;
    QString readyMarkerPath;
    QString logPath;
};

inline bool parseOptions(const QStringList& arguments, Options* options)
{
    if (options == nullptr) return false;
    for (int index = 1; index < arguments.size(); ++index) {
        const QString argument = arguments.at(index);
        if (argument == QStringLiteral("--duration-ms") && index + 1 < arguments.size()) {
            bool valid = false;
            const int value = arguments.at(++index).toInt(&valid);
            if (!valid || value < 1000 || value > 120000) return false;
            options->durationMs = value;
        } else if (argument == QStringLiteral("--ready-marker") && index + 1 < arguments.size()) {
            options->readyMarkerPath = arguments.at(++index);
        } else if (argument == QStringLiteral("--log") && index + 1 < arguments.size()) {
            options->logPath = arguments.at(++index);
        } else {
            return false;
        }
    }
    return !options->readyMarkerPath.isEmpty() && !options->logPath.isEmpty();
}

class EventLog {
public:
    EventLog(QString role, const QString& path)
        : role_(std::move(role)), file_(path)
    {
    }

    bool open()
    {
        return file_.open(QIODevice::WriteOnly | QIODevice::Truncate);
    }

    bool write(QString event, QJsonObject fields = {})
    {
        fields.insert(QStringLiteral("schema"),
                      QStringLiteral("desktopup.qts-01-01.baseline-event.v1"));
        fields.insert(QStringLiteral("role"), role_);
        fields.insert(QStringLiteral("event"), std::move(event));
        fields.insert(QStringLiteral("utc"),
                      QDateTime::currentDateTimeUtc().toString(Qt::ISODateWithMs));
        const QByteArray line = QJsonDocument(fields).toJson(QJsonDocument::Compact) + '\n';
        return file_.write(line) == line.size() && file_.flush();
    }

private:
    QString role_;
    QFile file_;
};

inline bool publishReadyMarker(const QString& path, const QString& role)
{
    QFile marker(path);
    if (!marker.open(QIODevice::WriteOnly | QIODevice::Truncate)) return false;
    const QByteArray payload = role.toUtf8() + QByteArrayLiteral("\n");
    return marker.write(payload) == payload.size() && marker.flush();
}

inline int usageError(const char* executable)
{
    fprintf(stderr,
            "Usage: %s --duration-ms 1000..120000 --ready-marker PATH --log PATH\n",
            executable);
    return 2;
}

}  // namespace desktopup::qts0101
