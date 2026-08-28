import QtQuick
import QtQuick.Controls

ApplicationWindow {
    id: window
    width: 720
    height: 480
    visible: true
    title: qsTr("DesktopUp V2 — QTS-03 Control Center")
    color: "#0b1118"

    Column {
        anchors.centerIn: parent
        spacing: 12

        Label {
            text: qsTr("CONTROL CENTER ROLE")
            color: "#76dff2"
            font.pixelSize: 13
            font.letterSpacing: 2
        }
        Label {
            text: qsTr("QTS-03 production skeleton")
            color: "#f2f5f7"
            font.pixelSize: 28
        }
        Label {
            text: qsTr("Runtime owns state, desktop host and renderer.\nThis process owns drafts and exits when this window closes.")
            color: "#9aa8b4"
            horizontalAlignment: Text.AlignHCenter
        }
    }
}
