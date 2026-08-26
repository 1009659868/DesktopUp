#include "desktop_host_contract.h"

#include <QCoreApplication>
#include <QDateTime>
#include <QDir>
#include <QElapsedTimer>
#include <QFile>
#include <QFileInfo>
#include <QGuiApplication>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QPainter>
#include <QQuickWindow>
#include <QSaveFile>
#include <QTimer>
#include <QSGRendererInterface>

#include <windows.h>
#include <windowsx.h>
#include <shellapi.h>

#include <algorithm>
#include <cstring>
#include <functional>
#include <iostream>
#include <memory>
#include <optional>
#include <utility>
#include <vector>

namespace {

using desktopup::tsc01::DesktopHostCoordinator;
using desktopup::tsc01::QtQuickSurfaceAdapter;

struct Options {
    bool runFixture = false;
    bool runR4Manual = false;
    bool runR4CManual = false;
    bool runR4DManual = false;
    bool runR4D2Manual = false;
    bool runR4D3Manual = false;
    bool runR4D4Installed = false;
    bool r4d4Autostart = false;
    bool r4d4RequestExit = false;
    bool r4d2SelfTest = false;
    bool r4d3SelfTest = false;
    bool r3Diagnostics = false;
    bool injectSurfaceFault = false;
    int durationMs = 1500;
    QString logPath;
    QString r3ReadyMarkerPath;
};

bool parseOptions(const QStringList& arguments, Options* options)
{
    if (arguments.size() == 1) {
        options->runR4D4Installed = true;
        return true;
    }
    for (int index = 1; index < arguments.size(); ++index) {
        const QString argument = arguments.at(index);
        if (argument == QStringLiteral("--run-r2-fixture")) {
            options->runFixture = true;
        } else if (argument == QStringLiteral("--run-r4-manual")) {
            options->runR4Manual = true;
        } else if (argument == QStringLiteral("--run-r4c-manual")) {
            options->runR4CManual = true;
        } else if (argument == QStringLiteral("--run-r4d-manual")) {
            options->runR4DManual = true;
        } else if (argument == QStringLiteral("--run-r4d2-manual")) {
            options->runR4D2Manual = true;
        } else if (argument == QStringLiteral("--run-r4d3-manual")) {
            options->runR4D3Manual = true;
        } else if (argument == QStringLiteral("--autostart")) {
            options->runR4D4Installed = true;
            options->r4d4Autostart = true;
        } else if (argument == QStringLiteral("--request-exit")) {
            options->r4d4RequestExit = true;
        } else if (argument == QStringLiteral("--r4d2-self-test")) {
            options->r4d2SelfTest = true;
        } else if (argument == QStringLiteral("--r4d3-self-test")) {
            options->r4d3SelfTest = true;
        } else if (argument == QStringLiteral("--r3-diagnostics")) {
            options->r3Diagnostics = true;
        } else if (argument == QStringLiteral("--inject-surface-fault")) {
            options->injectSurfaceFault = true;
        } else if (argument == QStringLiteral("--duration-ms") && index + 1 < arguments.size()) {
            bool valid = false;
            const int value = arguments.at(++index).toInt(&valid);
            const int maximumDuration =
                (options->runR4Manual || options->runR4CManual ||
                 options->runR4DManual || options->runR4D2Manual ||
                 options->runR4D3Manual)
                ? 900000
                : 60000;
            if (!valid || value < 250 || value > maximumDuration) return false;
            options->durationMs = value;
        } else if (argument == QStringLiteral("--log") && index + 1 < arguments.size()) {
            options->logPath = arguments.at(++index);
        } else if (argument == QStringLiteral("--r3-ready-marker") && index + 1 < arguments.size()) {
            options->r3ReadyMarkerPath = arguments.at(++index);
        } else {
            return false;
        }
    }
    const int modeCount =
        (options->runFixture ? 1 : 0) +
        (options->runR4Manual ? 1 : 0) +
        (options->runR4CManual ? 1 : 0) +
        (options->runR4DManual ? 1 : 0) +
        (options->runR4D2Manual ? 1 : 0) +
        (options->runR4D3Manual ? 1 : 0) +
        (options->runR4D4Installed ? 1 : 0) +
        (options->r4d4RequestExit ? 1 : 0);
    return modeCount == 1 &&
           (!options->injectSurfaceFault || options->r3Diagnostics) &&
           (!options->r4d2SelfTest ||
            (options->runR4D2Manual && options->r3Diagnostics)) &&
           (!options->r4d3SelfTest ||
            (options->runR4D3Manual && options->r3Diagnostics)) &&
           (options->r3ReadyMarkerPath.isEmpty() || options->r3Diagnostics);
}

struct HitRegion {
    QString id;
    QString role;
    QRect localRect;
    int priority = 0;
};

QJsonObject regionJson(const HitRegion& region)
{
    return {
        {QStringLiteral("id"), region.id},
        {QStringLiteral("role"), region.role},
        {QStringLiteral("priority"), region.priority},
        {QStringLiteral("shape"), QStringLiteral("RECT")},
        {QStringLiteral("rect"), QJsonArray {
            region.localRect.left(),
            region.localRect.top(),
            region.localRect.right() + 1,
            region.localRect.bottom() + 1
        }}
    };
}

QJsonArray regionsJson(const std::vector<HitRegion>& regions)
{
    QJsonArray result;
    for (const HitRegion& region : regions) result.append(regionJson(region));
    return result;
}

int childIndex(HWND parent, HWND child)
{
    int index = 0;
    for (HWND current = GetWindow(parent, GW_CHILD);
         current != nullptr;
         current = GetWindow(current, GW_HWNDNEXT), ++index) {
        if (current == child) return index;
    }
    return -1;
}

class LayeredVisualSurface final {
public:
    explicit LayeredVisualSurface(HWND parent)
        : parent_(parent), instance_(GetModuleHandleW(nullptr))
    {
    }

    ~LayeredVisualSurface()
    {
        clear();
    }

    bool initialize(QString* error)
    {
        WNDCLASSEXW windowClass {};
        windowClass.cbSize = sizeof(windowClass);
        windowClass.lpfnWndProc = &LayeredVisualSurface::windowProc;
        windowClass.hInstance = instance_;
        windowClass.hCursor = LoadCursorW(nullptr, IDC_ARROW);
        windowClass.lpszClassName = className_;
        if (RegisterClassExW(&windowClass) == 0) {
            if (error != nullptr) {
                *error = QStringLiteral("R2_VISUAL_CLASS_REGISTER_FAILED: %1")
                    .arg(GetLastError());
            }
            return false;
        }
        registered_ = true;
        window_ = CreateWindowExW(
            WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW,
            className_,
            L"DesktopUp TS-C01 Host Layered Visual",
            WS_CHILD | WS_CLIPSIBLINGS,
            0, 0, 1, 1,
            parent_,
            nullptr,
            instance_,
            this);
        if (window_ == nullptr ||
            !SetLayeredWindowAttributes(window_, 0, 238, LWA_ALPHA)) {
            if (error != nullptr) {
                *error = QStringLiteral("R2_HOST_LAYERED_VISUAL_CREATE_FAILED: %1")
                    .arg(GetLastError());
            }
            clear();
            return false;
        }
        return true;
    }

    bool publish(const QImage& frame, const QRect& screenRect, QString* error)
    {
        if (window_ == nullptr || !IsWindow(window_) || frame.isNull() ||
            !screenRect.isValid()) {
            if (error != nullptr) *error = QStringLiteral("R2_VISUAL_FRAME_INVALID");
            return false;
        }

        frame_ = QImage(frame.size(), QImage::Format_RGB32);
        frame_.fill(QColor(QStringLiteral("#071018")));
        {
            QPainter painter(&frame_);
            painter.setCompositionMode(QPainter::CompositionMode_SourceOver);
            painter.drawImage(QPoint(0, 0), frame);
        }

        if (!previewBounds(screenRect, error, true)) return false;

        const LONG_PTR style = GetWindowLongPtrW(window_, GWL_STYLE);
        const LONG_PTR exStyle = GetWindowLongPtrW(window_, GWL_EXSTYLE);
        const bool valid =
            GetParent(window_) == parent_ &&
            (style & WS_CHILD) != 0 &&
            (exStyle & WS_EX_LAYERED) != 0 &&
            (exStyle & WS_EX_TRANSPARENT) != 0 &&
            (exStyle & WS_EX_TOPMOST) == 0 &&
            IsWindowVisible(window_) &&
            GetWindow(window_, GW_OWNER) == nullptr;
        if (!valid && error != nullptr) {
            *error = QStringLiteral("R2_HOST_LAYERED_VISUAL_CONTRACT_FAILED");
        }
        return valid;
    }

    bool previewBounds(
        const QRect& screenRect,
        QString* error,
        bool repaintImmediately = false)
    {
        if (window_ == nullptr || !IsWindow(window_) || !screenRect.isValid()) {
            if (error != nullptr) *error = QStringLiteral("R4_VISUAL_PREVIEW_BOUNDS_INVALID");
            return false;
        }

        POINT origin {screenRect.left(), screenRect.top()};
        MapWindowPoints(nullptr, parent_, &origin, 1);
        const int radius = qRound(48.0 * screenRect.width() / 420.0);
        const bool shapeChanged =
            regionSize_ != screenRect.size() || regionRadius_ != radius;
        if (shapeChanged) {
            HRGN region = CreateRoundRectRgn(
                1, 1, screenRect.width() - 1, screenRect.height() - 1,
                radius, radius);
            if (region == nullptr || SetWindowRgn(window_, region, FALSE) == 0) {
                if (region != nullptr) DeleteObject(region);
                if (error != nullptr) {
                    *error = QStringLiteral("R4_VISUAL_PREVIEW_REGION_FAILED");
                }
                return false;
            }
            regionSize_ = screenRect.size();
            regionRadius_ = radius;
        }
        const HWND insertAfter = hasPublished_ ? nullptr : HWND_TOP;
        const UINT positionFlags =
            SWP_NOACTIVATE | SWP_SHOWWINDOW |
            (hasPublished_ ? SWP_NOZORDER : 0);
        if (!SetWindowPos(
                window_, insertAfter, origin.x, origin.y,
                screenRect.width(), screenRect.height(),
                positionFlags)) {
            if (error != nullptr) {
                *error = QStringLiteral("R4_VISUAL_PREVIEW_POSITION_FAILED: %1")
                    .arg(GetLastError());
            }
            return false;
        }
        hasPublished_ = true;
        if (shapeChanged || repaintImmediately) {
            InvalidateRect(window_, nullptr, FALSE);
        }
        if (repaintImmediately) {
            UpdateWindow(window_);
        }
        return true;
    }
    void clear()
    {
        if (window_ != nullptr && IsWindow(window_)) {
            DestroyWindow(window_);
        }
        window_ = nullptr;
        frame_ = QImage();
        regionSize_ = QSize();
        regionRadius_ = -1;
        hasPublished_ = false;
        if (registered_) {
            UnregisterClassW(className_, instance_);
            registered_ = false;
        }
    }

    HWND window() const
    {
        return window_;
    }

    int liveCount() const
    {
        return window_ != nullptr && IsWindow(window_) ? 1 : 0;
    }

    quint64 unexpectedPointerCount() const
    {
        return unexpectedPointerCount_;
    }

private:
    static LRESULT CALLBACK windowProc(
        HWND window, UINT message, WPARAM wParam, LPARAM lParam)
    {
        auto* self = reinterpret_cast<LayeredVisualSurface*>(
            GetWindowLongPtrW(window, GWLP_USERDATA));
        if (message == WM_NCCREATE) {
            const auto* create = reinterpret_cast<CREATESTRUCTW*>(lParam);
            self = static_cast<LayeredVisualSurface*>(create->lpCreateParams);
            SetWindowLongPtrW(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(self));
            if (self != nullptr) self->window_ = window;
        }

        switch (message) {
        case WM_MOUSEACTIVATE:
            if (self != nullptr) ++self->unexpectedPointerCount_;
            return MA_NOACTIVATE;
        case WM_LBUTTONDOWN:
        case WM_LBUTTONUP:
        case WM_RBUTTONDOWN:
        case WM_RBUTTONUP:
        case WM_CONTEXTMENU:
            if (self != nullptr) ++self->unexpectedPointerCount_;
            return 0;
        case WM_ERASEBKGND:
            return 1;
        case WM_PAINT: {
            PAINTSTRUCT paint {};
            HDC dc = BeginPaint(window, &paint);
            if (self != nullptr && !self->frame_.isNull()) {
                BITMAPINFO bitmap {};
                bitmap.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
                bitmap.bmiHeader.biWidth = self->frame_.width();
                bitmap.bmiHeader.biHeight = -self->frame_.height();
                bitmap.bmiHeader.biPlanes = 1;
                bitmap.bmiHeader.biBitCount = 32;
                bitmap.bmiHeader.biCompression = BI_RGB;
                RECT client {};
                GetClientRect(window, &client);
                StretchDIBits(
                    dc,
                    0, 0, client.right, client.bottom,
                    0, 0, self->frame_.width(), self->frame_.height(),
                    self->frame_.constBits(),
                    &bitmap,
                    DIB_RGB_COLORS,
                    SRCCOPY);
            }
            EndPaint(window, &paint);
            return 0;
        }
        default:
            return DefWindowProcW(window, message, wParam, lParam);
        }
    }

    const wchar_t* className_ = L"DesktopUpTSC01R2HostLayeredVisual";
    HWND parent_ = nullptr;
    HWND window_ = nullptr;
    HINSTANCE instance_ = nullptr;
    bool registered_ = false;
    QImage frame_;
    QSize regionSize_;
    int regionRadius_ = -1;
    bool hasPublished_ = false;
    quint64 unexpectedPointerCount_ = 0;
};

class InputIslandSet;

struct IslandState {
    InputIslandSet* owner = nullptr;
    HitRegion region;
    HWND window = nullptr;
    quint64 clickCount = 0;
};

class InputIslandSet final {
public:
    using InteractionHandler = std::function<void(
        const QString&, const QString&, const QString&, const QPoint&)>;

    void setInteractionHandler(InteractionHandler handler)
    {
        interactionHandler_ = std::move(handler);
    }

    InputIslandSet(
        HWND parent,
        HWND visual,
        HWND defView,
        HWND iconView,
        DesktopHostCoordinator& host)
        : parent_(parent),
          visual_(visual),
          defView_(defView),
          iconView_(iconView),
          host_(host),
          instance_(GetModuleHandleW(nullptr))
    {
        pointerMoveLogClock_.start();
    }

    ~InputIslandSet()
    {
        clear();
        if (registered_) UnregisterClassW(className_, instance_);
    }

    bool initialize(QString* error)
    {
        WNDCLASSEXW windowClass {};
        windowClass.cbSize = sizeof(windowClass);
        windowClass.style = CS_DBLCLKS;
        windowClass.lpfnWndProc = &InputIslandSet::windowProc;
        windowClass.hInstance = instance_;
        windowClass.hCursor = LoadCursorW(nullptr, IDC_ARROW);
        windowClass.lpszClassName = className_;
        if (RegisterClassExW(&windowClass) == 0) {
            if (error != nullptr) {
                *error = QStringLiteral("R2_ISLAND_CLASS_REGISTER_FAILED: %1")
                    .arg(GetLastError());
            }
            return false;
        }
        registered_ = true;
        return true;
    }

