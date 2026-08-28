#include "adapters/renderer-qtquick/qtquick_renderer.h"

#include <QAbstractListModel>
#include <QColor>
#include <QCoreApplication>
#include <QHash>
#include <QQmlComponent>
#include <QQmlContext>
#include <QQmlEngine>
#include <QQuickItem>
#include <QQuickRenderControl>
#include <QQuickRenderTarget>
#include <QQuickWindow>
#include <QSGRendererInterface>
#include <QStringList>
#include <QUrl>
#include <QVariant>

#include <algorithm>
#include <array>
#include <cmath>
#include <utility>

namespace desktopup::adapters {
namespace {

QString text(const std::string& value) {
  return QString::fromUtf8(value.data(), static_cast<qsizetype>(value.size()));
}

class NightOpsItemModel final : public QAbstractListModel {
  Q_OBJECT
  Q_PROPERTY(int count READ count CONSTANT)

 public:
  enum Role {
    InitialsRole = Qt::UserRole + 1,
    TitleRole,
    AccentRole,
  };

  explicit NightOpsItemModel(QObject* parent = nullptr)
      : QAbstractListModel(parent) {}

  [[nodiscard]] int count() const noexcept {
    return static_cast<int>(items_.size());
  }

  int rowCount(const QModelIndex& parent = {}) const override {
    return parent.isValid() ? 0 : count();
  }

  QVariant data(const QModelIndex& index, const int role) const override {
    if (!index.isValid() || index.row() < 0 || index.row() >= count()) {
      return {};
    }
    const auto& item = items_[static_cast<std::size_t>(index.row())];
    switch (role) {
      case InitialsRole:
        return text(item.initials);
      case TitleRole:
        return text(item.title);
      case AccentRole:
        return QColor::fromRgba(item.accentRgba);
      default:
        return {};
    }
  }

  QHash<int, QByteArray> roleNames() const override {
    return {
        {InitialsRole, QByteArrayLiteral("initials")},
        {TitleRole, QByteArrayLiteral("itemTitle")},
        {AccentRole, QByteArrayLiteral("accentColor")},
    };
  }

  void setItems(const std::array<NightOpsItemVisual, 6>& items) {
    const bool unchanged = std::equal(
        items_.begin(), items_.end(), items.begin(), items.end(),
        [](const NightOpsItemVisual& left, const NightOpsItemVisual& right) {
          return left.initials == right.initials &&
                 left.title == right.title &&
                 left.accentRgba == right.accentRgba;
        });
    if (unchanged) return;
    beginResetModel();
    items_ = items;
    endResetModel();
  }

 private:
  std::array<NightOpsItemVisual, 6> items_{};
};

QRectF itemRect(QQuickItem* root, const QString& objectName) {
  if (root == nullptr) return {};
  auto* item = root->findChild<QQuickItem*>(objectName, Qt::FindChildrenRecursively);
  if (item == nullptr && root->window() != nullptr) {
    item = root->window()->contentItem()->findChild<QQuickItem*>(
        objectName, Qt::FindChildrenRecursively);
  }
  if (item == nullptr) return {};
  return item->mapRectToItem(root, item->boundingRect());
}

qreal realProperty(const QObject* object, const char* name, const qreal fallback = 0.0) {
  if (object == nullptr) return fallback;
  bool ok = false;
  const qreal value = object->property(name).toReal(&ok);
  return ok && std::isfinite(value) ? value : fallback;
}

std::uint32_t unsignedProperty(const QObject* object,
                               const char* name,
                               const std::uint32_t fallback = 0) {
  if (object == nullptr) return fallback;
  bool ok = false;
  const auto value = object->property(name).toUInt(&ok);
  return ok ? value : fallback;
}

bool configureSoftwareSceneGraph() {
  QQuickWindow::setGraphicsApi(QSGRendererInterface::Software);
  return true;
}

}  // namespace

struct QtQuickRenderer::Impl {
  bool backendConfigured{configureSoftwareSceneGraph()};
  NightOpsItemModel model;
  QQuickRenderControl renderControl;
  QQuickWindow window{&renderControl};
  QQmlEngine engine;
  QQuickItem* root{};
  QString error;
  bool initialized{};

  Impl() {
    Q_UNUSED(backendConfigured);
    window.setColor(Qt::transparent);
    engine.rootContext()->setContextProperty(
        QStringLiteral("_desktopUpItemModel"), &model);

    QQmlComponent component(
        &engine, QUrl(QStringLiteral("qrc:/DesktopUp/Renderer/NightOpsPoster.qml")));
    if (component.status() == QQmlComponent::Error) {
      const auto errors = component.errors();
      for (const auto& entry : errors) {
        if (!error.isEmpty()) error.append(QLatin1Char('\n'));
        error.append(entry.toString());
      }
      return;
    }

    QObject* created = component.create();
    root = qobject_cast<QQuickItem*>(created);
    if (root == nullptr) {
      error = QStringLiteral("NightOpsPoster.qml did not create a QQuickItem");
      delete created;
      return;
    }
    root->setParent(window.contentItem());
    root->setParentItem(window.contentItem());

  }

