#define wmain TsH01D1UnusedEntry
#include "../../ts-h01-d1/src/main.cpp"
#undef wmain

#include <cmath>
#include <limits>

namespace {

constexpr wchar_t kD3VisualClass[] = L"DesktopUp.TS-H01-D3.VisualSurface";
constexpr wchar_t kD3IslandClass[] = L"DesktopUp.TS-H01-D3.InteractionIsland";
constexpr wchar_t kD3BuildId[] = L"TS-H01-D3-20260821-01-BOUNDARY";
constexpr DWORD kD3DefaultDurationMs = 5000;
constexpr DWORD kD3MaximumDurationMs = 900000;
constexpr DWORD kD3SampleIntervalMs = 8;
constexpr double kDipBase = 96.0;
constexpr double kSafeMarginDip = 24.0;
constexpr double kMinWidthDip = 240.0;
constexpr double kMinExpandedHeightDip = 240.0;
constexpr double kCollapsedHeightDip = 60.0;
constexpr double kDragThresholdDip = 5.0;

enum class D3Role { Title, Item, Collapse, Resize };

struct D3Options {
  bool run_boundary_test = false;
  DWORD duration_ms = kD3DefaultDurationMs;
  std::wstring scenario;
  UINT expected_dpi = 0;
  std::uint64_t expected_drags = 0;
  std::uint64_t expected_resizes = 0;
  std::uint64_t expected_collapse_cycles = 0;
  std::uint64_t expected_display_changes = 0;
  std::optional<std::filesystem::path> log_path;
};

struct LogicalGeometry {
  double left_dip = 0.0;
  double top_dip = 0.0;
  double width_dip = 0.0;
  double expanded_height_dip = 0.0;
  double anchor_x = 1.0;
  double anchor_y = 0.0;
  bool collapsed = false;
};

struct DisplaySnapshot {
  RECT monitor{};
  RECT work{};
  UINT dpi = 96;
};

struct D3IslandState {
  D3Role role = D3Role::Title;
  HWND hwnd = nullptr;
  bool pointer_down = false;
  bool gesture_counted = false;
  POINT pointer_origin{};
  LogicalGeometry geometry_origin{};
  std::uint64_t click_count = 0;
};

struct D3Runtime {
  JsonlSink* sink = nullptr;
  Clock::time_point started_at{};
  ShellTarget target{};
  D3Options options{};
  HWND visual = nullptr;
  std::array<D3IslandState, 4> islands{};
  LogicalGeometry geometry{};
  DisplaySnapshot display{};
  RECT expected_window_rect{};
  SIZE render_target{};
  std::uint64_t render_revision = 0;
  std::uint64_t geometry_revision = 0;
  std::uint64_t hit_region_revision = 0;
  std::uint64_t drag_count = 0;
  std::uint64_t resize_count = 0;
  std::uint64_t collapse_toggle_count = 0;
  std::uint64_t collapse_cycle_count = 0;
  std::uint64_t preset_count = 0;
  std::uint64_t display_change_count = 0;
  std::uint64_t visual_pointer_messages = 0;
  bool applying_geometry = false;
};

D3Runtime* g_d3 = nullptr;

double ClampDouble(double value, double low, double high) {
  if (high < low) {
    return low;
  }
  return std::clamp(value, low, high);
}

int DipToPx(double value, UINT dpi) {
  return static_cast<int>(std::lround(value * static_cast<double>(dpi) /
                                      kDipBase));
}

double PxToDip(int value, UINT dpi) {
  return static_cast<double>(value) * kDipBase / static_cast<double>(dpi);
}

int RectWidth(const RECT& rect) { return rect.right - rect.left; }
int RectHeight(const RECT& rect) { return rect.bottom - rect.top; }

bool EqualRectValue(const RECT& first, const RECT& second) {
  return first.left == second.left && first.top == second.top &&
         first.right == second.right && first.bottom == second.bottom;
}

bool NearRect(const RECT& first, const RECT& second, int tolerance) {
  return std::abs(first.left - second.left) <= tolerance &&
         std::abs(first.top - second.top) <= tolerance &&
         std::abs(first.right - second.right) <= tolerance &&
         std::abs(first.bottom - second.bottom) <= tolerance;
}

const char* D3RoleName(D3Role role) {
  switch (role) {
    case D3Role::Title: return "title";
    case D3Role::Item: return "preset";
    case D3Role::Collapse: return "collapse";
    case D3Role::Resize: return "resize";
  }
  return "unknown";
}

DisplaySnapshot QueryPrimaryDisplay(HWND dpi_source) {
  DisplaySnapshot snapshot;
  (void)dpi_source;
  MONITORINFO info{};
  info.cbSize = sizeof(info);
  GetMonitorInfoW(MonitorFromPoint(POINT{0, 0}, MONITOR_DEFAULTTOPRIMARY),
                  &info);
  snapshot.monitor = info.rcMonitor;
  snapshot.work = info.rcWork;
  // D3 is intentionally primary-monitor-only. Explorer's desktop parent and
  // our embedded child report the Shell host DPI (96) even when the primary
  // display is scaled. The PMv2 process system DPI is the projection authority.
  snapshot.dpi = GetDpiForSystem();
  if (snapshot.dpi == 0) {
    snapshot.dpi = 96;
  }
  return snapshot;
}

bool SameDisplay(const DisplaySnapshot& first, const DisplaySnapshot& second) {
  return EqualRectValue(first.monitor, second.monitor) &&
         EqualRectValue(first.work, second.work) && first.dpi == second.dpi;
}

double WorkWidthDip(const DisplaySnapshot& display) {
  return PxToDip(RectWidth(display.work), display.dpi);
}

double WorkHeightDip(const DisplaySnapshot& display) {
  return PxToDip(RectHeight(display.work), display.dpi);
}

void ClampGeometry(D3Runtime& runtime) {
  const double work_width = WorkWidthDip(runtime.display);
  const double work_height = WorkHeightDip(runtime.display);
  const double max_width = std::max(kMinWidthDip, work_width * 0.45);
  const double max_height = std::max(kMinExpandedHeightDip,
                                     work_height - 2.0 * kSafeMarginDip);
  runtime.geometry.width_dip = ClampDouble(runtime.geometry.width_dip,
                                           kMinWidthDip, max_width);
  runtime.geometry.expanded_height_dip = ClampDouble(
      runtime.geometry.expanded_height_dip, kMinExpandedHeightDip, max_height);
  const double current_height = runtime.geometry.collapsed
                                    ? kCollapsedHeightDip
                                    : runtime.geometry.expanded_height_dip;
  runtime.geometry.left_dip = ClampDouble(
      runtime.geometry.left_dip, 0.0,
      std::max(0.0, work_width - runtime.geometry.width_dip));
  runtime.geometry.top_dip = ClampDouble(
      runtime.geometry.top_dip, 0.0,
      std::max(0.0, work_height - current_height));
  const double available_x = std::max(0.0, work_width - runtime.geometry.width_dip);
  const double available_y = std::max(0.0, work_height - current_height);
  runtime.geometry.anchor_x = available_x > 0.0
                                  ? runtime.geometry.left_dip / available_x
                                  : 0.0;
  runtime.geometry.anchor_y = available_y > 0.0
                                  ? runtime.geometry.top_dip / available_y
                                  : 0.0;
}

RECT ProjectWindowRect(const D3Runtime& runtime) {
  const double height_dip = runtime.geometry.collapsed
                                ? kCollapsedHeightDip
                                : runtime.geometry.expanded_height_dip;
  const int left = runtime.display.work.left +
                   DipToPx(runtime.geometry.left_dip, runtime.display.dpi);
  const int top = runtime.display.work.top +
                  DipToPx(runtime.geometry.top_dip, runtime.display.dpi);
  return RECT{left, top,
              left + DipToPx(runtime.geometry.width_dip, runtime.display.dpi),
              top + DipToPx(height_dip, runtime.display.dpi)};
}

RECT ScreenToParentRect(HWND parent, const RECT& screen_rect) {
  POINT points[2]{{screen_rect.left, screen_rect.top},
                  {screen_rect.right, screen_rect.bottom}};
  MapWindowPoints(nullptr, parent, points, 2);
  return RECT{points[0].x, points[0].y, points[1].x, points[1].y};
}

std::array<RECT, 4> IslandRects(const D3Runtime& runtime,
                                const RECT& parent_rect) {
  const UINT dpi = runtime.display.dpi;
  const int left = parent_rect.left;
  const int top = parent_rect.top;
  const int right = parent_rect.right;
  const int bottom = parent_rect.bottom;
  const int title_left = left + DipToPx(18.0, dpi);
  const int title_top = top + DipToPx(10.0, dpi);
  const int title_right = std::max(title_left + 1, right - DipToPx(116.0, dpi));
  const int title_bottom = std::min(bottom, top + DipToPx(52.0, dpi));
  const int action_left = std::max(left, right - DipToPx(104.0, dpi));
  const int action_right = std::max(action_left + 1, right - DipToPx(54.0, dpi));
  const int resize_size = DipToPx(30.0, dpi);

  if (runtime.geometry.collapsed) {
    return {{
        RECT{title_left, title_top, title_right, title_bottom},
        RECT{left, top, left, top},
        RECT{action_left, title_top, action_right, title_bottom},
        RECT{right, bottom, right, bottom},
    }};
  }

  return {{
      RECT{title_left, title_top, title_right, title_bottom},
      RECT{left + DipToPx(24.0, dpi), top + DipToPx(88.0, dpi),
           std::min(right - DipToPx(24.0, dpi), left + DipToPx(224.0, dpi)),
           std::min(bottom - DipToPx(48.0, dpi), top + DipToPx(178.0, dpi))},
      RECT{action_left, title_top, action_right, title_bottom},
      RECT{right - resize_size, bottom - resize_size, right, bottom},
  }};
}

void LogD3Event(const char* event, const char* result = nullptr) {
  if (g_d3 == nullptr || g_d3->sink == nullptr) {
    return;
  }
  const double elapsed_ms = std::chrono::duration<double, std::milli>(
      Clock::now() - g_d3->started_at).count();
  std::ostringstream stream;
  stream << "{\"schema\":\"desktopup.ts-h01-d3.event.v1\""
         << ",\"event\":\"" << event << "\""
         << ",\"elapsed_ms\":" << std::fixed << std::setprecision(3)
         << elapsed_ms;
  if (result != nullptr) {
    stream << ",\"result\":\"" << result << "\"";
  }
  stream << ",\"geometry_revision\":" << g_d3->geometry_revision
         << ",\"hit_region_revision\":" << g_d3->hit_region_revision
         << ",\"drag_count\":" << g_d3->drag_count
         << ",\"resize_count\":" << g_d3->resize_count
         << ",\"collapse_cycle_count\":" << g_d3->collapse_cycle_count
         << ",\"display_change_count\":" << g_d3->display_change_count
         << "}";
  g_d3->sink->Write(stream.str());
}

bool ApplyGeometry(D3Runtime& runtime, bool preserve_anchor) {
  if (runtime.applying_geometry || runtime.visual == nullptr) {
    return false;
  }
  runtime.applying_geometry = true;
  if (preserve_anchor) {
    const double work_width = WorkWidthDip(runtime.display);
    const double work_height = WorkHeightDip(runtime.display);
    const double height = runtime.geometry.collapsed
                              ? kCollapsedHeightDip
                              : runtime.geometry.expanded_height_dip;
    runtime.geometry.left_dip = runtime.geometry.anchor_x *
        std::max(0.0, work_width - runtime.geometry.width_dip);
    runtime.geometry.top_dip = runtime.geometry.anchor_y *
        std::max(0.0, work_height - height);
  }
  ClampGeometry(runtime);
  const RECT screen_rect = ProjectWindowRect(runtime);
  const RECT parent_rect = ScreenToParentRect(runtime.target.icon_host, screen_rect);
  const auto island_rects = IslandRects(runtime, parent_rect);
  const std::uint64_t next_revision = runtime.geometry_revision + 1;

  HDWP batch = BeginDeferWindowPos(1 + static_cast<int>(runtime.islands.size()));
  if (batch == nullptr) {
    runtime.applying_geometry = false;
    return false;
  }
  batch = DeferWindowPos(batch, runtime.visual, nullptr, parent_rect.left,
                         parent_rect.top, RectWidth(parent_rect),
                         RectHeight(parent_rect),
                         SWP_NOACTIVATE | SWP_NOZORDER);
  for (std::size_t index = 0; batch != nullptr && index < runtime.islands.size();
       ++index) {
    const RECT rect = island_rects[index];
    batch = DeferWindowPos(batch, runtime.islands[index].hwnd, nullptr,
                           rect.left, rect.top, std::max(0, RectWidth(rect)),
                           std::max(0, RectHeight(rect)),
                           SWP_NOACTIVATE | SWP_NOZORDER);
  }
  const bool applied = batch != nullptr && EndDeferWindowPos(batch) != FALSE;
  if (applied) {
    runtime.expected_window_rect = screen_rect;
    runtime.geometry_revision = next_revision;
    runtime.hit_region_revision = next_revision;
    InvalidateRect(runtime.visual, nullptr, FALSE);
    UpdateWindow(runtime.visual);
    for (D3IslandState& island : runtime.islands) {
      InvalidateRect(island.hwnd, nullptr, FALSE);
      UpdateWindow(island.hwnd);
    }
  }
  runtime.applying_geometry = false;
  return applied;
}

void BeginGesture(D3IslandState& island) {
  if (g_d3 == nullptr) {
    return;
  }
  island.pointer_down = true;
  island.gesture_counted = false;
  GetCursorPos(&island.pointer_origin);
  island.geometry_origin = g_d3->geometry;
  SetCapture(island.hwnd);
}

void ContinueGesture(D3IslandState& island) {
  if (g_d3 == nullptr || !island.pointer_down || GetCapture() != island.hwnd) {
    return;
  }
  POINT current{};
  GetCursorPos(&current);
  const int delta_x_px = current.x - island.pointer_origin.x;
  const int delta_y_px = current.y - island.pointer_origin.y;
  const double delta_x = PxToDip(delta_x_px, g_d3->display.dpi);
  const double delta_y = PxToDip(delta_y_px, g_d3->display.dpi);
  if (!island.gesture_counted &&
      std::hypot(delta_x, delta_y) < kDragThresholdDip) {
    return;
  }
  if (!island.gesture_counted) {
    island.gesture_counted = true;
    if (island.role == D3Role::Title) {
      ++g_d3->drag_count;
      LogD3Event("drag_started");
    } else {
      ++g_d3->resize_count;
      LogD3Event("resize_started");
    }
  }
  g_d3->geometry = island.geometry_origin;
  if (island.role == D3Role::Title) {
    g_d3->geometry.left_dip += delta_x;
    g_d3->geometry.top_dip += delta_y;
  } else if (island.role == D3Role::Resize && !g_d3->geometry.collapsed) {
    g_d3->geometry.width_dip += delta_x;
    g_d3->geometry.expanded_height_dip += delta_y;
  }
  if (!ApplyGeometry(*g_d3, false)) {
    LogD3Event("geometry_apply_failed", "gesture");
  }
}

void EndGesture(D3IslandState& island) {
  if (island.pointer_down && GetCapture() == island.hwnd) {
    ReleaseCapture();
  }
  if (island.pointer_down && island.gesture_counted) {
    LogD3Event(island.role == D3Role::Title ? "drag_completed"
                                            : "resize_completed");
  }
  island.pointer_down = false;
  island.gesture_counted = false;
}

void ApplyNextPreset() {
  if (g_d3 == nullptr || g_d3->geometry.collapsed) {
    return;
  }
  ++g_d3->preset_count;
  const double work_width = WorkWidthDip(g_d3->display);
  const double work_height = WorkHeightDip(g_d3->display);
  const double default_width = std::max(work_width * 0.22, 280.0);
  const double max_width = std::max(kMinWidthDip, work_width * 0.45);
  const double default_height = std::max(
      kMinExpandedHeightDip, work_height - 2.0 * kSafeMarginDip);
  const double max_height = default_height;
  const std::array<double, 5> widths{{240.0, 280.0, 360.0,
                                      default_width, max_width}};
  const std::array<double, 5> heights{{kMinExpandedHeightDip, default_height,
                                       max_height, default_height, max_height}};
  const std::size_t index = static_cast<std::size_t>(
      (g_d3->preset_count - 1) % widths.size());
  g_d3->geometry.width_dip = widths[index];
  g_d3->geometry.expanded_height_dip = heights[index];
  if (!ApplyGeometry(*g_d3, false)) {
    LogD3Event("geometry_apply_failed", "preset");
  } else {
    LogD3Event("preset_applied");
  }
}

void ToggleCollapse() {
  if (g_d3 == nullptr) {
    return;
  }
  const bool was_collapsed = g_d3->geometry.collapsed;
  g_d3->geometry.collapsed = !was_collapsed;
  ++g_d3->collapse_toggle_count;
  if (was_collapsed) {
    ++g_d3->collapse_cycle_count;
  }
  if (!ApplyGeometry(*g_d3, false)) {
    LogD3Event("geometry_apply_failed", "collapse");
  } else {
    LogD3Event(g_d3->geometry.collapsed ? "collapsed" : "expanded");
  }
}

LRESULT CALLBACK D3VisualWindowProc(HWND hwnd, UINT message, WPARAM w_param,
                                    LPARAM l_param) {
  switch (message) {
    case WM_MOUSEACTIVATE:
    case WM_LBUTTONDOWN:
    case WM_LBUTTONUP:
    case WM_RBUTTONDOWN:
    case WM_RBUTTONUP:
    case WM_CONTEXTMENU:
      if (g_d3 != nullptr) {
        ++g_d3->visual_pointer_messages;
        LogD3Event("unexpected_visual_input", "fail");
      }
      return message == WM_MOUSEACTIVATE ? MA_NOACTIVATE : 0;
    case WM_ERASEBKGND:
      return 1;
    case WM_PAINT: {
      PAINTSTRUCT paint{};
      HDC dc = BeginPaint(hwnd, &paint);
      RECT client{};
      GetClientRect(hwnd, &client);
      HBRUSH background = CreateSolidBrush(RGB(9, 29, 39));
      FillRect(dc, &client, background);
      DeleteObject(background);
      HPEN border = CreatePen(PS_SOLID, 2, RGB(80, 226, 235));
      HGDIOBJ old_pen = SelectObject(dc, border);
      HGDIOBJ old_brush = SelectObject(dc, GetStockObject(HOLLOW_BRUSH));
      Rectangle(dc, 0, 0, client.right, client.bottom);
      SelectObject(dc, old_brush);
      SelectObject(dc, old_pen);
      DeleteObject(border);
      SetBkMode(dc, TRANSPARENT);
      SetTextColor(dc, RGB(166, 231, 236));
      RECT heading{24, 64, client.right - 24, client.bottom - 24};
      DrawTextW(dc,
                g_d3 != nullptr && g_d3->geometry.collapsed
                    ? L"TS-H01-D3  COLLAPSED BOUNDARY"
                    : L"TS-H01-D3  DPI / WORK AREA / CLIENT / DWM\n\n"
                      L"BLANK AREA MUST REMAIN NATIVE DESKTOP INPUT",
                -1, &heading, DT_LEFT | DT_TOP | DT_WORDBREAK | DT_NOPREFIX);
      if (g_d3 != nullptr) {
        g_d3->render_target = SIZE{client.right - client.left,
                                  client.bottom - client.top};
        g_d3->render_revision = g_d3->geometry_revision;
      }
      EndPaint(hwnd, &paint);
      return 0;
    }
    default:
      return DefWindowProcW(hwnd, message, w_param, l_param);
  }
}

const wchar_t* D3RoleLabel(D3Role role) {
  switch (role) {
    case D3Role::Title: return L"DRAG TITLE (5 DIP)";
    case D3Role::Item: return L"SIZE PRESET";
    case D3Role::Collapse: return L"COLLAPSE";
    case D3Role::Resize: return L"RESIZE";
  }
  return L"UNKNOWN";
}

LRESULT CALLBACK D3IslandWindowProc(HWND hwnd, UINT message, WPARAM w_param,
                                    LPARAM l_param) {
  auto* state = reinterpret_cast<D3IslandState*>(
      GetWindowLongPtrW(hwnd, GWLP_USERDATA));
  if (message == WM_NCCREATE) {
    const auto* create = reinterpret_cast<CREATESTRUCTW*>(l_param);
    state = static_cast<D3IslandState*>(create->lpCreateParams);
    SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(state));
    if (state != nullptr) {
      state->hwnd = hwnd;
    }
  }