    bool publish(const std::vector<HitRegion>& regions, QString* error)
    {
        if (canUpdateInPlace(regions)) return preview(regions, error);
        clear();
        std::vector<HitRegion> ordered = regions;
        std::stable_sort(
            ordered.begin(), ordered.end(),
            [](const HitRegion& left, const HitRegion& right) {
                return left.priority < right.priority;
            });

        RECT visualRect {};
        GetWindowRect(visual_, &visualRect);
        POINT visualOrigin {visualRect.left, visualRect.top};
        MapWindowPoints(nullptr, parent_, &visualOrigin, 1);

        for (const HitRegion& region : ordered) {
            if (!region.localRect.isValid()) {
                if (error != nullptr) *error = QStringLiteral("HIT_REGION_INVALID");
                clear();
                return false;
            }
            auto state = std::make_unique<IslandState>();
            state->owner = this;
            state->region = region;
            const std::wstring title = region.id.toStdWString();
            const QRect rect = region.localRect;
            state->window = CreateWindowExW(
                WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW,
                className_,
                title.c_str(),
                WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS,
                visualOrigin.x + rect.x(),
                visualOrigin.y + rect.y(),
                rect.width(),
                rect.height(),
                parent_,
                nullptr,
                instance_,
                state.get());
            if (state->window == nullptr) {
                if (error != nullptr) {
                    *error = QStringLiteral("R2_ISLAND_CREATE_FAILED: %1")
                        .arg(GetLastError());
                }
                clear();
                return false;
            }
            SetLayeredWindowAttributes(state->window, 0, 1, LWA_ALPHA);
            SetWindowPos(
                state->window, HWND_TOP, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            states_.push_back(std::move(state));
        }
        return true;
    }

    bool canUpdateInPlace(const std::vector<HitRegion>& regions) const
    {
        if (states_.size() != regions.size()) return false;
        std::vector<HitRegion> ordered = regions;
        std::stable_sort(
            ordered.begin(), ordered.end(),
            [](const HitRegion& left, const HitRegion& right) {
                return left.priority < right.priority;
            });
        for (std::size_t index = 0; index < ordered.size(); ++index) {
            if (states_[index]->region.id != ordered[index].id ||
                states_[index]->region.role != ordered[index].role) {
                return false;
            }
        }
        return true;
    }

    bool preview(const std::vector<HitRegion>& regions, QString* error)
    {
        if (!canUpdateInPlace(regions)) {
            if (error != nullptr) *error = QStringLiteral("R4_ISLAND_PREVIEW_SET_MISMATCH");
            return false;
        }
        std::vector<HitRegion> ordered = regions;
        std::stable_sort(
            ordered.begin(), ordered.end(),
            [](const HitRegion& left, const HitRegion& right) {
                return left.priority < right.priority;
            });

        RECT visualRect {};
        GetWindowRect(visual_, &visualRect);
        POINT visualOrigin {visualRect.left, visualRect.top};
        MapWindowPoints(nullptr, parent_, &visualOrigin, 1);
        if (ordered.empty()) return true;
        HDWP batch = BeginDeferWindowPos(static_cast<int>(ordered.size()));
        if (batch == nullptr) {
            if (error != nullptr) {
                *error = QStringLiteral("R4_ISLAND_PREVIEW_BATCH_BEGIN_FAILED");
            }
            return false;
        }
        for (std::size_t index = 0; index < ordered.size(); ++index) {
            const QRect rect = ordered[index].localRect;
            if (!rect.isValid()) {
                if (error != nullptr) *error = QStringLiteral("HIT_REGION_INVALID");
                return false;
            }
            batch = DeferWindowPos(
                batch,
                states_[index]->window,
                nullptr,
                visualOrigin.x + rect.x(),
                visualOrigin.y + rect.y(),
                rect.width(),
                rect.height(),
                SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOZORDER);
            if (batch == nullptr) {
                if (error != nullptr) {
                    *error = QStringLiteral("R4_ISLAND_PREVIEW_BATCH_UPDATE_FAILED: %1")
                        .arg(GetLastError());
                }
                return false;
            }
        }
        if (!EndDeferWindowPos(batch)) {
            if (error != nullptr) {
                *error = QStringLiteral("R4_ISLAND_PREVIEW_BATCH_COMMIT_FAILED: %1")
                    .arg(GetLastError());
            }
            return false;
        }
        for (std::size_t index = 0; index < ordered.size(); ++index) {
            states_[index]->region = ordered[index];
        }
        return true;
    }
    bool validate(
        const std::vector<HitRegion>& regions,
        const QString& scenario,
        QString* error) const
    {
        if (states_.size() != regions.size()) {
            if (error != nullptr) *error = QStringLiteral("R2_ISLAND_COUNT_MISMATCH");
            return false;
        }

        const LONG_PTR visualStyle = GetWindowLongPtrW(visual_, GWL_STYLE);
        const LONG_PTR visualExStyle = GetWindowLongPtrW(visual_, GWL_EXSTYLE);
        const int visualIndex = childIndex(parent_, visual_);
        const int defViewIndex = childIndex(parent_, defView_);
        const bool visualContract =
            GetParent(visual_) == parent_ &&
            (visualStyle & WS_CHILD) != 0 &&
            (visualExStyle & WS_EX_LAYERED) != 0 &&
            (visualExStyle & WS_EX_TRANSPARENT) != 0 &&
            (visualExStyle & WS_EX_TOPMOST) == 0 &&
            visualIndex >= 0 && defViewIndex >= 0 && visualIndex < defViewIndex;
        if (!visualContract) {
            if (error != nullptr) *error = QStringLiteral("R2_VISUAL_SPLIT_INPUT_CONTRACT_FAILED");
            return false;
        }

        constexpr UINT flags = CWP_SKIPINVISIBLE | CWP_SKIPDISABLED | CWP_SKIPTRANSPARENT;
        RECT visualScreen {};
        GetWindowRect(visual_, &visualScreen);
        POINT visualOrigin {visualScreen.left, visualScreen.top};
        MapWindowPoints(nullptr, parent_, &visualOrigin, 1);

        for (const std::unique_ptr<IslandState>& state : states_) {
            const LONG_PTR style = GetWindowLongPtrW(state->window, GWL_STYLE);
            const LONG_PTR exStyle = GetWindowLongPtrW(state->window, GWL_EXSTYLE);
            std::optional<QPoint> sampleLocal;
            for (int y = state->region.localRect.top();
                 y <= state->region.localRect.bottom() && !sampleLocal.has_value();
                 y += 3) {
                for (int x = state->region.localRect.left();
                     x <= state->region.localRect.right();
                     x += 3) {
                    const QPoint candidate(x, y);
                    const bool coveredByHigherPriority = std::any_of(
                        states_.begin(), states_.end(),
                        [&](const std::unique_ptr<IslandState>& other) {
                            return other.get() != state.get() &&
                                   other->region.priority > state->region.priority &&
                                   other->region.localRect.contains(candidate);
                        });
                    if (!coveredByHigherPriority) {
                        sampleLocal = candidate;
                        break;
                    }
                }
            }
            const bool routedToIsland = !sampleLocal.has_value() ||
                ChildWindowFromPointEx(
                    parent_,
                    POINT {
                        visualOrigin.x + sampleLocal->x(),
                        visualOrigin.y + sampleLocal->y()
                    },
                    flags) == state->window;
            const bool islandContract =
                GetParent(state->window) == parent_ &&
                (style & WS_CHILD) != 0 &&
                (exStyle & WS_EX_LAYERED) != 0 &&
                (exStyle & WS_EX_TRANSPARENT) == 0 &&
                (exStyle & WS_EX_TOPMOST) == 0 &&
                GetWindow(state->window, GW_OWNER) == nullptr &&
                childIndex(parent_, state->window) >= 0 &&
                childIndex(parent_, state->window) < visualIndex &&
                routedToIsland;
            if (!islandContract) {
                if (error != nullptr) {
                    *error = QStringLiteral("R2_ISLAND_CONTRACT_FAILED: ") +
                             state->region.id;
                }
                return false;
            }
        }

        std::optional<QPoint> blank;
        RECT visualClient {};
        GetClientRect(visual_, &visualClient);
        for (int y = 4; y < visualClient.bottom && !blank.has_value(); y += 9) {
            for (int x = 4; x < visualClient.right; x += 9) {
                const QPoint point(x, y);
                const bool covered = std::any_of(
                    regions.begin(), regions.end(),
                    [&](const HitRegion& region) {
                        return region.localRect.contains(point);
                    });
                if (!covered) {
                    blank = point;
                    break;
                }
            }
        }
        if (!blank.has_value()) {
            if (error != nullptr) *error = QStringLiteral("R2_NO_BLANK_SAMPLE_AVAILABLE");
            return false;
        }
        const POINT blankPoint {
            visualOrigin.x + blank->x(),
            visualOrigin.y + blank->y()
        };
        const HWND blankTarget = ChildWindowFromPointEx(parent_, blankPoint, flags);
        const bool nativeBlank =
            blankTarget == defView_ ||
            blankTarget == iconView_ ||
            (defView_ != nullptr && IsChild(defView_, blankTarget));
        if (!nativeBlank) {
            if (error != nullptr) {
                *error = QStringLiteral("R2_BLANK_NOT_ROUTED_TO_EXPLORER: %1")
                    .arg(reinterpret_cast<quintptr>(blankTarget));
            }
            return false;
        }

        if (scenario == QStringLiteral("OVERLAP") && states_.size() >= 2) {
            const IslandState* low = states_.front().get();
            const IslandState* high = states_.back().get();
            const QRect overlap = low->region.localRect.intersected(high->region.localRect);
            if (!overlap.isValid() || low->region.priority >= high->region.priority) {
                if (error != nullptr) *error = QStringLiteral("R2_OVERLAP_FIXTURE_INVALID");
                return false;
            }
            const QPoint center = overlap.center();
            const POINT sample {
                visualOrigin.x + center.x(),
                visualOrigin.y + center.y()
            };
            if (ChildWindowFromPointEx(parent_, sample, flags) != high->window) {
                if (error != nullptr) *error = QStringLiteral("R2_OVERLAP_PRIORITY_FAILED");
                return false;
            }
        }
        return true;
    }

    void clear()
    {
        for (auto iterator = states_.rbegin(); iterator != states_.rend(); ++iterator) {
            if ((*iterator)->window != nullptr && IsWindow((*iterator)->window)) {
                DestroyWindow((*iterator)->window);
            }
            (*iterator)->window = nullptr;
        }
        states_.clear();
    }

    int liveCount() const
    {
        return static_cast<int>(std::count_if(
            states_.begin(), states_.end(),
            [](const std::unique_ptr<IslandState>& state) {
                return state->window != nullptr && IsWindow(state->window);
            }));
    }

private:
    static LRESULT CALLBACK windowProc(
        HWND window, UINT message, WPARAM wParam, LPARAM lParam)
    {
        auto* state = reinterpret_cast<IslandState*>(
            GetWindowLongPtrW(window, GWLP_USERDATA));
        if (message == WM_NCCREATE) {
            const auto* create = reinterpret_cast<CREATESTRUCTW*>(lParam);
            state = static_cast<IslandState*>(create->lpCreateParams);
            SetWindowLongPtrW(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(state));
            if (state != nullptr) state->window = window;
        }

        switch (message) {
        case WM_MOUSEACTIVATE:
            return MA_NOACTIVATE;
        case WM_LBUTTONDOWN: {
            if (state != nullptr && state->owner != nullptr) {
                POINT point {GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam)};
                ClientToScreen(window, &point);
                SetCapture(window);
                state->owner->dispatchPointer(
                    *state, QStringLiteral("POINTER_DOWN"), QPoint(point.x, point.y));
            }
            return 0;
        }
        case WM_MOUSEMOVE: {
            if (state != nullptr && state->owner != nullptr &&
                (wParam & MK_LBUTTON) != 0 && GetCapture() == window) {
                POINT point {GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam)};
                ClientToScreen(window, &point);
                state->owner->dispatchPointer(
                    *state, QStringLiteral("POINTER_MOVE"), QPoint(point.x, point.y));
            }
            return 0;
        }
        case WM_LBUTTONUP: {
            if (state != nullptr && state->owner != nullptr) {
                POINT point {GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam)};
                ClientToScreen(window, &point);
                if (GetCapture() == window) ReleaseCapture();
                state->owner->dispatchPointer(
                    *state, QStringLiteral("POINTER_UP"), QPoint(point.x, point.y));
                ++state->clickCount;
                state->owner->dispatchPointer(
                    *state, QStringLiteral("LEFT_CLICK"), QPoint(point.x, point.y));
            }
            return 0;
        }
        case WM_LBUTTONDBLCLK: {
            if (state != nullptr && state->owner != nullptr) {
                POINT point {GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam)};
                ClientToScreen(window, &point);
                state->owner->dispatchPointer(
                    *state, QStringLiteral("DOUBLE_CLICK"), QPoint(point.x, point.y));
            }
            return 0;
        }
        case WM_RBUTTONUP: {
            if (state != nullptr && state->owner != nullptr) {
                POINT point {GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam)};
                ClientToScreen(window, &point);
                state->owner->dispatchPointer(
                    *state, QStringLiteral("RIGHT_CLICK"), QPoint(point.x, point.y));
            }
            return 0;
        }
        case WM_ERASEBKGND:
            return 1;
        case WM_PAINT: {
            PAINTSTRUCT paint {};
            BeginPaint(window, &paint);
            EndPaint(window, &paint);
            return 0;
        }
        default:
            return DefWindowProcW(window, message, wParam, lParam);
        }
    }

    void dispatchPointer(
        const IslandState& state, const QString& action, const QPoint& screenPoint)
    {
        const bool pointerMove = action == QStringLiteral("POINTER_MOVE");
        const bool recordEvent =
            !pointerMove || !pointerMoveLogClock_.isValid() ||
            pointerMoveLogClock_.elapsed() >= 16;
        if (recordEvent) {
            host_.recordFixtureEvent(QStringLiteral("fixture_pointer_event"), {
                {QStringLiteral("region_id"), state.region.id},
                {QStringLiteral("region_role"), state.region.role},
                {QStringLiteral("priority"), state.region.priority},
                {QStringLiteral("action"), action},
                {QStringLiteral("screen_x"), screenPoint.x()},
                {QStringLiteral("screen_y"), screenPoint.y()},
                {QStringLiteral("click_count"), static_cast<qint64>(state.clickCount)}
            });
            if (pointerMove) pointerMoveLogClock_.restart();
        }
        if (interactionHandler_) {
            interactionHandler_(state.region.id, state.region.role, action, screenPoint);
        }
    }

    const wchar_t* className_ = L"DesktopUpTSC01R2InputIsland";
    HWND parent_ = nullptr;
    HWND visual_ = nullptr;
    HWND defView_ = nullptr;
    HWND iconView_ = nullptr;
    DesktopHostCoordinator& host_;
    HINSTANCE instance_ = nullptr;
    bool registered_ = false;
    QElapsedTimer pointerMoveLogClock_;
    std::vector<std::unique_ptr<IslandState>> states_;
    InteractionHandler interactionHandler_;
};

struct FixtureState {
    QRect screenRect;
    int expandedHeight = 620;
    QString presentation = QStringLiteral("EXPANDED");
    QString interaction = QStringLiteral("USING");
    bool locked = false;
    QString selectedThemeId = QStringLiteral("transparent-default");
    QString effectiveThemeId = QStringLiteral("transparent-default");
    QString lastKnownGoodThemeId = QStringLiteral("transparent-default");
    quint64 themeRevision = 1;
    bool themeFallbackActive = false;
};

std::vector<HitRegion> regionsFor(
    const FixtureState& state, const QString& scenario)
{
    const qreal scale = static_cast<qreal>(state.screenRect.width()) / 420.0;
    const int width = state.screenRect.width();
    const int height = state.screenRect.height();
    auto scaled = [scale](qreal value) {
        return qRound(value * scale);
    };

    if (scenario == QStringLiteral("ZERO")) return {};
    if (scenario == QStringLiteral("ONE")) {
        return {
            {QStringLiteral("collapse"), QStringLiteral("PRESENTATION_TOGGLE"),
             QRect(width - scaled(58), scaled(14), scaled(34), scaled(34)), 110}
        };
    }
    if (scenario == QStringLiteral("MULTI")) {
        return {
            {QStringLiteral("title"), QStringLiteral("TITLE_DRAG"),
             QRect(scaled(22), scaled(10), width - scaled(178), scaled(48)), 20},
            {QStringLiteral("item-01"), QStringLiteral("ITEM"),
             QRect(scaled(28), scaled(183), scaled(113), scaled(92)), 50},
            {QStringLiteral("collapse"), QStringLiteral("PRESENTATION_TOGGLE"),
             QRect(width - scaled(58), scaled(14), scaled(34), scaled(34)), 110}
        };
    }
    if (scenario == QStringLiteral("OVERLAP")) {
        return {
            {QStringLiteral("title-low"), QStringLiteral("TITLE_DRAG"),
             QRect(scaled(32), scaled(12), scaled(150), scaled(38)), 10},
            {QStringLiteral("control-high"), QStringLiteral("CONTROL"),
             QRect(scaled(82), scaled(18), scaled(86), scaled(27)), 100}
        };
    }

    std::vector<HitRegion> regions;
    if (!state.locked) {
        regions.push_back({
            QStringLiteral("title"),
            QStringLiteral("TITLE_DRAG"),
            QRect(scaled(22), scaled(10), width - scaled(178), scaled(48)),
            20
        });
    }
    regions.push_back({
        QStringLiteral("lock"),
        QStringLiteral("LOCK_TOGGLE"),
        QRect(width - scaled(100), scaled(14), scaled(34), scaled(34)),
        100
    });
    regions.push_back({
        QStringLiteral("collapse"),
        QStringLiteral("PRESENTATION_TOGGLE"),
        QRect(width - scaled(58), scaled(14), scaled(34), scaled(34)),
        110
    });

    if (state.presentation == QStringLiteral("EXPANDED")) {
        const int gap = scaled(12);
        const int cardWidth = (width - scaled(80)) / 3;
        const int availableCardHeight = (height - scaled(310)) / 2;
        const int cardHeight = std::max(scaled(66), std::min(scaled(92), availableCardHeight));
        const int cardTop = scaled(183);
        const int rowStride = cardHeight + scaled(39);
        for (int index = 0; index < 6; ++index) {
            regions.push_back({
                QStringLiteral("item-%1").arg(index + 1, 2, 10, QLatin1Char('0')),
                QStringLiteral("ITEM"),
                QRect(
                    scaled(28) + (index % 3) * (cardWidth + gap),
                    cardTop + (index / 3) * rowStride,
                    cardWidth,
                    cardHeight),
                50
            });
        }
        if (state.interaction == QStringLiteral("EDITING") && !state.locked) {
            regions.push_back({
                QStringLiteral("resize-corner"),
                QStringLiteral("RESIZE_BOTTOM_RIGHT"),
                QRect(width - scaled(24), height - scaled(24), scaled(20), scaled(20)),
                120
            });
        }
    }
    return regions;
}

