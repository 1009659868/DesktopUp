#pragma once

#include "contracts/ports.h"

#include <QImage>
#include <QRectF>

#include <array>
#include <cstdint>
#include <string>

namespace desktopup::adapters {

enum class PosterPresentation : std::uint8_t {
  Expanded,
  Collapsed,
};

struct NightOpsItemVisual {
  std::string initials;
  std::string title;
  std::uint32_t accentRgba{};
};

struct NightOpsScene {
  PosterPresentation presentation{PosterPresentation::Expanded};
  std::int32_t selectedItem{-1};
  bool locked{false};
  qreal deviceScale{1.0};
  qreal contentScale{1.0};
  qreal itemScrollOffsetDip{0.0};
  std::string collection;
  std::string archive;
  std::string title;
  std::string status;
  std::string footerLeft;
  std::string footerRight;
  std::array<NightOpsItemVisual, 6> items{};

  [[nodiscard]] static NightOpsScene fixture();
};

struct NightOpsLayout {
  QRectF posterBounds;
  QRectF headerBounds;
  QRectF titleDragBounds;
  QRectF lockButtonBounds;
  QRectF collapseButtonBounds;
  QRectF contentBounds;
  QRectF itemViewportBounds;
  QRectF itemScrollTrackBounds;
  QRectF itemScrollThumbBounds;
  QRectF itemScrollInputBounds;
  QRectF resizeCornerBounds;
  QRectF footerBounds;
  std::array<QRectF, 6> cardBounds{};
  std::array<QRectF, 6> labelBounds{};
  std::array<QRectF, 6> itemHitBounds{};
  std::array<bool, 6> itemVisible{};
  std::uint32_t columnCount{};
  std::uint32_t rowCount{};
  std::uint32_t visibleItemCount{};
  qreal itemRowStride{};
  qreal itemContentHeight{};
  qreal itemScrollOffset{};
  qreal maxItemScrollOffset{};
  qreal unit{1.0};
  qreal deviceScale{1.0};
  qreal contentScale{1.0};
  qreal fontScale{1.0};
  qreal cornerRadius{24.0};
  bool contentVisible{false};
};

class QPainterRenderer final : public contracts::IRenderer {
 public:
  QPainterRenderer();
  explicit QPainterRenderer(NightOpsScene scene);

  contracts::FrameDescriptor render(const contracts::RenderRequest& request) override;
  contracts::PremultipliedFrameView frameView(
      contracts::BufferHandle buffer) const noexcept override;
  void release(contracts::BufferHandle buffer) noexcept override;
  void beginExit() noexcept override;

  void setScene(NightOpsScene scene);
  [[nodiscard]] const NightOpsScene& scene() const noexcept;
  [[nodiscard]] const NightOpsLayout& layout() const noexcept;
  [[nodiscard]] bool hasBuffer() const noexcept;
  [[nodiscard]] const QImage& image() const noexcept;

  [[nodiscard]] static constexpr contracts::PixelSize defaultExpandedSize() noexcept {
    return {420, 620};
  }
  [[nodiscard]] static constexpr contracts::PixelSize minimumExpandedSize() noexcept {
    return {320, 420};
  }
  [[nodiscard]] static std::uint32_t collapsedHeightForScale(qreal scale) noexcept;

 private:
  [[nodiscard]] static NightOpsLayout calculateLayout(
      const contracts::PixelSize& size,
      const NightOpsScene& scene) noexcept;
  static void paintNightOps(QImage& image,
                            const NightOpsScene& scene,
                            const NightOpsLayout& layout);

  NightOpsScene scene_;
  NightOpsLayout layout_;
  QImage image_;
  contracts::BufferHandle buffer_{};
  contracts::BufferHandle nextBuffer_{1};
  bool exiting_{false};
};

}  // namespace desktopup::adapters
