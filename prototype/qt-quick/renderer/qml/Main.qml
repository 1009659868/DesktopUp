import QtQuick
import QtQuick.Window

Window {
    id: posterWindow
    width: 450
    height: 703
    visible: true

    // 用户拖动结束信号(C++ 侧监听 → geometry_changed 事件上报核心)
    signal dragEnded()

    // L0/L1 参数演示(由 IPC set_params 驱动;预设即修改这两个值)
    property color accentColor: "#72e4ff"
    property real posterOpacity: 0.82

    color: "transparent"

    Rectangle {
        id: poster
        anchors.fill: parent
        anchors.margins: 12
        radius: 18
        color: Qt.rgba(8, 12, 18, posterOpacity)
        border.color: Qt.rgba(accentColor.r, accentColor.g, accentColor.b, 0.25)
        border.width: 1

        // 自制标题栏(参考构图:集合标识 + 锁定/收起按钮)+ 原生拖动
        Item {
            id: titlebar
            anchors.left: parent.left
            anchors.right: parent.right
            anchors.top: parent.top
            anchors.margins: 16
            height: 58

            MouseArea {
                anchors.fill: parent
                property real pressX: 0
                property real pressY: 0
                property bool moved: false
                onPressed: { pressX = mouse.x; pressY = mouse.y; moved = false }
                onPositionChanged: {
                    if (!moved && (Math.abs(mouse.x - pressX) > 5 || Math.abs(mouse.y - pressY) > 5)) {
                        moved = true
                        posterWindow.startSystemMove()
                    }
                }
                onReleased: { if (moved) posterWindow.dragEnded() }
            }

            Column {
                anchors.fill: parent
                spacing: 6
                Row {
                    width: parent.width
                    Text {
                        text: "DESKTOP COLLECTION — 01"
                        color: Qt.rgba(1, 1, 1, 0.55)
                        font.pixelSize: 11
                        font.letterSpacing: 2
                    }
                    Item { width: 10; height: 1 }
                    Rectangle { width: 26; height: 26; radius: 6; color: Qt.rgba(1, 1, 1, 0.06)
                        Text { anchors.centerIn: parent; text: "锁"; color: "white"; font.pixelSize: 12 } }
                    Rectangle { width: 26; height: 26; radius: 6; color: Qt.rgba(1, 1, 1, 0.06)
                        Text { anchors.centerIn: parent; text: "收"; color: "white"; font.pixelSize: 12 } }
                }

                Text {
                    text: "TACTICAL ARCHIVE / 01"
                    color: accentColor
                    font.pixelSize: 11
                    font.letterSpacing: 3
                }
                Text {
                    text: "NIGHT OPS"
                    color: "white"
                    font.pixelSize: 28
                    font.weight: Font.Bold
                }
                Text {
                    text: "06 TITLES · READY"
                    color: Qt.rgba(1, 1, 1, 0.45)
                    font.pixelSize: 11
                }
            }
        }

        // 六张占位卡片(参考构图 3×2)
        Grid {
            id: cardGrid
            anchors.left: parent.left
            anchors.right: parent.right
            anchors.top: parent.top
            anchors.topMargin: 168
            anchors.margins: 16
            columns: 3
            rows: 2
            spacing: 12

            Repeater {
                model: ["AL", "GS", "IV", "LO", "SR", "HC"]
                Rectangle {
                    required property string modelData
                    width: (cardGrid.width - cardGrid.spacing * 2) / 3
                    height: 96
                    radius: 10
                    color: Qt.rgba(16, 24, 36, 0.9)
                    border.color: Qt.rgba(accentColor.r, accentColor.g, accentColor.b, 0.12)
                    border.width: 1

                    // 主题色斜带(参考构图装饰)
                    Rectangle {
                        width: 6
                        anchors.left: parent.left
                        anchors.top: parent.top
                        anchors.bottom: parent.bottom
                        color: accentColor
                        opacity: 0.35
                    }
                    Text {
                        anchors.centerIn: parent
                        text: modelData
                        color: "white"
                        font.pixelSize: 22
                        font.weight: Font.DemiBold
                        font.letterSpacing: 2
                    }
                }
            }
        }

        // 底部状态栏
        Row {
            anchors.left: parent.left
            anchors.right: parent.right
            anchors.bottom: parent.bottom
            anchors.margins: 14
            Text { text: "LOCAL LIBRARY"; color: Qt.rgba(1, 1, 1, 0.35); font.pixelSize: 10; font.letterSpacing: 2 }
            Item { width: 8; height: 1 }
            Text { text: "DRAG TO MOVE"; color: Qt.rgba(1, 1, 1, 0.22); font.pixelSize: 10 }
        }

        // 呈现健康标记:固定纯色块,核心桩对屏幕采样比对(RP-INV-11)
        Rectangle {
            id: healthMarker
            anchors.right: parent.right
            anchors.bottom: parent.bottom
            anchors.margins: 14
            width: 16
            height: 16
            color: "#ff00ff"
        }
    }
}