bool setFixtureProperties(
    QtQuickSurfaceAdapter& adapter,
    const FixtureState& state,
    const QString& scenario,
    quint64 nextRevision,
    QString* error)
{
    return adapter.setRootProperty("presentationMode", state.presentation, error) &&
           adapter.setRootProperty("interactionMode", state.interaction, error) &&
           adapter.setRootProperty("isLocked", state.locked, error) &&
           adapter.setRootProperty("hitScenario", scenario, error) &&
           adapter.setRootProperty("themeId", state.effectiveThemeId, error) &&
           adapter.setRootProperty(
               "fixtureRevision", QVariant::fromValue<qulonglong>(nextRevision), error);
}

bool applyProjection(
    DesktopHostCoordinator& host,
    QtQuickSurfaceAdapter& adapter,
    LayeredVisualSurface& visual,
    InputIslandSet& islands,
    const FixtureState& state,
    const QString& scenario,
    const QString& reason,
    QString* error)
{
    const std::vector<HitRegion> regions = regionsFor(state, scenario);
    if (!islands.canUpdateInPlace(regions)) islands.clear();
    const quint64 nextRevision = host.hostProjectionRevision() + 1;
    QElapsedTimer projectionClock;
    projectionClock.start();
    if (!setFixtureProperties(adapter, state, scenario, nextRevision, error) ||
        !host.commitFixtureProjection(
            state.screenRect,
            state.presentation,
            state.interaction,
            state.locked,
            regionsJson(regions),
            reason,
            error)) {
        return false;
    }
    const qint64 commitMicros = projectionClock.nsecsElapsed() / 1000;
    if (!adapter.waitForNextFrame(5000, error)) {
        return false;
    }
    const qint64 captureMicros =
        projectionClock.nsecsElapsed() / 1000 - commitMicros;
    if (!visual.publish(adapter.lastFrame(), state.screenRect, error) ||
        !host.notifyProjectionFramePresented(error) ||
        !islands.publish(regions, error) ||
        !islands.validate(regions, scenario, error)) {
        return false;
    }
    const qint64 totalMicros = projectionClock.nsecsElapsed() / 1000;
    host.recordFixtureEvent(QStringLiteral("projection_latency_sample"), {
        {QStringLiteral("projection_reason"), reason},
        {QStringLiteral("commit_ms"), commitMicros / 1000.0},
        {QStringLiteral("capture_ms"), captureMicros / 1000.0},
        {QStringLiteral("publish_ms"),
         (totalMicros - commitMicros - captureMicros) / 1000.0},
        {QStringLiteral("total_ms"), totalMicros / 1000.0},
        {QStringLiteral("interaction_budget_ms"), 100},
        {QStringLiteral("budget_result"),
         totalMicros <= 100000 ? QStringLiteral("PASS") : QStringLiteral("FAIL")}
    });

    host.recordFixtureEvent(QStringLiteral("hit_matrix_sample"), {
        {QStringLiteral("scenario"), scenario},
        {QStringLiteral("matrix_result"), QStringLiteral("PASS")},
        {QStringLiteral("published_region_count"), static_cast<int>(regions.size())},
        {QStringLiteral("island_window_count"), islands.liveCount()},
        {QStringLiteral("presentation_mode"), state.presentation},
        {QStringLiteral("interaction_mode"), state.interaction},
        {QStringLiteral("is_locked"), state.locked}
    });
    return true;
}

bool runFixtureMatrix(
    DesktopHostCoordinator& host,
    QtQuickSurfaceAdapter& adapter,
    LayeredVisualSurface& visual,
    InputIslandSet& islands,
    FixtureState* state,
    QString* error)
{
    const QRect original = state->screenRect;
    state->expandedHeight = original.height();

    for (const QString& scenario : {
             QStringLiteral("ZERO"),
             QStringLiteral("ONE"),
             QStringLiteral("MULTI"),
             QStringLiteral("OVERLAP")}) {
        if (!applyProjection(
                host, adapter, visual, islands, *state, scenario,
                QStringLiteral("HIT_MATRIX_") + scenario, error)) {
            return false;
        }
    }

    const int collapsedHeight = qRound(60.0 * original.width() / 420.0);
    state->presentation = QStringLiteral("COLLAPSED");
    state->screenRect.setHeight(collapsedHeight);
    if (!applyProjection(
            host, adapter, visual, islands, *state, QStringLiteral("FINAL"),
            QStringLiteral("PRESENTATION_COLLAPSE"), error)) {
        return false;
    }
    const QRect collapsed = host.screenPixelRect();
    if (collapsed.left() != original.left() || collapsed.top() != original.top() ||
        collapsed.width() != original.width() ||
        collapsed.height() != collapsedHeight) {
        if (error != nullptr) *error = QStringLiteral("R2_COLLAPSE_TOP_LEFT_WIDTH_MOVED");
        return false;
    }

    state->presentation = QStringLiteral("EXPANDED");
    state->screenRect.setHeight(state->expandedHeight);
    if (!applyProjection(
            host, adapter, visual, islands, *state, QStringLiteral("FINAL"),
            QStringLiteral("PRESENTATION_EXPAND"), error)) {
        return false;
    }

    state->locked = true;
    if (!applyProjection(
            host, adapter, visual, islands, *state, QStringLiteral("FINAL"),
            QStringLiteral("LOCK_ENABLE"), error)) {
        return false;
    }
    const QRect beforeRejectedMove = host.screenPixelRect();
    host.recordFixtureEvent(QStringLiteral("geometry_gesture_cancelled"), {
        {QStringLiteral("gesture_kind"), QStringLiteral("MOVE")},
        {QStringLiteral("cancel_reason"), QStringLiteral("LOCKED_OPERATION_REJECTED")},
        {QStringLiteral("result"), QStringLiteral("LOCKED_OPERATION_REJECTED")}
    });
    if (host.screenPixelRect() != beforeRejectedMove) {
        if (error != nullptr) *error = QStringLiteral("R2_LOCKED_GEOMETRY_CHANGED");
        return false;
    }

    state->locked = false;
    state->interaction = QStringLiteral("EDITING");
    host.recordFixtureEvent(QStringLiteral("geometry_gesture_started"), {
        {QStringLiteral("gesture_kind"), QStringLiteral("MOVE")},
        {QStringLiteral("start_revision"), static_cast<qint64>(host.hostProjectionRevision())}
    });
    state->screenRect.translate(-24, 16);
    if (!applyProjection(
            host, adapter, visual, islands, *state, QStringLiteral("FINAL"),
            QStringLiteral("EDITING_MOVE"), error)) {
        return false;
    }
    host.recordFixtureEvent(QStringLiteral("geometry_gesture_ended"), {
        {QStringLiteral("gesture_kind"), QStringLiteral("MOVE")},
        {QStringLiteral("final_revision"), static_cast<qint64>(host.hostProjectionRevision())}
    });

    const QPoint resizeTopLeft = state->screenRect.topLeft();
    host.recordFixtureEvent(QStringLiteral("geometry_gesture_started"), {
        {QStringLiteral("gesture_kind"), QStringLiteral("RESIZE_BOTTOM_RIGHT")},
        {QStringLiteral("start_revision"), static_cast<qint64>(host.hostProjectionRevision())}
    });
    state->screenRect.setSize(
        QSize(state->screenRect.width() + 36, state->screenRect.height() - 28));
    if (!applyProjection(
            host, adapter, visual, islands, *state, QStringLiteral("FINAL"),
            QStringLiteral("EDITING_RESIZE"), error)) {
        return false;
    }
    if (host.screenPixelRect().topLeft() != resizeTopLeft) {
        if (error != nullptr) *error = QStringLiteral("R2_RESIZE_DID_NOT_FIX_TOP_LEFT");
        return false;
    }
    host.recordFixtureEvent(QStringLiteral("geometry_gesture_ended"), {
        {QStringLiteral("gesture_kind"), QStringLiteral("RESIZE_BOTTOM_RIGHT")},
        {QStringLiteral("final_revision"), static_cast<qint64>(host.hostProjectionRevision())}
    });

    state->interaction = QStringLiteral("USING");
    state->screenRect = original;
    if (!applyProjection(
            host, adapter, visual, islands, *state, QStringLiteral("FINAL"),
            QStringLiteral("R2_FINAL_USING_STATE"), error)) {
        return false;
    }
    host.recordFixtureEvent(QStringLiteral("r2_fixture_matrix_completed"), {
        {QStringLiteral("result"), QStringLiteral("PASS")},
        {QStringLiteral("scenarios"), QJsonArray {
            QStringLiteral("ZERO"),
            QStringLiteral("ONE"),
            QStringLiteral("MULTI"),
            QStringLiteral("OVERLAP"),
            QStringLiteral("COLLAPSE_EXPAND"),
            QStringLiteral("LOCK_REJECT"),
            QStringLiteral("EDITING_MOVE"),
            QStringLiteral("EDITING_RESIZE"),
            QStringLiteral("FINAL_USING")
        }}
    });
    return true;
}

class R4ManualInteractionController final {
public:
    R4ManualInteractionController(
        DesktopHostCoordinator& host,
        QtQuickSurfaceAdapter& adapter,
        LayeredVisualSurface& visual,
        InputIslandSet& islands,
        FixtureState& state)
        : host_(host), adapter_(adapter), visual_(visual), islands_(islands), state_(state)
    {
        previewTimer_.setSingleShot(true);
        previewTimer_.setInterval(16);
        previewTimer_.setTimerType(Qt::PreciseTimer);
        QObject::connect(
            &previewTimer_, &QTimer::timeout, &previewTimer_, [this]() {
                if (!gesture_.active || failed_) return;
                previewGesture(pendingCursor_);
            });
    }

    ~R4ManualInteractionController()
    {
        islands_.setInteractionHandler({});
    }

    void install()
    {
        islands_.setInteractionHandler(
            [this](const QString& regionId,
                   const QString& role,
                   const QString& action,
                   const QPoint& screenPoint) {
                handle(regionId, role, action, screenPoint);
            });
        host_.recordFixtureEvent(QStringLiteral("r4_manual_interaction_ready"), {
            {QStringLiteral("mode_toggle_gesture"), QStringLiteral("TITLE_RIGHT_CLICK")},
            {QStringLiteral("move_preview"), QStringLiteral("HOST_BOUNDS_THEN_ATOMIC_COMMIT")},
            {QStringLiteral("resize_preview"), QStringLiteral("HOST_STRETCH_THEN_ATOMIC_COMMIT")},
            {QStringLiteral("p0_result"), QStringLiteral("NOT_RUN")}
        });
    }

    bool failed() const { return failed_; }
    QString error() const { return error_; }

private:
    struct Gesture {
        bool active = false;
        QString role;
        QPoint startCursor;
        QRect startRect;
        int rawMoveCount = 0;
        int previewCount = 0;
        qint64 previewTotalMicros = 0;
        qint64 previewMaxMicros = 0;
    };

    QRect monitorWorkArea(const QRect& reference) const
    {
        RECT nativeRect {
            reference.left(), reference.top(),
            reference.right() + 1, reference.bottom() + 1
        };
        const HMONITOR monitor = MonitorFromRect(&nativeRect, MONITOR_DEFAULTTONEAREST);
        MONITORINFO info {};
        info.cbSize = sizeof(info);
        if (monitor != nullptr && GetMonitorInfoW(monitor, &info)) {
            return QRect(
                info.rcWork.left,
                info.rcWork.top,
                info.rcWork.right - info.rcWork.left,
                info.rcWork.bottom - info.rcWork.top);
        }
        return reference;
    }

    QRect previewRectFor(const QPoint& cursor) const
    {
        const QPoint delta = cursor - gesture_.startCursor;
        const QRect work = monitorWorkArea(gesture_.startRect);
        if (gesture_.role == QStringLiteral("TITLE_DRAG")) {
            QRect candidate = gesture_.startRect.translated(delta);
            const int maximumX = work.x() + std::max(0, work.width() - candidate.width());
            const int maximumY = work.y() + std::max(0, work.height() - candidate.height());
            candidate.moveTo(
                std::clamp(candidate.x(), work.x(), maximumX),
                std::clamp(candidate.y(), work.y(), maximumY));
            return candidate;
        }

        const UINT dpi = GetDpiForWindow(visual_.window());
        const qreal scale = static_cast<qreal>(dpi == 0 ? 96 : dpi) / 96.0;
        const int minimumWidth = qRound(320.0 * scale);
        const int minimumHeight = qRound(420.0 * scale);
        const int maximumWidth = std::max(
            minimumWidth,
            work.x() + work.width() - gesture_.startRect.x());
        const int maximumHeight = std::max(
            minimumHeight,
            work.y() + work.height() - gesture_.startRect.y());
        QRect candidate = gesture_.startRect;
        candidate.setSize(QSize(
            std::clamp(gesture_.startRect.width() + delta.x(), minimumWidth, maximumWidth),
            std::clamp(gesture_.startRect.height() + delta.y(), minimumHeight, maximumHeight)));
        return candidate;
    }

    bool previewGesture(const QPoint& cursor)
    {
        if (!gesture_.active) return true;
        QElapsedTimer previewClock;
        previewClock.start();
        state_.screenRect = previewRectFor(cursor);
        QString previewError;
        const std::vector<HitRegion> regions = regionsFor(state_, QStringLiteral("FINAL"));
        if (!visual_.previewBounds(state_.screenRect, &previewError) ||
            !islands_.preview(regions, &previewError)) {
            fail(previewError.isEmpty()
                     ? QStringLiteral("R4_GESTURE_PREVIEW_FAILED")
                     : previewError);
            return false;
        }
        const qint64 elapsedMicros = previewClock.nsecsElapsed() / 1000;
        ++gesture_.previewCount;
        gesture_.previewTotalMicros += elapsedMicros;
        gesture_.previewMaxMicros =
            std::max(gesture_.previewMaxMicros, elapsedMicros);
        return true;
    }

    void schedulePreview(const QPoint& cursor)
    {
        if (!gesture_.active || failed_) return;
        pendingCursor_ = cursor;
        ++gesture_.rawMoveCount;
        if (!previewTimer_.isActive()) previewTimer_.start();
    }

    void beginGesture(const QString& role, const QPoint& cursor)
    {
        if (failed_ || gesture_.active || state_.locked) return;
        const bool move = role == QStringLiteral("TITLE_DRAG");
        const bool resize = role == QStringLiteral("RESIZE_BOTTOM_RIGHT") &&
                            state_.interaction == QStringLiteral("EDITING") &&
                            state_.presentation == QStringLiteral("EXPANDED");
        if (!move && !resize) return;
        gesture_.active = true;
        gesture_.role = role;
        gesture_.startCursor = cursor;
        gesture_.startRect = state_.screenRect;
        gesture_.rawMoveCount = 0;
        gesture_.previewCount = 0;
        gesture_.previewTotalMicros = 0;
        gesture_.previewMaxMicros = 0;
        pendingCursor_ = cursor;
        host_.recordFixtureEvent(QStringLiteral("geometry_gesture_started"), {
            {QStringLiteral("gesture_kind"), move
                ? QStringLiteral("MOVE")
                : QStringLiteral("RESIZE_BOTTOM_RIGHT")},
            {QStringLiteral("start_revision"),
             static_cast<qint64>(host_.hostProjectionRevision())},
            {QStringLiteral("source"), QStringLiteral("R4_REAL_POINTER")}
        });
    }

