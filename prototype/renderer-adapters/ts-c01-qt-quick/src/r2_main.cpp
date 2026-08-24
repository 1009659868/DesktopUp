#include "desktop_host_contract.h"

#include <QCoreApplication>
#include <QFile>
#include <QGuiApplication>
#include <QJsonArray>
#include <QJsonObject>
#include <QPainter>
#include <QQuickWindow>
#include <QTimer>
#include <QSGRendererInterface>

#include <windows.h>
#include <windowsx.h>

#include <algorithm>
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
    bool r3Diagnostics = false;
    bool injectSurfaceFault = false;
    int durationMs = 1500;
    QString logPath;
    QString r3ReadyMarkerPath;
};

bool parseOptions(const QStringList& arguments, Options* options)
{
    for (int index = 1; index < arguments.size(); ++index) {
        const QString argument = arguments.at(index);
        if (argument == QStringLiteral("--run-r2-fixture")) {
            options->runFixture = true;
        } else if (argument == QStringLiteral("--run-r4-manual")) {
            options->runR4Manual = true;
        } else if (argument == QStringLiteral("--r3-diagnostics")) {
            options->r3Diagnostics = true;
        } else if (argument == QStringLiteral("--inject-surface-fault")) {
            options->injectSurfaceFault = true;
        } else if (argument == QStringLiteral("--duration-ms") && index + 1 < arguments.size()) {
            bool valid = false;
            const int value = arguments.at(++index).toInt(&valid);
            const int maximumDuration = options->runR4Manual ? 900000 : 60000;
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
    const bool exactlyOneMode = options->runFixture != options->runR4Manual;
    return exactlyOneMode &&
           (!options->injectSurfaceFault || options->r3Diagnostics) &&
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

        if (!previewBounds(screenRect, error)) return false;

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

    bool previewBounds(const QRect& screenRect, QString* error)
    {
        if (window_ == nullptr || !IsWindow(window_) || !screenRect.isValid()) {
            if (error != nullptr) *error = QStringLiteral("R4_VISUAL_PREVIEW_BOUNDS_INVALID");
            return false;
        }

        POINT origin {screenRect.left(), screenRect.top()};
        MapWindowPoints(nullptr, parent_, &origin, 1);
        const int radius = qRound(48.0 * screenRect.width() / 420.0);
        HRGN region = CreateRoundRectRgn(
            1, 1, screenRect.width() - 1, screenRect.height() - 1,
            radius, radius);
        if (region == nullptr || SetWindowRgn(window_, region, FALSE) == 0) {
            if (region != nullptr) DeleteObject(region);
            if (error != nullptr) *error = QStringLiteral("R4_VISUAL_PREVIEW_REGION_FAILED");
            return false;
        }
        if (!SetWindowPos(
                window_, HWND_TOP, origin.x, origin.y,
                screenRect.width(), screenRect.height(),
                SWP_NOACTIVATE | SWP_SHOWWINDOW)) {
            if (error != nullptr) {
                *error = QStringLiteral("R4_VISUAL_PREVIEW_POSITION_FAILED: %1")
                    .arg(GetLastError());
            }
            return false;
        }
        InvalidateRect(window_, nullptr, FALSE);
        UpdateWindow(window_);
        return true;
    }
    void clear()
    {
        if (window_ != nullptr && IsWindow(window_)) {
            DestroyWindow(window_);
        }
        window_ = nullptr;
        frame_ = QImage();
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
        for (std::size_t index = 0; index < ordered.size(); ++index) {
            const QRect rect = ordered[index].localRect;
            if (!rect.isValid() ||
                !SetWindowPos(
                    states_[index]->window,
                    HWND_TOP,
                    visualOrigin.x + rect.x(),
                    visualOrigin.y + rect.y(),
                    rect.width(),
                    rect.height(),
                    SWP_NOACTIVATE | SWP_SHOWWINDOW)) {
                if (error != nullptr) {
                    *error = QStringLiteral("R4_ISLAND_PREVIEW_POSITION_FAILED: %1")
                        .arg(GetLastError());
                }
                return false;
            }
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
        host_.recordFixtureEvent(QStringLiteral("fixture_pointer_event"), {
            {QStringLiteral("region_id"), state.region.id},
            {QStringLiteral("region_role"), state.region.role},
            {QStringLiteral("priority"), state.region.priority},
            {QStringLiteral("action"), action},
            {QStringLiteral("screen_x"), screenPoint.x()},
            {QStringLiteral("screen_y"), screenPoint.y()},
            {QStringLiteral("click_count"), static_cast<qint64>(state.clickCount)}
        });
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
    std::vector<std::unique_ptr<IslandState>> states_;
    InteractionHandler interactionHandler_;
};

struct FixtureState {
    QRect screenRect;
    int expandedHeight = 620;
    QString presentation = QStringLiteral("EXPANDED");
    QString interaction = QStringLiteral("USING");
    bool locked = false;
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
    if (!setFixtureProperties(adapter, state, scenario, nextRevision, error) ||
        !host.commitFixtureProjection(
            state.screenRect,
            state.presentation,
            state.interaction,
            state.locked,
            regionsJson(regions),
            reason,
            error) ||
        !adapter.waitForNextFrame(5000, error) ||
        !visual.publish(adapter.lastFrame(), state.screenRect, error) ||
        !host.notifyProjectionFramePresented(error) ||
        !islands.publish(regions, error) ||
        !islands.validate(regions, scenario, error)) {
        return false;
    }

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
        return true;
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
            {QStringLiteral("source"), QStringLiteral("R4_REAL_POINTER")}
        });
    }

    void deferCommand(const QString& regionId, const QString& role, const QString& action)
    {
        QTimer::singleShot(0, [this, regionId, role, action]() {
            if (failed_) return;
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
            }
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
            previewGesture(screenPoint);
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
    bool failed_ = false;
    QString error_;
};
}  // namespace

int main(int argc, char* argv[])
{
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
    QQuickWindow::setDefaultAlphaBuffer(true);
    QQuickWindow::setGraphicsApi(QSGRendererInterface::Direct3D11);

    Options options;
    if (!parseOptions(QCoreApplication::arguments(), &options)) {
        std::cerr << "Usage: ts-c01-qt-r2 (--run-r2-fixture | --run-r4-manual) "
                     "[--r3-diagnostics] [--inject-surface-fault] "
                     "[--r3-ready-marker path] "
                     "[--duration-ms 250..60000] [--log path]\n";
        return 2;
    }

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
    if (succeeded && options.runR4Manual) {
        manualController = std::make_unique<R4ManualInteractionController>(
            host, adapter, *visual, *islands, state);
        manualController->install();
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
        succeeded = host.holdVisibleFor(options.durationMs);
    }
    if (options.runR4Manual && manualController != nullptr) {
        if (manualController->failed()) {
            succeeded = false;
            error = manualController->error();
        }
        host.recordFixtureEvent(QStringLiteral("r4_manual_session_finished"), {
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
            succeeded = false;
            if (error.isEmpty()) {
                error = QStringLiteral("R2_VISUAL_RECEIVED_POINTER_INPUT");
            }
        }
        visual.reset();
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

    std::cout << "TS-C01 R2 Night Ops fixture preflight PASS; P0 remains NOT RUN.\n";
    return 0;
}