  ~Impl() {
    if (initialized) renderControl.invalidate();
  }
};

QtQuickRenderer::QtQuickRenderer()
    : QtQuickRenderer(NightOpsScene::fixture()) {}

QtQuickRenderer::QtQuickRenderer(NightOpsScene scene)
    : scene_(std::move(scene)), impl_(std::make_unique<Impl>()) {}

QtQuickRenderer::~QtQuickRenderer() = default;

bool QtQuickRenderer::ensureInitialized() {
  return !exiting_ && impl_ != nullptr && impl_->root != nullptr;
}

bool QtQuickRenderer::updateSceneGraph(const contracts::PixelSize& size) {
  if (!ensureInitialized() || size.width == 0 || size.height == 0) return false;

  impl_->model.setItems(scene_.items);
  auto* root = impl_->root;
  root->setSize(QSizeF(size.width, size.height));
  impl_->window.setGeometry(0, 0, static_cast<int>(size.width),
                            static_cast<int>(size.height));
  impl_->window.contentItem()->setSize(QSizeF(size.width, size.height));

  root->setProperty("expanded",
                    scene_.presentation == PosterPresentation::Expanded);
  root->setProperty("locked", scene_.locked);
  root->setProperty("selectedItem", scene_.selectedItem);
  root->setProperty("deviceScale", scene_.deviceScale);
  root->setProperty("contentScale", scene_.contentScale);
  root->setProperty("scrollOffsetDip", scene_.itemScrollOffsetDip);
  root->setProperty("collectionText", text(scene_.collection));
  root->setProperty("archiveText", text(scene_.archive));
  root->setProperty("titleText", text(scene_.title));
  root->setProperty("statusText", text(scene_.status));
  root->setProperty("footerLeftText", text(scene_.footerLeft));
  root->setProperty("footerRightText", text(scene_.footerRight));

  const QSize targetSize(static_cast<int>(size.width),
                         static_cast<int>(size.height));
  if (image_.size() != targetSize ||
      image_.format() != QImage::Format_ARGB32_Premultiplied) {
    image_ = QImage(targetSize, QImage::Format_ARGB32_Premultiplied);
    if (image_.isNull()) return false;
    image_.fill(Qt::transparent);
    impl_->window.setRenderTarget(QQuickRenderTarget::fromPaintDevice(&image_));
  }
  if (!impl_->initialized) {
    // Qt 6.8 explicitly requires that initialize(), beginFrame(), and
    // endFrame() are not called for the software adaptation.
    impl_->initialized = true;
  }

  auto* viewport = impl_->root->findChild<QQuickItem*>(
      QStringLiteral("itemViewportBounds"), Qt::FindChildrenRecursively);
  if (viewport != nullptr) {
    QMetaObject::invokeMethod(viewport, "forceLayout", Qt::DirectConnection);
  }
  // The first pass creates or reuses GridView delegates. A second forced
  // layout/sync pass makes delegate geometry and pixels part of this same
  // returned frame rather than leaking framework incubation into the next
  // Host present.
  for (int pass = 0; pass < 2; ++pass) {
    impl_->renderControl.polishItems();
    static_cast<void>(impl_->renderControl.sync());
    impl_->renderControl.render();
    if (viewport != nullptr) {
      QMetaObject::invokeMethod(viewport, "forceLayout", Qt::DirectConnection);
    }
  }
  captureLayout(size);
  return !image_.isNull();
}

void QtQuickRenderer::captureLayout(const contracts::PixelSize& size) {
  layout_ = {};
  auto* root = impl_ != nullptr ? impl_->root : nullptr;
  if (root == nullptr) return;

  layout_.posterBounds = itemRect(root, QStringLiteral("posterFrame"));
  layout_.headerBounds = itemRect(root, QStringLiteral("headerBounds"));
  layout_.titleDragBounds = itemRect(root, QStringLiteral("titleDragBounds"));
  layout_.lockButtonBounds = itemRect(root, QStringLiteral("lockButtonBounds"));
  layout_.collapseButtonBounds = itemRect(root, QStringLiteral("collapseButtonBounds"));
  layout_.contentBounds = itemRect(root, QStringLiteral("contentBounds"));
  layout_.itemViewportBounds = itemRect(root, QStringLiteral("itemViewportBounds"));
  layout_.resizeCornerBounds = itemRect(root, QStringLiteral("resizeCornerBounds"));
  layout_.footerBounds = itemRect(root, QStringLiteral("footerBounds"));

  layout_.unit = realProperty(root, "unit", 1.0);
  layout_.deviceScale = std::clamp(scene_.deviceScale, 0.5, 4.0);
  layout_.contentScale = std::clamp(scene_.contentScale, 0.5, 3.0);
  layout_.fontScale = realProperty(root, "fontScale", layout_.unit);
  layout_.cornerRadius =
      realProperty(root->findChild<QObject*>(QStringLiteral("posterFrame")),
                   "radius",
                   24.0 * layout_.fontScale);
  layout_.contentVisible = root->property("contentVisible").toBool();
  layout_.columnCount = unsignedProperty(root, "columnCount");
  layout_.rowCount = unsignedProperty(root, "rowCount");
  layout_.itemRowStride = realProperty(root, "cellHeightPx");
  layout_.itemContentHeight = realProperty(root, "itemContentHeightPx");
  layout_.itemScrollOffset = realProperty(root, "actualScrollPx");
  layout_.maxItemScrollOffset = realProperty(root, "maximumScrollPx");

  const bool overflow = root->property("overflow").toBool();
  if (overflow) {
    layout_.itemScrollTrackBounds =
        itemRect(root, QStringLiteral("itemScrollTrackBounds"));
    layout_.itemScrollThumbBounds =
        itemRect(root, QStringLiteral("itemScrollThumbBounds"));
    layout_.itemScrollInputBounds =
        itemRect(root, QStringLiteral("itemScrollInputBounds"));
  }

  for (std::size_t index = 0; index < scene_.items.size(); ++index) {
    QVariant delegateResult;
    const bool invoked = QMetaObject::invokeMethod(
        root,
        "delegateAt",
        Qt::DirectConnection,
        Q_RETURN_ARG(QVariant, delegateResult),
        Q_ARG(QVariant, QVariant::fromValue(static_cast<int>(index))));
    auto* delegate = invoked
                         ? qobject_cast<QQuickItem*>(delegateResult.value<QObject*>())
                         : nullptr;
    if (delegate != nullptr) {
      const QRectF delegateBounds =
          delegate->mapRectToItem(root, delegate->boundingRect());
      const qreal cardHeight = realProperty(root, "cardHeightPx");
      const qreal labelGap = realProperty(root, "labelGapPx");
      const qreal labelHeight = realProperty(root, "labelHeightPx");
      layout_.cardBounds[index] =
          QRectF(delegateBounds.left(), delegateBounds.top(),
                 delegateBounds.width(), cardHeight);
      layout_.labelBounds[index] =
          QRectF(delegateBounds.left(), delegateBounds.top() + cardHeight + labelGap,
                 delegateBounds.width(), labelHeight);
    }
    QRectF complete;
    if (!layout_.cardBounds[index].isEmpty()) {
      complete = layout_.cardBounds[index].united(layout_.labelBounds[index]);
    }
    layout_.itemHitBounds[index] =
        complete.intersected(layout_.itemViewportBounds);
    layout_.itemVisible[index] = layout_.contentVisible &&
                                 !layout_.itemHitBounds[index].isEmpty();
    if (layout_.itemVisible[index]) ++layout_.visibleItemCount;
  }

  if (layout_.posterBounds.isEmpty()) {
    layout_.posterBounds =
        QRectF(2.0, 2.0, std::max(0.0, static_cast<double>(size.width) - 4.0),
               std::max(0.0, static_cast<double>(size.height) - 4.0));
  }
}

contracts::FrameDescriptor QtQuickRenderer::render(
    const contracts::RenderRequest& request) {
  if (exiting_ || request.revision == 0 || request.surfaceEpoch == 0 ||
      request.bounds.width == 0 || request.bounds.height == 0 ||
      !updateSceneGraph(request.bounds)) {
    return {};
  }
  buffer_ = nextBuffer_++;
  return {request.revision, request.surfaceEpoch, request.bounds, buffer_};
}

contracts::PremultipliedFrameView QtQuickRenderer::frameView(
    const contracts::BufferHandle buffer) const noexcept {
  if (buffer == 0 || buffer != buffer_ || image_.isNull()) return {};
  return {
      buffer_,
      {static_cast<std::uint32_t>(image_.width()),
       static_cast<std::uint32_t>(image_.height())},
      static_cast<std::uint32_t>(image_.bytesPerLine()),
      image_.constBits(),
  };
}

void QtQuickRenderer::release(const contracts::BufferHandle buffer) noexcept {
  if (buffer == 0 || buffer != buffer_) return;
  buffer_ = 0;
  image_ = {};
}

void QtQuickRenderer::beginExit() noexcept {
  exiting_ = true;
  buffer_ = 0;
  image_ = {};
  layout_ = {};
  impl_.reset();
}

void QtQuickRenderer::setScene(NightOpsScene scene) {
  if (!exiting_) scene_ = std::move(scene);
}

const NightOpsScene& QtQuickRenderer::scene() const noexcept {
  return scene_;
}

const NightOpsLayout& QtQuickRenderer::layout() const noexcept {
  return layout_;
}

bool QtQuickRenderer::hasBuffer() const noexcept {
  return buffer_ != 0 && !image_.isNull();
}

const QImage& QtQuickRenderer::image() const noexcept {
  return image_;
}

bool QtQuickRenderer::ready() const noexcept {
  return impl_ != nullptr && impl_->root != nullptr && impl_->error.isEmpty();
}

const QString& QtQuickRenderer::errorString() const noexcept {
  static const QString empty;
  return impl_ != nullptr ? impl_->error : empty;
}

}  // namespace desktopup::adapters

#include "qtquick_renderer.moc"
