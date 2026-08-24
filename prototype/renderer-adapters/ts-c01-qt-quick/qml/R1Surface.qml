import QtQuick
import QtQuick.Window

Window {
    id: root

    readonly property string r1SurfaceMarker: "desktopup.ts-c01.qt-quick.r1-surface.v1"

    width: 420
    height: 620
    visible: false
    color: "transparent"
    flags: Qt.FramelessWindowHint | Qt.WindowDoesNotAcceptFocus
    title: "DesktopUp TS-C01 R1 Surface"

    Rectangle {
        anchors.fill: parent
        anchors.margins: 2
        radius: 26
        color: "#E5101923"
        border.color: "#667E91A4"
        border.width: 1

        Rectangle {
            x: 22
            y: 22
            width: parent.width - 44
            height: 2
            color: "#59D4E8"
            opacity: 0.75
        }

        Text {
            x: 28
            y: 50
            text: "DESKTOP COLLECTION  /  R1"
            color: "#89A4B8"
            font.pixelSize: 12
            font.letterSpacing: 2
            font.bold: true
        }

        Text {
            x: 28
            y: 105
            text: "NIGHT OPS"
            color: "#F5F7F9"
            font.pixelSize: 42
            font.bold: true
        }

        Text {
            x: 30
            y: 163
            text: "QT QUICK 6.8.3 · NATIVE CHILD"
            color: "#59D4E8"
            font.pixelSize: 12
            font.letterSpacing: 1
        }

        Repeater {
            model: 6
            delegate: Rectangle {
                required property int index
                x: 28 + (index % 3) * 122
                y: 215 + Math.floor(index / 3) * 112
                width: 106
                height: 76
                radius: 10
                color: index % 2 === 0 ? "#C1172635" : "#C113202C"
                border.color: "#34495B"

                Text {
                    anchors.centerIn: parent
                    text: ["AL", "GS", "IV", "LO", "SR", "HC"][index]
                    color: "#F4F7FA"
                    font.pixelSize: 22
                    font.bold: true
                }
            }
        }

        Text {
            anchors.left: parent.left
            anchors.leftMargin: 28
            anchors.bottom: parent.bottom
            anchors.bottomMargin: 25
            text: "R1 ADAPTER BOUNDARY · ZERO HIT REGIONS"
            color: "#6D8293"
            font.pixelSize: 10
            font.letterSpacing: 1
        }
    }
}