    void endGesture(const QPoint& cursor)
    {
        if (!gesture_.active || failed_) return;
        previewTimer_.stop();
        pendingCursor_ = cursor;
        if (!previewGesture(cursor)) return;
        const QString role = gesture_.role;
        gesture_.active = false;
        if (role == QStringLiteral("RESIZE_BOTTOM_RIGHT")) {
            state_.expandedHeight = state_.screenRect.height();
        }
        QString commitError;
        const QString reason = role == QStringLiteral("TITLE_DRAG")
            ? QStringLiteral("R4_POINTER_MOVE_COMMIT")
            : QStringLiteral("R4_POINTER_RESIZE_COMMIT");
        if (!applyProjection(
                host_, adapter_, visual_, islands_, state_, QStringLiteral("FINAL"),
                reason, &commitError)) {
            fail(commitError.isEmpty()
                     ? QStringLiteral("R4_GESTURE_COMMIT_FAILED")
                     : commitError);
            return;
        }
        host_.recordFixtureEvent(QStringLiteral("geometry_gesture_ended"), {
            {QStringLiteral("gesture_kind"), role == QStringLiteral("TITLE_DRAG")
                ? QStringLiteral("MOVE")
                : QStringLiteral("RESIZE_BOTTOM_RIGHT")},
            {QStringLiteral("final_revision"),
             static_cast<qint64>(host_.hostProjectionRevision())},
            {QStringLiteral("raw_move_count"), gesture_.rawMoveCount},
            {QStringLiteral("preview_count"), gesture_.previewCount},
            {QStringLiteral("preview_total_ms"),
             gesture_.previewTotalMicros / 1000.0},
            {QStringLiteral("preview_max_ms"),
             gesture_.previewMaxMicros / 1000.0},
            {QStringLiteral("source"), QStringLiteral("R4_REAL_POINTER")}
        });
    }

    void deferCommand(const QString& regionId, const QString& role, const QString& action)
    {
        QTimer::singleShot(0, [this, regionId, role, action]() {
            if (failed_) return;
            QElapsedTimer commandClock;
            commandClock.start();
            QString commandError;
            QString reason;
            if (role == QStringLiteral("LOCK_TOGGLE") &&
                action == QStringLiteral("LEFT_CLICK")) {
                state_.locked = !state_.locked;
                reason = state_.locked
                    ? QStringLiteral("R4_LOCK_ENABLE")
                    : QStringLiteral("R4_LOCK_DISABLE");
            } else if (role == QStringLiteral("PRESENTATION_TOGGLE") &&
                       action == QStringLiteral("LEFT_CLICK")) {
                if (state_.presentation == QStringLiteral("EXPANDED")) {
                    state_.expandedHeight = state_.screenRect.height();
                    state_.presentation = QStringLiteral("COLLAPSED");
                    state_.screenRect.setHeight(
                        qRound(60.0 * state_.screenRect.width() / 420.0));
                    reason = QStringLiteral("R4_PRESENTATION_COLLAPSE");
                } else {
                    state_.presentation = QStringLiteral("EXPANDED");
                    state_.screenRect.setHeight(state_.expandedHeight);
                    reason = QStringLiteral("R4_PRESENTATION_EXPAND");
                }
            } else if (role == QStringLiteral("TITLE_DRAG") &&
                       action == QStringLiteral("RIGHT_CLICK") &&
                       !state_.locked) {
                state_.interaction = state_.interaction == QStringLiteral("USING")
                    ? QStringLiteral("EDITING")
                    : QStringLiteral("USING");
                reason = state_.interaction == QStringLiteral("EDITING")
                    ? QStringLiteral("R4_INTERACTION_EDITING")
                    : QStringLiteral("R4_INTERACTION_USING");
            } else if (role == QStringLiteral("ITEM") &&
                       (action == QStringLiteral("LEFT_CLICK") ||
                        action == QStringLiteral("DOUBLE_CLICK"))) {
                host_.recordFixtureEvent(QStringLiteral("r4_item_action"), {
                    {QStringLiteral("region_id"), regionId},
                    {QStringLiteral("action"), action},
                    {QStringLiteral("interaction_mode"), state_.interaction},
                    {QStringLiteral("launch_performed"), false},
                    {QStringLiteral("fixture_only"), true}
                });
                return;
            } else {
                return;
            }

            if (!applyProjection(
                    host_, adapter_, visual_, islands_, state_, QStringLiteral("FINAL"),
                    reason, &commandError)) {
                fail(commandError.isEmpty()
                         ? QStringLiteral("R4_COMMAND_COMMIT_FAILED")
                         : commandError);
                return;
            }
            const qint64 elapsedMicros = commandClock.nsecsElapsed() / 1000;
            host_.recordFixtureEvent(QStringLiteral("r4_command_latency_sample"), {
                {QStringLiteral("region_id"), regionId},
                {QStringLiteral("region_role"), role},
                {QStringLiteral("action"), action},
                {QStringLiteral("projection_reason"), reason},
                {QStringLiteral("latency_ms"), elapsedMicros / 1000.0},
                {QStringLiteral("interaction_budget_ms"), 100},
                {QStringLiteral("budget_result"),
                 elapsedMicros <= 100000
                     ? QStringLiteral("PASS")
                     : QStringLiteral("FAIL")}
            });
        });
    }

    void handle(
        const QString& regionId,
        const QString& role,
        const QString& action,
        const QPoint& screenPoint)
    {
        if (failed_) return;
        if (action == QStringLiteral("POINTER_DOWN")) {
            beginGesture(role, screenPoint);
        } else if (action == QStringLiteral("POINTER_MOVE")) {
            schedulePreview(screenPoint);
        } else if (action == QStringLiteral("POINTER_UP")) {
            endGesture(screenPoint);
        } else if (action == QStringLiteral("LEFT_CLICK") ||
                   action == QStringLiteral("RIGHT_CLICK") ||
                   action == QStringLiteral("DOUBLE_CLICK")) {
            deferCommand(regionId, role, action);
        }
    }

    void fail(const QString& reason)
    {
        if (failed_) return;
        failed_ = true;
        error_ = reason;
        gesture_.active = false;
        host_.recordFixtureEvent(QStringLiteral("r4_manual_interaction_failed"), {
            {QStringLiteral("error"), reason},
            {QStringLiteral("result"), QStringLiteral("FAIL")}
        });
    }

    DesktopHostCoordinator& host_;
    QtQuickSurfaceAdapter& adapter_;
    LayeredVisualSurface& visual_;
    InputIslandSet& islands_;
    FixtureState& state_;
    Gesture gesture_;
    QTimer previewTimer_;
    QPoint pendingCursor_;
    bool failed_ = false;
    QString error_;
};

class R4CControlCenterRoleProbe final {
public:
    explicit R4CControlCenterRoleProbe(DesktopHostCoordinator& host)
        : host_(host), instance_(GetModuleHandleW(nullptr))
    {
    }

    ~R4CControlCenterRoleProbe()
    {
        clear();
    }

    bool initialize(QString* error)
    {
        WNDCLASSEXW windowClass {};
        windowClass.cbSize = sizeof(windowClass);
        windowClass.lpfnWndProc = &R4CControlCenterRoleProbe::windowProc;
        windowClass.hInstance = instance_;
        windowClass.hCursor = LoadCursorW(nullptr, IDC_ARROW);
        windowClass.hbrBackground = CreateSolidBrush(RGB(12, 18, 25));
        windowClass.lpszClassName = className_;
        backgroundBrush_ = windowClass.hbrBackground;
        if (RegisterClassExW(&windowClass) == 0) {
            if (error != nullptr) {
                *error = QStringLiteral("R4C_CONTROL_CENTER_CLASS_REGISTER_FAILED: %1")
                    .arg(GetLastError());
            }
            clear();
            return false;
        }
        registered_ = true;

        const int width = 520;
        const int height = 320;
        const int screenWidth = GetSystemMetrics(SM_CXSCREEN);
        const int screenHeight = GetSystemMetrics(SM_CYSCREEN);
        window_ = CreateWindowExW(
            WS_EX_APPWINDOW,
            className_,
            L"DesktopUp R4-C Control Center Role Probe",
            WS_OVERLAPPEDWINDOW,
            std::max(0, (screenWidth - width) / 2),
            std::max(0, (screenHeight - height) / 2),
            width,
            height,
            nullptr,
            nullptr,
            instance_,
            this);
        if (window_ == nullptr) {
            if (error != nullptr) {
                *error = QStringLiteral("R4C_CONTROL_CENTER_CREATE_FAILED: %1")
                    .arg(GetLastError());
            }
            clear();
            return false;
        }
        if (!RegisterHotKey(
                window_, hotkeyId_,
                MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT,
                static_cast<UINT>('C'))) {
            if (error != nullptr) {
                *error = QStringLiteral("R4C_CONTROL_CENTER_HOTKEY_FAILED: %1")
                    .arg(GetLastError());
            }
            clear();
            return false;
        }
        hotkeyRegistered_ = true;
        host_.recordFixtureEvent(
            QStringLiteral("r4c_control_center_role_probe_ready"), {
                {QStringLiteral("shortcut"), QStringLiteral("CTRL+SHIFT+C")},
                {QStringLiteral("window_role"),
                 QStringLiteral("CONTROL_CENTER_ROLE_PROBE")},
                {QStringLiteral("product_control_center_implemented"), false},
                {QStringLiteral("topmost"), false},
                {QStringLiteral("ordinary_top_level_fallback"), false}
            });
        return true;
    }

    void clear()
    {
        if (hotkeyRegistered_) {
            UnregisterHotKey(window_, hotkeyId_);
            hotkeyRegistered_ = false;
        }
        if (window_ != nullptr && IsWindow(window_)) {
            DestroyWindow(window_);
        }
        window_ = nullptr;
        if (registered_) {
            UnregisterClassW(className_, instance_);
            registered_ = false;
        }
        if (backgroundBrush_ != nullptr) {
            DeleteObject(backgroundBrush_);
            backgroundBrush_ = nullptr;
        }
    }

    int openCount() const { return openCount_; }
    int closeCount() const { return closeCount_; }
    void openFromLifecycle() { show(); }

private:
    void show()
    {
        if (window_ == nullptr || !IsWindow(window_) || IsWindowVisible(window_)) {
            return;
        }
        ShowWindow(window_, SW_SHOWNORMAL);
        SetForegroundWindow(window_);
        ++openCount_;
        host_.recordFixtureEvent(
            QStringLiteral("r4c_control_center_role_probe_opened"), {
                {QStringLiteral("open_count"), openCount_},
                {QStringLiteral("window_role"),
                 QStringLiteral("CONTROL_CENTER_ROLE_PROBE")},
                {QStringLiteral("topmost"), false},
                {QStringLiteral("ordinary_top_level_fallback"), false}
            });
    }

    void hide()
    {
        if (window_ == nullptr || !IsWindow(window_) || !IsWindowVisible(window_)) {
            return;
        }
        ShowWindow(window_, SW_HIDE);
        ++closeCount_;
        host_.recordFixtureEvent(
            QStringLiteral("r4c_control_center_role_probe_closed"), {
                {QStringLiteral("close_count"), closeCount_},
                {QStringLiteral("window_role"),
                 QStringLiteral("CONTROL_CENTER_ROLE_PROBE")},
                {QStringLiteral("topmost"), false},
                {QStringLiteral("ordinary_top_level_fallback"), false}
            });
    }

    void toggle()
    {
        if (window_ != nullptr && IsWindowVisible(window_)) hide();
        else show();
    }

    static LRESULT CALLBACK windowProc(
        HWND window, UINT message, WPARAM wParam, LPARAM lParam)
    {
        auto* self = reinterpret_cast<R4CControlCenterRoleProbe*>(
            GetWindowLongPtrW(window, GWLP_USERDATA));
        if (message == WM_NCCREATE) {
            const auto* create = reinterpret_cast<CREATESTRUCTW*>(lParam);
            self = static_cast<R4CControlCenterRoleProbe*>(create->lpCreateParams);
            SetWindowLongPtrW(
                window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(self));
        }
        switch (message) {
        case WM_HOTKEY:
            if (self != nullptr &&
                static_cast<int>(wParam) == self->hotkeyId_) {
                self->toggle();
            }
            return 0;
        case WM_CLOSE:
            if (self != nullptr) self->hide();
            return 0;
        case WM_ERASEBKGND:
            return 1;
        case WM_PAINT: {
            PAINTSTRUCT paint {};
            HDC dc = BeginPaint(window, &paint);
            RECT client {};
            GetClientRect(window, &client);
            HBRUSH background = CreateSolidBrush(RGB(12, 18, 25));
            FillRect(dc, &client, background);
            DeleteObject(background);
            SetBkMode(dc, TRANSPARENT);
            SetTextColor(dc, RGB(126, 224, 255));
            HFONT font = CreateFontW(
                28, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE,
                DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
                CLEARTYPE_QUALITY, DEFAULT_PITCH, L"Segoe UI");
            HGDIOBJ oldFont = SelectObject(dc, font);
            RECT title {32, 32, client.right - 32, 82};
            DrawTextW(
                dc, L"R4-C CONTROL CENTER ROLE PROBE", -1, &title,
                DT_LEFT | DT_TOP | DT_NOPREFIX);
            SelectObject(dc, oldFont);
            DeleteObject(font);
            SetTextColor(dc, RGB(220, 228, 236));
            RECT body {32, 104, client.right - 32, client.bottom - 32};
            DrawTextW(
                dc,
                L"This is an ordinary non-TopMost diagnostic window.\n\n"
                L"It validates the control-center z-order/focus role only; "
                L"it is not the product control center.\n\n"
                L"Press Ctrl+Shift+C or close this window to hide it.",
                -1, &body,
                DT_LEFT | DT_TOP | DT_WORDBREAK | DT_NOPREFIX);
            EndPaint(window, &paint);
            return 0;
        }
        default:
            return DefWindowProcW(window, message, wParam, lParam);
        }
    }

    const wchar_t* className_ =
        L"DesktopUpTSC01R4CControlCenterRoleProbe";
    static constexpr int hotkeyId_ = 0xD4C;
    DesktopHostCoordinator& host_;
    HINSTANCE instance_ = nullptr;
    HWND window_ = nullptr;
    HBRUSH backgroundBrush_ = nullptr;
    bool registered_ = false;
    bool hotkeyRegistered_ = false;
    int openCount_ = 0;
    int closeCount_ = 0;
};

class R4CShellLifecycleController final {
public:
    using CleanupCallback = std::function<void()>;
    using RecoverCallback = std::function<bool(QString*)>;

    R4CShellLifecycleController(
        DesktopHostCoordinator& host,
        CleanupCallback cleanup,
        RecoverCallback recover)
        : host_(host),
          cleanup_(std::move(cleanup)),
          recover_(std::move(recover))
    {
        timer_.setInterval(sampleIntervalMs_);
        timer_.setTimerType(Qt::PreciseTimer);
        QObject::connect(&timer_, &QTimer::timeout, &timer_, [this]() {
            sample();
        });
    }

    ~R4CShellLifecycleController()
    {
        timer_.stop();
    }

    void start()
    {
        invalidSamples_ = 0;
        timer_.start();
        host_.recordFixtureEvent(
            QStringLiteral("r4c_shell_lifecycle_monitor_ready"), {
                {QStringLiteral("sample_interval_ms"), sampleIntervalMs_},
                {QStringLiteral("invalid_samples_before_loss"),
                 invalidSamplesBeforeLoss_},
                {QStringLiteral("stable_samples_before_recovery"),
                 stableSamplesBeforeRecovery_},
                {QStringLiteral("recovery_budget_ms"), recoveryBudgetMs_},
                {QStringLiteral("strategy"),
                 QStringLiteral("BOUNDED_SAFE_REDISCOVERY_NO_TOP_LEVEL_FALLBACK")},
                {QStringLiteral("topmost"), false},
                {QStringLiteral("ordinary_top_level_fallback"), false}
            });
    }

