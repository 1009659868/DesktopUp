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
    property string themeId: "transparent-default"

    readonly property bool expanded: presentationMode === "EXPANDED"
    readonly property bool neonTheme: themeId === "neon-hud"
    readonly property bool cinemaTheme: themeId === "cinema-lightbox"
    readonly property bool minimalFallback: themeId === "minimal-fallback"
    readonly property color posterBaseColor: minimalFallback ? "#F21B1D21"
                                             : cinemaTheme ? "#E3302419"
                                             : neonTheme ? "#E30B0717"
                                             : "#E30A1017"
    readonly property color posterBorderColor: minimalFallback ? "#7B858C"
                                               : cinemaTheme ? "#B89256"
                                               : neonTheme ? "#B649E8"
                                               : "#344653"
    readonly property color editingColor: cinemaTheme ? "#FFC96B"
                                          : neonTheme ? "#4DFFDE"
                                          : "#A6D83B"
    readonly property color accentColor: minimalFallback ? "#C9D0D4"
                                         : cinemaTheme ? "#F2C879"
                                         : neonTheme ? "#43F4E5"
                                         : "#42C5D6"
    readonly property color primaryTextColor: minimalFallback ? "#F3F4F5"
                                              : cinemaTheme ? "#FFF3D7"
                                              : neonTheme ? "#FFF4FF"
                                              : "#F5F7F8"
    readonly property color secondaryTextColor: minimalFallback ? "#C2C8CC"
                                                : cinemaTheme ? "#D8C39B"
                                                : neonTheme ? "#C69CFF"
                                                : "#A7AFB5"
    readonly property color mutedTextColor: minimalFallback ? "#92999E"
                                            : cinemaTheme ? "#A9936B"
                                            : neonTheme ? "#8A7CAB"
                                            : "#6F7B84"
    readonly property color buttonColor: minimalFallback ? "#24282C"
                                         : cinemaTheme ? "#3A2D21"
                                         : neonTheme ? "#1A0D28"
                                         : "#101923"
    readonly property color cardColor: minimalFallback ? "#E5222529"
                                       : cinemaTheme ? "#E33B2B1D"
                                       : neonTheme ? "#E3120B22"
                                       : "#D4101822"
    readonly property color cardBorderColor: minimalFallback ? "#687177"
                                             : cinemaTheme ? "#9B7848"
                                             : neonTheme ? "#6C3A8D"
                                             : "#344451"
    readonly property color cardGradientStart: minimalFallback ? "#30363B"
                                               : cinemaTheme ? "#5A4228"
                                               : neonTheme ? "#311448"
                                               : "#1D2C3A"
    readonly property color cardGradientEnd: minimalFallback ? "#171A1D"
                                             : cinemaTheme ? "#1F1812"
                                             : neonTheme ? "#090612"
                                             : "#071018"
    readonly property color decorColor: cinemaTheme ? "#6A4A261D"
                                        : neonTheme ? "#3D8C1A65"
                                        : "#153E7181"
    readonly property string titleFontFamily: cinemaTheme ? "Georgia"
                                              : minimalFallback ? "Segoe UI"
                                              : "Bahnschrift Condensed"
    readonly property bool editing: interactionMode === "EDITING"
    readonly property real scaleUnit: Math.max(0.78, Math.min(1.18, width / 420))
    readonly property real contentOpacity: expanded ? 1.0 : 0.0
    readonly property var cardInitials: ["AL", "GS", "IV", "LO", "SR", "HC"]
    readonly property var cardNames: [
        "AFTERLIGHT", "GHOST SIGNAL", "IRON VEIL",
        "LAST ORBIT", "SABLE RUN", "HOLLOW CROWN"
    ]
    readonly property var accents: minimalFallback
        ? ["#8B949A", "#8B949A", "#8B949A", "#8B949A", "#8B949A", "#8B949A"]
        : cinemaTheme
            ? ["#C08B46", "#B99A63", "#9B613C", "#D0A65C", "#8F744B", "#B67A45"]
            : neonTheme
                ? ["#FF43D1", "#43F4E5", "#9D62FF", "#FFB13D", "#3C8CFF", "#57FF8B"]
                : ["#6E4AA5", "#3A7B8A", "#9B3E43", "#A47743", "#385FA2", "#39745F"]

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
        radius: (root.minimalFallback ? 14 : root.cinemaTheme ? 18 : 24) * root.scaleUnit
        color: root.posterBaseColor
        border.width: root.editing ? 2 : 1
        border.color: root.editing ? root.editingColor : root.posterBorderColor
        clip: true

        Rectangle {
            anchors.fill: parent
            color: "transparent"
            opacity: 0.22
            gradient: Gradient {
                GradientStop { position: 0.0; color: root.cinemaTheme ? "#504D2B18" : root.neonTheme ? "#403D0960" : "#245A788E" }
                GradientStop { position: 0.35; color: root.minimalFallback ? "#10121416" : "#18111A23" }
                GradientStop { position: 1.0; color: root.cinemaTheme ? "#302A160C" : root.neonTheme ? "#301C0828" : "#30100C10" }
            }
        }

        Rectangle {
            width: poster.width * 0.78
            height: poster.height * 0.48
            x: poster.width * 0.38
            y: -poster.height * 0.18
            rotation: -13
            radius: width / 2
            color: root.decorColor
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
                color: root.mutedTextColor
                font.pixelSize: 9 * root.scaleUnit
                font.letterSpacing: 1.8 * root.scaleUnit
                font.bold: true
            }

            Rectangle {
                x: 140 * root.scaleUnit
                anchors.verticalCenter: parent.verticalCenter
                width: Math.max(28, parent.width - 260 * root.scaleUnit)
                height: 1
                color: root.accentColor
                opacity: 0.75
            }

            Text {
                x: parent.width - 112 * root.scaleUnit
                anchors.verticalCenter: parent.verticalCenter
                text: "01"
                color: root.accentColor
                font.pixelSize: 9 * root.scaleUnit
                font.bold: true
            }

            Rectangle {
                x: parent.width - 78 * root.scaleUnit
                anchors.verticalCenter: parent.verticalCenter
                width: 32 * root.scaleUnit
                height: 32 * root.scaleUnit
                radius: width / 2
                color: root.buttonColor
                border.color: root.posterBorderColor

                Text {
                    anchors.centerIn: parent
                    text: root.isLocked ? "●" : "○"
                    color: root.isLocked ? root.editingColor : root.secondaryTextColor
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
                color: root.buttonColor
                border.color: root.posterBorderColor

                Text {
                    anchors.centerIn: parent
                    text: root.expanded ? "⌄" : "⌃"
                    color: root.secondaryTextColor
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

            Text {
                x: 0
                y: 0
                text: "TACTICAL ARCHIVE / 01"
                color: root.accentColor
                font.pixelSize: 9 * root.scaleUnit
                font.letterSpacing: 1.4 * root.scaleUnit
                font.bold: true
            }

            Text {
                x: 0
                y: 23 * root.scaleUnit
                text: "NIGHT OPS"
                color: root.primaryTextColor
                font.pixelSize: 38 * root.scaleUnit
                font.bold: true
                font.family: root.titleFontFamily
            }

            Text {
                x: 1 * root.scaleUnit
                y: 72 * root.scaleUnit
                text: "06 TITLES  ·  READY"
                color: root.mutedTextColor
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
                        color: root.cardColor
                        border.width: 1
                        border.color: root.cardBorderColor
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
                                GradientStop { position: 0.0; color: root.cardGradientStart }
                                GradientStop { position: 1.0; color: root.cardGradientEnd }
                            }
                            opacity: 0.56
                        }

                        Text {
                            anchors.centerIn: parent
                            text: root.cardInitials[index]
                            color: root.primaryTextColor
                            font.pixelSize: 22 * root.scaleUnit
                            font.bold: true
                        }

                        Text {
                            anchors.right: parent.right
                            anchors.bottom: parent.bottom
                            anchors.margins: 6 * root.scaleUnit
                            text: "0" + (index + 1)
                            color: root.mutedTextColor
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
                        color: root.secondaryTextColor
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
                color: root.editing ? root.editingColor : root.mutedTextColor
                font.pixelSize: 8 * root.scaleUnit
                font.letterSpacing: 1.1 * root.scaleUnit
                font.bold: true
            }

            Text {
                anchors.right: parent.right
                anchors.rightMargin: 28 * root.scaleUnit
                anchors.verticalCenter: parent.verticalCenter
                text: root.isLocked ? "LOCKED" : "DRAG TO MOVE"
                color: root.isLocked ? root.editingColor : root.mutedTextColor
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
            border.color: root.accentColor
            opacity: root.hitScenario === "FINAL" ? 0.0 : 0.22
        }

        Rectangle {
            anchors.right: parent.right
            anchors.bottom: parent.bottom
            width: 18
            height: 18
            color: root.editingColor
            opacity: root.editing && !root.isLocked && root.expanded ? 0.9 : 0.0
        }
    }
}
