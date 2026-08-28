#pragma once

#include "adapters/renderer-qpainter/qpainter_renderer.h"

#include <QImage>

#include <memory>

namespace desktopup::adapters {

// Bounded Qt Quick adapter probe. It deliberately preserves the existing
// IRenderer, NightOpsScene, and NightOpsLayout contracts so Runtime and the
// Win32 DesktopHost do not gain a second geometry or hit-testing protocol.
class QtQuickRenderer final : public contracts::IRenderer {
 public:
  QtQuickRenderer();
  explicit QtQuickRenderer(NightOpsScene scene);
  ~QtQuickRenderer() override;

  QtQuickRenderer(const QtQuickRenderer&) = delete;
  QtQuickRenderer& operator=(const QtQuickRenderer&) = delete;

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
  [[nodiscard]] bool ready() const noexcept;
  [[nodiscard]] const QString& errorString() const noexcept;

  [[nodiscard]] static constexpr contracts::PixelSize defaultExpandedSize() noexcept {
    return QPainterRenderer::defaultExpandedSize();
  }
  [[nodiscard]] static constexpr contracts::PixelSize minimumExpandedSize() noexcept {
    return QPainterRenderer::minimumExpandedSize();
  }
  [[nodiscard]] static std::uint32_t collapsedHeightForScale(qreal scale) noexcept {
    return QPainterRenderer::collapsedHeightForScale(scale);
  }

 private:
  struct Impl;

  [[nodiscard]] bool ensureInitialized();
  [[nodiscard]] bool updateSceneGraph(const contracts::PixelSize& size);
  void captureLayout(const contracts::PixelSize& size);

  NightOpsScene scene_;
  NightOpsLayout layout_;
  QImage image_;
  contracts::BufferHandle buffer_{};
  contracts::BufferHandle nextBuffer_{1};
  bool exiting_{false};
  std::unique_ptr<Impl> impl_;
};

}  // namespace desktopup::adapters
