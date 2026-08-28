#include "adapters/renderer-qpainter/qpainter_renderer.h"

#include <QGuiApplication>
#include <QImage>
#include <QString>

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <iostream>
#include <string>

namespace {

int failures = 0;

void expect(const bool condition, const char* message) {
  if (!condition) {
    ++failures;
    std::cerr << "QTS04_RENDERER_FAILURE: " << message << '\n';
  }
}

bool outerEdgeIsTransparent(const QImage& image) {
  if (image.isNull()) {
    return false;
  }
  for (int x = 0; x < image.width(); ++x) {
    if (qAlpha(image.pixel(x, 0)) != 0 || qAlpha(image.pixel(x, image.height() - 1)) != 0) {
      return false;
    }
  }
  for (int y = 0; y < image.height(); ++y) {
    if (qAlpha(image.pixel(0, y)) != 0 || qAlpha(image.pixel(image.width() - 1, y)) != 0) {
      return false;
    }
  }
  return true;
}

bool hasOpaqueNeutralStrip(const QImage& image, const int scanDepth) {
  const int depth = std::min(scanDepth, image.height() / 2);
  for (int y = 0; y < depth; ++y) {
    int neutralOpaque = 0;
    for (int x = 0; x < image.width(); ++x) {
      const QColor color = image.pixelColor(x, y);
      const int maximum = std::max({color.red(), color.green(), color.blue()});
      const int minimum = std::min({color.red(), color.green(), color.blue()});
      if (color.alpha() >= 235 && maximum - minimum <= 18 && maximum >= 105) {
        ++neutralOpaque;
      }
    }
    if (neutralOpaque >= static_cast<int>(image.width() * 0.65)) {
      return true;
    }
  }
  for (int y = image.height() - depth; y < image.height(); ++y) {
    int neutralOpaque = 0;
    for (int x = 0; x < image.width(); ++x) {
      const QColor color = image.pixelColor(x, y);
      const int maximum = std::max({color.red(), color.green(), color.blue()});
      const int minimum = std::min({color.red(), color.green(), color.blue()});
      if (color.alpha() >= 235 && maximum - minimum <= 18 && maximum >= 105) {
        ++neutralOpaque;
      }
    }
    if (neutralOpaque >= static_cast<int>(image.width() * 0.65)) {
      return true;
    }
  }
  return false;
}

bool rectInside(const QRectF& inner, const QRectF& outer) {
  return !inner.isEmpty() && outer.adjusted(-0.01, -0.01, 0.01, 0.01).contains(inner);
}

std::string optionValue(int argc, char** argv, const std::string& option) {
  for (int index = 1; index + 1 < argc; ++index) {
    if (argv[index] == option) {
      return argv[index + 1];
    }
  }
  return {};
}

}  // namespace

