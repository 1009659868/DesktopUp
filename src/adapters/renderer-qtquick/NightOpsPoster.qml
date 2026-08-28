import QtQuick

Item {
    id: root
    objectName: "nightOpsRoot"

    property var itemModel: _desktopUpItemModel
    property bool expanded: true
    property bool locked: false
    property int selectedItem: -1
    property real deviceScale: 1.0
    property real contentScale: 1.0
    property real scrollOffsetDip: 0.0
    property string collectionText: ""
    property string archiveText: ""
    property string titleText: ""
    property string statusText: ""
    property string footerLeftText: ""
    property string footerRightText: ""

    readonly property real unit: Math.max(0.25, deviceScale * contentScale)
    readonly property real fontScale: unit
    readonly property bool contentVisible: expanded && width >= 320 * deviceScale && height >= 420 * deviceScale
    readonly property real cardWidthPx: 113 * unit
    readonly property real cardHeightPx: 92 * unit
    readonly property real cardGapPx: 12 * unit
    readonly property real labelGapPx: 5 * unit
    readonly property real labelHeightPx: 18 * unit
    readonly property real rowGapPx: 16 * unit
    readonly property real cellWidthPx: cardWidthPx + cardGapPx
    readonly property real cellHeightPx: cardHeightPx + labelGapPx + labelHeightPx + rowGapPx
    readonly property int itemCount: itemModel ? itemModel.count : 0
    readonly property int columnCount: contentVisible && itemViewport.width + 0.01 >= cardWidthPx
                                       ? Math.min(itemCount, Math.floor((itemViewport.width + cardGapPx) / cellWidthPx))
                                       : 0
    readonly property int rowCount: columnCount > 0 ? Math.ceil(itemCount / columnCount) : 0
    readonly property real itemContentHeightPx: rowCount > 0
                                                ? rowCount * (cardHeightPx + labelGapPx + labelHeightPx) +
                                                  (rowCount - 1) * rowGapPx
                                                : 0
    readonly property real maximumScrollPx: Math.max(0, itemContentHeightPx - itemViewport.height)
    readonly property real actualScrollPx: Math.min(maximumScrollPx,
                                                    Math.max(0, scrollOffsetDip * unit))
    readonly property bool overflow: contentVisible && maximumScrollPx > 0.01
    function delegateAt(index) {
        return itemViewport.itemAtIndex(index)
    }

    Rectangle {
        id: posterFrame
        objectName: "posterFrame"
        x: 2
        y: 2
        width: Math.max(0, root.width - 4)
        height: Math.max(0, root.height - 4)
        radius: Math.min(24 * root.fontScale, Math.min(width, height) * 0.42)
        color: "#e80a1824"
        border.width: Math.max(1, root.unit)
        border.color: "#5c65bcd1"

        Rectangle {
            anchors.fill: parent
            anchors.margins: Math.max(1, root.unit)
            radius: Math.max(0, parent.radius - anchors.margins)
            gradient: Gradient {
                GradientStop { position: 0.0; color: "#b8285363" }
                GradientStop { position: 0.42; color: "#501a3b49" }
                GradientStop { position: 1.0; color: "#70040a10" }
            }
            opacity: 0.56
        }
    }

    Item {
        id: header
        objectName: "headerBounds"
        x: 22 * root.unit
        y: 10 * root.unit
        width: Math.max(0, root.width - 44 * root.unit)
        height: 40 * root.unit

        Text {
            anchors.left: parent.left
            anchors.verticalCenter: parent.verticalCenter
            width: 142 * root.unit
            text: root.collectionText
            color: "#a0b1c1ca"
            font.family: "Segoe UI"
            font.pixelSize: 8 * root.fontScale
            font.weight: Font.DemiBold
            font.letterSpacing: 1.35 * root.fontScale
        }
        Rectangle {
            x: 142 * root.unit
            anchors.verticalCenter: parent.verticalCenter
            width: Math.max(0, Math.min(98 * root.unit, parent.width - 228 * root.unit))
            height: Math.max(1, root.unit)
            color: "#b452d6ef"
        }
        Text {
            x: Math.min(parent.width - 86 * root.unit, 240 * root.unit) + 8 * root.unit
            anchors.verticalCenter: parent.verticalCenter
            text: "01"
            color: "#e666e2f4"
            font.family: "Segoe UI"
            font.pixelSize: 8 * root.fontScale
            font.bold: true
        }
    }

    Item {
        id: titleDrag
        objectName: "titleDragBounds"
        x: header.x
        y: header.y
        width: Math.max(0, lockButton.x - header.x - 10 * root.fontScale)
        height: header.height
    }

    Rectangle {
        id: lockButton
        objectName: "lockButtonBounds"
        width: 32 * root.fontScale
        height: width
        x: collapseButton.x - 10 * root.fontScale - width
        y: header.y + (header.height - height) / 2
        radius: width / 2
        color: "#4607111b"
        border.width: Math.max(1, root.unit)
        border.color: "#5cc4dde5"
        Text {
            anchors.centerIn: parent
            text: root.locked ? "L" : "U"
            color: "#bfc4dde5"
            font.pixelSize: 10 * root.fontScale
            font.bold: true
        }
    }

    Rectangle {
        id: collapseButton
        objectName: "collapseButtonBounds"
        width: 32 * root.fontScale
        height: width
        x: header.x + header.width - width
        y: header.y + (header.height - height) / 2
        radius: width / 2
        color: "#4607111b"
        border.width: Math.max(1, root.unit)
        border.color: "#5cc4dde5"
        Text {
            anchors.centerIn: parent
            text: root.expanded ? "v" : "^"
            color: "#bfc4dde5"
            font.pixelSize: 15 * root.fontScale
        }
    }

    Item {
        id: content
        objectName: "contentBounds"
        visible: root.contentVisible
        x: 28 * root.unit
        y: 78 * root.unit
        width: Math.max(0, root.width - 56 * root.unit)
        height: Math.max(0, footer.y - 10 * root.unit - y)

        Text {
            x: 0
            y: 0
            width: parent.width
            height: 22 * root.unit
            text: root.archiveText
            color: "#e068dfee"
            font.family: "Segoe UI"
            font.pixelSize: 10 * root.fontScale
            font.bold: true
            font.letterSpacing: 1.15 * root.fontScale
            verticalAlignment: Text.AlignVCenter
        }
        Text {
            x: 0
            y: 25 * root.unit
            width: parent.width
            height: 52 * root.unit
            text: root.titleText
            color: "#faf4f8f9"
            font.family: "Bahnschrift Condensed"
            font.pixelSize: 38 * root.fontScale
            font.weight: Font.Black
            verticalAlignment: Text.AlignVCenter
        }
        Text {
            x: 0
            y: 68 * root.unit
            width: parent.width
            height: 20 * root.unit
            text: root.statusText
            color: "#969aa9b1"
            font.family: "Segoe UI"
            font.pixelSize: 8 * root.fontScale
            font.weight: Font.DemiBold
            font.letterSpacing: 1.3 * root.fontScale
            verticalAlignment: Text.AlignVCenter
        }
    }

    GridView {
        id: itemViewport
        objectName: "itemViewportBounds"
        visible: root.contentVisible
        clip: true
        interactive: false
        reuseItems: true
        cacheBuffer: 4096
        x: 28 * root.unit
        y: 183 * root.unit
        width: Math.max(0, root.width - 56 * root.unit)
        height: Math.max(0, footer.y - 10 * root.unit - y)
        cellWidth: root.cellWidthPx
        cellHeight: root.cellHeightPx
        flow: GridView.FlowLeftToRight
        model: root.itemModel
        contentY: root.actualScrollPx

        delegate: Item {
            id: itemDelegate
            required property int index
            required property string initials
            required property string itemTitle
            required property color accentColor
            objectName: "itemDelegate_" + index
            width: root.cardWidthPx
            height: root.cardHeightPx + root.labelGapPx + root.labelHeightPx

            Rectangle {
                id: card
                objectName: "itemCard_" + itemDelegate.index
                width: root.cardWidthPx
                height: root.cardHeightPx
                radius: 7 * root.fontScale
                color: "#f0172735"
                border.width: Math.max(1, 0.8 * root.unit)
                border.color: itemDelegate.index === root.selectedItem ? "#e669e5f4" : "#4c779eb0"
                clip: true
                Rectangle {
                    anchors.centerIn: parent
                    width: 22 * root.unit
                    height: parent.height * 1.45
                    rotation: 20
                    color: itemDelegate.accentColor
                }
                Text {
                    anchors.centerIn: parent
                    text: itemDelegate.initials
                    color: "#fff4f7f8"
                    font.family: "Segoe UI"
                    font.pixelSize: 23 * root.fontScale
                    font.bold: true
                }
                Text {
                    anchors.right: parent.right
                    anchors.rightMargin: 5 * root.unit
                    anchors.bottom: parent.bottom
                    anchors.bottomMargin: 5 * root.unit
                    text: (itemDelegate.index + 1).toString().padStart(2, "0")
                    color: "#84acbac2"
                    font.family: "Segoe UI"
                    font.pixelSize: 7 * root.fontScale
                }
            }

            Text {
                objectName: "itemLabel_" + itemDelegate.index
                x: 0
                y: root.cardHeightPx + root.labelGapPx
                width: root.cardWidthPx
                height: root.labelHeightPx
                text: itemDelegate.itemTitle
                color: "#d4bac5ca"
                font.family: "Segoe UI"
                font.pixelSize: 8 * root.fontScale
                font.weight: Font.DemiBold
                verticalAlignment: Text.AlignTop
            }
        }
    }

    Item {
        id: scrollInput
        objectName: "itemScrollInputBounds"
        visible: root.overflow
        x: itemViewport.x + itemViewport.width - 12 * root.unit
        y: itemViewport.y
        width: 12 * root.unit
        height: itemViewport.height
    }
    Rectangle {
        id: scrollTrack
        objectName: "itemScrollTrackBounds"
        visible: root.overflow
        x: itemViewport.x + itemViewport.width - Math.max(2, 2 * root.unit)
        y: itemViewport.y
        width: Math.max(2, 2 * root.unit)
        height: itemViewport.height
        color: "#3052d6ef"
        Rectangle {
            id: scrollThumb
            objectName: "itemScrollThumbBounds"
            width: parent.width
            height: Math.max(20 * root.unit,
                             Math.min(parent.height,
                                      parent.height * parent.height / Math.max(parent.height, root.itemContentHeightPx)))
            y: root.maximumScrollPx > 0
               ? (parent.height - height) * root.actualScrollPx / root.maximumScrollPx
               : 0
            color: "#e552d6ef"
        }
    }

    Item {
        id: resizeCorner
        objectName: "resizeCornerBounds"
        visible: root.contentVisible
        x: Math.max(2, root.width - 24 * root.unit)
        y: Math.max(2, root.height - 24 * root.unit)
        width: 20 * root.unit
        height: 20 * root.unit
    }

    Rectangle {
        id: footer
        objectName: "footerBounds"
        visible: root.contentVisible
        x: 2
        y: root.height - 46 * root.unit
        width: Math.max(0, root.width - 4)
        height: 44 * root.unit
        color: "#16040a10"
        border.width: 0
        Rectangle {
            anchors.left: parent.left
            anchors.right: parent.right
            anchors.top: parent.top
            height: Math.max(1, root.unit)
            color: "#223c6070"
        }
        Text {
            anchors.left: parent.left
            anchors.leftMargin: 26 * root.unit
            anchors.verticalCenter: parent.verticalCenter
            text: root.footerLeftText
            color: "#8a7f929a"
            font.family: "Segoe UI"
            font.pixelSize: 7 * root.fontScale
            font.weight: Font.DemiBold
            font.letterSpacing: 1.0 * root.fontScale
        }
        Text {
            anchors.right: parent.right
            anchors.rightMargin: 26 * root.unit
            anchors.verticalCenter: parent.verticalCenter
            text: root.footerRightText
            color: "#8a7f929a"
            font.family: "Segoe UI"
            font.pixelSize: 7 * root.fontScale
            font.weight: Font.DemiBold
            font.letterSpacing: 1.0 * root.fontScale
        }
    }
}
