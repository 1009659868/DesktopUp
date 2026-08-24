import QtQuick
import QtQuick.Window

Window {
    id: root

    readonly property string entryMarker: "desktopup.ts-c01.qt-quick.entry.v1"

    width: 640
    height: 420
    visible: !entrySelfCheck
    color: "#101923"
    title: "TS-C01 Qt Quick R0 Entry Preflight"

    Rectangle {
        anchors.fill: parent
        anchors.margins: 18
        radius: 24
        color: "#D914202C"
        border.color: "#66D9F4"
        border.width: 1

        Column {
            anchors.centerIn: parent
            spacing: 12

            Text {
                anchors.horizontalCenter: parent.horizontalCenter
                text: "NIGHT OPS"
                color: "#F4F7FA"
                font.pixelSize: 48
                font.bold: true
            }

            Text {
                anchors.horizontalCenter: parent.horizontalCenter
                text: "TS-C01 · QT QUICK · R0 ENTRY ONLY"
                color: "#66D9F4"
                font.pixelSize: 16
                font.letterSpacing: 2
            }

            Text {
                anchors.horizontalCenter: parent.horizontalCenter
                text: "P0 NOT RUN"
                color: "#A7B3BE"
                font.pixelSize: 14
            }
        }
    }
}