    bool failed() const { return failed_; }
    QString error() const { return error_; }
    int recoveryCount() const { return recoveryCount_; }
    int degradedCount() const { return degradedCount_; }
    bool degraded() const { return degraded_; }

private:
    void fail(const QString& reason)
    {
        if (failed_) return;
        failed_ = true;
        error_ = reason;
        timer_.stop();
        host_.recordFixtureEvent(
            QStringLiteral("r4c_shell_lifecycle_failed"), {
                {QStringLiteral("reason"), reason},
                {QStringLiteral("recovery_count"), recoveryCount_},
                {QStringLiteral("degraded_count"), degradedCount_},
                {QStringLiteral("topmost"), false},
                {QStringLiteral("ordinary_top_level_fallback"), false},
                {QStringLiteral("result"), QStringLiteral("FAIL")}
            });
    }

    void sample()
    {
        if (failed_) return;
        if (!degraded_) {
            if (host_.shellAttachmentStillValid()) {
                invalidSamples_ = 0;
                return;
            }
            ++invalidSamples_;
            if (invalidSamples_ < invalidSamplesBeforeLoss_) return;

            host_.recordFixtureEvent(
                QStringLiteral("r4c_shell_generation_change_detected"), {
                    {QStringLiteral("invalid_samples"), invalidSamples_},
                    {QStringLiteral("topmost"), false},
                    {QStringLiteral("ordinary_top_level_fallback"), false}
                });
            cleanup_();
            QString degradeError;
            if (!host_.enterShellDegraded(
                    QStringLiteral("R4C_EXPLORER_PARENT_TREE_INVALID"),
                    &degradeError)) {
                fail(degradeError.isEmpty()
                         ? QStringLiteral("R4C_ENTER_DEGRADED_FAILED")
                         : degradeError);
                return;
            }
            degraded_ = true;
            ++degradedCount_;
            invalidSamples_ = 0;
            stableSignature_.clear();
            stableSamples_ = 0;
            recoveryClock_.restart();
            host_.recordFixtureEvent(
                QStringLiteral("r4c_shell_recovery_cycle_started"), {
                    {QStringLiteral("degraded_count"), degradedCount_},
                    {QStringLiteral("recovery_budget_ms"), recoveryBudgetMs_},
                    {QStringLiteral("topmost"), false},
                    {QStringLiteral("ordinary_top_level_fallback"), false}
                });
            return;
        }

        if (recoveryClock_.isValid() &&
            recoveryClock_.elapsed() > recoveryBudgetMs_) {
            host_.recordFixtureEvent(
                QStringLiteral("r4c_shell_recovery_budget_exhausted"), {
                    {QStringLiteral("degraded_count"), degradedCount_},
                    {QStringLiteral("elapsed_ms"), recoveryClock_.elapsed()},
                    {QStringLiteral("topmost"), false},
                    {QStringLiteral("ordinary_top_level_fallback"), false},
                    {QStringLiteral("result"), QStringLiteral("FAIL")}
                });
            fail(QStringLiteral("R4C_SHELL_RECOVERY_BUDGET_EXHAUSTED"));
            return;
        }

        const QString candidateSignature =
            host_.discoverSafeShellSignature();
        if (candidateSignature.isEmpty()) {
            stableSignature_.clear();
            stableSamples_ = 0;
            return;
        }
        if (candidateSignature == stableSignature_) {
            ++stableSamples_;
        } else {
            stableSignature_ = candidateSignature;
            stableSamples_ = 1;
        }
        if (stableSamples_ < stableSamplesBeforeRecovery_) return;

        timer_.stop();
        QString recoveryError;
        if (!recover_(&recoveryError)) {
            fail(recoveryError.isEmpty()
                     ? QStringLiteral("R4C_SHELL_RECOVERY_ATTACH_FAILED")
                     : recoveryError);
            return;
        }
        ++recoveryCount_;
        degraded_ = false;
        stableSignature_.clear();
        stableSamples_ = 0;
        invalidSamples_ = 0;
        timer_.start();
        host_.recordFixtureEvent(
            QStringLiteral("r4c_shell_recovery_cycle_completed"), {
                {QStringLiteral("recovery_count"), recoveryCount_},
                {QStringLiteral("degraded_count"), degradedCount_},
                {QStringLiteral("recovery_elapsed_ms"),
                 recoveryClock_.elapsed()},
                {QStringLiteral("topmost"), false},
                {QStringLiteral("ordinary_top_level_fallback"), false},
                {QStringLiteral("result"), QStringLiteral("PASS")}
            });
    }

    static constexpr int sampleIntervalMs_ = 250;
    static constexpr int invalidSamplesBeforeLoss_ = 2;
    static constexpr int stableSamplesBeforeRecovery_ = 4;
    static constexpr int recoveryBudgetMs_ = 30000;
    DesktopHostCoordinator& host_;
    CleanupCallback cleanup_;
    RecoverCallback recover_;
    QTimer timer_;
    QElapsedTimer recoveryClock_;
    QString stableSignature_;
    int invalidSamples_ = 0;
    int stableSamples_ = 0;
    int recoveryCount_ = 0;
    int degradedCount_ = 0;
    bool degraded_ = false;
    bool failed_ = false;
    QString error_;
};

struct R4DDisplaySnapshot {
    QRect monitor;
    QRect work;
    UINT dpi = 96;
};

R4DDisplaySnapshot queryR4DPrimaryDisplay()
{
    MONITORINFO info {};
    info.cbSize = sizeof(info);
    GetMonitorInfoW(
        MonitorFromPoint(POINT {0, 0}, MONITOR_DEFAULTTOPRIMARY), &info);
    const UINT dpi = GetDpiForSystem();
    return {
        QRect(info.rcMonitor.left, info.rcMonitor.top,
              info.rcMonitor.right - info.rcMonitor.left,
              info.rcMonitor.bottom - info.rcMonitor.top),
        QRect(info.rcWork.left, info.rcWork.top,
              info.rcWork.right - info.rcWork.left,
              info.rcWork.bottom - info.rcWork.top),
        dpi == 0 ? 96u : dpi
    };
}

bool sameR4DDisplay(
    const R4DDisplaySnapshot& left, const R4DDisplaySnapshot& right)
{
    return left.monitor == right.monitor &&
           left.work == right.work && left.dpi == right.dpi;
}

QJsonArray r4dRectArray(const QRect& rect)
{
    return {rect.left(), rect.top(), rect.right() + 1, rect.bottom() + 1};
}

class R4DDisplayEnvironmentController final {
public:
    R4DDisplayEnvironmentController(
        DesktopHostCoordinator& host,
        QtQuickSurfaceAdapter& adapter,
        LayeredVisualSurface& visual,
        InputIslandSet& islands,
        FixtureState& state)
        : host_(host), adapter_(adapter), visual_(visual),
          islands_(islands), state_(state)
    {
        timer_.setInterval(sampleIntervalMs_);
        timer_.setTimerType(Qt::PreciseTimer);
        QObject::connect(&timer_, &QTimer::timeout, &timer_, [this]() {
            sample();
        });
    }

    ~R4DDisplayEnvironmentController()
    {
        timer_.stop();
    }

    void start()
    {
        display_ = queryR4DPrimaryDisplay();
        timer_.start();
        host_.recordFixtureEvent(
            QStringLiteral("r4d_display_environment_monitor_ready"), {
                {QStringLiteral("scope"),
                 QStringLiteral("PRIMARY_DISPLAY_ONLY")},
                {QStringLiteral("sample_interval_ms"), sampleIntervalMs_},
                {QStringLiteral("monitor_rect_px"),
                 r4dRectArray(display_.monitor)},
                {QStringLiteral("work_area_rect_px"),
                 r4dRectArray(display_.work)},
                {QStringLiteral("effective_dpi"),
                 static_cast<int>(display_.dpi)},
                {QStringLiteral("multi_display"),
                 QStringLiteral("DEFERRED")}
            });
    }

    bool failed() const { return failed_; }
    QString error() const { return error_; }
    int changeCount() const { return changeCount_; }

private:
    static qreal pxToDip(int pixels, UINT dpi)
    {
        return static_cast<qreal>(pixels) * 96.0 /
               static_cast<qreal>(dpi == 0 ? 96 : dpi);
    }

    static int dipToPx(qreal dips, UINT dpi)
    {
        return qRound(dips * static_cast<qreal>(dpi == 0 ? 96 : dpi) / 96.0);
    }

    void fail(const QString& reason)
    {
        if (failed_) return;
        failed_ = true;
        error_ = reason;
        timer_.stop();
        host_.recordFixtureEvent(
            QStringLiteral("r4d_display_environment_reprojection_failed"), {
                {QStringLiteral("reason"), reason},
                {QStringLiteral("display_change_count"), changeCount_},
                {QStringLiteral("result"), QStringLiteral("FAIL")}
            });
    }

    void sample()
    {
        if (failed_) return;
        const R4DDisplaySnapshot next = queryR4DPrimaryDisplay();
        if (sameR4DDisplay(next, display_)) return;

        const QRect source = state_.screenRect;
        const int oldAvailableX =
            std::max(0, display_.work.width() - source.width());
        const int oldAvailableY =
            std::max(0, display_.work.height() - source.height());
        const qreal anchorX = oldAvailableX > 0
            ? qBound(0.0,
                     static_cast<qreal>(source.left() - display_.work.left()) /
                         static_cast<qreal>(oldAvailableX),
                     1.0)
            : 0.0;
        const qreal anchorY = oldAvailableY > 0
            ? qBound(0.0,
                     static_cast<qreal>(source.top() - display_.work.top()) /
                         static_cast<qreal>(oldAvailableY),
                     1.0)
            : 0.0;

        const qreal workWidthDip = pxToDip(next.work.width(), next.dpi);
        const qreal workHeightDip = pxToDip(next.work.height(), next.dpi);
        const qreal maxWidthDip = std::max<qreal>(240.0, workWidthDip * 0.45);
        const qreal maxExpandedHeightDip =
            std::max<qreal>(240.0, workHeightDip - 48.0);
        const qreal widthDip = qBound(
            240.0, pxToDip(source.width(), display_.dpi), maxWidthDip);
        const qreal previousExpandedDip = pxToDip(
            std::max(state_.expandedHeight, source.height()), display_.dpi);
        const qreal expandedHeightDip = qBound(
            240.0, previousExpandedDip, maxExpandedHeightDip);
        const int widthPx = dipToPx(widthDip, next.dpi);
        const int expandedHeightPx = dipToPx(expandedHeightDip, next.dpi);
        const int heightPx = state_.presentation == QStringLiteral("COLLAPSED")
            ? dipToPx(60.0, next.dpi)
            : expandedHeightPx;
        const int availableX = std::max(0, next.work.width() - widthPx);
        const int availableY = std::max(0, next.work.height() - heightPx);
        const QRect projected(
            next.work.left() + qRound(anchorX * availableX),
            next.work.top() + qRound(anchorY * availableY),
            widthPx,
            heightPx);

        QJsonObject changeFields {
            {QStringLiteral("old_monitor_rect_px"),
             r4dRectArray(display_.monitor)},
            {QStringLiteral("old_work_area_rect_px"),
             r4dRectArray(display_.work)},
            {QStringLiteral("old_effective_dpi"),
             static_cast<int>(display_.dpi)},
            {QStringLiteral("new_monitor_rect_px"),
             r4dRectArray(next.monitor)},
            {QStringLiteral("new_work_area_rect_px"),
             r4dRectArray(next.work)},
            {QStringLiteral("new_effective_dpi"),
             static_cast<int>(next.dpi)},
            {QStringLiteral("normalized_anchor"), QJsonObject {
                {QStringLiteral("x"), anchorX},
                {QStringLiteral("y"), anchorY}
            }},
            {QStringLiteral("logical_geometry_dip"), QJsonObject {
                {QStringLiteral("width"), widthDip},
                {QStringLiteral("expanded_height"), expandedHeightDip},
                {QStringLiteral("collapsed_height"), 60.0}
            }},
            {QStringLiteral("projected_screen_rect_px"),
             r4dRectArray(projected)},
            {QStringLiteral("change_kind"),
             display_.dpi != next.dpi
                 ? QStringLiteral("DPI_OR_DISPLAY")
                 : QStringLiteral("RESOLUTION_OR_WORK_AREA")}
        };
        QString updateError;
        if (!host_.advanceDisplayEnvironmentRevision(
                changeFields, &updateError)) {
            fail(updateError.isEmpty()
                     ? QStringLiteral("R4D_DISPLAY_REVISION_ADVANCE_FAILED")
                     : updateError);
            return;
        }

        state_.screenRect = projected;
        state_.expandedHeight = expandedHeightPx;
        if (!applyProjection(
                host_, adapter_, visual_, islands_, state_,
                QStringLiteral("FINAL"),
                QStringLiteral("R4D_DISPLAY_ENVIRONMENT_REPROJECT"),
                &updateError)) {
            fail(updateError.isEmpty()
                     ? QStringLiteral("R4D_DISPLAY_REPROJECT_FAILED")
                     : updateError);
            return;
        }
        display_ = next;
        ++changeCount_;
        host_.recordFixtureEvent(
            QStringLiteral("r4d_display_environment_reprojected"), {
                {QStringLiteral("display_change_count"), changeCount_},
                {QStringLiteral("normalized_anchor"), QJsonObject {
                    {QStringLiteral("x"), anchorX},
                    {QStringLiteral("y"), anchorY}
                }},
                {QStringLiteral("projected_screen_rect_px"),
                 r4dRectArray(projected)},
                {QStringLiteral("result"), QStringLiteral("PASS")}
            });
    }

    static constexpr int sampleIntervalMs_ = 250;
    DesktopHostCoordinator& host_;
    QtQuickSurfaceAdapter& adapter_;
    LayeredVisualSurface& visual_;
    InputIslandSet& islands_;
    FixtureState& state_;
    QTimer timer_;
    R4DDisplaySnapshot display_;
    int changeCount_ = 0;
    bool failed_ = false;
    QString error_;
};

class R4D2ThemeController final {
public:
    R4D2ThemeController(
        DesktopHostCoordinator& host,
        QtQuickSurfaceAdapter& adapter,
        LayeredVisualSurface& visual,
        InputIslandSet& islands,
        FixtureState& state)
        : host_(host), adapter_(adapter), visual_(visual),
          islands_(islands), state_(state),
          instance_(GetModuleHandleW(nullptr))
    {
        debounceTimer_.setSingleShot(true);
        debounceTimer_.setInterval(debounceIntervalMs_);
        debounceTimer_.setTimerType(Qt::PreciseTimer);
        QObject::connect(
            &debounceTimer_, &QTimer::timeout, &debounceTimer_, [this]() {
                applyPending();
            });
    }

    ~R4D2ThemeController()
    {
        clear();
    }