int main(int argc, char** argv) {
  QGuiApplication application(argc, argv);
  using desktopup::adapters::NightOpsScene;
  using desktopup::adapters::PosterPresentation;
  using desktopup::adapters::QPainterRenderer;

  constexpr desktopup::contracts::Revision revision = 7;
  constexpr desktopup::contracts::SurfaceEpoch surfaceEpoch = 11;
  const auto expandedSize = QPainterRenderer::defaultExpandedSize();

  QPainterRenderer renderer;
  const auto first = renderer.render({revision, surfaceEpoch, expandedSize});
  const auto firstView = renderer.frameView(first.buffer);
  expect(first.buffer != 0, "expanded render must allocate a buffer");
  expect(first.bounds == expandedSize, "frame descriptor must preserve exact requested bounds");
  expect(firstView.valid(), "expanded premultiplied frame view must be valid");
  expect(firstView.bounds == expandedSize, "frame view must preserve exact requested bounds");
  expect(renderer.image().format() == QImage::Format_ARGB32_Premultiplied,
         "renderer must use ARGB32 premultiplied storage");
  expect(renderer.image().bytesPerLine() == static_cast<int>(firstView.strideBytes),
         "frame stride must match the QImage buffer");

  const QImage firstPixels = renderer.image().copy();
  expect(outerEdgeIsTransparent(firstPixels),
         "the complete outer pixel ring must remain alpha zero");
  expect(qAlpha(firstPixels.pixel(1, 1)) == 0 &&
             qAlpha(firstPixels.pixel(firstPixels.width() - 2, 1)) == 0 &&
             qAlpha(firstPixels.pixel(1, firstPixels.height() - 2)) == 0 &&
             qAlpha(firstPixels.pixel(firstPixels.width() - 2,
                                      firstPixels.height() - 2)) == 0,
         "all pixels outside the rounded contour corners must be alpha zero");
  expect(!hasOpaqueNeutralStrip(firstPixels, 32),
         "top and bottom scan zones must not contain a white or gray opaque strip");
  expect(qAlpha(firstPixels.pixel(firstPixels.width() / 2, firstPixels.height() / 2)) > 120,
         "poster interior must be initialized and visibly translucent");

  const auto expandedLayout = renderer.layout();
  expect(expandedLayout.contentVisible, "expanded scene must expose content layout");
  expect(expandedLayout.visibleItemCount == 6 && expandedLayout.columnCount == 3 &&
             expandedLayout.rowCount == 2,
         "the reference frame must expose the complete stable 3x2 fixture");
  expect(expandedLayout.headerBounds.left() > expandedLayout.posterBounds.left(),
         "header must remain inside the visible poster contour");
  for (std::size_t index = 0; index < expandedLayout.cardBounds.size(); ++index) {
    expect(expandedLayout.itemVisible[index],
           "all six reference items must be visible");
    expect(rectInside(expandedLayout.cardBounds[index], expandedLayout.posterBounds),
           "all six item cards must remain inside the poster");
    expect(rectInside(expandedLayout.labelBounds[index], expandedLayout.posterBounds),
           "all six item labels must remain inside the poster");
    expect(expandedLayout.labelBounds[index].bottom() < expandedLayout.footerBounds.top(),
           "all item cards and labels must remain above the footer");
    expect(qAlpha(firstPixels.pixel(static_cast<int>(expandedLayout.cardBounds[index].center().x()),
                                    static_cast<int>(expandedLayout.cardBounds[index].center().y()))) > 0,
           "every item card center must contain initialized pixels");
  }

  QPainterRenderer responsiveRenderer;
  const desktopup::contracts::PixelSize wideShortSize{820, 440};
  responsiveRenderer.render({revision + 1, surfaceEpoch, wideShortSize});
  const auto wideShortLayout = responsiveRenderer.layout();
  expect(wideShortLayout.unit == expandedLayout.unit &&
             wideShortLayout.fontScale == expandedLayout.fontScale,
         "frame resize must not mutate content or typography scale");
  expect(wideShortLayout.visibleItemCount == 6 &&
             wideShortLayout.columnCount == 6 && wideShortLayout.rowCount == 1,
         "a wide short viewport must reflow six fixed-size cards into one row");
  for (std::size_t index = 0; index < wideShortLayout.cardBounds.size(); ++index) {
    expect(wideShortLayout.itemVisible[index],
           "the wide short viewport must keep all fixture items visible");
    expect(std::abs(wideShortLayout.cardBounds[index].width() -
                    expandedLayout.cardBounds[index].width()) < 0.01 &&
               std::abs(wideShortLayout.cardBounds[index].height() -
                        expandedLayout.cardBounds[index].height()) < 0.01,
           "responsive reflow must preserve card metrics");
    expect(rectInside(wideShortLayout.cardBounds[index],
                      wideShortLayout.itemViewportBounds) &&
               rectInside(wideShortLayout.labelBounds[index],
                          wideShortLayout.itemViewportBounds),
           "wide short items must remain wholly inside the item viewport");
    expect(wideShortLayout.labelBounds[index].bottom() <
               wideShortLayout.footerBounds.top(),
           "wide short items must never overlap the footer");
  }

  const desktopup::contracts::PixelSize narrowTallSize{320, 620};
  responsiveRenderer.render({revision + 2, surfaceEpoch, narrowTallSize});
  const auto narrowTallLayout = responsiveRenderer.layout();
  expect(narrowTallLayout.visibleItemCount == 6 &&
             narrowTallLayout.columnCount == 2 && narrowTallLayout.rowCount == 3,
         "a narrow tall viewport must reflow the fixed-size cards into 2x3");
  expect(std::abs(narrowTallLayout.cardBounds[0].width() -
                  expandedLayout.cardBounds[0].width()) < 0.01,
         "narrow reflow must not shrink item metrics");

  const auto minimumSize = QPainterRenderer::minimumExpandedSize();
  responsiveRenderer.render({revision + 3, surfaceEpoch, minimumSize});
  const auto minimumLayout = responsiveRenderer.layout();
  expect(minimumLayout.columnCount == 2 && minimumLayout.rowCount == 3 &&
             minimumLayout.maxItemScrollOffset > 0.0,
         "minimum viewport must retain all six items in a scrollable 2x3 flow");
  for (std::size_t index = 0; index < minimumLayout.cardBounds.size(); ++index) {
    expect(!minimumLayout.cardBounds[index].isEmpty() &&
               !minimumLayout.labelBounds[index].isEmpty(),
           "overflow must never delete item layout geometry");
  }
  expect(!minimumLayout.itemScrollTrackBounds.isEmpty() &&
             !minimumLayout.itemScrollThumbBounds.isEmpty() &&
             !minimumLayout.itemScrollInputBounds.isEmpty(),
         "overflow must expose a visible scrollbar and an independent wheel island");

  auto partialScene = NightOpsScene::fixture();
  partialScene.itemScrollOffsetDip = 40.0;
  responsiveRenderer.setScene(partialScene);
  responsiveRenderer.render({revision + 4, surfaceEpoch, minimumSize});
  const auto partialLayout = responsiveRenderer.layout();
  const qreal completeFirstItemHeight =
      partialLayout.labelBounds[0].bottom() - partialLayout.cardBounds[0].top();
  expect(partialLayout.itemVisible[0] &&
             partialLayout.itemHitBounds[0].height() > 0.0 &&
             partialLayout.itemHitBounds[0].height() < completeFirstItemHeight,
         "a partially intersecting row must remain continuously visible and hittable");
  expect(partialLayout.visibleItemCount > 0 &&
             !partialLayout.itemScrollInputBounds.isEmpty(),
         "intermediate scroll offsets must never create an unscrollable empty viewport");

  auto bottomPartialScene = NightOpsScene::fixture();
  bottomPartialScene.itemScrollOffsetDip = 100.0;
  QPainterRenderer bottomPartialRenderer(bottomPartialScene);
  bottomPartialRenderer.render({revision + 5, surfaceEpoch, minimumSize});
  const auto bottomPartialLayout = bottomPartialRenderer.layout();
  const QImage bottomPartialPixels = bottomPartialRenderer.image().copy();
  expect(bottomPartialLayout.itemVisible[4] &&
             bottomPartialLayout.cardBounds[4].bottom() >
                 bottomPartialLayout.itemViewportBounds.bottom(),
         "the final row fixture must cross the bottom viewport edge for clip regression");

  auto noAccentScene = bottomPartialScene;
  for (auto& item : noAccentScene.items) item.accentRgba = 0x00000000u;
  QPainterRenderer noAccentRenderer(noAccentScene);
  noAccentRenderer.render({revision + 6, surfaceEpoch, minimumSize});
  const QImage noAccentPixels = noAccentRenderer.image().copy();
  std::uint32_t leakedAccentPixels = 0;
  const int leakTop = static_cast<int>(
      std::floor(bottomPartialLayout.itemViewportBounds.bottom())) + 1;
  const int leakBottom = std::min(
      bottomPartialPixels.height() - 1,
      static_cast<int>(std::ceil(bottomPartialLayout.cardBounds[4].bottom())) - 1);
  const int leakLeft = std::max(
      0, static_cast<int>(std::floor(bottomPartialLayout.cardBounds[4].left())));
  const int leakRight = std::min(
      bottomPartialPixels.width() - 1,
      static_cast<int>(std::ceil(bottomPartialLayout.cardBounds[4].right())) - 1);
  for (int y = leakTop; y <= leakBottom; ++y) {
    for (int x = leakLeft; x <= leakRight; ++x) {
      if (bottomPartialPixels.pixel(x, y) != noAccentPixels.pixel(x, y)) {
        ++leakedAccentPixels;
      }
    }
  }
  expect(leakedAccentPixels == 0,
         "card-local clipping must intersect the item viewport and never leak accent pixels into the footer");

  auto scrolledScene = NightOpsScene::fixture();
  scrolledScene.itemScrollOffsetDip = 10000.0;
  responsiveRenderer.setScene(scrolledScene);
  responsiveRenderer.render({revision + 5, surfaceEpoch, minimumSize});
  const auto scrolledMinimumLayout = responsiveRenderer.layout();
  expect(std::abs(scrolledMinimumLayout.itemScrollOffset -
                  scrolledMinimumLayout.maxItemScrollOffset) < 0.01,
         "scroll offset must clamp to the complete overflow range");
  expect(scrolledMinimumLayout.itemVisible[4] &&
             scrolledMinimumLayout.itemVisible[5],
         "the final item row must be reachable through the V1 overflow viewport");

  auto dpiScene = NightOpsScene::fixture();
  dpiScene.deviceScale = 1.25;
  QPainterRenderer dpiRenderer(dpiScene);
  dpiRenderer.render({revision + 6, surfaceEpoch, {525, 775}});
  const auto dpiLayout = dpiRenderer.layout();
  expect(std::abs(dpiLayout.unit - 1.25) < 0.001 &&
             dpiLayout.visibleItemCount == 6 && dpiLayout.columnCount == 3,
         "125 percent DPI must scale fixed content independently of frame resize");
  expect(std::abs(dpiLayout.cardBounds[0].width() -
                  expandedLayout.cardBounds[0].width() * 1.25) < 0.01,
         "device scale must be the only automatic metric scale");
  dpiRenderer.render({revision + 7, surfaceEpoch, {1025, 550}});
  expect(std::abs(dpiRenderer.layout().cardBounds[0].width() -
                  dpiLayout.cardBounds[0].width()) < 0.01,
         "resizing at fixed DPI must preserve item metrics");

  const auto second = renderer.render({revision, surfaceEpoch, expandedSize});
  expect(second.buffer != first.buffer, "replacement render must allocate a new buffer identity");
  expect(!renderer.frameView(first.buffer).valid(),
         "a replaced buffer identity must become invalid immediately");
  expect(renderer.image() == firstPixels,
         "identical scene and bounds must produce deterministic pixels");

  auto lockedScene = NightOpsScene::fixture();
  lockedScene.locked = true;
  renderer.setScene(lockedScene);
  renderer.render({revision + 1, surfaceEpoch, expandedSize});
  expect(renderer.image() != firstPixels, "locked and unlocked header glyphs must differ visually");
  expect(renderer.layout().headerBounds == expandedLayout.headerBounds,
         "lock state must not move the title bar layout");

  auto collapsedScene = NightOpsScene::fixture();
  collapsedScene.presentation = PosterPresentation::Collapsed;
  renderer.setScene(collapsedScene);
  const desktopup::contracts::PixelSize collapsedSize{
      expandedSize.width, QPainterRenderer::collapsedHeightForScale(1.0)};
  const auto collapsed = renderer.render({revision + 2, surfaceEpoch, collapsedSize});
  const QImage collapsedPixels = renderer.image().copy();
  expect(collapsed.bounds.width == expandedSize.width,
         "collapse must preserve the expanded poster width");
  expect(collapsed.bounds.height == 60,
         "the 420-pixel reference poster must collapse to the frozen 60-pixel height");
  expect(QPainterRenderer::collapsedHeightForScale(1.25) == 75,
         "collapsed height must follow DPI/content scale instead of frame width");
  expect(!renderer.layout().contentVisible,
         "collapsed scene must not expose content or item card layout");
  expect(renderer.layout().headerBounds.left() == expandedLayout.headerBounds.left() &&
             renderer.layout().headerBounds.top() == expandedLayout.headerBounds.top() &&
             renderer.layout().headerBounds.width() == expandedLayout.headerBounds.width(),
         "collapse must preserve title bar x/y/width geometry");
  expect(outerEdgeIsTransparent(collapsedPixels),
         "collapsed frame must preserve a transparent outer pixel ring");
  expect(!hasOpaqueNeutralStrip(collapsedPixels, 24),
         "collapsed frame must not introduce a white or gray opaque strip");

  const auto collapsedBuffer = collapsed.buffer;
  renderer.release(collapsedBuffer);
  expect(!renderer.hasBuffer() && !renderer.frameView(collapsedBuffer).valid(),
         "release must invalidate the active buffer and pixels");
  renderer.beginExit();
  expect(renderer.render({revision + 3, surfaceEpoch, expandedSize}).buffer == 0,
         "renderer must reject new buffers after beginExit");

  const std::string exportPath = optionValue(argc, argv, "--export-png");
  if (!exportPath.empty()) {
    expect(firstPixels.save(QString::fromStdString(exportPath), "PNG"),
           "expanded NIGHT OPS reference PNG must be exportable");
  }

  if (failures != 0) {
    return 1;
  }
  std::cout << "QTS04_NIGHT_OPS_RENDERER_PASS"
            << " expanded=" << expandedSize.width << 'x' << expandedSize.height
            << " collapsed=" << collapsedSize.width << 'x' << collapsedSize.height
            << " cards=6 edgeAlpha=0 deterministic=1\n";
  return 0;
}
