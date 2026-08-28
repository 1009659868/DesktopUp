#include "adapters/renderer-qtquick/qtquick_renderer.h"

#include <QGuiApplication>
#include <QImage>

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <iostream>

namespace {

int failures{};

void expect(const bool condition, const char* message) {
  if (!condition) {
    ++failures;
    std::cerr << "QTS04_QTQUICK_RENDERER_FAILURE: " << message << '\n';
  }
}

bool outerEdgeIsTransparent(const QImage& image) {
  if (image.isNull()) return false;
  for (int x = 0; x < image.width(); ++x) {
    if (qAlpha(image.pixel(x, 0)) != 0 ||
        qAlpha(image.pixel(x, image.height() - 1)) != 0) {
      return false;
    }
  }
  for (int y = 0; y < image.height(); ++y) {
    if (qAlpha(image.pixel(0, y)) != 0 ||
        qAlpha(image.pixel(image.width() - 1, y)) != 0) {
      return false;
    }
  }
  return true;
}

}  // namespace

int main(int argc, char** argv) {
  QGuiApplication application(argc, argv);
  using namespace desktopup;

  adapters::QtQuickRenderer renderer;
  expect(renderer.ready(), "QQuickRenderControl and QML component must initialize");
  if (!renderer.ready()) {
    std::cerr << renderer.errorString().toStdString() << '\n';
    return 1;
  }

  constexpr contracts::Revision revision = 31;
  constexpr contracts::SurfaceEpoch epoch = 41;
  const auto referenceSize = adapters::QtQuickRenderer::defaultExpandedSize();
  const auto first = renderer.render({revision, epoch, referenceSize});
  const auto firstView = renderer.frameView(first.buffer);
  if (first.buffer == 0) {
    std::cerr << "QTS04_QTQUICK_RENDERER_DIAGNOSTIC: "
              << renderer.errorString().toStdString() << '\n';
  }
  expect(first.buffer != 0 && firstView.valid(),
         "Qt Quick adapter must publish a premultiplied frame");
  expect(renderer.image().format() == QImage::Format_ARGB32_Premultiplied,
         "software scene graph target must preserve ARGB32 premultiplied storage");
  expect(outerEdgeIsTransparent(renderer.image()),
         "Qt Quick poster must preserve a fully transparent outer pixel ring");
  expect(renderer.layout().contentVisible &&
             renderer.layout().columnCount == 3 &&
             renderer.layout().rowCount == 2 &&
             renderer.layout().visibleItemCount == 6,
         "reference frame must use GridView as a complete 3x2 six-item view");
  for (std::size_t index = 0; index < renderer.layout().cardBounds.size(); ++index) {
    expect(!renderer.layout().cardBounds[index].isEmpty() &&
               !renderer.layout().labelBounds[index].isEmpty(),
           "GridView delegate geometry must be projected for every fixture item");
  }

  const auto firstPixels = renderer.image().copy();
  const auto second = renderer.render({revision + 1, epoch, referenceSize});
  expect(second.buffer != first.buffer && renderer.image() == firstPixels,
         "identical Qt Quick scene renders must be deterministic and replace buffer identity");

  constexpr contracts::PixelSize wideShort{820, 440};
  renderer.render({revision + 2, epoch, wideShort});
  expect(renderer.layout().columnCount == 6 &&
             renderer.layout().rowCount == 1 &&
             renderer.layout().visibleItemCount == 6,
         "GridView must reflow the fixed metrics into one row without deleting items");

  constexpr contracts::PixelSize narrowTall{320, 620};
  renderer.render({revision + 3, epoch, narrowTall});
  expect(renderer.layout().columnCount == 2 &&
             renderer.layout().rowCount == 3 &&
             renderer.layout().visibleItemCount == 6,
         "GridView must reflow the same six delegates into a continuous 2x3 view");

  const auto minimum = adapters::QtQuickRenderer::minimumExpandedSize();
  renderer.render({revision + 4, epoch, minimum});
  const auto minimumLayout = renderer.layout();
  expect(minimumLayout.columnCount == 2 &&
             minimumLayout.rowCount == 3 &&
             minimumLayout.maxItemScrollOffset > 0.0,
         "minimum frame must retain all six model rows behind a continuous Flickable viewport");
  expect(!minimumLayout.itemScrollTrackBounds.isEmpty() &&
             !minimumLayout.itemScrollThumbBounds.isEmpty() &&
             !minimumLayout.itemScrollInputBounds.isEmpty(),
         "overflow must expose a framework-backed scrollbar and independent wheel strip");

  auto partial = adapters::NightOpsScene::fixture();
  partial.itemScrollOffsetDip = 40.0;
  renderer.setScene(partial);
  renderer.render({revision + 5, epoch, minimum});
  expect(renderer.layout().itemVisible[0] &&
             renderer.layout().itemHitBounds[0].height() > 0.0 &&
             renderer.layout().itemHitBounds[0].height() <
                 renderer.layout().cardBounds[0].height(),
         "Flickable contentY must reveal partial rows continuously instead of row-snapping");

  partial.itemScrollOffsetDip = 10000.0;
  renderer.setScene(partial);
  renderer.render({revision + 6, epoch, minimum});
  expect(std::abs(renderer.layout().itemScrollOffset -
                  renderer.layout().maxItemScrollOffset) < 0.01,
         "Qt Quick scroll offset must clamp to the complete content extent");
  expect(renderer.layout().itemVisible[4] && renderer.layout().itemVisible[5],
         "the final model row must remain reachable after clamping");

  // Exercise both column thresholds repeatedly. Rendering must remain valid on
  // every threshold crossing; the manual gate checks whether Host presentation
  // remains visually atomic while this loop is driven by pointer resize.
  const contracts::PixelSize reflowSizes[] = {
      {420, 620}, {319, 620}, {320, 620}, {570, 620},
      {569, 620}, {820, 440}, {420, 620},
  };
  for (std::size_t index = 0; index < std::size(reflowSizes); ++index) {
    const auto frame = renderer.render(
        {revision + 10 + static_cast<contracts::Revision>(index), epoch,
         reflowSizes[index]});
    expect(frame.buffer != 0 && renderer.frameView(frame.buffer).valid(),
           "every dynamic-column threshold frame must remain renderable");
  }

  auto collapsedScene = adapters::NightOpsScene::fixture();
  collapsedScene.presentation = adapters::PosterPresentation::Collapsed;
  renderer.setScene(collapsedScene);
  const contracts::PixelSize collapsedSize{
      referenceSize.width,
      adapters::QtQuickRenderer::collapsedHeightForScale(1.0)};
  renderer.render({revision + 30, epoch, collapsedSize});
  expect(!renderer.layout().contentVisible &&
             renderer.layout().headerBounds.width() > 0.0 &&
             outerEdgeIsTransparent(renderer.image()),
         "collapsed Qt Quick scene must preserve the stable title bar and transparent edge");

  const auto activeBuffer = renderer.frameView(renderer.render(
      {revision + 31, epoch, referenceSize}).buffer).buffer;
  renderer.release(activeBuffer);
  expect(!renderer.hasBuffer() && !renderer.frameView(activeBuffer).valid(),
         "release must invalidate the active Qt Quick buffer");
  renderer.beginExit();
  expect(renderer.render({revision + 32, epoch, referenceSize}).buffer == 0,
         "Qt Quick adapter must reject frames after beginExit");

  if (failures == 0) {
    std::cout << "QTS04_QTQUICK_RENDERER_PASS"
              << " reference=420x620 cards=6 grid=3x2"
              << " continuousScroll=1 dynamicColumns=1\n";
  }
  return failures == 0 ? 0 : 1;
}