    bool initialize(bool runSelfTest, QString* error)
    {
        WNDCLASSEXW windowClass {};
        windowClass.cbSize = sizeof(windowClass);
        windowClass.lpfnWndProc = &R4D2ThemeController::windowProc;
        windowClass.hInstance = instance_;
        windowClass.hCursor = LoadCursorW(nullptr, IDC_ARROW);
        windowClass.hbrBackground = GetSysColorBrush(COLOR_WINDOW);
        windowClass.lpszClassName = className_;
        if (RegisterClassExW(&windowClass) == 0) {
            if (error != nullptr) {
                *error = QStringLiteral("R4D2_HOTKEY_CLASS_REGISTER_FAILED: %1")
                    .arg(GetLastError());
            }
            return false;
        }
        registered_ = true;

        const DWORD extendedStyle = runSelfTest
            ? WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE
            : WS_EX_APPWINDOW;
        const DWORD windowStyle = runSelfTest
            ? WS_POPUP
            : WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX;
        window_ = CreateWindowExW(
            extendedStyle,
            className_,
            L"DesktopUp R4-D2 Theme / Fallback Manual Probe",
            windowStyle,
            runSelfTest ? 0 : 48,
            runSelfTest ? 0 : 64,
            runSelfTest ? 1 : 700,
            runSelfTest ? 1 : 430,
            nullptr,
            nullptr,
            instance_,
            this);
        if (window_ == nullptr) {
            if (error != nullptr) {
                *error = QStringLiteral("R4D2_HOTKEY_WINDOW_CREATE_FAILED: %1")
                    .arg(GetLastError());
            }
            clear();
            return false;
        }

        if (!runSelfTest) {
            const auto createButton = [this](
                int identifier, const wchar_t* label,
                int x, int y, int width, int height) -> bool {
                HWND button = CreateWindowExW(
                    0,
                    L"BUTTON",
                    label,
                    WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_PUSHBUTTON,
                    x, y, width, height,
                    window_,
                    reinterpret_cast<HMENU>(static_cast<INT_PTR>(identifier)),
                    instance_,
                    nullptr);
                if (button == nullptr) return false;
                manualButtons_.push_back(button);
                return true;
            };
            if (!createButton(hotkeyTheme1_, L"1  Transparent Default", 20, 116, 205, 42) ||
                !createButton(hotkeyTheme2_, L"2  Neon HUD", 238, 116, 205, 42) ||
                !createButton(hotkeyTheme3_, L"3  Cinema Lightbox", 456, 116, 205, 42) ||
                !createButton(hotkeyUnknown_, L"F  Unknown Theme", 20, 176, 205, 42) ||
                !createButton(hotkeyResource_, L"R  Resource Failure", 238, 176, 205, 42) ||
                !createButton(hotkeyParameters_, L"P  Invalid Parameters", 456, 176, 205, 42) ||
                !createButton(hotkeyFallback_, L"0  No Last-Known-Good -> Minimal Fallback", 20, 236, 641, 42)) {
                if (error != nullptr) {
                    *error = QStringLiteral("R4D2_MANUAL_BUTTON_CREATE_FAILED: %1")
                        .arg(GetLastError());
                }
                clear();
                return false;
            }
        }

        const std::vector<std::pair<int, UINT>> bindings {
            {hotkeyTheme1_, static_cast<UINT>('1')},
            {hotkeyTheme2_, static_cast<UINT>('2')},
            {hotkeyTheme3_, static_cast<UINT>('3')},
            {hotkeyUnknown_, static_cast<UINT>('F')},
            {hotkeyResource_, static_cast<UINT>('R')},
            {hotkeyParameters_, static_cast<UINT>('P')},
            {hotkeyFallback_, static_cast<UINT>('0')}
        };
        for (const auto& [identifier, key] : bindings) {
            if (!RegisterHotKey(
                    window_, identifier,
                    MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT,
                    key)) {
                if (error != nullptr) {
                    *error = QStringLiteral("R4D2_HOTKEY_REGISTER_FAILED_%1: %2")
                        .arg(identifier)
                        .arg(GetLastError());
                }
                clear();
                return false;
            }
            registeredHotkeys_.push_back(identifier);
        }

        host_.recordFixtureEvent(
            QStringLiteral("r4d2_theme_controller_ready"), {
                {QStringLiteral("normal_theme_ids"), QJsonArray {
                    QStringLiteral("transparent-default"),
                    QStringLiteral("neon-hud"),
                    QStringLiteral("cinema-lightbox")
                }},
                {QStringLiteral("fallback_theme_id"),
                 QStringLiteral("minimal-fallback")},
                {QStringLiteral("fallback_in_normal_selector"), false},
                {QStringLiteral("debounce_ms"), debounceIntervalMs_},
                {QStringLiteral("selected_theme_id"), state_.selectedThemeId},
                {QStringLiteral("effective_theme_id"), state_.effectiveThemeId},
                {QStringLiteral("theme_revision"),
                 static_cast<qint64>(state_.themeRevision)},
                {QStringLiteral("self_test"), runSelfTest},
                {QStringLiteral("manual_control_entry"),
                 runSelfTest
                     ? QStringLiteral("NONE")
                     : QStringLiteral("VISIBLE_NATIVE_BUTTON_PANEL")},
                {QStringLiteral("registered_hotkey_count"),
                 static_cast<int>(registeredHotkeys_.size())}
            });
        host_.recordFixtureEvent(
            QStringLiteral("r4d2_theme_isolation_contract_ready"), {
                {QStringLiteral("payload_model"),
                 QStringLiteral("HOST_VALIDATED_BUILTIN_STRUCTURED_ENUM")},
                {QStringLiteral("arbitrary_qml"), false},
                {QStringLiteral("arbitrary_script"), false},
                {QStringLiteral("native_code"), false},
                {QStringLiteral("file_capability"), false},
                {QStringLiteral("network_capability"), false},
                {QStringLiteral("process_capability"), false},
                {QStringLiteral("registry_capability"), false},
                {QStringLiteral("credential_capability"), false},
                {QStringLiteral("generic_ipc_capability"), false},
                {QStringLiteral("host_owns_protected_interaction"), true},
                {QStringLiteral("product_theme_package_platform_implemented"),
                 false}
            });
        if (runSelfTest) {
            scheduleSelfTest();
        } else {
            ShowWindow(window_, SW_SHOWNORMAL);
            UpdateWindow(window_);
            SetForegroundWindow(window_);
        }
        return true;
    }

    void clear()
    {
        debounceTimer_.stop();
        for (const int identifier : registeredHotkeys_) {
            if (window_ != nullptr) UnregisterHotKey(window_, identifier);
        }
        registeredHotkeys_.clear();
        manualButtons_.clear();
        if (window_ != nullptr && IsWindow(window_)) {
            DestroyWindow(window_);
        }
        window_ = nullptr;
        if (registered_) {
            UnregisterClassW(className_, instance_);
            registered_ = false;
        }
    }

    bool failed() const { return failed_; }
    QString error() const { return error_; }
    int committedCount() const { return committedCount_; }
    int rejectedCount() const { return rejectedCount_; }
    int fallbackCount() const { return fallbackCount_; }
    int supersededRequestCount() const { return supersededRequestCount_; }
    bool selfTestCompleted() const { return selfTestCompleted_; }

private:
    enum class FaultKind {
        None,
        UnknownTheme,
        ResourceFailure,
        InvalidParameters,
        NoLastKnownGood
    };

    struct PendingRequest {
        QString themeId;
        QString source;
        FaultKind fault = FaultKind::None;
        quint64 requestRevision = 0;
    };

    static bool isNormalTheme(const QString& themeId)
    {
        return themeId == QStringLiteral("transparent-default") ||
               themeId == QStringLiteral("neon-hud") ||
               themeId == QStringLiteral("cinema-lightbox");
    }

    static QString faultName(FaultKind fault)
    {
        switch (fault) {
        case FaultKind::None:
            return QStringLiteral("NONE");
        case FaultKind::UnknownTheme:
            return QStringLiteral("UNKNOWN_THEME");
        case FaultKind::ResourceFailure:
            return QStringLiteral("RESOURCE_FAILURE");
        case FaultKind::InvalidParameters:
            return QStringLiteral("INVALID_PARAMETERS");
        case FaultKind::NoLastKnownGood:
            return QStringLiteral("NO_LAST_KNOWN_GOOD");
        }
        return QStringLiteral("UNKNOWN");
    }

    void request(
        const QString& themeId, FaultKind fault, const QString& source)
    {
        if (failed_) return;
        const quint64 nextRequestRevision = ++requestRevision_;
        if (pendingActive_) {
            ++supersededRequestCount_;
            host_.recordFixtureEvent(
                QStringLiteral("r4d2_theme_request_superseded"), {
                    {QStringLiteral("superseded_request_revision"),
                     static_cast<qint64>(pending_.requestRevision)},
                    {QStringLiteral("winning_request_revision"),
                     static_cast<qint64>(nextRequestRevision)},
                    {QStringLiteral("superseded_theme_id"), pending_.themeId},
                    {QStringLiteral("winning_theme_id"), themeId}
                });
        }
        pending_ = {themeId, source, fault, nextRequestRevision};
        pendingActive_ = true;
        debounceTimer_.start();
        if (window_ != nullptr && IsWindow(window_)) {
            InvalidateRect(window_, nullptr, FALSE);
            QTimer::singleShot(
                debounceIntervalMs_ + 50, &debounceTimer_, [this]() {
                    if (window_ != nullptr && IsWindow(window_)) {
                        InvalidateRect(window_, nullptr, FALSE);
                    }
                });
        }
        host_.recordFixtureEvent(
            QStringLiteral("r4d2_theme_switch_requested"), {
                {QStringLiteral("request_revision"),
                 static_cast<qint64>(nextRequestRevision)},
                {QStringLiteral("requested_theme_id"), themeId},
                {QStringLiteral("fault_kind"), faultName(fault)},
                {QStringLiteral("source"), source},
                {QStringLiteral("debounce_ms"), debounceIntervalMs_}
            });
    }

    bool commitCandidate(
        const FixtureState& candidate,
        const QString& reason,
        const PendingRequest& request)
    {
        QElapsedTimer clock;
        clock.start();
        QString applyError;
        if (!applyProjection(
                host_, adapter_, visual_, islands_, candidate,
                QStringLiteral("FINAL"), reason, &applyError)) {
            QString rollbackError;
            const bool rollbackSucceeded = applyProjection(
                host_, adapter_, visual_, islands_, state_,
                QStringLiteral("FINAL"),
                QStringLiteral("R4D2_THEME_ROLLBACK"),
                &rollbackError);
            host_.recordFixtureEvent(
                QStringLiteral("r4d2_theme_switch_failed"), {
                    {QStringLiteral("request_revision"),
                     static_cast<qint64>(request.requestRevision)},
                    {QStringLiteral("requested_theme_id"), request.themeId},
                    {QStringLiteral("failure_reason"), applyError},
                    {QStringLiteral("rollback_succeeded"), rollbackSucceeded},
                    {QStringLiteral("rollback_error"), rollbackError},
                    {QStringLiteral("result"), QStringLiteral("FAIL")}
                });
            fail(rollbackSucceeded
                     ? QStringLiteral("R4D2_THEME_RENDER_FAILED")
                     : QStringLiteral("R4D2_THEME_ROLLBACK_FAILED"));
            return false;
        }
        state_ = candidate;
        const qint64 elapsedMicros = clock.nsecsElapsed() / 1000;
        host_.recordFixtureEvent(
            QStringLiteral("r4d2_theme_switch_committed"), {
                {QStringLiteral("request_revision"),
                 static_cast<qint64>(request.requestRevision)},
                {QStringLiteral("selected_theme_id"), state_.selectedThemeId},
                {QStringLiteral("effective_theme_id"), state_.effectiveThemeId},
                {QStringLiteral("last_known_good_theme_id"),
                 state_.lastKnownGoodThemeId},
                {QStringLiteral("theme_revision"),
                 static_cast<qint64>(state_.themeRevision)},
                {QStringLiteral("fallback_active"),
                 state_.themeFallbackActive},
                {QStringLiteral("screen_rect_px"),
                 r4dRectArray(state_.screenRect)},
                {QStringLiteral("presentation_mode"), state_.presentation},
                {QStringLiteral("interaction_mode"), state_.interaction},
                {QStringLiteral("is_locked"), state_.locked},
                {QStringLiteral("latency_ms"), elapsedMicros / 1000.0},
                {QStringLiteral("switch_budget_ms"), 200},
                {QStringLiteral("budget_result"),
                 elapsedMicros <= 200000
                     ? QStringLiteral("PASS")
                     : QStringLiteral("FAIL")},
                {QStringLiteral("source"), request.source}
            });
        return true;
    }

    void applyPending()
    {
        if (failed_ || !pendingActive_) return;
        const PendingRequest request = pending_;
        pendingActive_ = false;

        if (request.fault == FaultKind::None &&
            isNormalTheme(request.themeId)) {
            FixtureState candidate = state_;
            candidate.selectedThemeId = request.themeId;
            candidate.effectiveThemeId = request.themeId;
            candidate.lastKnownGoodThemeId = request.themeId;
            candidate.themeFallbackActive = false;
            ++candidate.themeRevision;
            if (!commitCandidate(
                    candidate,
                    QStringLiteral("R4D2_THEME_SWITCH"),
                    request)) {
                return;
            }
            ++committedCount_;
            if (request.source == QStringLiteral("SELF_TEST_RECOVER")) {
                completeSelfTest();
            }
            return;
        }

        ++rejectedCount_;
        host_.recordFixtureEvent(
            QStringLiteral("r4d2_theme_switch_rejected"), {
                {QStringLiteral("request_revision"),
                 static_cast<qint64>(request.requestRevision)},
                {QStringLiteral("requested_theme_id"), request.themeId},
                {QStringLiteral("fault_kind"), faultName(request.fault)},
                {QStringLiteral("last_known_good_theme_id"),
                 state_.lastKnownGoodThemeId},
                {QStringLiteral("source"), request.source}
            });

        if (request.fault != FaultKind::NoLastKnownGood &&
            isNormalTheme(state_.lastKnownGoodThemeId)) {
            host_.recordFixtureEvent(
                QStringLiteral("r4d2_theme_fault_resolved"), {
                    {QStringLiteral("request_revision"),
                     static_cast<qint64>(request.requestRevision)},
                    {QStringLiteral("fault_kind"), faultName(request.fault)},
                    {QStringLiteral("resolution"),
                     QStringLiteral("LAST_KNOWN_GOOD_RETAINED")},
                    {QStringLiteral("selected_theme_id"),
                     state_.selectedThemeId},
                    {QStringLiteral("effective_theme_id"),
                     state_.effectiveThemeId},
                    {QStringLiteral("theme_revision"),
                     static_cast<qint64>(state_.themeRevision)},
                    {QStringLiteral("projection_changed"), false},
                    {QStringLiteral("fallback_written_to_selection"), false}
                });
            return;
        }

        FixtureState candidate = state_;
        candidate.selectedThemeId = request.themeId;
        candidate.effectiveThemeId = QStringLiteral("minimal-fallback");
        candidate.lastKnownGoodThemeId.clear();
        candidate.themeFallbackActive = true;
        ++candidate.themeRevision;
        if (!commitCandidate(
                candidate,
                QStringLiteral("R4D2_MINIMAL_FALLBACK"),
                request)) {
            return;
        }
        ++fallbackCount_;
        host_.recordFixtureEvent(
            QStringLiteral("r4d2_theme_fault_resolved"), {
                {QStringLiteral("request_revision"),
                 static_cast<qint64>(request.requestRevision)},
                {QStringLiteral("fault_kind"), faultName(request.fault)},
                {QStringLiteral("resolution"),
                 QStringLiteral("MINIMAL_FALLBACK_PRESENTED")},
                {QStringLiteral("selected_theme_id"),
                 state_.selectedThemeId},
                {QStringLiteral("effective_theme_id"),
                 state_.effectiveThemeId},
                {QStringLiteral("theme_revision"),
                 static_cast<qint64>(state_.themeRevision)},
                {QStringLiteral("projection_changed"), true},
                {QStringLiteral("fallback_written_to_selection"), false}
            });
    }

    void scheduleSelfTest()
    {
        QTimer::singleShot(0, [this]() {
            request(
                QStringLiteral("neon-hud"), FaultKind::None,
                QStringLiteral("SELF_TEST_BURST_1"));
        });
        QTimer::singleShot(50, [this]() {
            request(
                QStringLiteral("cinema-lightbox"), FaultKind::None,
                QStringLiteral("SELF_TEST_BURST_2"));
        });
        QTimer::singleShot(100, [this]() {
            request(
                QStringLiteral("transparent-default"), FaultKind::None,
                QStringLiteral("SELF_TEST_BURST_WINNER"));
        });
        QTimer::singleShot(500, [this]() {
            request(
                QStringLiteral("neon-hud"), FaultKind::None,
                QStringLiteral("SELF_TEST_THEME_2"));
        });
        QTimer::singleShot(900, [this]() {
            request(
                QStringLiteral("cinema-lightbox"), FaultKind::None,
                QStringLiteral("SELF_TEST_THEME_3"));
        });
        QTimer::singleShot(1300, [this]() {
            request(
                QStringLiteral("missing-theme"),
                FaultKind::UnknownTheme,
                QStringLiteral("SELF_TEST_UNKNOWN"));
        });
        QTimer::singleShot(1700, [this]() {
            request(
                QStringLiteral("cinema-lightbox"),
                FaultKind::ResourceFailure,
                QStringLiteral("SELF_TEST_RESOURCE"));
        });
        QTimer::singleShot(2100, [this]() {
            request(
                QStringLiteral("cinema-lightbox"),
                FaultKind::InvalidParameters,
                QStringLiteral("SELF_TEST_PARAMETERS"));
        });
        QTimer::singleShot(2500, [this]() {
            request(
                QStringLiteral("missing-theme-no-lkg"),
                FaultKind::NoLastKnownGood,
                QStringLiteral("SELF_TEST_FALLBACK"));
        });
        QTimer::singleShot(2900, [this]() {
            request(
                QStringLiteral("transparent-default"),
                FaultKind::None,
                QStringLiteral("SELF_TEST_RECOVER"));
        });
    }

