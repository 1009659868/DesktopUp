// DesktopUp V2 — C-QTP 渲染侧车(C++/Qt Quick)
// 职责:透明圆角海报窗口、IPC 客户端、几何/参数命令、呈现健康上报。
#include <QGuiApplication>
#include <QQmlApplicationEngine>
#include <QQmlContext>
#include <QQuickWindow>
#include <QTimer>
#include <QThread>
#include <QJsonDocument>
#include <QJsonObject>
#include <windows.h>

static QQuickWindow *gWindow = nullptr;
static QQmlApplicationEngine *gEngine = nullptr;

// ---- named pipe client ----
struct PipeClient {
    HANDLE handle = INVALID_HANDLE_VALUE;

    bool connect(const QString &name) {
        handle = CreateFileW(reinterpret_cast<LPCWSTR>(name.utf16()), GENERIC_READ | GENERIC_WRITE,
                             0, nullptr, OPEN_EXISTING, 0, nullptr);
        return handle != INVALID_HANDLE_VALUE;
    }

    bool send(const QJsonObject &obj) {
        if (handle == INVALID_HANDLE_VALUE) return false;
        QByteArray data = QJsonDocument(obj).toJson(QJsonDocument::Compact);
        quint32 len = static_cast<quint32>(data.size());
        QByteArray frame;
        frame.append(reinterpret_cast<const char *>(&len), 4);
        frame.append(data);
        DWORD written = 0;
        return WriteFile(handle, frame.constData(), static_cast<DWORD>(frame.size()), &written, nullptr)
               && written == static_cast<DWORD>(frame.size());
    }

    void closePipe() {
        if (handle != INVALID_HANDLE_VALUE) { CloseHandle(handle); handle = INVALID_HANDLE_VALUE; }
    }
};

static void handleCommand(PipeClient &pipe, const QJsonObject &cmd) {
    const QString name = cmd.value("cmd").toString();
    QJsonObject reply;
    reply["id"] = cmd.value("id");

    if (name == "ping") {
        reply["ok"] = true;
    } else if (name == "set_geometry") {
        const int x = cmd.value("x").toInt();
        const int y = cmd.value("y").toInt();
        const int w = cmd.value("w").toInt();
        const int h = cmd.value("h").toInt();
        QMetaObject::invokeMethod(gWindow, [=]() {
            gWindow->setGeometry(x, y, w, h);
        }, Qt::QueuedConnection);
        reply["ok"] = true;
    } else if (name == "set_visible") {
        const bool visible = cmd.value("visible").toBool();
        QMetaObject::invokeMethod(gWindow, [=]() {
            if (visible) gWindow->show(); else gWindow->hide();
        }, Qt::QueuedConnection);
        reply["ok"] = true;
    } else if (name == "set_params") {
        const QString accent = cmd.value("accent").toString();
        const double opacity = cmd.value("opacity").toDouble(0.8);
        QMetaObject::invokeMethod(gWindow, [=]() {
            QObject *root = gEngine->rootObjects().value(0);
            if (root) {
                root->setProperty("accentColor", accent);
                root->setProperty("posterOpacity", opacity);
            }
        }, Qt::QueuedConnection);
        reply["ok"] = true;
    } else if (name == "get_health") {
        reply["ok"] = true;
        reply["exposed"] = gWindow ? gWindow->isExposed() : false;
        reply["visible"] = gWindow ? gWindow->isVisible() : false;
        reply["geometry"] = gWindow ? QString("%1,%2,%3,%4")
                                           .arg(gWindow->x()).arg(gWindow->y())
                                           .arg(gWindow->width()).arg(gWindow->height())
                                    : QString();
    } else if (name == "shutdown") {
        reply["ok"] = true;
        pipe.send(reply);
        QMetaObject::invokeMethod(qApp, "quit", Qt::QueuedConnection);
        return;
    } else {
        reply["ok"] = false;
        reply["error"] = QString("unknown cmd: %1").arg(name);
    }
    pipe.send(reply);
}

static void readerLoop(PipeClient &pipe) {
    QByteArray buffer;
    while (pipe.handle != INVALID_HANDLE_VALUE) {
        char chunk[8192];
        DWORD read = 0;
        if (!ReadFile(pipe.handle, chunk, sizeof(chunk), &read, nullptr) || read == 0) {
            if (GetLastError() == ERROR_BROKEN_PIPE) { pipe.closePipe(); break; }
            continue;
        }
        buffer.append(chunk, static_cast<int>(read));
        while (buffer.size() >= 4) {
            quint32 len = 0;
            memcpy(&len, buffer.constData(), 4);
            if (buffer.size() < static_cast<int>(4 + len)) break;
            const QByteArray payload = buffer.mid(4, static_cast<int>(len));
            buffer.remove(0, static_cast<int>(4 + len));
            QJsonParseError err{};
            const QJsonDocument doc = QJsonDocument::fromJson(payload, &err);
            if (err.error == QJsonParseError::NoError && doc.isObject()) {
                handleCommand(pipe, doc.object());
            }
        }
    }
}

int main(int argc, char *argv[]) {
    QGuiApplication app(argc, argv);
    const QString pipeName = argc > 1 ? QString::fromLocal8Bit(argv[1])
                                      : QStringLiteral("\\\\.\\pipe\\desktopup-qtp");

    qputenv("QT_QUICK_BACKEND", "software"); // deterministic for the prototype; revisit later
    qputenv("QSG_RENDER_LOOP", "basic");

    QQmlApplicationEngine engine;
    gEngine = &engine;
    engine.load(QUrl(QStringLiteral("qrc:/qt/qml/PosterRenderer/qml/Main.qml")));
    if (engine.rootObjects().isEmpty())
        return -1;

    QQuickWindow *window = qobject_cast<QQuickWindow *>(engine.rootObjects().value(0));
    if (!window) return -1;
    gWindow = window;

    window->setFlags(Qt::FramelessWindowHint | Qt::Tool | Qt::WindowDoesNotAcceptFocus);
    window->setColor(QColor(Qt::transparent));
    window->setOpacity(1.0);
    window->setDefaultAlphaBuffer(true);
    window->setGeometry(1700, 200, 450, 703);
    window->show();

    PipeClient pipe;
    if (!pipe.connect(pipeName)) {
        // fallback: still show the window so visuals can be verified without the core
        qWarning("renderer: could not connect pipe %s; running standalone", qPrintable(pipeName));
    } else {
        QThread *reader = QThread::create([&pipe]() { readerLoop(pipe); });
        reader->start();
        QObject::connect(&app, &QCoreApplication::aboutToQuit, [&pipe, reader]() {
            pipe.closePipe();
            if (reader->isRunning()) { reader->quit(); reader->wait(2000); }
        });
    }

    return app.exec();
}