  switch (message) {
    case WM_MOUSEACTIVATE:
      return MA_NOACTIVATE;
    case WM_LBUTTONDOWN:
      if (state != nullptr &&
          (state->role == D3Role::Title || state->role == D3Role::Resize)) {
        BeginGesture(*state);
      }
      return 0;
    case WM_MOUSEMOVE:
      if (state != nullptr) {
        ContinueGesture(*state);
      }
      return 0;
    case WM_LBUTTONUP:
      if (state != nullptr) {
        if (state->role == D3Role::Title || state->role == D3Role::Resize) {
          EndGesture(*state);
        } else if (state->role == D3Role::Item) {
          ++state->click_count;
          ApplyNextPreset();
        } else if (state->role == D3Role::Collapse) {
          ++state->click_count;
          ToggleCollapse();
        }
        InvalidateRect(hwnd, nullptr, FALSE);
      }
      return 0;
    case WM_CAPTURECHANGED:
      if (state != nullptr) {
        state->pointer_down = false;
        state->gesture_counted = false;
      }
      return 0;
    case WM_ERASEBKGND:
      return 1;
    case WM_PAINT: {
      PAINTSTRUCT paint{};
      HDC dc = BeginPaint(hwnd, &paint);
      RECT client{};
      GetClientRect(hwnd, &client);
      const D3Role role = state != nullptr ? state->role : D3Role::Title;
      const COLORREF color = role == D3Role::Title
                                 ? RGB(14, 87, 96)
                                 : role == D3Role::Item
                                       ? RGB(91, 24, 70)
                                       : role == D3Role::Collapse
                                             ? RGB(105, 79, 8)
                                             : RGB(32, 101, 62);
      HBRUSH background = CreateSolidBrush(color);
      FillRect(dc, &client, background);
      DeleteObject(background);
      SetBkMode(dc, TRANSPARENT);
      SetTextColor(dc, RGB(245, 252, 253));
      RECT text = client;
      DrawTextW(dc, D3RoleLabel(role), -1, &text,
                DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
      EndPaint(hwnd, &paint);
      return 0;
    }
    default:
      return DefWindowProcW(hwnd, message, w_param, l_param);
  }
}

bool RegisterD3Classes(HINSTANCE instance) {
  WNDCLASSEXW visual_class{};
  visual_class.cbSize = sizeof(visual_class);
  visual_class.lpfnWndProc = D3VisualWindowProc;
  visual_class.hInstance = instance;
  visual_class.hCursor = LoadCursorW(nullptr, IDC_ARROW);
  visual_class.lpszClassName = kD3VisualClass;

  WNDCLASSEXW island_class{};
  island_class.cbSize = sizeof(island_class);
  island_class.lpfnWndProc = D3IslandWindowProc;
  island_class.hInstance = instance;
  island_class.hCursor = LoadCursorW(nullptr, IDC_HAND);
  island_class.lpszClassName = kD3IslandClass;
  return RegisterClassExW(&visual_class) != 0 &&
         RegisterClassExW(&island_class) != 0;
}

std::optional<std::uint64_t> ParseCount(const wchar_t* text) {
  try {
    std::size_t consumed = 0;
    const unsigned long long value = std::stoull(text, &consumed);
    if (consumed != std::wstring(text).size()) {
      return std::nullopt;
    }
    return static_cast<std::uint64_t>(value);
  } catch (...) {
    return std::nullopt;
  }
}

std::optional<D3Options> ParseD3Options(int argc, wchar_t** argv) {
  D3Options options;
  for (int index = 1; index < argc; ++index) {
    const std::wstring argument = argv[index];
    if (argument == L"--run-boundary-test") {
      options.run_boundary_test = true;
    } else if (argument == L"--scenario" && index + 1 < argc) {
      options.scenario = argv[++index];
      if (options.scenario != L"static" &&
          options.scenario != L"interactive" &&
          options.scenario != L"display-change") {
        return std::nullopt;
      }
    } else if (argument == L"--expected-dpi" && index + 1 < argc) {
      const auto value = ParseCount(argv[++index]);
      if (!value.has_value() || (*value != 96 && *value != 120 &&
                                 *value != 144 && *value != 192)) {
        return std::nullopt;
      }
      options.expected_dpi = static_cast<UINT>(*value);
    } else if (argument == L"--duration-ms" && index + 1 < argc) {
      const auto value = ParseCount(argv[++index]);
      if (!value.has_value() || *value == 0 || *value > kD3MaximumDurationMs) {
        return std::nullopt;
      }
      options.duration_ms = static_cast<DWORD>(*value);
    } else if (argument == L"--expected-drags" && index + 1 < argc) {
      const auto value = ParseCount(argv[++index]);
      if (!value.has_value()) return std::nullopt;
      options.expected_drags = *value;
    } else if (argument == L"--expected-resizes" && index + 1 < argc) {
      const auto value = ParseCount(argv[++index]);
      if (!value.has_value()) return std::nullopt;
      options.expected_resizes = *value;
    } else if (argument == L"--expected-collapse-cycles" &&
               index + 1 < argc) {
      const auto value = ParseCount(argv[++index]);
      if (!value.has_value()) return std::nullopt;
      options.expected_collapse_cycles = *value;
    } else if (argument == L"--expected-display-changes" &&
               index + 1 < argc) {
      const auto value = ParseCount(argv[++index]);
      if (!value.has_value()) return std::nullopt;
      options.expected_display_changes = *value;
    } else if (argument == L"--log" && index + 1 < argc) {
      options.log_path = std::filesystem::path(argv[++index]);
    } else {
      return std::nullopt;
    }
  }
  if (!options.run_boundary_test || options.scenario.empty() ||
      options.expected_dpi == 0) {
    return std::nullopt;
  }
  return options;
}

RECT ClientScreenRect(HWND hwnd) {
  RECT client{};
  GetClientRect(hwnd, &client);
  POINT points[2]{{client.left, client.top}, {client.right, client.bottom}};
  MapWindowPoints(hwnd, nullptr, points, 2);
  return RECT{points[0].x, points[0].y, points[1].x, points[1].y};
}

std::string DpiContextName(DPI_AWARENESS_CONTEXT context) {
  if (AreDpiAwarenessContextsEqual(
          context, DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)) {
    return "PER_MONITOR_AWARE_V2";
  }
  if (AreDpiAwarenessContextsEqual(
          context, DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE)) {
    return "PER_MONITOR_AWARE";
  }
  if (AreDpiAwarenessContextsEqual(context,
                                   DPI_AWARENESS_CONTEXT_SYSTEM_AWARE)) {
    return "SYSTEM_AWARE";
  }
  return "OTHER";
}

std::string D3StartJson(const D3Runtime& runtime) {
  std::ostringstream stream;
  stream << "{\"schema\":\"desktopup.ts-h01-d3.event.v1\""
         << ",\"event\":\"boundary_test_start\""
         << ",\"build_id\":\"" << JsonEscape(Utf8(kD3BuildId)) << "\""
         << ",\"scenario\":\"" << JsonEscape(Utf8(runtime.options.scenario))
         << "\",\"duration_ms\":" << runtime.options.duration_ms
         << ",\"expected_dpi\":" << runtime.options.expected_dpi
         << ",\"expected_drags\":" << runtime.options.expected_drags
         << ",\"detected_dpi\":" << runtime.display.dpi
         << ",\"expected_resizes\":" << runtime.options.expected_resizes
         << ",\"expected_collapse_cycles\":"
         << runtime.options.expected_collapse_cycles
         << ",\"expected_display_changes\":"
         << runtime.options.expected_display_changes
         << ",\"dpi_context\":\""
         << DpiContextName(GetThreadDpiAwarenessContext()) << "\""
         << ",\"topology\":\"" << JsonEscape(Utf8(runtime.target.topology))
         << "\",\"explorer_pid\":" << runtime.target.explorer_pid << "}";
  return stream.str();
}

std::string D3SampleJson(std::uint64_t sequence, const D3Runtime& runtime,
                         bool* passed) {
  RECT window{};
  GetWindowRect(runtime.visual, &window);
  const RECT client_screen = ClientScreenRect(runtime.visual);
  const RECT extended = ExtendedFrameBounds(runtime.visual);
  const LONG_PTR style = GetWindowLongPtrW(runtime.visual, GWL_STYLE);
  const LONG_PTR ex_style = GetWindowLongPtrW(runtime.visual, GWL_EXSTYLE);
  const int visual_index = ChildIndex(runtime.target.icon_host, runtime.visual);
  const int def_view_index = ChildIndex(runtime.target.icon_host,
                                        runtime.target.def_view);
  bool island_contract = true;
  bool z_contract = visual_index >= 0 && def_view_index >= 0 &&
                    visual_index < def_view_index;
  for (const D3IslandState& island : runtime.islands) {
    const LONG_PTR island_style = GetWindowLongPtrW(island.hwnd, GWL_STYLE);
    const LONG_PTR island_ex = GetWindowLongPtrW(island.hwnd, GWL_EXSTYLE);
    const int island_index = ChildIndex(runtime.target.icon_host, island.hwnd);
    island_contract = island_contract && IsWindow(island.hwnd) &&
        GetParent(island.hwnd) == runtime.target.icon_host &&
        (island_style & WS_CHILD) != 0 && (island_style & WS_CAPTION) == 0 &&
        (island_style & WS_THICKFRAME) == 0 &&
        (island_ex & WS_EX_TOPMOST) == 0 &&
        GetWindow(island.hwnd, GW_OWNER) == nullptr;
    z_contract = z_contract && island_index >= 0 &&
                 island_index < visual_index;
  }
  const bool bounds_contract = NearRect(window, runtime.expected_window_rect, 1) &&
      EqualRectValue(window, client_screen) && EqualRectValue(window, extended) &&
      runtime.render_target.cx == RectWidth(client_screen) &&
      runtime.render_target.cy == RectHeight(client_screen) &&
      runtime.render_revision == runtime.geometry_revision;
  const bool style_contract = GetParent(runtime.visual) == runtime.target.icon_host &&
      (style & WS_CHILD) != 0 && (style & WS_CAPTION) == 0 &&
      (style & WS_THICKFRAME) == 0 && (ex_style & WS_EX_LAYERED) != 0 &&
      (ex_style & WS_EX_TRANSPARENT) != 0 &&
      (ex_style & WS_EX_TOPMOST) == 0 &&
      GetWindow(runtime.visual, GW_OWNER) == nullptr &&
      !IsIconic(runtime.visual) && !IsCloaked(runtime.visual) &&
      GetForegroundWindow() != runtime.visual;
  const UINT window_dpi = GetDpiForWindow(runtime.visual);
  const UINT shell_parent_dpi = GetDpiForWindow(runtime.target.icon_host);
  const bool pmv2_context = AreDpiAwarenessContextsEqual(
      GetThreadDpiAwarenessContext(),
      DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2) != FALSE;
  const bool dpi_contract = runtime.display.dpi == runtime.options.expected_dpi &&
      pmv2_context && window_dpi != 0 && shell_parent_dpi != 0;
  const bool revision_contract = runtime.geometry_revision != 0 &&
      runtime.hit_region_revision == runtime.geometry_revision;
  const bool topology_contract = runtime.target.explorer_pid != 0 &&
      IsWindow(runtime.target.icon_host) && IsWindow(runtime.target.def_view) &&
      GetParent(runtime.target.def_view) == runtime.target.icon_host;
  const bool pass = bounds_contract && style_contract && island_contract &&
      z_contract && dpi_contract && revision_contract && topology_contract &&
      runtime.visual_pointer_messages == 0;
  if (passed != nullptr) {
    *passed = pass;
  }
  const double elapsed_ms = std::chrono::duration<double, std::milli>(
      Clock::now() - runtime.started_at).count();
  const double current_height = runtime.geometry.collapsed
                                    ? kCollapsedHeightDip
                                    : runtime.geometry.expanded_height_dip;
  std::ostringstream stream;
  stream << "{\"schema\":\"desktopup.ts-h01-d3.sample.v1\""
         << ",\"sequence\":" << sequence
         << ",\"elapsed_ms\":" << std::fixed << std::setprecision(3)
         << elapsed_ms
         << ",\"logical_geometry_dip\":{\"left\":"
         << runtime.geometry.left_dip << ",\"top\":"
         << runtime.geometry.top_dip << ",\"width\":"
         << runtime.geometry.width_dip << ",\"height\":" << current_height
         << "},\"normalized_anchor\":{\"x\":"
         << runtime.geometry.anchor_x << ",\"y\":"
         << runtime.geometry.anchor_y << "}"
         << ",\"monitor_rect_px\":" << RectJson(runtime.display.monitor)
         << ",\"work_area_rect_px\":" << RectJson(runtime.display.work)
         << ",\"expected_window_rect_px\":"
         << RectJson(runtime.expected_window_rect)
         << ",\"window_rect_px\":" << RectJson(window)
         << ",\"client_screen_rect_px\":" << RectJson(client_screen)
         << ",\"extended_frame_bounds_px\":" << RectJson(extended)
         << ",\"render_target_px\":{\"width\":"
         << runtime.render_target.cx << ",\"height\":"
         << runtime.render_target.cy << "}"
         << ",\"effective_dpi\":" << runtime.display.dpi
         << ",\"window_dpi\":" << window_dpi
         << ",\"shell_parent_dpi\":" << shell_parent_dpi
         << ",\"window_dpi_matches_effective\":" << (window_dpi == runtime.display.dpi ? "true" : "false")
         << ",\"dpi_context\":\""
         << DpiContextName(GetThreadDpiAwarenessContext()) << "\""
         << ",\"collapsed\":" << (runtime.geometry.collapsed ? "true" : "false")
         << ",\"geometry_revision\":" << runtime.geometry_revision
         << ",\"hit_region_revision\":" << runtime.hit_region_revision
         << ",\"render_revision\":" << runtime.render_revision
         << ",\"drag_count\":" << runtime.drag_count
         << ",\"resize_count\":" << runtime.resize_count
         << ",\"collapse_toggle_count\":" << runtime.collapse_toggle_count
         << ",\"collapse_cycle_count\":" << runtime.collapse_cycle_count
         << ",\"preset_count\":" << runtime.preset_count
         << ",\"display_change_count\":" << runtime.display_change_count
         << ",\"visual_index\":" << visual_index
         << ",\"def_view_index\":" << def_view_index
         << ",\"bounds_contract\":" << (bounds_contract ? "true" : "false")
         << ",\"style_contract\":" << (style_contract ? "true" : "false")
         << ",\"island_contract\":" << (island_contract ? "true" : "false")
         << ",\"z_contract\":" << (z_contract ? "true" : "false")
         << ",\"dpi_contract\":" << (dpi_contract ? "true" : "false")
         << ",\"revision_contract\":"
         << (revision_contract ? "true" : "false")
         << ",\"topology_contract\":"
         << (topology_contract ? "true" : "false")
         << ",\"visual_pointer_messages\":"
         << runtime.visual_pointer_messages
         << ",\"pass\":" << (pass ? "true" : "false") << "}";
  return stream.str();
}

void InitializeGeometry(D3Runtime& runtime) {
  const double work_width = WorkWidthDip(runtime.display);
  const double work_height = WorkHeightDip(runtime.display);
  runtime.geometry.width_dip = ClampDouble(
      std::max(work_width * 0.22, 280.0), kMinWidthDip,
      std::max(kMinWidthDip, work_width * 0.45));
  runtime.geometry.expanded_height_dip = std::max(
      kMinExpandedHeightDip, work_height - 2.0 * kSafeMarginDip);
  runtime.geometry.left_dip = std::max(
      0.0, work_width - runtime.geometry.width_dip - kSafeMarginDip);
  runtime.geometry.top_dip = std::min(
      kSafeMarginDip,
      std::max(0.0, work_height - runtime.geometry.expanded_height_dip));
  runtime.geometry.collapsed = false;
  ClampGeometry(runtime);
}

bool CreateD3Surfaces(D3Runtime& runtime, HINSTANCE instance) {
  const RECT initial_screen = ProjectWindowRect(runtime);
  const RECT initial_parent = ScreenToParentRect(runtime.target.icon_host,
                                                 initial_screen);
  runtime.visual = CreateLayeredChild(
      WS_EX_TRANSPARENT, kD3VisualClass, L"TS-H01-D3 Transparent Visual",
      initial_parent, runtime.target.icon_host, instance, nullptr);
  runtime.islands = {{
      {D3Role::Title},
      {D3Role::Item},
      {D3Role::Collapse},
      {D3Role::Resize},
  }};
  const auto rects = IslandRects(runtime, initial_parent);
  for (std::size_t index = 0; index < runtime.islands.size(); ++index) {
    runtime.islands[index].hwnd = CreateLayeredChild(
        0, kD3IslandClass, L"TS-H01-D3 Interaction Island", rects[index],
        runtime.target.icon_host, instance, &runtime.islands[index]);
  }
  const bool created = runtime.visual != nullptr &&
      std::all_of(runtime.islands.begin(), runtime.islands.end(),
                  [](const D3IslandState& island) {
                    return island.hwnd != nullptr;
                  });
  if (!created) {
    return false;
  }
  runtime.expected_window_rect = initial_screen;
  SetWindowPos(runtime.visual, HWND_TOP, 0, 0, 0, 0,
               SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
  for (D3IslandState& island : runtime.islands) {
    SetWindowPos(island.hwnd, HWND_TOP, 0, 0, 0, 0,
                 SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
  }
  return ApplyGeometry(runtime, false);
}

bool DestroyD3Surfaces(D3Runtime& runtime) {
  bool destroyed = true;
  for (auto it = runtime.islands.rbegin(); it != runtime.islands.rend(); ++it) {
    if (it->hwnd != nullptr && IsWindow(it->hwnd)) {
      destroyed = (DestroyWindow(it->hwnd) != FALSE) && destroyed;
    }
  }
  if (runtime.visual != nullptr && IsWindow(runtime.visual)) {
    destroyed = (DestroyWindow(runtime.visual) != FALSE) && destroyed;
  }
  return destroyed;
}

bool ExpectedCountsMatch(const D3Runtime& runtime) {
  if (runtime.options.scenario == L"interactive") {
    return runtime.drag_count >= runtime.options.expected_drags &&
           runtime.resize_count >= runtime.options.expected_resizes &&
           runtime.collapse_cycle_count >=
               runtime.options.expected_collapse_cycles &&
           runtime.display_change_count ==
               runtime.options.expected_display_changes;
  }
  if (runtime.options.scenario == L"display-change") {
    return runtime.drag_count == runtime.options.expected_drags &&
           runtime.resize_count == runtime.options.expected_resizes &&
           runtime.collapse_cycle_count ==
               runtime.options.expected_collapse_cycles &&
           runtime.display_change_count >=
               runtime.options.expected_display_changes;
  }
  return runtime.drag_count == runtime.options.expected_drags &&
         runtime.resize_count == runtime.options.expected_resizes &&
         runtime.collapse_cycle_count == runtime.options.expected_collapse_cycles &&
         runtime.display_change_count == runtime.options.expected_display_changes;
}

}  // namespace

int wmain(int argc, wchar_t** argv) {
  const auto options = ParseD3Options(argc, argv);
  if (!options.has_value()) {
    std::wcerr << L"Explicit --run-boundary-test, valid --scenario and "
                  L"--expected-dpi are required.\n";
    return 2;
  }
  if (!SetProcessDpiAwarenessContext(
          DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)) {
    if (!AreDpiAwarenessContextsEqual(
            GetThreadDpiAwarenessContext(),
            DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)) {
      std::wcerr << L"Failed to establish Per-Monitor v2 DPI awareness.\n";
      return 2;
    }
  }

  JsonlSink sink(options->log_path);
  if (!sink.IsValid(options->log_path)) {
    std::wcerr << L"Failed to open log path.\n";
    return 2;
  }
  D3Runtime runtime;
  runtime.sink = &sink;
  runtime.started_at = Clock::now();
  runtime.options = *options;
  runtime.target = DiscoverShellTarget();
  runtime.display = QueryPrimaryDisplay(runtime.target.icon_host);
  g_d3 = &runtime;
  sink.Write(D3StartJson(runtime));
  if (runtime.target.topology != L"RAISED_PROGMAN" ||
      runtime.target.icon_host == nullptr || runtime.target.def_view == nullptr ||
      runtime.target.icon_view == nullptr ||
      runtime.target.visible_def_views.size() != 1) {
    LogD3Event("degraded_no_desktop_host",
               "requires_unambiguous_raised_progman");
    g_d3 = nullptr;
    return 3;
  }
  if (runtime.display.dpi != options->expected_dpi) {
    LogD3Event("expected_dpi_mismatch", "fail_closed");
    g_d3 = nullptr;
    return 3;
  }

  HINSTANCE instance = GetModuleHandleW(nullptr);
  if (!RegisterD3Classes(instance)) {
    LogD3Event("window_class_failed", "registration_failed");
    g_d3 = nullptr;
    return 4;
  }
  InitializeGeometry(runtime);
  if (!CreateD3Surfaces(runtime, instance)) {
    DestroyD3Surfaces(runtime);
    UnregisterClassW(kD3IslandClass, instance);
    UnregisterClassW(kD3VisualClass, instance);
    LogD3Event("window_create_failed", "layered_child_failed");
    g_d3 = nullptr;
    return 4;
  }
  LogD3Event("boundary_surface_created", "no_nonclient_no_topmost");

  HANDLE timer = CreateWaitableTimerExW(
      nullptr, nullptr, CREATE_WAITABLE_TIMER_HIGH_RESOLUTION,
      TIMER_ALL_ACCESS);
  LARGE_INTEGER due{};
  due.QuadPart = -10000;
  const bool timer_ready = timer != nullptr &&
      SetWaitableTimerEx(timer, &due, static_cast<LONG>(kD3SampleIntervalMs),
                         nullptr, nullptr, nullptr, 0) != FALSE;
  bool all_samples_passed = timer_ready;
  if (!timer_ready) {
    LogD3Event("high_resolution_timer_failed", "fail");
  }

  const auto deadline = runtime.started_at +
                        std::chrono::milliseconds(options->duration_ms);
  std::uint64_t sequence = 0;
  std::optional<Clock::time_point> interaction_contract_since;
  while (timer_ready && Clock::now() < deadline && IsWindow(runtime.visual)) {
    const DWORD wait_result = MsgWaitForMultipleObjectsEx(
        1, &timer, 100, QS_ALLINPUT, MWMO_INPUTAVAILABLE);
    MSG message{};
    while (PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE)) {
      TranslateMessage(&message);
      DispatchMessageW(&message);
    }
    if (wait_result == WAIT_OBJECT_0) {
      const DisplaySnapshot current = QueryPrimaryDisplay(runtime.visual);
      if (!SameDisplay(current, runtime.display)) {
        runtime.display = current;
        ++runtime.display_change_count;
        if (!ApplyGeometry(runtime, true)) {
          all_samples_passed = false;
          LogD3Event("geometry_apply_failed", "display_change");
        } else {
          LogD3Event("display_geometry_changed", "anchor_reprojected");
        }
      }
      bool sample_pass = false;
      sink.Write(D3SampleJson(sequence++, runtime, &sample_pass));
      all_samples_passed = all_samples_passed && sample_pass;
      if (options->scenario != L"static" &&
          ExpectedCountsMatch(runtime)) {
        if (!interaction_contract_since.has_value()) {
          interaction_contract_since = Clock::now();
          LogD3Event("scenario_contract_satisfied",
                     "stable_observation_started");
        } else if (Clock::now() - *interaction_contract_since >=
                   std::chrono::milliseconds(1500)) {
          LogD3Event("scenario_contract_stable",
                     "bounded_run_complete");
          break;
        }
      } else {
        interaction_contract_since.reset();
      }
    } else if (wait_result == WAIT_FAILED) {
      all_samples_passed = false;
      LogD3Event("high_resolution_timer_failed", "wait_failed");
      break;
    }
  }

  if (timer != nullptr) {
    CancelWaitableTimer(timer);
    CloseHandle(timer);
  }
  const bool count_contract = ExpectedCountsMatch(runtime);
  const std::uint64_t visual_pointer_messages = runtime.visual_pointer_messages;
  const bool destroyed = DestroyD3Surfaces(runtime);
  UnregisterClassW(kD3IslandClass, instance);
  UnregisterClassW(kD3VisualClass, instance);
  const bool complete = all_samples_passed && sequence != 0 &&
                        count_contract && destroyed &&
                        visual_pointer_messages == 0;
  LogD3Event("boundary_test_complete",
             complete ? "boundary_telemetry_pass_manual_visual_pending"
                      : "boundary_contract_fail");
  g_d3 = nullptr;
  return complete ? 0 : 4;
}