    void completeSelfTest()
    {
        if (selfTestCompleted_) return;
        selfTestCompleted_ = true;
        host_.recordFixtureEvent(
            QStringLiteral("r4d2_theme_self_test_completed"), {
                {QStringLiteral("committed_count"), committedCount_},
                {QStringLiteral("rejected_count"), rejectedCount_},
                {QStringLiteral("fallback_count"), fallbackCount_},
                {QStringLiteral("superseded_request_count"),
                 supersededRequestCount_},
                {QStringLiteral("selected_theme_id"), state_.selectedThemeId},
                {QStringLiteral("effective_theme_id"), state_.effectiveThemeId},
                {QStringLiteral("fallback_active"),
                 state_.themeFallbackActive},
                {QStringLiteral("result"), QStringLiteral("PASS")}
            });
    }

    void handleAction(int identifier, const QString& source)
    {
        switch (identifier) {
        case hotkeyTheme1_:
            request(
                QStringLiteral("transparent-default"),
                FaultKind::None,
                source);
            break;
        case hotkeyTheme2_:
            request(
                QStringLiteral("neon-hud"),
                FaultKind::None,
                source);
            break;
        case hotkeyTheme3_:
            request(
                QStringLiteral("cinema-lightbox"),
                FaultKind::None,
                source);
            break;
        case hotkeyUnknown_:
            request(
                QStringLiteral("missing-theme"),
                FaultKind::UnknownTheme,
                source);
            break;
        case hotkeyResource_:
            request(
                state_.selectedThemeId,
                FaultKind::ResourceFailure,
                source);
            break;
        case hotkeyParameters_:
            request(
                state_.selectedThemeId,
                FaultKind::InvalidParameters,
                source);
            break;
        case hotkeyFallback_:
            request(
                QStringLiteral("missing-theme-no-lkg"),
                FaultKind::NoLastKnownGood,
                source);
            break;
        default:
            break;
        }
    }
    void fail(const QString& reason)
    {
        if (failed_) return;
        failed_ = true;
        error_ = reason;
        debounceTimer_.stop();
        host_.recordFixtureEvent(
            QStringLiteral("r4d2_theme_controller_failed"), {
                {QStringLiteral("reason"), reason},
                {QStringLiteral("committed_count"), committedCount_},
                {QStringLiteral("rejected_count"), rejectedCount_},
                {QStringLiteral("fallback_count"), fallbackCount_},
                {QStringLiteral("result"), QStringLiteral("FAIL")}
            });
    }

    static LRESULT CALLBACK windowProc(
        HWND window, UINT message, WPARAM wParam, LPARAM lParam)
    {
        auto* self = reinterpret_cast<R4D2ThemeController*>(
            GetWindowLongPtrW(window, GWLP_USERDATA));
        if (message == WM_NCCREATE) {
            const auto* create =
                reinterpret_cast<const CREATESTRUCTW*>(lParam);
            self = static_cast<R4D2ThemeController*>(
                create->lpCreateParams);
            SetWindowLongPtrW(
                window, GWLP_USERDATA,
                reinterpret_cast<LONG_PTR>(self));
        }
        switch (message) {
        case WM_HOTKEY:
            if (self != nullptr) {
                self->handleAction(
                    static_cast<int>(wParam), QStringLiteral("HOTKEY"));
            }
            return 0;
        case WM_COMMAND:
            if (self != nullptr && HIWORD(wParam) == BN_CLICKED) {
                self->handleAction(
                    LOWORD(wParam), QStringLiteral("MANUAL_BUTTON"));
            }
            return 0;
        case WM_CLOSE:
            ShowWindow(window, SW_MINIMIZE);
            return 0;
        case WM_ERASEBKGND:
            return 1;
        case WM_PAINT: {
            PAINTSTRUCT paint {};
            HDC dc = BeginPaint(window, &paint);
            RECT client {};
            GetClientRect(window, &client);
            FillRect(dc, &client, GetSysColorBrush(COLOR_WINDOW));
            SetBkMode(dc, TRANSPARENT);
            SetTextColor(dc, RGB(22, 36, 48));
            HFONT titleFont = CreateFontW(
                25, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE,
                DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
                CLEARTYPE_QUALITY, DEFAULT_PITCH, L"Segoe UI");
            HGDIOBJ oldFont = SelectObject(dc, titleFont);
            RECT title {20, 16, client.right - 20, 48};
            DrawTextW(
                dc, L"R4-D2 THEME / FALLBACK MANUAL PROBE", -1, &title,
                DT_LEFT | DT_TOP | DT_NOPREFIX);
            SelectObject(dc, oldFont);
            DeleteObject(titleFont);
            SetTextColor(dc, RGB(55, 65, 74));
            RECT instructions {20, 52, client.right - 20, 108};
            DrawTextW(
                dc,
                L"Click the buttons below; keyboard shortcuts remain optional. "
                L"Theme buttons must visibly change the poster. Fault buttons "
                L"must retain the current theme. This is diagnostic UI, not product UI.",
                -1, &instructions,
                DT_LEFT | DT_TOP | DT_WORDBREAK | DT_NOPREFIX);
            if (self != nullptr) {
                const QString status = QStringLiteral(
                    "Current: %1    Last-known-good: %2    Fallback: %3    Pending: %4")
                    .arg(self->state_.effectiveThemeId)
                    .arg(self->state_.lastKnownGoodThemeId.isEmpty()
                             ? QStringLiteral("<none>")
                             : self->state_.lastKnownGoodThemeId)
                    .arg(self->state_.themeFallbackActive
                             ? QStringLiteral("YES")
                             : QStringLiteral("NO"))
                    .arg(self->pendingActive_
                             ? QStringLiteral("YES (300 ms debounce)")
                             : QStringLiteral("NO"));
                RECT statusRect {20, 294, client.right - 20, client.bottom - 12};
                DrawTextW(
                    dc,
                    reinterpret_cast<const wchar_t*>(status.utf16()),
                    -1,
                    &statusRect,
                    DT_LEFT | DT_TOP | DT_WORDBREAK | DT_NOPREFIX);
            }
            EndPaint(window, &paint);
            return 0;
        }
        case WM_NCDESTROY:
            SetWindowLongPtrW(window, GWLP_USERDATA, 0);
            break;
        default:
            break;
        }
        return DefWindowProcW(window, message, wParam, lParam);
    }

    const wchar_t* className_ =
        L"DesktopUpTSC01R4D2ThemeHotkeyProbe";
    static constexpr int hotkeyTheme1_ = 0xD21;
    static constexpr int hotkeyTheme2_ = 0xD22;
    static constexpr int hotkeyTheme3_ = 0xD23;
    static constexpr int hotkeyUnknown_ = 0xD2F;
    static constexpr int hotkeyResource_ = 0xD2A;
    static constexpr int hotkeyParameters_ = 0xD2B;
    static constexpr int hotkeyFallback_ = 0xD20;
    static constexpr int debounceIntervalMs_ = 300;
    DesktopHostCoordinator& host_;
    QtQuickSurfaceAdapter& adapter_;
    LayeredVisualSurface& visual_;
    InputIslandSet& islands_;
    FixtureState& state_;
    HINSTANCE instance_ = nullptr;
    HWND window_ = nullptr;
    bool registered_ = false;
    std::vector<int> registeredHotkeys_;
    std::vector<HWND> manualButtons_;
    QTimer debounceTimer_;
    PendingRequest pending_;
    bool pendingActive_ = false;
    quint64 requestRevision_ = 0;
    int committedCount_ = 0;
    int rejectedCount_ = 0;
    int fallbackCount_ = 0;
    int supersededRequestCount_ = 0;
    bool selfTestCompleted_ = false;
    bool failed_ = false;
    QString error_;
};
#include "r4d3_controller.inc"
#include "r4d4_lifecycle.inc"
}  // namespace

