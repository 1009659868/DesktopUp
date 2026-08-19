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
#include <QTimer>
#include <QRegularExpression>
#include <QStringList>
#include <atomic>
#include <cstdio>

#include "drageventsink.h"

static QQuickWindow *gWindow = nullptr;
static QQmlApplicationEngine *gEngine = nullptr;
static std::atomic<quint64> gFrames{0};

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
    } else if (name == "hello_ack") {
        // 配置注入:未收 ack 前窗口不显示;几何/参数以核心下发为准
        const QJsonObject geo = cmd.value("geometry").toObject();
        const int x = geo.value("x").toInt();
        const int y = geo.value("y").toInt();
        const int w = geo.value("w").toInt();
        const int h = geo.value("h").toInt();
        const QJsonObject params = cmd.value("params").toObject();
        const QString accent = params.value("accent").toString(QStringLiteral("#72e4ff"));
        const double opacity = params.value("opacity").toDouble(0.82);
        QMetaObject::invokeMethod(gWindow, [=]() {
            QObject *root = gEngine->rootObjects().value(0);
            if (root) {
                root->setProperty("accentColor", accent);
                root->setProperty("posterOpacity", opacity);
            }
            gWindow->setGeometry(x, y, w, h);
            gWindow->show();
        }, Qt::BlockingQueuedConnection);
        reply["ok"] = true;
    } else if (name == "set_geometry") {
        const int x = cmd.value("x").toInt();
        const int y = cmd.value("y").toInt();
        const int w = cmd.value("w").toInt();
        const int h = cmd.value("h").toInt();
        QMetaObject::invokeMethod(gWindow, [=]() {
            gWindow->setGeometry(x, y, w, h);
        }, Qt::BlockingQueuedConnection);
        reply["ok"] = true;
        reply["geometry"] = gWindow ? QString("%1,%2,%3,%4")
                                          .arg(gWindow->x()).arg(gWindow->y())
                                          .arg(gWindow->width()).arg(gWindow->height())
                                    : QString();
    } else if (name == "set_visible") {
        const bool visible = cmd.value("visible").toBool();
        QMetaObject::invokeMethod(gWindow, [=]() {
            if (visible) gWindow->show(); else gWindow->hide();
        }, Qt::BlockingQueuedConnection);
        reply["ok"] = true;
        reply["visible"] = gWindow ? gWindow->isVisible() : false;
    } else if (name == "set_params") {
        const QString accent = cmd.value("accent").toString();
        const double opacity = cmd.value("opacity").toDouble(-1.0);
        // 参数校验:颜色 #RRGGBB;透明度 [0.2, 1.0];非法值拒绝
        const QRegularExpression colorRe(QStringLiteral("^#[0-9a-fA-F]{6}$"));
        if (!colorRe.match(accent).hasMatch() || opacity < 0.2 || opacity > 1.0) {
            reply["ok"] = false;
            reply["error"] = "invalid_params";
        } else {
            QMetaObject::invokeMethod(gWindow, [=]() {
                QObject *root = gEngine->rootObjects().value(0);
                if (root) {
                    root->setProperty("accentColor", accent);
                    root->setProperty("posterOpacity", opacity);
                }
            }, Qt::BlockingQueuedConnection);
            reply["ok"] = true;
        }
    } else if (name == "get_health") {
        reply["ok"] = true;
        reply["exposed"] = gWindow ? gWindow->isExposed() : false;
        reply["visible"] = gWindow ? gWindow->isVisible() : false;
        reply["frames"] = static_cast<double>(gFrames.load());
        reply["geometry"] = gWindow ? QString("%1,%2,%3,%4")
                                           .arg(gWindow->x()).arg(gWindow->y())
                                           .arg(gWindow->width()).arg(gWindow->height())
                                    : QString();
    } else if (name == "capture") {
        // 呈现自检:requestUpdate 强制产帧后,延迟到事件循环干净上下文抓取。
        // 教训:绝不在 frameSwapped 信号处理器内调 grabWindow(渲染线程敏感态,
        // 与显隐/几何搅动叠加会死锁/崩溃:0xC0000374/0xC0000005 已复现)。
        QMetaObject::invokeMethod(gWindow, [id = cmd.value("id")]() {
            QQuickWindow *win = gWindow;
            if (!win) {
                QJsonObject r;
                r["id"] = id;
                r["ok"] = false;
                r["error"] = "no window";
                sendFrame(r);
                return;
            }
            win->requestUpdate();
            auto replied = std::make_shared<bool>(false);
            QTimer::singleShot(30, win, [win, id, replied]() {
                if (*replied) return;
                *replied = true;
                // 未呈现 = 自检失败:隐藏/未曝光时 grabWindow 会返回陈旧缓冲,不能当作呈现证据
                if (!win->isVisible()) {
                    QJsonObject r;
                    r["id"] = id;
                    r["ok"] = true;
                    r["size"] = "hidden";
                    r["magenta"] = 0.0;
                    sendFrame(r);
                    return;
                }
                QImage img = win->grabWindow();
                const QString path = "G:\\A_\\Tools\\DesktopUp_V2\\prototype\\qt-quick\\capture.png";
                const bool saved = img.save(path);
                // 呈现自检:全帧扫描品红健康标记(#ff00ff),计数作为帧内证据
                quint64 magenta = 0;
                for (int y = 0; y < img.height(); ++y) {
                    const QRgb *line = reinterpret_cast<const QRgb *>(img.constScanLine(y));
                    for (int x = 0; x < img.width(); ++x) {
                        if (qRed(line[x]) == 255 && qGreen(line[x]) == 0
                            && qBlue(line[x]) == 255) {
                            ++magenta;
                        }
                    }
                }
                QJsonObject r;
                r["id"] = id;
                r["ok"] = saved;
                r["size"] = QString("%1x%2").arg(img.width()).arg(img.height());
                r["magenta"] = static_cast<double>(magenta);
                sendFrame(r);
            });
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

// 用户拖动结束 → geometry_changed 事件(状态权威在核心,渲染端只上报)
void DragEventSink::onDragEnded() {
    QQuickWindow *window = gWindow;
    if (!window)
        return;
    QJsonObject ev;
    ev["cmd"] = "geometry_changed";
    QJsonObject rect;
    rect["x"] = window->x();
    rect["y"] = window->y();
    rect["w"] = window->width();
    rect["h"] = window->height();
    ev["rect"] = rect;
    sendFrame(ev);
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
    // 配置注入:几何与显示由 hello_ack 决定,此处不 setGeometry 不 show

    // 0-FPS 证据:统计实际换帧次数(静态构图应趋近于零)
    QObject::connect(window, &QQuickWindow::frameSwapped, window, []() {
        gFrames.fetch_add(1, std::memory_order_relaxed);
    });

    // 用户拖动结束 → 几何变化事件(QML 信号经槽桥接)
    static DragEventSink sink;
    QObject::connect(window, SIGNAL(dragEnded()), &sink, SLOT(onDragEnded()));

    QJsonObject hello;
    hello["cmd"] = "hello";
    hello["hwnd"] = static_cast<double>(window->winId());
    QJsonObject rect;
    rect["x"] = window->x();
    rect["y"] = window->y();
    rect["w"] = window->width();
    rect["h"] = window->height();
    hello["rect"] = rect;
    // 命中区域(窗口坐标,语义化:id + actions 能力声明)
    QJsonArray regions;
    auto addRegion = [&regions](const QString &id, const QStringList &actions,
                                int x, int y, int w, int h) {
        QJsonArray arr;
        for (const QString &a : actions)
            arr.append(a);
        QJsonObject r;
        r["id"] = id;
        r["x"] = static_cast<double>(x);
        r["y"] = static_cast<double>(y);
        r["w"] = static_cast<double>(w);
        r["h"] = static_cast<double>(h);
        r["actions"] = arr;
        regions.append(r);
    };
    addRegion(QStringLiteral("titlebar"), {QStringLiteral("drag")}, 16, 16,
              window->width() - 32, 58);
    const int cols = 3, rows = 2, spacing = 12, cardH = 96, top = 168;
    const int cardW = (window->width() - 32 - spacing * (cols - 1)) / cols;
    for (int j = 0; j < rows; ++j) {
        for (int i = 0; i < cols; ++i) {
            addRegion(QStringLiteral("card:%1").arg(j * cols + i),
                      {QStringLiteral("launch")}, 16 + i * (cardW + spacing),
                      top + j * (cardH + spacing), cardW, cardH);
        }
    }
    hello["regions"] = regions;
    sendFrame(hello);

    QThread *reader = QThread::create([]() { readerLoop(); });
    reader->start();

    return app.exec();
}
