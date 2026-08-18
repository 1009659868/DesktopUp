// DesktopUp V2 — C-QTP 渲染侧车(C++/Qt Quick)
// 职责:透明圆角海报窗口、stdio IPC(stdin 下行命令 / stdout 上行回复)、几何/参数命令、呈现健康上报。
// 传输:4 字节 LE 长度前缀 + UTF-8 JSON。
#include <windows.h>

#include <QGuiApplication>
#include <QQmlApplicationEngine>
#include <QQuickWindow>
#include <QThread>
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonArray>
#include <QImage>
#include <cstdio>

static QQuickWindow *gWindow = nullptr;
static QQmlApplicationEngine *gEngine = nullptr;

static void rlog(const char *fmt, ...) {
    static FILE *file = nullptr;
    if (!file) file = fopen("G:\\A_\\Tools\\DesktopUp_V2\\prototype\\qt-quick\\renderer.log", "a");
    if (!file) return;
    va_list args;
    va_start(args, fmt);
    vfprintf(file, fmt, args);
    va_end(args);
    fprintf(file, "\n");
    fflush(file);
}

// ---- stdio 帧传输 ----
static HANDLE gStdin = INVALID_HANDLE_VALUE;
static HANDLE gStdout = INVALID_HANDLE_VALUE;

static bool sendFrame(const QJsonObject &obj) {
    if (gStdout == INVALID_HANDLE_VALUE) return false;
    QByteArray data = QJsonDocument(obj).toJson(QJsonDocument::Compact);
    quint32 len = static_cast<quint32>(data.size());
    QByteArray frame;
    frame.append(reinterpret_cast<const char *>(&len), 4);
    frame.append(data);
    DWORD written = 0;
    BOOL ok = WriteFile(gStdout, frame.constData(), static_cast<DWORD>(frame.size()), &written, nullptr);
    rlog("sendFrame ok=%d written=%lu", int(ok), written);
    return ok && written == static_cast<DWORD>(frame.size());
}

static void handleCommand(const QJsonObject &cmd) {
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
    } else if (name == "capture") {
        // 异步:在 GUI 线程抓取窗口自绘帧并保存 PNG(呈现自检素材)
        QMetaObject::invokeMethod(gWindow, [id = cmd.value("id")]() {
            QImage img = gWindow->grabWindow();
            const QString path = "G:\\A_\\Tools\\DesktopUp_V2\\prototype\\qt-quick\\capture.png";
            const bool saved = img.save(path);
            QJsonObject r;
            r["id"] = id;
            r["ok"] = saved;
            r["size"] = QString("%1x%2").arg(img.width()).arg(img.height());
            sendFrame(r);
        }, Qt::QueuedConnection);
        return;
    } else if (name == "shutdown") {
        reply["ok"] = true;
        sendFrame(reply);
        QMetaObject::invokeMethod(qApp, "quit", Qt::QueuedConnection);
        return;
    } else {
        reply["ok"] = false;
        reply["error"] = QString("unknown cmd: %1").arg(name);
    }
    sendFrame(reply);
}

static void readerLoop() {
    QByteArray buffer;
    rlog("readerLoop started on stdin");
    while (gStdin != INVALID_HANDLE_VALUE) {
        char chunk[8192];
        DWORD read = 0;
        if (!ReadFile(gStdin, chunk, sizeof(chunk), &read, nullptr) || read == 0) {
            rlog("stdin closed, err=%lu", GetLastError());
            break;
        }
        buffer.append(chunk, static_cast<int>(read));
        while (buffer.size() >= 4) {
            quint32 len = 0;
            memcpy(&len, buffer.constData(), 4);
            if (buffer.size() < static_cast<int>(4 + len)) break;
            const QByteArray payload = buffer.mid(4, static_cast<int>(len));
            buffer.remove(0, static_cast<int>(4 + len));
            QJsonParseError parseErr{};
            const QJsonDocument doc = QJsonDocument::fromJson(payload, &parseErr);
            if (parseErr.error == QJsonParseError::NoError && doc.isObject()) {
                rlog("cmd: %s", qPrintable(doc.object().value("cmd").toString()));
                handleCommand(doc.object());
            } else {
                rlog("bad frame, parse err=%d", int(parseErr.error));
            }
        }
    }
    rlog("readerLoop ended; quitting");
    QMetaObject::invokeMethod(qApp, "quit", Qt::QueuedConnection);
}

int main(int argc, char *argv[]) {
    QGuiApplication app(argc, argv);

    gStdin = GetStdHandle(STD_INPUT_HANDLE);
    gStdout = GetStdHandle(STD_OUTPUT_HANDLE);

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
    window->setDefaultAlphaBuffer(true);
    window->setGeometry(1700, 200, 450, 703);
    window->show();

    QJsonObject hello;
    hello["cmd"] = "hello";
    hello["hwnd"] = static_cast<double>(window->winId());
    QJsonObject rect;
    rect["x"] = window->x();
    rect["y"] = window->y();
    rect["w"] = window->width();
    rect["h"] = window->height();
    hello["rect"] = rect;
    // 命中区域(窗口坐标):标题栏 + 3×2 卡片
    QJsonArray regions;
    QJsonObject titlebar;
    titlebar["x"] = 16.0; titlebar["y"] = 16.0;
    titlebar["w"] = static_cast<double>(window->width() - 32); titlebar["h"] = 58.0;
    regions.append(titlebar);
    const int cols = 3, rows = 2, spacing = 12, cardH = 96, top = 168;
    const int cardW = (window->width() - 32 - spacing * (cols - 1)) / cols;
    for (int j = 0; j < rows; ++j) {
        for (int i = 0; i < cols; ++i) {
            QJsonObject card;
            card["x"] = static_cast<double>(16 + i * (cardW + spacing));
            card["y"] = static_cast<double>(top + j * (cardH + spacing));
            card["w"] = static_cast<double>(cardW);
            card["h"] = static_cast<double>(cardH);
            regions.append(card);
        }
    }
    hello["regions"] = regions;
    sendFrame(hello);

    QThread *reader = QThread::create([]() { readerLoop(); });
    reader->start();

    return app.exec();
}
