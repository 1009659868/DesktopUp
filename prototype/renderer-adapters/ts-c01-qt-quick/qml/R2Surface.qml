import QtQuick
import QtQuick.Window

Window {
    id: root

    readonly property string r2SurfaceMarker: "desktopup.ts-c01.qt-quick.r2-surface.v1"
    property string presentationMode: "EXPANDED"
    property string interactionMode: "USING"
    property bool isLocked: false
    property int fixtureRevision: 0
    property string hitScenario: "FINAL"

    readonly property bool expanded: presentationMode === "EXPANDED"
    readonly property bool editing: interactionMode === "EDITING"
    readonly property real scaleUnit: Math.max(0.78, Math.min(1.18, width / 420))
    readonly property real contentOpacity: expanded ? 1.0 : 0.0
    readonly property var cardInitials: ["AL", "GS", "IV", "LO", "SR", "HC"]
    readonly property var cardNames: [
        "AFTERLIGHT", "GHOST SIGNAL", "IRON VEIL",
        "LAST ORBIT", "SABLE RUN", "HOLLOW CROWN"
    ]
    readonly property var accents: [
        "#6E4AA5", "#3A7B8A", "#9B3E43",
        "#A47743", "#385FA2", "#39745F"
    ]

    width: 420
    height: 620
    visible: false
    color: "transparent"
    flags: Qt.FramelessWindowHint | Qt.WindowDoesNotAcceptFocus
    title: "DesktopUp TS-C01 R2 Night Ops Fixture"

    Rectangle {
        id: poster
        anchors.fill: parent
        anchors.margins: 1
        radius: 24 * root.scaleUnit
        color: "#E30A1017"
        border.width: root.editing ? 2 : 1
        border.color: root.editing ? "#A6D83B" : "#344653"
        clip: true

        Rectangle {
            anchors.fill: parent
            color: "transparent"
            opacity: 0.22
            gradient: Gradient {
                GradientStop { position: 0.0; color: "#245A788E" }
                GradientStop { position: 0.35; color: "#18111A23" }
                GradientStop { position: 1.0; color: "#30100C10" }
            }
        }

        Rectangle {
            width: poster.width * 0.78
            height: poster.height * 0.48
            x: poster.width * 0.38
            y: -poster.height * 0.18
            rotation: -13
            radius: width / 2
            color: "#153E7181"
        }

        Item {
            id: header
            x: 22 * root.scaleUnit
            y: 10 * root.scaleUnit
            width: poster.width - 44 * root.scaleUnit
            height: 48 * root.scaleUnit

            Text {
                anchors.left: parent.left
                anchors.verticalCenter: parent.verticalCenter
                text: "DESKTOP COLLECTION"
                color: "#84919D"
                font.pixelSize: 9 * root.scaleUnit
                font.letterSpacing: 1.8 * root.scaleUnit
                font.bold: true
            }

            Rectangle {
                x: 140 * root.scaleUnit
                anchors.verticalCenter: parent.verticalCenter
                width: Math.max(28, parent.width - 260 * root.scaleUnit)
                height: 1
                color: "#42C5D6"
                opacity: 0.75
            }

            Text {
                x: parent.width - 112 * root.scaleUnit
                anchors.verticalCenter: parent.verticalCenter
                text: "01"
                color: "#66D6E3"
                font.pixelSize: 9 * root.scaleUnit
                font.bold: true
            }

            Rectangle {
                x: parent.width - 78 * root.scaleUnit
                anchors.verticalCenter: parent.verticalCenter
                width: 32 * root.scaleUnit
                height: 32 * root.scaleUnit
                radius: width / 2
                color: "#101923"
                border.color: "#52616C"

                Text {
                    anchors.centerIn: parent
                    text: root.isLocked ? "●" : "○"
                    color: root.isLocked ? "#A6D83B" : "#B8C1C8"
                    font.pixelSize: 15 * root.scaleUnit
                    font.bold: true
                }
            }

            Rectangle {
                x: parent.width - 36 * root.scaleUnit
                anchors.verticalCenter: parent.verticalCenter
                width: 32 * root.scaleUnit
                height: 32 * root.scaleUnit
                radius: width / 2
                color: "#101923"
                border.color: "#52616C"

                Text {
                    anchors.centerIn: parent
                    text: root.expanded ? "⌄" : "⌃"
                    color: "#C5CDD3"
                    font.pixelSize: 19 * root.scaleUnit
                }
            }
        }

        Item {
            id: content
            x: 28 * root.scaleUnit
            y: 78 * root.scaleUnit
            width: poster.width - 56 * root.scaleUnit
            height: Math.max(0, poster.height - 118 * root.scaleUnit)
            visible: root.expanded || opacity > 0
            opacity: root.contentOpacity

            Behavior on opacity {
                NumberAnimation {
                    duration: root.expanded ? 150 : 0
                    easing.type: Easing.OutCubic
                }
            }

            Text {
                x: 0
                y: 0
                text: "TACTICAL ARCHIVE / 01"
                color: "#72D6DF"
                font.pixelSize: 9 * root.scaleUnit
                font.letterSpacing: 1.4 * root.scaleUnit
                font.bold: true
            }

            Text {
                x: 0
                y: 23 * root.scaleUnit
                text: "NIGHT OPS"
                color: "#F5F7F8"
                font.pixelSize: 38 * root.scaleUnit
                font.bold: true
                font.family: "Bahnschrift Condensed"
            }

            Text {
                x: 1 * root.scaleUnit
                y: 72 * root.scaleUnit
                text: "06 TITLES  ·  READY"
                color: "#6F7B84"
                font.pixelSize: 8 * root.scaleUnit
                font.letterSpacing: 1.5 * root.scaleUnit
                font.bold: true
            }

            Repeater {
                model: 6

                delegate: Item {
                    required property int index
                    readonly property real gap: 12 * root.scaleUnit
                    readonly property real cardWidth: (content.width - gap * 2) / 3
                    readonly property real cardHeight: Math.max(66 * root.scaleUnit,
                                                               Math.min(92 * root.scaleUnit,
                                                                        (poster.height - 310 * root.scaleUnit) / 2))
                    x: (index % 3) * (cardWidth + gap)
                    y: 105 * root.scaleUnit + Math.floor(index / 3) * (cardHeight + 39 * root.scaleUnit)
                    width: cardWidth
                    height: cardHeight + 25 * root.scaleUnit

                    Rectangle {
                        width: parent.width
                        height: parent.cardHeight
                        radius: 8 * root.scaleUnit
                        color: "#D4101822"
                        border.width: 1
                        border.color: "#344451"
                        clip: true

                        Rectangle {
                            width: 24 * root.scaleUnit
                            height: parent.height * 1.7
                            x: parent.width * 0.58
                            y: -parent.height * 0.34
                            rotation: 29
                            color: root.accents[index]
                            opacity: 0.78
                        }

                        Rectangle {
                            anchors.fill: parent
                            color: "transparent"
                            gradient: Gradient {
                                GradientStop { position: 0.0; color: "#1D2C3A" }
                                GradientStop { position: 1.0; color: "#071018" }
                            }
                            opacity: 0.56
                        }

                        Text {
                            anchors.centerIn: parent
                            text: root.cardInitials[index]
                            color: "#F4F6F7"
                            font.pixelSize: 22 * root.scaleUnit
                            font.bold: true
                        }

                        Text {
                            anchors.right: parent.right
                            anchors.bottom: parent.bottom
                            anchors.margins: 6 * root.scaleUnit
                            text: "0" + (index + 1)
                            color: "#7C8993"
                            font.pixelSize: 7 * root.scaleUnit
                            font.bold: true
                        }
                    }

                    Text {
                        anchors.left: parent.left
                        anchors.top: parent.top
                        anchors.topMargin: parent.cardHeight + 8 * root.scaleUnit
                        width: parent.width
                        text: root.cardNames[index]
                        elide: Text.ElideRight
                        color: "#A7AFB5"
                        font.pixelSize: 8 * root.scaleUnit
                        font.letterSpacing: 0.35 * root.scaleUnit
                        font.bold: true
                    }
                }
            }
        }

        Rectangle {
            anchors.left: parent.left
            anchors.right: parent.right
            anchors.bottom: parent.bottom
            height: root.expanded ? 46 * root.scaleUnit : 0
            visible: root.expanded
            color: "transparent"
            border.width: 0

            Text {
                anchors.left: parent.left
                anchors.leftMargin: 28 * root.scaleUnit
                anchors.verticalCenter: parent.verticalCenter
                text: root.editing ? "EDITING · AUTO GRID" : "LOCAL LIBRARY"
                color: root.editing ? "#A6D83B" : "#69757E"
                font.pixelSize: 8 * root.scaleUnit
                font.letterSpacing: 1.1 * root.scaleUnit
                font.bold: true
            }

            Text {
                anchors.right: parent.right
                anchors.rightMargin: 28 * root.scaleUnit
                anchors.verticalCenter: parent.verticalCenter
                text: root.isLocked ? "LOCKED" : "DRAG TO MOVE"
                color: root.isLocked ? "#A6D83B" : "#67737B"
                font.pixelSize: 8 * root.scaleUnit
                font.letterSpacing: 1.1 * root.scaleUnit
                font.bold: true
            }
        }

        Rectangle {
            anchors.fill: parent
            anchors.margins: 6
            radius: poster.radius - 5
            color: "transparent"
            border.width: 1
            border.color: "#42C5D6"
            opacity: root.hitScenario === "FINAL" ? 0.0 : 0.22
        }

        Rectangle {
            anchors.right: parent.right
            anchors.bottom: parent.bottom
            width: 18
            height: 18
            color: "#A6D83B"
            opacity: root.editing && !root.isLocked && root.expanded ? 0.9 : 0.0
        }
    }
}
