#include "adapters/renderer-qpainter/qpainter_renderer.h"

#include <QBrush>
#include <QColor>
#include <QFont>
#include <QFontMetricsF>
#include <QLinearGradient>
#include <QPainter>
#include <QPainterPath>
#include <QPen>
#include <QPolygonF>
#include <QRadialGradient>
#include <QString>

#include <algorithm>
#include <cmath>
#include <utility>

namespace desktopup::adapters {
namespace {

constexpr qreal kMinimumDeviceScale = 0.5;
constexpr qreal kMaximumDeviceScale = 4.0;
constexpr qreal kMinimumContentScale = 0.5;
constexpr qreal kMaximumContentScale = 3.0;
constexpr qreal kCollapsedHeight = 60.0;
constexpr qreal kCardWidth = 113.0;
constexpr qreal kCardHeight = 92.0;
constexpr qreal kCardGap = 12.0;
constexpr qreal kLabelGap = 5.0;
constexpr qreal kLabelHeight = 18.0;
constexpr qreal kRowGap = 16.0;

QString text(const std::string& value) {
  return QString::fromUtf8(value.data(), static_cast<qsizetype>(value.size()));
}

QColor rgba(const std::uint32_t value) {
  return QColor::fromRgba(value);
}

QFont uiFont(const qreal pixelSize,
             const int weight,
             const qreal letterSpacing = 0.0,
             const bool condensed = false) {
  QFont font(condensed ? QStringLiteral("Bahnschrift Condensed")
                       : QStringLiteral("Segoe UI"));
  font.setPixelSize(std::max(1, static_cast<int>(std::lround(pixelSize))));
  font.setWeight(static_cast<QFont::Weight>(weight));
  if (letterSpacing != 0.0) {
    font.setLetterSpacing(QFont::AbsoluteSpacing, letterSpacing);
  }
  font.setHintingPreference(QFont::PreferFullHinting);
  return font;
}

void drawText(QPainter& painter,
              const QRectF& bounds,
              const QString& value,
              const QColor& color,
              const QFont& font,
              const int flags = Qt::AlignLeft | Qt::AlignVCenter) {
  painter.setPen(color);
  painter.setFont(font);
  painter.drawText(bounds, flags, value);
}

void drawLockGlyph(QPainter& painter, const QRectF& bounds, const bool locked, const qreal scale) {
  painter.save();
  painter.setRenderHint(QPainter::Antialiasing, true);
  painter.setBrush(QColor(7, 17, 27, 70));
  painter.setPen(QPen(QColor(196, 221, 229, 92), std::max<qreal>(1.0, scale)));
  painter.drawEllipse(bounds);

  const qreal bodyWidth = 10.0 * scale;
  const qreal bodyHeight = 8.0 * scale;
  const QRectF body(bounds.center().x() - bodyWidth / 2.0,
                    bounds.center().y() - bodyHeight / 2.0 + 3.0 * scale,
                    bodyWidth,
                    bodyHeight);
  painter.setBrush(Qt::NoBrush);
  painter.drawRoundedRect(body, 1.5 * scale, 1.5 * scale);

  QPainterPath shackle;
  const qreal shackleWidth = 7.0 * scale;
  const qreal shackleHeight = 7.0 * scale;
  if (locked) {
    shackle.moveTo(bounds.center().x() - shackleWidth / 2.0,
                   body.top() + 1.0 * scale);
    shackle.lineTo(bounds.center().x() - shackleWidth / 2.0,
                   body.top() - shackleHeight / 2.0);
    shackle.cubicTo(bounds.center().x() - shackleWidth / 2.0,
                    body.top() - shackleHeight,
                    bounds.center().x() + shackleWidth / 2.0,
                    body.top() - shackleHeight,
                    bounds.center().x() + shackleWidth / 2.0,
                    body.top() - shackleHeight / 2.0);
    shackle.lineTo(bounds.center().x() + shackleWidth / 2.0,
                   body.top() + 1.0 * scale);
  } else {
    shackle.moveTo(bounds.center().x() - shackleWidth / 2.0,
                   body.top() + 1.0 * scale);
    shackle.lineTo(bounds.center().x() - shackleWidth / 2.0,
                   body.top() - shackleHeight / 2.0);
    shackle.cubicTo(bounds.center().x() - shackleWidth / 2.0,
                    body.top() - shackleHeight,
                    bounds.center().x() + shackleWidth / 2.0,
                    body.top() - shackleHeight,
                    bounds.center().x() + shackleWidth / 2.0,
                    body.top() - shackleHeight / 2.0);
  }
  painter.drawPath(shackle);
  painter.restore();
}

void drawCollapseGlyph(QPainter& painter,
                       const QRectF& bounds,
                       const PosterPresentation presentation,
                       const qreal scale) {
  painter.save();
  painter.setRenderHint(QPainter::Antialiasing, true);
  painter.setBrush(QColor(7, 17, 27, 70));
  painter.setPen(QPen(QColor(196, 221, 229, 92), std::max<qreal>(1.0, scale),
                      Qt::SolidLine, Qt::RoundCap, Qt::RoundJoin));
  painter.drawEllipse(bounds);
  const qreal half = 4.5 * scale;
  const qreal direction = presentation == PosterPresentation::Expanded ? 1.0 : -1.0;
  QPainterPath chevron;
  chevron.moveTo(bounds.center().x() - half,
                 bounds.center().y() - direction * 2.0 * scale);
  chevron.lineTo(bounds.center().x(),
                 bounds.center().y() + direction * 2.5 * scale);
  chevron.lineTo(bounds.center().x() + half,
                 bounds.center().y() - direction * 2.0 * scale);
  painter.drawPath(chevron);
  painter.restore();
}

}  // namespace

NightOpsScene NightOpsScene::fixture() {
  NightOpsScene scene;
  scene.collection = "DESKTOP COLLECTION";
  scene.archive = "TACTICAL ARCHIVE / 01";
  scene.title = "NIGHT OPS";
  scene.status = "06 TITLES  ·  READY";
  scene.footerLeft = "LOCAL LIBRARY";
  scene.footerRight = "DRAG TO MOVE";
  scene.items = {{{"AL", "AFTERLIGHT", 0xFF6E4AA5u},
                  {"GS", "GHOST SIGNAL", 0xFF3C7784u},
                  {"IV", "IRON VEIL", 0xFFA24145u},
                  {"LO", "LAST ORBIT", 0xFFA06A35u},
                  {"SR", "SABLE RUN", 0xFF365B9Bu},
                  {"HC", "HOLLOW CROWN", 0xFF3D7964u}}};
  return scene;
}

QPainterRenderer::QPainterRenderer() : scene_(NightOpsScene::fixture()) {}

QPainterRenderer::QPainterRenderer(NightOpsScene scene) : scene_(std::move(scene)) {}

std::uint32_t QPainterRenderer::collapsedHeightForScale(const qreal scale) noexcept {
  const qreal safeScale =
      std::clamp(std::isfinite(scale) ? scale : 1.0,
                 kMinimumDeviceScale * kMinimumContentScale,
                 kMaximumDeviceScale * kMaximumContentScale);
  return static_cast<std::uint32_t>(
      std::max<qreal>(1.0, std::lround(kCollapsedHeight * safeScale)));
}

NightOpsLayout QPainterRenderer::calculateLayout(
    const contracts::PixelSize& size,
    const NightOpsScene& scene) noexcept {
  NightOpsLayout layout;
  const qreal width = static_cast<qreal>(size.width);
  const qreal height = static_cast<qreal>(size.height);
  layout.deviceScale = std::clamp(
      std::isfinite(scene.deviceScale) ? scene.deviceScale : 1.0,
      kMinimumDeviceScale, kMaximumDeviceScale);
  layout.contentScale = std::clamp(
      std::isfinite(scene.contentScale) ? scene.contentScale : 1.0,
      kMinimumContentScale, kMaximumContentScale);
  layout.unit = layout.deviceScale * layout.contentScale;
  layout.fontScale = layout.unit;
  layout.cornerRadius = std::min(24.0 * layout.fontScale, std::min(width, height) * 0.42);
  layout.posterBounds = QRectF(2.0, 2.0, std::max<qreal>(0.0, width - 4.0),
                              std::max<qreal>(0.0, height - 4.0));

  const qreal u = layout.unit;
  layout.headerBounds = QRectF(22.0 * u, 10.0 * u,
                               std::max<qreal>(0.0, width - 44.0 * u), 40.0 * u);
  const qreal buttonSize = 32.0 * layout.fontScale;
  const qreal buttonGap = 10.0 * layout.fontScale;
  layout.collapseButtonBounds =
      QRectF(layout.headerBounds.right() - buttonSize,
             layout.headerBounds.center().y() - buttonSize / 2.0,
             buttonSize, buttonSize);
  layout.lockButtonBounds =
      QRectF(layout.collapseButtonBounds.left() - buttonGap - buttonSize,
             layout.collapseButtonBounds.top(), buttonSize, buttonSize);
  layout.titleDragBounds = QRectF(layout.headerBounds.left(), layout.headerBounds.top(),
                                  std::max<qreal>(0.0, layout.lockButtonBounds.left() -
                                                          layout.headerBounds.left() - buttonGap),
                                  layout.headerBounds.height());

  const auto minimumExpanded = minimumExpandedSize();
  layout.contentVisible = scene.presentation == PosterPresentation::Expanded &&
                          width >= minimumExpanded.width * layout.deviceScale &&
                          height >= minimumExpanded.height * layout.deviceScale;
  if (!layout.contentVisible) {
    return layout;
  }

  layout.resizeCornerBounds =
      QRectF(std::max<qreal>(2.0, width - 24.0 * u),
             std::max<qreal>(2.0, height - 24.0 * u),
             20.0 * u, 20.0 * u);
  layout.footerBounds = QRectF(2.0, height - 46.0 * u,
                               std::max<qreal>(0.0, width - 4.0), 44.0 * u);
  const qreal contentTop = 78.0 * u;
  const qreal contentBottom = std::max(contentTop, layout.footerBounds.top() - 10.0 * u);
  layout.contentBounds = QRectF(28.0 * u, contentTop,
                                std::max<qreal>(0.0, width - 56.0 * u),
                                std::max<qreal>(0.0, contentBottom - contentTop));

  const qreal gap = kCardGap * u;
  const qreal cardWidth = kCardWidth * u;
  const qreal cardHeight = kCardHeight * u;
  const qreal labelGap = kLabelGap * u;
  const qreal labelHeight = kLabelHeight * u;
  const qreal rowGap = kRowGap * u;
  const qreal cellHeight = cardHeight + labelGap + labelHeight;
  const qreal rowStride = cellHeight + rowGap;
  layout.itemRowStride = rowStride;
  const qreal firstRowTop = 183.0 * u;
  const qreal viewportBottom = std::max(firstRowTop, layout.footerBounds.top() - 10.0 * u);
  layout.itemViewportBounds =
      QRectF(layout.contentBounds.left(), firstRowTop,
             layout.contentBounds.width(),
             std::max<qreal>(0.0, viewportBottom - firstRowTop));

  const auto itemCount = static_cast<std::uint32_t>(layout.cardBounds.size());
  const auto availableColumns =
      layout.itemViewportBounds.width() + 0.01 >= cardWidth
          ? static_cast<std::uint32_t>(std::floor(
                (layout.itemViewportBounds.width() + gap) / (cardWidth + gap)))
          : 0;
  layout.columnCount = std::min(itemCount, availableColumns);
  layout.rowCount =
      layout.columnCount == 0
          ? 0
          : static_cast<std::uint32_t>(
                (itemCount + layout.columnCount - 1) / layout.columnCount);
  layout.itemContentHeight =
      layout.rowCount == 0
          ? 0.0
          : layout.rowCount * cellHeight + (layout.rowCount - 1) * rowGap;
  layout.maxItemScrollOffset =
      std::max<qreal>(0.0,
                      layout.itemContentHeight - layout.itemViewportBounds.height());
  const qreal requestedScrollOffset =
      std::max<qreal>(0.0, scene.itemScrollOffsetDip) * u;
  layout.itemScrollOffset =
      std::clamp(requestedScrollOffset, 0.0, layout.maxItemScrollOffset);
  if (layout.maxItemScrollOffset > 0.0 &&
      layout.itemViewportBounds.height() > 0.0 &&
      layout.itemContentHeight > 0.0) {
    const qreal trackWidth = std::max<qreal>(2.0, 2.0 * u);
    layout.itemScrollTrackBounds =
        QRectF(layout.itemViewportBounds.right() - trackWidth,
               layout.itemViewportBounds.top(),
               trackWidth,
               layout.itemViewportBounds.height());
    const qreal thumbHeight = std::clamp(
        layout.itemScrollTrackBounds.height() *
            layout.itemScrollTrackBounds.height() / layout.itemContentHeight,
        20.0 * u,
        layout.itemScrollTrackBounds.height());
    const qreal progress =
        layout.itemScrollOffset / layout.maxItemScrollOffset;
    layout.itemScrollThumbBounds =
        QRectF(layout.itemScrollTrackBounds.left(),
               layout.itemScrollTrackBounds.top() +
                   (layout.itemScrollTrackBounds.height() - thumbHeight) * progress,
               layout.itemScrollTrackBounds.width(),
               thumbHeight);
    const qreal scrollInputWidth = std::max<qreal>(12.0, 12.0 * u);
    layout.itemScrollInputBounds =
        QRectF(layout.itemViewportBounds.right() - scrollInputWidth,
               layout.itemViewportBounds.top(),
               scrollInputWidth,
               layout.itemViewportBounds.height());
  }
  if (layout.columnCount == 0) return layout;

  for (std::uint32_t index = 0; index < itemCount; ++index) {
    const auto column = index % layout.columnCount;
    const auto row = index / layout.columnCount;
    const qreal x = layout.contentBounds.left() + column * (cardWidth + gap);
    const qreal y = firstRowTop + row * rowStride - layout.itemScrollOffset;
    layout.cardBounds[index] = QRectF(x, y, cardWidth, cardHeight);
    layout.labelBounds[index] =
        QRectF(x, y + cardHeight + labelGap, cardWidth, labelHeight);
    const QRectF completeItemBounds(
        x, y, cardWidth, cardHeight + labelGap + labelHeight);
    layout.itemHitBounds[index] =
        completeItemBounds.intersected(layout.itemViewportBounds);
    layout.itemVisible[index] = !layout.itemHitBounds[index].isEmpty();
    if (layout.itemVisible[index]) ++layout.visibleItemCount;
  }
  return layout;
}

void QPainterRenderer::paintNightOps(QImage& image,
                                     const NightOpsScene& scene,
                                     const NightOpsLayout& layout) {
  image.fill(Qt::transparent);
  QPainter painter(&image);
  painter.setRenderHint(QPainter::Antialiasing, true);
  painter.setRenderHint(QPainter::TextAntialiasing, true);

  QPainterPath posterPath;
  posterPath.addRoundedRect(layout.posterBounds, layout.cornerRadius, layout.cornerRadius);

  QLinearGradient background(layout.posterBounds.topLeft(), layout.posterBounds.bottomRight());
  background.setColorAt(0.0, QColor(10, 24, 36, 232));
  background.setColorAt(0.48, QColor(6, 16, 25, 224));
  background.setColorAt(1.0, QColor(4, 10, 16, 238));
  painter.fillPath(posterPath, background);

  painter.save();
  painter.setClipPath(posterPath);
  const qreal u = layout.unit;

  QRadialGradient glow(QPointF(image.width() * 0.72, image.height() * 0.12),
                       image.width() * 0.56);
  glow.setColorAt(0.0, QColor(55, 111, 131, 94));
  glow.setColorAt(0.42, QColor(33, 75, 91, 42));
  glow.setColorAt(1.0, QColor(12, 20, 29, 0));
  painter.fillRect(image.rect(), glow);

  QPainterPath diagonal;
  diagonal.moveTo(image.width() * 0.36, 0.0);
  diagonal.lineTo(image.width() * 0.61, 0.0);
  diagonal.lineTo(image.width() * 0.31, image.height());
  diagonal.lineTo(image.width() * 0.06, image.height());
  diagonal.closeSubpath();
  painter.fillPath(diagonal, QColor(67, 105, 122, 18));
  painter.setPen(QPen(QColor(106, 189, 211, 24), std::max<qreal>(1.0, u)));
  painter.drawLine(QPointF(image.width() * 0.61, 0.0),
                   QPointF(image.width() * 0.31, image.height()));
  painter.restore();

  painter.setPen(QPen(QColor(101, 188, 209, 92), std::max<qreal>(0.8, layout.unit)));
  painter.setBrush(Qt::NoBrush);
  painter.drawPath(posterPath);

  const qreal fontScale = layout.fontScale;
  const QRectF header = layout.headerBounds;
  drawText(painter, QRectF(header.left(), header.top(), 142.0 * layout.unit, header.height()),
           text(scene.collection), QColor(177, 193, 202, 160),
           uiFont(8.0 * fontScale, QFont::DemiBold, 1.35 * fontScale));
  const qreal lineStart = header.left() + 142.0 * layout.unit;
  const qreal lineEnd = std::min(header.right() - 86.0 * layout.unit,
                                 lineStart + 98.0 * layout.unit);
  painter.setPen(QPen(QColor(82, 214, 239, 180), std::max<qreal>(1.0, layout.unit)));
  painter.drawLine(QPointF(lineStart, header.center().y()),
                   QPointF(lineEnd, header.center().y()));
  drawText(painter,
           QRectF(lineEnd + 8.0 * layout.unit, header.top(), 28.0 * layout.unit, header.height()),
           QStringLiteral("01"), QColor(102, 226, 244, 230),
           uiFont(8.0 * fontScale, QFont::Bold, 0.8 * fontScale));

  drawLockGlyph(painter, layout.lockButtonBounds, scene.locked, fontScale);
  drawCollapseGlyph(painter, layout.collapseButtonBounds, scene.presentation, fontScale);

  if (!layout.contentVisible) {
    painter.end();
    return;
  }

  const QRectF content = layout.contentBounds;
  drawText(painter, QRectF(content.left(), content.top(), content.width(), 22.0 * layout.unit),
           text(scene.archive), QColor(104, 223, 239, 224),
           uiFont(10.0 * fontScale, QFont::Bold, 1.15 * fontScale));
  drawText(painter,
           QRectF(content.left(), content.top() + 25.0 * layout.unit,
                  content.width(), 52.0 * layout.unit),
           text(scene.title), QColor(244, 248, 249, 250),
           uiFont(38.0 * fontScale, QFont::Black, 0.1, true));
  drawText(painter,
           QRectF(content.left(), content.top() + 68.0 * layout.unit,
                  content.width(), 20.0 * layout.unit),
           text(scene.status), QColor(154, 169, 177, 150),
           uiFont(8.0 * fontScale, QFont::DemiBold, 1.3 * fontScale));

  painter.save();
  painter.setClipRect(layout.itemViewportBounds);
  for (std::size_t index = 0; index < scene.items.size(); ++index) {
    if (!layout.itemVisible[index]) continue;
    const QRectF card = layout.cardBounds[index];
    const auto& item = scene.items[index];
    QPainterPath cardPath;
    cardPath.addRoundedRect(card, 7.0 * fontScale, 7.0 * fontScale);

    QLinearGradient cardGradient(card.topLeft(), card.bottomRight());
    cardGradient.setColorAt(0.0, QColor(23, 39, 53, 244));
    cardGradient.setColorAt(1.0, QColor(5, 12, 20, 250));
    painter.fillPath(cardPath, cardGradient);

    painter.save();
    painter.setClipPath(cardPath, Qt::IntersectClip);
    painter.setPen(QPen(QColor(77, 114, 133, 26), std::max<qreal>(0.7, layout.unit)));
    for (int stripe = -1; stripe < 5; ++stripe) {
      const qreal x = card.left() + stripe * 31.0 * layout.unit;
      painter.drawLine(QPointF(x, card.bottom()),
                       QPointF(x + 44.0 * layout.unit, card.top()));
    }

    const QColor accent = rgba(item.accentRgba);
    QPolygonF band;
    band << QPointF(card.center().x() + 7.0 * layout.unit, card.top())
         << QPointF(card.center().x() + 28.0 * layout.unit, card.top())
         << QPointF(card.center().x() - 4.0 * layout.unit, card.bottom())
         << QPointF(card.center().x() - 25.0 * layout.unit, card.bottom());
    painter.setBrush(accent);
    painter.setPen(Qt::NoPen);
    painter.drawPolygon(band);
    painter.restore();

    painter.setBrush(Qt::NoBrush);
    painter.setPen(QPen(QColor(119, 158, 176, 76), std::max<qreal>(0.8, layout.unit)));
    painter.drawPath(cardPath);
    drawText(painter, card, text(item.initials), QColor(244, 247, 248, 255),
             uiFont(23.0 * fontScale, QFont::Bold), Qt::AlignCenter);
    drawText(painter,
             QRectF(card.right() - 24.0 * layout.unit,
                    card.bottom() - 20.0 * layout.unit,
                    19.0 * layout.unit, 14.0 * layout.unit),
             QStringLiteral("%1").arg(static_cast<int>(index + 1), 2, 10, QLatin1Char('0')),
             QColor(172, 186, 194, 132),
             uiFont(7.0 * fontScale, QFont::DemiBold), Qt::AlignRight | Qt::AlignVCenter);
    if (scene.selectedItem == static_cast<std::int32_t>(index)) {
      painter.setBrush(Qt::NoBrush);
      painter.setPen(QPen(QColor(105, 229, 244, 230),
                          std::max<qreal>(1.5, layout.unit * 1.5)));
      painter.drawPath(cardPath);
    }
    drawText(painter, layout.labelBounds[index], text(item.title),
             QColor(185, 194, 199, 196),
             uiFont(8.0 * fontScale, QFont::Bold, 0.25 * fontScale));
  }
  painter.restore();

  if (!layout.itemScrollTrackBounds.isEmpty() &&
      !layout.itemScrollThumbBounds.isEmpty()) {
    const qreal trackWidth = layout.itemScrollTrackBounds.width();
    painter.setPen(Qt::NoPen);
    painter.setBrush(QColor(112, 151, 168, 34));
    painter.drawRoundedRect(layout.itemScrollTrackBounds,
                            trackWidth / 2.0,
                            trackWidth / 2.0);
    painter.setBrush(QColor(102, 221, 239, 138));
    painter.drawRoundedRect(layout.itemScrollThumbBounds,
                            trackWidth / 2.0,
                            trackWidth / 2.0);
  }

  const QRectF footer = layout.footerBounds;
  painter.setPen(QPen(QColor(121, 155, 170, 30), std::max<qreal>(0.8, layout.unit)));
  painter.drawLine(QPointF(footer.left(), footer.top()),
                   QPointF(footer.right(), footer.top()));
  drawText(painter,
           QRectF(footer.left() + 26.0 * layout.unit, footer.top(),
                  140.0 * layout.unit, footer.height()),
           text(scene.footerLeft), QColor(144, 156, 164, 116),
           uiFont(7.0 * fontScale, QFont::Bold, 1.0 * fontScale));
  drawText(painter,
           QRectF(footer.right() - 150.0 * layout.unit, footer.top(),
                  124.0 * layout.unit, footer.height()),
           text(scene.footerRight), QColor(144, 156, 164, 116),
           uiFont(7.0 * fontScale, QFont::Bold, 1.0 * fontScale),
           Qt::AlignRight | Qt::AlignVCenter);
  painter.end();
}

contracts::FrameDescriptor QPainterRenderer::render(const contracts::RenderRequest& request) {
  if (exiting_ || request.bounds.width == 0 || request.bounds.height == 0) {
    return {};
  }

  image_ = QImage(static_cast<int>(request.bounds.width),
                  static_cast<int>(request.bounds.height),
                  QImage::Format_ARGB32_Premultiplied);
  layout_ = calculateLayout(request.bounds, scene_);
  paintNightOps(image_, scene_, layout_);

  buffer_ = nextBuffer_++;
  return {request.revision, request.surfaceEpoch, request.bounds, buffer_};
}

contracts::PremultipliedFrameView QPainterRenderer::frameView(
    const contracts::BufferHandle buffer) const noexcept {
  if (buffer == 0 || buffer != buffer_ || image_.isNull() ||
      image_.format() != QImage::Format_ARGB32_Premultiplied) {
    return {};
  }
  return {buffer_,
          {static_cast<std::uint32_t>(image_.width()),
           static_cast<std::uint32_t>(image_.height())},
          static_cast<std::uint32_t>(image_.bytesPerLine()),
          image_.constBits()};
}

void QPainterRenderer::release(const contracts::BufferHandle buffer) noexcept {
  if (buffer != 0 && buffer == buffer_) {
    image_ = {};
    layout_ = {};
    buffer_ = 0;
  }
}

void QPainterRenderer::beginExit() noexcept {
  exiting_ = true;
}

void QPainterRenderer::setScene(NightOpsScene scene) {
  scene_ = std::move(scene);
}

const NightOpsScene& QPainterRenderer::scene() const noexcept {
  return scene_;
}

const NightOpsLayout& QPainterRenderer::layout() const noexcept {
  return layout_;
}

bool QPainterRenderer::hasBuffer() const noexcept {
  return buffer_ != 0 && !image_.isNull();
}

const QImage& QPainterRenderer::image() const noexcept {
  return image_;
}

}  // namespace desktopup::adapters
