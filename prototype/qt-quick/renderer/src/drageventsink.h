#pragma once
// QML dragEnded 信号 → C++ 侧几何事件上报的桥接槽(AUTOMOC 处理)
#include <QObject>

class DragEventSink : public QObject {
    Q_OBJECT
public slots:
    void onDragEnded();
};