int main(int argc, char* argv[])
{
    const bool lifecycleConsole =
        argc == 1 ||
        (argc == 2 &&
         (std::strcmp(argv[1], "--autostart") == 0 ||
          std::strcmp(argv[1], "--request-exit") == 0));
    if (lifecycleConsole) {
        const HWND console = GetConsoleWindow();
        if (console != nullptr) ShowWindow(console, SW_HIDE);
        FreeConsole();
    }

    if (!SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2) &&
        !AreDpiAwarenessContextsEqual(
            GetThreadDpiAwarenessContext(),
            DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)) {
        std::cerr << "Failed to establish Per-Monitor v2 DPI awareness.\n";
        return 2;
    }

    QGuiApplication application(argc, argv);
    application.setApplicationName(QStringLiteral("DesktopUp TS-C01 Qt R2/R4"));
    application.setOrganizationName(QStringLiteral("DesktopUp"));
    application.setQuitOnLastWindowClosed(false);
    QQuickWindow::setDefaultAlphaBuffer(true);
    QQuickWindow::setGraphicsApi(QSGRendererInterface::Direct3D11);

    Options options;
    if (!parseOptions(QCoreApplication::arguments(), &options)) {
        std::cerr << "Usage: ts-c01-qt-r2 "
                     "(--run-r2-fixture | --run-r4-manual | "
                     "--run-r4c-manual | --run-r4d-manual | "
                     "--run-r4d2-manual | --run-r4d3-manual | "
                     "--autostart | --request-exit | no arguments) "
                     "[--r4d2-self-test] [--r4d3-self-test] "
                     "[--r3-diagnostics] [--inject-surface-fault] "
                     "[--r3-ready-marker path] "
                     "[--duration-ms 250..900000] [--log path]\n";
        return 2;
    }

    if (options.r4d4RequestExit) {
        QString requestError;
        if (!R4D4InstanceGate::requestExit(&requestError)) {
            std::cerr << requestError.toStdString() << "\n";
            return 3;
        }
        return 0;
    }

    std::unique_ptr<R4D4InstanceGate> r4d4InstanceGate;
    if (options.runR4D4Installed) {
        if (!R4D4StateStore::isFormalInstalledExecutable()) {
            std::cerr << "R4D4_NOT_FORMAL_INSTALLED_EXECUTABLE\n";
            return 3;
        }
        r4d4InstanceGate = std::make_unique<R4D4InstanceGate>();
        QString gateError;
        const auto result = r4d4InstanceGate->acquire(options.r4d4Autostart, &gateError);
        if (result == R4D4InstanceGate::Result::Error) {
            std::cerr << gateError.toStdString() << "\n";
            return 3;
        }
        if (result == R4D4InstanceGate::Result::SecondaryForwarded) return 0;
        options.logPath = R4D4StateStore::defaultLogPath();
        if (options.logPath.isEmpty()) {
            std::cerr << "R4D4_LOG_ROOT_CREATE_FAILED\n";
            return 3;
        }
    }

    const bool runManual =
        options.runR4Manual || options.runR4CManual ||
        options.runR4DManual || options.runR4D2Manual ||
        options.runR4D3Manual;
    const bool runInteractive = runManual || options.runR4D4Installed;

    desktopup::tsc01::EventSink sink(options.logPath);
    if (!sink.isValid()) {
        std::cerr << "Failed to open R2 JSONL log.\n";
        return 2;
    }

    QtQuickSurfaceAdapter adapter(desktopup::tsc01::SurfaceFixtureKind::R2NightOps);
    DesktopHostCoordinator host(sink);
    QString error;

    const QJsonObject capabilities = host.queryCapabilities();
    if (!capabilities.value(QStringLiteral("offscreen_frame_source")).toBool()) {
        error = QStringLiteral("HOST_CAPABILITY_MISSING");
    }

    desktopup::tsc01::SurfaceOffer offer;
    offer.candidateId = QString::fromLatin1(desktopup::tsc01::kCandidateId);
    offer.transport = desktopup::tsc01::SurfaceTransportKind::OffscreenFrameSource;
    offer.ownership = desktopup::tsc01::SurfaceOwnership::Host;
    const auto plan = error.isEmpty() ? host.negotiateSurface(offer, &error) : std::nullopt;
    bool succeeded = plan.has_value();
    if (succeeded) succeeded = host.attach(*plan, adapter, &error);
    if (succeeded) succeeded = adapter.presentAndWaitForFrame(5000, &error);

    std::unique_ptr<LayeredVisualSurface> visual;
    std::unique_ptr<InputIslandSet> islands;
    FixtureState state;
    std::unique_ptr<R4ManualInteractionController> manualController;
    std::unique_ptr<R4CControlCenterRoleProbe> controlCenterProbe;
    std::unique_ptr<R4CShellLifecycleController> shellLifecycleController;
    std::unique_ptr<R4DDisplayEnvironmentController>
        displayEnvironmentController;
    std::unique_ptr<R4D2ThemeController> themeController;
    std::unique_ptr<R4D3FaultPerformanceController> faultPerformanceController;
    std::unique_ptr<R4D4StateStore> r4d4StateStore;
    std::unique_ptr<R4D4LifecycleController> r4d4LifecycleController;
    bool deferredInteractionFailure = false;
    QString deferredInteractionError;

    auto releaseHostedPresentation = [&]() {
        themeController.reset();
        displayEnvironmentController.reset();
        if (manualController != nullptr && manualController->failed()) {
            deferredInteractionFailure = true;
            deferredInteractionError = manualController->error();
        }
        manualController.reset();
        if (islands != nullptr) {
            islands->clear();
            host.recordFixtureEvent(QStringLiteral("input_islands_released"), {
                {QStringLiteral("live_island_count"), islands->liveCount()}
            });
            islands.reset();
        }
        if (visual != nullptr) {
            const qint64 unexpectedPointers =
                static_cast<qint64>(visual->unexpectedPointerCount());
            visual->clear();
            host.recordFixtureEvent(QStringLiteral("host_layered_visual_released"), {
                {QStringLiteral("live_visual_count"), visual->liveCount()},
                {QStringLiteral("unexpected_visual_pointer_count"), unexpectedPointers}
            });
            if (unexpectedPointers != 0) {
                deferredInteractionFailure = true;
                deferredInteractionError =
                    QStringLiteral("R2_VISUAL_RECEIVED_POINTER_INPUT");
            }
            visual.reset();
        }
    };
    if (succeeded) {
        state.screenRect = host.screenPixelRect();
        visual = std::make_unique<LayeredVisualSurface>(host.approvedParent());
        succeeded = visual->initialize(&error) &&
                    visual->publish(adapter.lastFrame(), state.screenRect, &error);
    }
    if (succeeded) {
        host.recordFixtureEvent(QStringLiteral("host_layered_visual_ready"), {
            {QStringLiteral("surface_transport"), QStringLiteral("OFFSCREEN_FRAME_SOURCE")},
            {QStringLiteral("surface_ownership"), QStringLiteral("HOST")},
            {QStringLiteral("live_visual_count"), visual->liveCount()},
            {QStringLiteral("unexpected_visual_pointer_count"),
             static_cast<qint64>(visual->unexpectedPointerCount())}
        });
        succeeded = host.notifyFramePresented(&error);
    }
    if (succeeded) {
        islands = std::make_unique<InputIslandSet>(
            host.approvedParent(),
            visual->window(),
            host.explorerDefView(),
            host.explorerIconView(),
            host);
        succeeded = islands->initialize(&error);
    }
    if (succeeded) {
        succeeded = runFixtureMatrix(
            host, adapter, *visual, *islands, &state, &error);
    }
    if (succeeded && options.runR4D4Installed) {
        r4d4StateStore = std::make_unique<R4D4StateStore>(host);
        succeeded = r4d4StateStore->load(&state, &error) &&
                    applyProjection(
                        host, adapter, *visual, *islands, state,
                        QStringLiteral("FINAL"),
                        QStringLiteral("R4D4_STATE_RESTORE"), &error);
    }
    if (succeeded && runInteractive) {
        manualController = std::make_unique<R4ManualInteractionController>(
            host, adapter, *visual, *islands, state);
        manualController->install();
    }
    if (succeeded && (options.runR4CManual || options.runR4D4Installed)) {
        controlCenterProbe =
            std::make_unique<R4CControlCenterRoleProbe>(host);
        succeeded = controlCenterProbe->initialize(&error);
    }
    if (succeeded && (options.runR4CManual || options.runR4D4Installed)) {
        auto recoverHostedPresentation = [&](QString* recoveryError) {
            if (deferredInteractionFailure) {
                if (recoveryError != nullptr) {
                    *recoveryError = deferredInteractionError.isEmpty()
                        ? QStringLiteral("R4C_PREVIOUS_PRESENTATION_CLEANUP_FAILED")
                        : deferredInteractionError;
                }
                return false;
            }
            if (!host.recoverShellAttachment(recoveryError) ||
                !adapter.presentAndWaitForFrame(5000, recoveryError)) {
                return false;
            }

            visual = std::make_unique<LayeredVisualSurface>(
                host.approvedParent());
            if (!visual->initialize(recoveryError)) return false;
            host.recordFixtureEvent(
                QStringLiteral("host_layered_visual_ready"), {
                    {QStringLiteral("surface_transport"),
                     QStringLiteral("OFFSCREEN_FRAME_SOURCE")},
                    {QStringLiteral("surface_ownership"),
                     QStringLiteral("HOST")},
                    {QStringLiteral("live_visual_count"),
                     visual->liveCount()},
                    {QStringLiteral("unexpected_visual_pointer_count"),
                     static_cast<qint64>(
                         visual->unexpectedPointerCount())},
                    {QStringLiteral("recovery_restore"), true}
                });
            if (!host.notifyFramePresented(recoveryError)) return false;

            islands = std::make_unique<InputIslandSet>(
                host.approvedParent(),
                visual->window(),
                host.explorerDefView(),
                host.explorerIconView(),
                host);
            if (!islands->initialize(recoveryError) ||
                !applyProjection(
                    host,
                    adapter,
                    *visual,
                    *islands,
                    state,
                    QStringLiteral("FINAL"),
                    QStringLiteral("R4C_SHELL_RECOVERY_RESTORE"),
                    recoveryError)) {
                return false;
            }
            manualController =
                std::make_unique<R4ManualInteractionController>(
                    host, adapter, *visual, *islands, state);
            manualController->install();
            return true;
        };

        shellLifecycleController =
            std::make_unique<R4CShellLifecycleController>(
                host,
                releaseHostedPresentation,
                recoverHostedPresentation);
        shellLifecycleController->start();
    }
    if (succeeded && options.runR4D4Installed) {
        r4d4LifecycleController = std::make_unique<R4D4LifecycleController>(
            host, *r4d4InstanceGate, *controlCenterProbe, state, *r4d4StateStore);
        succeeded = r4d4LifecycleController->initialize(options.r4d4Autostart, &error);
    }
    if (succeeded && options.runR4DManual) {
        displayEnvironmentController =
            std::make_unique<R4DDisplayEnvironmentController>(
                host, adapter, *visual, *islands, state);
        displayEnvironmentController->start();
    }
    if (succeeded && options.runR4D2Manual) {
        themeController = std::make_unique<R4D2ThemeController>(
            host, adapter, *visual, *islands, state);
        succeeded = themeController->initialize(
            options.r4d2SelfTest, &error);
    }
    if (succeeded && options.runR4D3Manual) {
        auto recoverRendererSurface = [&](QString* recoveryError) {
            releaseHostedPresentation();
            if (deferredInteractionFailure) {
                if (recoveryError != nullptr) {
                    *recoveryError = deferredInteractionError.isEmpty()
                        ? QStringLiteral("R4D3_PREVIOUS_PRESENTATION_CLEANUP_FAILED")
                        : deferredInteractionError;
                }
                return false;
            }
            if (!host.recoverSurfaceAfterFault(recoveryError) ||
                !adapter.presentAndWaitForFrame(5000, recoveryError) ||
                !host.notifyFramePresented(recoveryError)) {
                return false;
            }

            visual = std::make_unique<LayeredVisualSurface>(
                host.approvedParent());
            if (!visual->initialize(recoveryError)) return false;
            host.recordFixtureEvent(
                QStringLiteral("host_layered_visual_ready"), {
                    {QStringLiteral("surface_transport"),
                     QStringLiteral("OFFSCREEN_FRAME_SOURCE")},
                    {QStringLiteral("surface_ownership"),
                     QStringLiteral("HOST")},
                    {QStringLiteral("live_visual_count"),
                     visual->liveCount()},
                    {QStringLiteral("unexpected_visual_pointer_count"),
                     static_cast<qint64>(
                         visual->unexpectedPointerCount())},
                    {QStringLiteral("surface_only_recovery_restore"), true}
                });
            islands = std::make_unique<InputIslandSet>(
                host.approvedParent(),
                visual->window(),
                host.explorerDefView(),
                host.explorerIconView(),
                host);
            if (!islands->initialize(recoveryError) ||
                !applyProjection(
                    host,
                    adapter,
                    *visual,
                    *islands,
                    state,
                    QStringLiteral("FINAL"),
                    QStringLiteral("R4D3_SURFACE_RECOVERY_RESTORE"),
                    recoveryError)) {
                return false;
            }
            manualController =
                std::make_unique<R4ManualInteractionController>(
                    host, adapter, *visual, *islands, state);
            manualController->install();
            host.recordFixtureEvent(
                QStringLiteral("r4d3_hosted_presentation_restored"), {
                    {QStringLiteral("surface_only"), true},
                    {QStringLiteral("topmost"), false},
                    {QStringLiteral("ordinary_top_level_fallback"), false},
                    {QStringLiteral("result"), QStringLiteral("PASS")}
                });
            return true;
        };
        auto projectStressState = [&](
            const FixtureState& candidate,
            const QString& reason,
            QString* projectionError) {
            if (visual == nullptr || islands == nullptr) {
                if (projectionError != nullptr) {
                    *projectionError =
                        QStringLiteral("R4D3_PRESENTATION_NOT_AVAILABLE");
                }
                return false;
            }
            return applyProjection(
                host,
                adapter,
                *visual,
                *islands,
                candidate,
                QStringLiteral("FINAL"),
                reason,
                projectionError);
        };
        faultPerformanceController =
            std::make_unique<R4D3FaultPerformanceController>(
                host,
                state,
                recoverRendererSurface,
                projectStressState);
        succeeded = faultPerformanceController->initialize(
            options.r4d3SelfTest, &error);
    }
    if (succeeded && options.r3Diagnostics) {
        QString staleError;
        const quint64 currentRevision = host.hostProjectionRevision();
        const bool staleAccepted = host.notifyProjectionFramePresentedAtRevision(
            currentRevision > 0 ? currentRevision - 1 : 0, &staleError);
        if (staleAccepted || staleError != QStringLiteral("STALE_FRAME_REJECTED")) {
            succeeded = false;
            error = QStringLiteral("R3_STALE_FRAME_WAS_NOT_REJECTED");
        }
    }
    if (succeeded && !options.r3ReadyMarkerPath.isEmpty()) {
        QFile marker(options.r3ReadyMarkerPath);
        const QByteArray payload("R3_MATRIX_READY\n");
        const bool opened = marker.open(QIODevice::WriteOnly | QIODevice::Truncate);
        const bool written = opened && marker.write(payload) == payload.size();
        const bool flushed = written && marker.flush();
        marker.close();
        if (!flushed) {
            succeeded = false;
            error = QStringLiteral("R3_READY_MARKER_WRITE_FAILED");
        }
    }
    if (succeeded) {
        if (options.runR4D4Installed) {
            succeeded = application.exec() == 0;
        } else {
            succeeded = host.holdVisibleFor(options.durationMs);
        }
    }
    if (options.runR4D4Installed) {
        if (manualController != nullptr && manualController->failed()) {
            succeeded = false;
            error = manualController->error();
        }
        if (shellLifecycleController != nullptr && shellLifecycleController->failed()) {
            succeeded = false;
            error = shellLifecycleController->error();
        }
        if (r4d4LifecycleController != nullptr && r4d4LifecycleController->failed()) {
            succeeded = false;
            error = r4d4LifecycleController->error();
        }
        host.recordFixtureEvent(QStringLiteral("r4d4_lifecycle_session_finished"), {
            {QStringLiteral("launch_intent"), options.r4d4Autostart
                ? QStringLiteral("AUTOSTART") : QStringLiteral("ORDINARY")},
            {QStringLiteral("control_center_probe_open_count"),
             controlCenterProbe != nullptr ? controlCenterProbe->openCount() : 0},
            {QStringLiteral("shell_recovery_count"),
             shellLifecycleController != nullptr ? shellLifecycleController->recoveryCount() : 0},
            {QStringLiteral("shell_degraded_count"),
             shellLifecycleController != nullptr ? shellLifecycleController->degradedCount() : 0},
            {QStringLiteral("result"), succeeded
                ? QStringLiteral("PASS") : QStringLiteral("FAIL")},
            {QStringLiteral("p0_result"), QStringLiteral("NOT_RUN")}
        });
    }
    if (runManual) {
        if (manualController != nullptr && manualController->failed()) {
            succeeded = false;
            error = manualController->error();
        }
        if (deferredInteractionFailure) {
            succeeded = false;
            if (error.isEmpty()) error = deferredInteractionError;
        }
        if (shellLifecycleController != nullptr &&
            shellLifecycleController->failed()) {
            succeeded = false;
            error = shellLifecycleController->error();
        }
        if (displayEnvironmentController != nullptr &&
            displayEnvironmentController->failed()) {
            succeeded = false;
            error = displayEnvironmentController->error();
        }
        if (themeController != nullptr && themeController->failed()) {
            succeeded = false;
            error = themeController->error();
        }
        if (faultPerformanceController != nullptr &&
            faultPerformanceController->failed()) {
            succeeded = false;
            error = faultPerformanceController->error();
        }
        if (options.r4d2SelfTest &&
            (themeController == nullptr ||
             !themeController->selfTestCompleted())) {
            succeeded = false;
            error = QStringLiteral("R4D2_THEME_SELF_TEST_INCOMPLETE");
        }
        if (options.r4d3SelfTest &&
            (faultPerformanceController == nullptr ||
             !faultPerformanceController->selfTestCompleted())) {
            succeeded = false;
            error = QStringLiteral("R4D3_SELF_TEST_INCOMPLETE");
        }
        host.recordFixtureEvent(QStringLiteral("r4_manual_session_finished"), {
            {QStringLiteral("manual_phase"), options.runR4D3Manual
                ? QStringLiteral("R4D3")
                : (options.runR4D2Manual
                    ? QStringLiteral("R4D2")
                    : (options.runR4DManual
                    ? QStringLiteral("R4D")
                        : (options.runR4CManual
                            ? QStringLiteral("R4C")
                            : QStringLiteral("R4A_R4B"))))},
            {QStringLiteral("shell_recovery_count"),
             shellLifecycleController != nullptr
                 ? shellLifecycleController->recoveryCount()
                 : 0},
            {QStringLiteral("shell_degraded_count"),
             shellLifecycleController != nullptr
                 ? shellLifecycleController->degradedCount()
                 : 0},
            {QStringLiteral("shell_degraded_at_finish"),
             shellLifecycleController != nullptr &&
             shellLifecycleController->degraded()},
            {QStringLiteral("control_center_probe_open_count"),
             controlCenterProbe != nullptr
                 ? controlCenterProbe->openCount()
                 : 0},
            {QStringLiteral("control_center_probe_close_count"),
             controlCenterProbe != nullptr
                 ? controlCenterProbe->closeCount()
                 : 0},
            {QStringLiteral("display_environment_change_count"),
             displayEnvironmentController != nullptr
                 ? displayEnvironmentController->changeCount()
                 : 0},
            {QStringLiteral("display_environment_failed"),
             displayEnvironmentController != nullptr &&
             displayEnvironmentController->failed()},
            {QStringLiteral("theme_switch_committed_count"),
             themeController != nullptr
                 ? themeController->committedCount()
                 : 0},
            {QStringLiteral("theme_switch_rejected_count"),
             themeController != nullptr
                 ? themeController->rejectedCount()
                 : 0},
            {QStringLiteral("theme_fallback_count"),
             themeController != nullptr
                 ? themeController->fallbackCount()
                 : 0},
            {QStringLiteral("theme_superseded_request_count"),
             themeController != nullptr
                 ? themeController->supersededRequestCount()
                 : 0},
            {QStringLiteral("theme_self_test_completed"),
             themeController != nullptr &&
             themeController->selfTestCompleted()},
            {QStringLiteral("selected_theme_id"), state.selectedThemeId},
            {QStringLiteral("effective_theme_id"), state.effectiveThemeId},
            {QStringLiteral("last_known_good_theme_id"),
             state.lastKnownGoodThemeId},
            {QStringLiteral("theme_fallback_active"),
             state.themeFallbackActive},
            {QStringLiteral("theme_revision"),
             static_cast<qint64>(state.themeRevision)},
            {QStringLiteral("r4d3_recovery_count"),
             faultPerformanceController != nullptr
                 ? faultPerformanceController->recoveryCount()
                 : 0},
            {QStringLiteral("r4d3_stale_reject_count"),
             faultPerformanceController != nullptr
                 ? faultPerformanceController->staleRejectCount()
                 : 0},
            {QStringLiteral("r4d3_duplicate_shell_reject_count"),
             faultPerformanceController != nullptr
                 ? faultPerformanceController->duplicateShellRejectCount()
                 : 0},
            {QStringLiteral("r4d3_stress_count"),
             faultPerformanceController != nullptr
                 ? faultPerformanceController->stressCount()
                 : 0},
            {QStringLiteral("r4d3_max_theme_switch_ms"),
             faultPerformanceController != nullptr
                 ? faultPerformanceController->maxThemeSwitchMs()
                 : 0.0},
            {QStringLiteral("r4d3_self_test_completed"),
             faultPerformanceController != nullptr &&
             faultPerformanceController->selfTestCompleted()},
            {QStringLiteral("result"), succeeded
                ? QStringLiteral("PASS")
                : QStringLiteral("FAIL")},
            {QStringLiteral("p0_result"), QStringLiteral("NOT_RUN")}
        });
    }
    if (succeeded && options.injectSurfaceFault) {
        succeeded = host.notifySurfaceFault(
            QStringLiteral("R3_INJECTED_RENDERER_DEVICE_LOSS"), true);
        if (!succeeded) {
            error = QStringLiteral("R3_FAULT_INJECTION_REJECTED");
        }
    }

    if (!succeeded && !error.isEmpty()) {
        host.notifySurfaceFault(error, false);
    }
    r4d4LifecycleController.reset();
    r4d4StateStore.reset();
    shellLifecycleController.reset();
    faultPerformanceController.reset();
    themeController.reset();
    displayEnvironmentController.reset();
    if (controlCenterProbe != nullptr) {
        const int controlCenterOpenCount =
            controlCenterProbe->openCount();
        const int controlCenterCloseCount =
            controlCenterProbe->closeCount();
        controlCenterProbe.reset();
        host.recordFixtureEvent(
            QStringLiteral("r4c_control_center_role_probe_released"), {
                {QStringLiteral("open_count"), controlCenterOpenCount},
                {QStringLiteral("close_count"), controlCenterCloseCount},
                {QStringLiteral("live_probe_window_count"), 0},
                {QStringLiteral("topmost"), false},
                {QStringLiteral("ordinary_top_level_fallback"), false}
            });
    }
    releaseHostedPresentation();
    if (deferredInteractionFailure) {
        succeeded = false;
        if (error.isEmpty()) error = deferredInteractionError;
    }

    QString detachError;
    const bool detached = host.detach(QStringLiteral("r2-detach-1"), &detachError);
    bool lateFrameRejected = true;
    if (options.r3Diagnostics) {
        QString lateError;
        const bool lateAccepted = host.notifyProjectionFramePresentedAtRevision(
            host.hostProjectionRevision(), &lateError);
        lateFrameRejected = !lateAccepted &&
                            lateError == QStringLiteral("LATE_FRAME_REJECTED");
    }
    const bool duplicateDetach = host.detach(QStringLiteral("r2-detach-1"), &detachError);
    QString exitError;
    const bool exited = host.beginExit(QStringLiteral("r2-exit-1"), &exitError);

    const bool expectedInjectedFault = options.injectSurfaceFault &&
        succeeded && detached && duplicateDetach && lateFrameRejected &&
        !exited && host.exitCode() == 1;
    if (expectedInjectedFault) {
        std::cout << "TS-C01 R3 injected-fault lifecycle PASS; "
                     "P0 remains NOT RUN.\n";
        return 0;
    }
    if (!succeeded || !detached || !duplicateDetach || !lateFrameRejected ||
        !exited || host.exitCode() != 0) {
        if (error.isEmpty()) error = !detachError.isEmpty() ? detachError : exitError;
        std::cerr << "TS-C01 R2/R3 fixture preflight failed: "
                  << error.toStdString() << "\n";
        return 1;
    }

    if (options.runR4D4Installed) {
        return 0;
    }
    std::cout << "TS-C01 R2 Night Ops fixture preflight PASS; P0 remains NOT RUN.\n";
    return 0;
}
