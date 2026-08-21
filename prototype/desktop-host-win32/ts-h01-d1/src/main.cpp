#include <windows.h>
#include <dwmapi.h>

#include <algorithm>
#include <array>
#include <chrono>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <optional>
#include <sstream>
#include <string>
#include <thread>
#include <vector>

namespace {

constexpr wchar_t kVisualClass[] = L"DesktopUp.TS-H01-D1.VisualSurface";
constexpr wchar_t kIslandClass[] = L"DesktopUp.TS-H01-D1.InteractionIsland";
constexpr wchar_t kBuildId[] = L"TS-H01-D1-20260820-01-CONTINUITY";
constexpr DWORD kDefaultDurationMs = 150000;
constexpr DWORD kMaximumDurationMs = 300000;
constexpr DWORD kSampleIntervalMs = 8;

using Clock = std::chrono::steady_clock;

struct Options {
  bool run_lifecycle_test = false;
  DWORD duration_ms = kDefaultDurationMs;
  std::wstring scenario;
  std::optional<std::filesystem::path> log_path;
};

struct ShellTarget {
  HWND progman = nullptr;
  HWND icon_host = nullptr;
  HWND def_view = nullptr;
  HWND icon_view = nullptr;
  DWORD explorer_pid = 0;
  std::wstring topology;
  std::vector<HWND> visible_def_views;
};

enum class IslandRole { Title, Item, Action };

struct IslandState {
  IslandRole role = IslandRole::Title;
  HWND hwnd = nullptr;
  std::uint64_t click_count = 0;
};

class JsonlSink {
 public:
  explicit JsonlSink(const std::optional<std::filesystem::path>& path) {
    echo_stdout_ = !path.has_value();
    if (path.has_value()) {
      file_.open(*path, std::ios::out | std::ios::trunc | std::ios::binary);
    }
  }

  ~JsonlSink() {
    if (file_.is_open()) {
      file_.flush();
    }
  }

  bool IsValid(const std::optional<std::filesystem::path>& path) const {
    return !path.has_value() || file_.is_open();
  }

  void Write(const std::string& line) {
    if (echo_stdout_) {
      std::cout << line << '\n';
    }
    if (file_.is_open()) {
      file_ << line << '\n';
    }
  }

 private:
  std::ofstream file_;
  bool echo_stdout_ = true;
};

struct Runtime {
  JsonlSink* sink = nullptr;
  Clock::time_point started_at{};
  std::uint64_t visual_pointer_messages = 0;
};

Runtime* g_runtime = nullptr;

std::string Utf8(const std::wstring& value) {
  if (value.empty()) {
    return {};
  }
  const int size = WideCharToMultiByte(CP_UTF8, 0, value.data(),
                                       static_cast<int>(value.size()), nullptr, 0,
                                       nullptr, nullptr);
  std::string result(static_cast<std::size_t>(size), '\0');
  WideCharToMultiByte(CP_UTF8, 0, value.data(), static_cast<int>(value.size()),
                      result.data(), size, nullptr, nullptr);
  return result;
}

std::string JsonEscape(const std::string& value) {
  std::ostringstream stream;
  for (const unsigned char ch : value) {
    switch (ch) {
      case '\\': stream << "\\\\"; break;
      case '"': stream << "\\\""; break;
      case '\b': stream << "\\b"; break;
      case '\f': stream << "\\f"; break;
      case '\n': stream << "\\n"; break;
      case '\r': stream << "\\r"; break;
      case '\t': stream << "\\t"; break;
      default:
        if (ch < 0x20) {
          stream << "\\u" << std::hex << std::setw(4) << std::setfill('0')
                 << static_cast<int>(ch) << std::dec;
        } else {
          stream << ch;
        }
    }
  }
  return stream.str();
}

std::string HwndJson(HWND hwnd) {
  if (hwnd == nullptr) {
    return "null";
  }
  std::ostringstream stream;
  stream << "\"0x" << std::uppercase << std::hex
         << reinterpret_cast<std::uintptr_t>(hwnd) << "\"";
  return stream.str();
}

std::wstring ClassName(HWND hwnd) {
  wchar_t buffer[256]{};
  const int length = GetClassNameW(hwnd, buffer, static_cast<int>(std::size(buffer)));
  return length > 0 ? std::wstring(buffer, static_cast<std::size_t>(length)) : L"";
}

bool IsClass(HWND hwnd, const wchar_t* class_name) {
  return ClassName(hwnd) == class_name;
}

bool IsCloaked(HWND hwnd) {
  DWORD cloaked = 0;
  return SUCCEEDED(DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, &cloaked,
                                         sizeof(cloaked))) && cloaked != 0;
}

std::vector<HWND> ChildZOrder(HWND parent) {
  std::vector<HWND> result;
  for (HWND child = GetWindow(parent, GW_CHILD); child != nullptr;
       child = GetWindow(child, GW_HWNDNEXT)) {
    result.push_back(child);
  }
  return result;
}

int ChildIndex(HWND parent, HWND target) {
  const auto children = ChildZOrder(parent);
  const auto it = std::find(children.begin(), children.end(), target);
  return it == children.end() ? -1 : static_cast<int>(it - children.begin());
}

std::string RectJson(const RECT& rect) {
  std::ostringstream stream;
  stream << "{\"left\":" << rect.left << ",\"top\":" << rect.top
         << ",\"right\":" << rect.right << ",\"bottom\":" << rect.bottom
         << ",\"width\":" << rect.right - rect.left
         << ",\"height\":" << rect.bottom - rect.top << "}";
  return stream.str();
}

RECT ExtendedFrameBounds(HWND hwnd) {
  RECT rect{};
  if (FAILED(DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, &rect,
                                   sizeof(rect)))) {
    GetWindowRect(hwnd, &rect);
  }
  return rect;
}

std::string WindowStateJson(HWND hwnd, HWND sibling_parent) {
  if (hwnd == nullptr || !IsWindow(hwnd)) {
    return "{\"exists\":false}";
  }
  RECT window_rect{};
  RECT client_rect{};
  GetWindowRect(hwnd, &window_rect);
  GetClientRect(hwnd, &client_rect);
  const LONG_PTR style = GetWindowLongPtrW(hwnd, GWL_STYLE);
  const LONG_PTR ex_style = GetWindowLongPtrW(hwnd, GWL_EXSTYLE);
  std::ostringstream stream;
  stream << "{\"exists\":true"
         << ",\"hwnd\":" << HwndJson(hwnd)
         << ",\"parent\":" << HwndJson(GetParent(hwnd))
         << ",\"owner\":" << HwndJson(GetWindow(hwnd, GW_OWNER))
         << ",\"z_above\":" << HwndJson(GetWindow(hwnd, GW_HWNDPREV))
         << ",\"z_below\":" << HwndJson(GetWindow(hwnd, GW_HWNDNEXT))
         << ",\"child_index\":" << ChildIndex(sibling_parent, hwnd)
         << ",\"style\":\"0x" << std::uppercase << std::hex
         << static_cast<std::uintptr_t>(style) << "\""
         << ",\"ex_style\":\"0x"
         << static_cast<std::uintptr_t>(ex_style) << "\"" << std::dec
         << ",\"visible\":" << (IsWindowVisible(hwnd) ? "true" : "false")
         << ",\"iconic\":" << (IsIconic(hwnd) ? "true" : "false")
         << ",\"cloaked\":" << (IsCloaked(hwnd) ? "true" : "false")
         << ",\"active\":" << (GetForegroundWindow() == hwnd ? "true" : "false")
         << ",\"topmost\":" << ((ex_style & WS_EX_TOPMOST) != 0 ? "true" : "false")
         << ",\"window_rect\":" << RectJson(window_rect)
         << ",\"client_rect\":" << RectJson(client_rect)
         << ",\"extended_frame_bounds\":" << RectJson(ExtendedFrameBounds(hwnd))
         << ",\"dpi\":" << GetDpiForWindow(hwnd) << "}";
  return stream.str();
}

BOOL CALLBACK CollectVisibleDefViews(HWND top_level, LPARAM parameter) {
  auto* targets = reinterpret_cast<std::vector<HWND>*>(parameter);
  HWND def_view = FindWindowExW(top_level, nullptr, L"SHELLDLL_DefView", nullptr);
  if (def_view != nullptr && IsWindowVisible(def_view) && !IsCloaked(def_view)) {
    targets->push_back(def_view);
  }
  return TRUE;
}

ShellTarget DiscoverShellTarget() {
  ShellTarget target;
  target.progman = FindWindowW(L"Progman", nullptr);
  EnumWindows(CollectVisibleDefViews,
              reinterpret_cast<LPARAM>(&target.visible_def_views));
  if (target.visible_def_views.size() != 1) {
    return target;
  }

  target.def_view = target.visible_def_views.front();
  target.icon_host = GetParent(target.def_view);
  GetWindowThreadProcessId(target.def_view, &target.explorer_pid);
  if (target.icon_host == target.progman && IsClass(target.icon_host, L"Progman")) {
    target.topology = L"RAISED_PROGMAN";
  } else if (IsClass(target.icon_host, L"WorkerW")) {
    target.topology = L"TRADITIONAL_WORKERW";
  } else {
    target.topology = L"UNSUPPORTED";
  }

  target.icon_view = FindWindowExW(target.def_view, nullptr, L"SysListView32", nullptr);
  if (target.icon_view == nullptr) {
    target.icon_view = FindWindowExW(target.def_view, nullptr, L"FolderView", nullptr);
  }
  return target;
}

std::optional<Options> ParseOptions(int argc, wchar_t** argv) {
  Options options;
  for (int index = 1; index < argc; ++index) {
    const std::wstring argument = argv[index];
    if (argument == L"--run-lifecycle-test") {
      options.run_lifecycle_test = true;
    } else if (argument == L"--scenario" && index + 1 < argc) {
      options.scenario = argv[++index];
      if (options.scenario != L"win-d" &&
          options.scenario != L"taskbar-show-desktop") {
        return std::nullopt;
      }
    } else if (argument == L"--duration-ms" && index + 1 < argc) {
      try {
        const unsigned long value = std::stoul(argv[++index]);
        if (value == 0 || value > kMaximumDurationMs) {
          return std::nullopt;
        }
        options.duration_ms = static_cast<DWORD>(value);
      } catch (...) {
        return std::nullopt;
      }
    } else if (argument == L"--log" && index + 1 < argc) {
      options.log_path = std::filesystem::path(argv[++index]);
    } else {
      return std::nullopt;
    }
  }
  if (options.scenario.empty()) {
    return std::nullopt;
  }
  return options;
}

const char* RoleName(IslandRole role) {
  switch (role) {
    case IslandRole::Title: return "title";
    case IslandRole::Item: return "item";
    case IslandRole::Action: return "action";
  }
  return "unknown";
}

void LogPointerEvent(const char* event, HWND hwnd, const char* role,
                     std::uint64_t count) {
  if (g_runtime == nullptr || g_runtime->sink == nullptr) {
    return;
  }
  const double elapsed_ms = std::chrono::duration<double, std::milli>(
      Clock::now() - g_runtime->started_at).count();
  std::ostringstream stream;
  stream << "{\"schema\":\"desktopup.ts-h01-d1.input.v1\""
         << ",\"event\":\"" << event << "\""
         << ",\"elapsed_ms\":" << std::fixed << std::setprecision(3)
         << elapsed_ms
         << ",\"hwnd\":" << HwndJson(hwnd)
         << ",\"role\":\"" << role << "\""
         << ",\"count\":" << count << "}";
  g_runtime->sink->Write(stream.str());
}

COLORREF IslandColor(IslandRole role, std::uint64_t click_count) {
  const bool toggled = (click_count % 2) != 0;
  switch (role) {
    case IslandRole::Title:
      return toggled ? RGB(16, 118, 128) : RGB(12, 76, 85);
    case IslandRole::Item:
      return toggled ? RGB(150, 35, 103) : RGB(91, 24, 70);
    case IslandRole::Action:
      return toggled ? RGB(144, 111, 10) : RGB(91, 71, 8);
  }
  return RGB(30, 30, 30);
}

const wchar_t* IslandLabel(IslandRole role) {
  switch (role) {
    case IslandRole::Title: return L"TITLE ISLAND — CLICK";
    case IslandRole::Item: return L"ITEM ISLAND\nCLICK COUNTER";
    case IslandRole::Action: return L"ACTION";
  }
  return L"UNKNOWN";
}

LRESULT CALLBACK VisualWindowProc(HWND hwnd, UINT message, WPARAM w_param,
                                  LPARAM l_param) {
  switch (message) {
    case WM_MOUSEACTIVATE:
    case WM_LBUTTONDOWN:
    case WM_LBUTTONUP:
    case WM_RBUTTONDOWN:
    case WM_RBUTTONUP:
    case WM_CONTEXTMENU:
      if (g_runtime != nullptr) {
        ++g_runtime->visual_pointer_messages;
        LogPointerEvent("unexpected_visual_input", hwnd, "visual",
                        g_runtime->visual_pointer_messages);
      }
      if (message == WM_MOUSEACTIVATE) {
        return MA_NOACTIVATE;
      }
      return 0;
    case WM_ERASEBKGND:
      return 1;
    case WM_PAINT: {
      PAINTSTRUCT paint{};
      HDC dc = BeginPaint(hwnd, &paint);
      RECT client{};
      GetClientRect(hwnd, &client);

      HBRUSH background = CreateSolidBrush(RGB(8, 24, 34));
      FillRect(dc, &client, background);
      DeleteObject(background);

      HPEN border = CreatePen(PS_SOLID, 3, RGB(75, 225, 236));
      HGDIOBJ old_pen = SelectObject(dc, border);
      HGDIOBJ old_brush = SelectObject(dc, GetStockObject(HOLLOW_BRUSH));
      Rectangle(dc, 1, 1, client.right - 1, client.bottom - 1);

      HPEN hatch = CreatePen(PS_DOT, 1, RGB(67, 110, 122));
      SelectObject(dc, hatch);
      RECT blank{260, 105, client.right - 24, client.bottom - 24};
      Rectangle(dc, blank.left, blank.top, blank.right, blank.bottom);
      for (int y = blank.top + 12; y < blank.bottom; y += 18) {
        MoveToEx(dc, blank.left + 8, y, nullptr);
        LineTo(dc, blank.right - 8, y);
      }
      SelectObject(dc, old_brush);
      SelectObject(dc, old_pen);
      DeleteObject(hatch);
      DeleteObject(border);

      SetBkMode(dc, TRANSPARENT);
      SetTextColor(dc, RGB(155, 223, 229));
      RECT heading{24, 78, client.right - 24, 103};
      DrawTextW(dc, L"TS-H01-D1  SHOW-DESKTOP CONTINUITY", -1, &heading,
                DT_LEFT | DT_TOP | DT_NOPREFIX);
      RECT blank_text{blank.left + 16, blank.top + 18, blank.right - 16,
                      blank.bottom - 16};
      DrawTextW(dc,
                L"BLANK / EXPLORER INPUT\n\nLEFT-DRAG: DESKTOP SELECTION\n"
                L"RIGHT-CLICK: DESKTOP MENU\nSHORTCUT: SELECT + LAUNCH",
                -1, &blank_text,
                DT_LEFT | DT_TOP | DT_WORDBREAK | DT_NOPREFIX);
      EndPaint(hwnd, &paint);
      return 0;
    }
    default:
      return DefWindowProcW(hwnd, message, w_param, l_param);
  }
}

LRESULT CALLBACK IslandWindowProc(HWND hwnd, UINT message, WPARAM w_param,
                                  LPARAM l_param) {
  auto* state = reinterpret_cast<IslandState*>(
      GetWindowLongPtrW(hwnd, GWLP_USERDATA));
  if (message == WM_NCCREATE) {
    const auto* create = reinterpret_cast<CREATESTRUCTW*>(l_param);
    state = static_cast<IslandState*>(create->lpCreateParams);
    SetWindowLongPtrW(hwnd, GWLP_USERDATA,
                      reinterpret_cast<LONG_PTR>(state));
    if (state != nullptr) {
      state->hwnd = hwnd;
    }
  }

  switch (message) {
    case WM_MOUSEACTIVATE:
      return MA_NOACTIVATE;
    case WM_LBUTTONUP:
      if (state != nullptr) {
        ++state->click_count;
        LogPointerEvent("island_click", hwnd, RoleName(state->role),
                        state->click_count);
        InvalidateRect(hwnd, nullptr, FALSE);
      }
      return 0;
    case WM_RBUTTONUP:
      if (state != nullptr) {
        LogPointerEvent("island_right_click", hwnd, RoleName(state->role),
                        state->click_count);
      }
      return 0;
    case WM_ERASEBKGND:
      return 1;
    case WM_PAINT: {
      PAINTSTRUCT paint{};
      HDC dc = BeginPaint(hwnd, &paint);
      RECT client{};
      GetClientRect(hwnd, &client);
      const IslandRole role = state != nullptr ? state->role : IslandRole::Title;
      const std::uint64_t clicks = state != nullptr ? state->click_count : 0;

      HBRUSH background = CreateSolidBrush(IslandColor(role, clicks));
      FillRect(dc, &client, background);
      DeleteObject(background);
      HPEN border = CreatePen(PS_SOLID, 2, RGB(229, 250, 252));
      HGDIOBJ old_pen = SelectObject(dc, border);
      HGDIOBJ old_brush = SelectObject(dc, GetStockObject(HOLLOW_BRUSH));
      Rectangle(dc, 1, 1, client.right - 1, client.bottom - 1);
      SelectObject(dc, old_brush);
      SelectObject(dc, old_pen);
      DeleteObject(border);

      SetBkMode(dc, TRANSPARENT);
      SetTextColor(dc, RGB(245, 252, 253));
      std::wostringstream label;
      label << IslandLabel(role) << L"\n#" << clicks;
      RECT text = client;
      text.left += 12;
      text.right -= 12;
      DrawTextW(dc, label.str().c_str(), -1, &text,
                DT_CENTER | DT_VCENTER | DT_WORDBREAK | DT_NOPREFIX);
      EndPaint(hwnd, &paint);
      return 0;
    }
    default:
      return DefWindowProcW(hwnd, message, w_param, l_param);
  }
}

bool RegisterProbeClasses(HINSTANCE instance) {
  WNDCLASSEXW visual_class{};
  visual_class.cbSize = sizeof(visual_class);
  visual_class.lpfnWndProc = VisualWindowProc;
  visual_class.hInstance = instance;
  visual_class.hCursor = LoadCursorW(nullptr, IDC_ARROW);
  visual_class.lpszClassName = kVisualClass;

  WNDCLASSEXW island_class{};
  island_class.cbSize = sizeof(island_class);
  island_class.lpfnWndProc = IslandWindowProc;
  island_class.hInstance = instance;
  island_class.hCursor = LoadCursorW(nullptr, IDC_HAND);
  island_class.lpszClassName = kIslandClass;

  return RegisterClassExW(&visual_class) != 0 &&
         RegisterClassExW(&island_class) != 0;
}

RECT DefaultHostRect(HWND parent) {
  MONITORINFO monitor_info{};
  monitor_info.cbSize = sizeof(monitor_info);
  GetMonitorInfoW(MonitorFromPoint(POINT{0, 0}, MONITOR_DEFAULTTOPRIMARY),
                  &monitor_info);

  const UINT dpi = GetDpiForWindow(parent);
  const int width = MulDiv(540, static_cast<int>(dpi), 96);
  const int height = MulDiv(420, static_cast<int>(dpi), 96);
  const RECT work = monitor_info.rcWork;
  const int screen_x = work.left + ((work.right - work.left) - width) / 2;
  const int screen_y = work.top + ((work.bottom - work.top) - height) / 2;
  POINT points[2]{POINT{screen_x, screen_y},
                  POINT{screen_x + width, screen_y + height}};
  MapWindowPoints(nullptr, parent, points, 2);
  return RECT{points[0].x, points[0].y, points[1].x, points[1].y};
}

bool IsNativeDesktopTarget(HWND hwnd, const ShellTarget& target) {
  return hwnd == target.icon_view || hwnd == target.def_view ||
         IsChild(target.def_view, hwnd) != FALSE;
}

std::string StartEventJson(const ShellTarget& target, DWORD duration_ms,
                           const std::wstring& scenario) {
  std::ostringstream stream;
  stream << "{\"schema\":\"desktopup.ts-h01-d1.event.v1\""
         << ",\"event\":\"lifecycle_test_start\""
         << ",\"build_id\":\"" << JsonEscape(Utf8(kBuildId)) << "\""
         << ",\"duration_ms\":" << duration_ms
         << ",\"scenario\":\"" << JsonEscape(Utf8(scenario)) << "\""
         << ",\"strategy\":\"transparent_visual_plus_interaction_islands\""
         << ",\"topology\":\"" << JsonEscape(Utf8(target.topology)) << "\""
         << ",\"progman\":" << HwndJson(target.progman)
         << ",\"icon_host\":" << HwndJson(target.icon_host)
         << ",\"def_view\":" << HwndJson(target.def_view)
         << ",\"icon_view\":" << HwndJson(target.icon_view)
         << ",\"explorer_pid\":" << target.explorer_pid
         << ",\"visible_def_view_count\":" << target.visible_def_views.size()
         << "}";
  return stream.str();
}

std::string EventJson(const char* event, const char* result) {
  std::ostringstream stream;
  stream << "{\"schema\":\"desktopup.ts-h01-d1.event.v1\""
         << ",\"event\":\"" << event << "\""
         << ",\"result\":\"" << result << "\"}";
  return stream.str();
}

std::string SampleJson(std::uint64_t sequence, Clock::time_point started_at,
                       const ShellTarget& target, HWND visual,
                       const std::array<IslandState, 3>& islands,
                       POINT blank_point, POINT title_point, POINT item_point,
                       POINT action_point) {
  constexpr UINT kHitFlags =
      CWP_SKIPINVISIBLE | CWP_SKIPDISABLED | CWP_SKIPTRANSPARENT;
  const HWND blank_target = ChildWindowFromPointEx(
      target.icon_host, blank_point, kHitFlags);
  const HWND title_target = ChildWindowFromPointEx(
      target.icon_host, title_point, kHitFlags);
  const HWND item_target = ChildWindowFromPointEx(
      target.icon_host, item_point, kHitFlags);
  const HWND action_target = ChildWindowFromPointEx(
      target.icon_host, action_point, kHitFlags);
  const int visual_index = ChildIndex(target.icon_host, visual);
  const int def_view_index = ChildIndex(target.icon_host, target.def_view);
  const int title_index = ChildIndex(target.icon_host, islands[0].hwnd);
  const int item_index = ChildIndex(target.icon_host, islands[1].hwnd);
  const int action_index = ChildIndex(target.icon_host, islands[2].hwnd);

  const LONG_PTR visual_style = GetWindowLongPtrW(visual, GWL_STYLE);
  const LONG_PTR visual_ex_style = GetWindowLongPtrW(visual, GWL_EXSTYLE);
  const bool visual_contract = GetParent(visual) == target.icon_host &&
      (visual_style & WS_CHILD) != 0 &&
      (visual_ex_style & WS_EX_LAYERED) != 0 &&
      (visual_ex_style & WS_EX_TRANSPARENT) != 0 &&
      (visual_ex_style & WS_EX_TOPMOST) == 0 &&
      IsWindowVisible(visual) != FALSE && !IsIconic(visual) && !IsCloaked(visual);

  bool islands_contract = true;
  for (const IslandState& island : islands) {
    const LONG_PTR style = GetWindowLongPtrW(island.hwnd, GWL_STYLE);
    const LONG_PTR ex_style = GetWindowLongPtrW(island.hwnd, GWL_EXSTYLE);
    islands_contract = islands_contract &&
        GetParent(island.hwnd) == target.icon_host &&
        (style & WS_CHILD) != 0 && (ex_style & WS_EX_LAYERED) != 0 &&
        (ex_style & WS_EX_TRANSPARENT) == 0 &&
        (ex_style & WS_EX_TOPMOST) == 0 &&
        IsWindowVisible(island.hwnd) != FALSE && !IsIconic(island.hwnd) &&
        !IsCloaked(island.hwnd);
  }

  const bool z_contract = action_index >= 0 && item_index >= 0 &&
      title_index >= 0 && visual_index >= 0 && def_view_index >= 0 &&
      action_index < visual_index && item_index < visual_index &&
      title_index < visual_index && visual_index < def_view_index;
  const bool hit_contract = IsNativeDesktopTarget(blank_target, target) &&
      title_target == islands[0].hwnd && item_target == islands[1].hwnd &&
      action_target == islands[2].hwnd;
  const bool pass = visual_contract && islands_contract && z_contract &&
      hit_contract && g_runtime != nullptr &&
      g_runtime->visual_pointer_messages == 0;
  const double elapsed_ms = std::chrono::duration<double, std::milli>(
      Clock::now() - started_at).count();

  std::ostringstream stream;
  stream << "{\"schema\":\"desktopup.ts-h01-d1.sample.v1\""
         << ",\"sequence\":" << sequence
         << ",\"elapsed_ms\":" << std::fixed << std::setprecision(3)
         << elapsed_ms
         << ",\"visual\":" << HwndJson(visual)
         << ",\"title\":" << HwndJson(islands[0].hwnd)
         << ",\"item\":" << HwndJson(islands[1].hwnd)
         << ",\"action\":" << HwndJson(islands[2].hwnd)
         << ",\"blank_target\":" << HwndJson(blank_target)
         << ",\"title_target\":" << HwndJson(title_target)
         << ",\"item_target\":" << HwndJson(item_target)
         << ",\"action_target\":" << HwndJson(action_target)
         << ",\"visual_index\":" << visual_index
         << ",\"title_index\":" << title_index
         << ",\"item_index\":" << item_index
         << ",\"action_index\":" << action_index
         << ",\"def_view_index\":" << def_view_index
         << ",\"visual_ex_style\":\"0x" << std::uppercase << std::hex
         << static_cast<std::uintptr_t>(visual_ex_style) << "\"" << std::dec
         << ",\"visual_pointer_messages\":"
         << (g_runtime != nullptr ? g_runtime->visual_pointer_messages : 0)
         << ",\"visual_state\":" << WindowStateJson(visual, target.icon_host)
         << ",\"title_state\":"
         << WindowStateJson(islands[0].hwnd, target.icon_host)
         << ",\"item_state\":"
         << WindowStateJson(islands[1].hwnd, target.icon_host)
         << ",\"action_state\":"
         << WindowStateJson(islands[2].hwnd, target.icon_host)
         << ",\"def_view_state\":"
         << WindowStateJson(target.def_view, target.icon_host)
         << ",\"attach_count\":1,\"detach_count\":0,\"recreate_count\":0"
         << ",\"hide_count\":0,\"show_count\":0"
         << ",\"visual_contract\":" << (visual_contract ? "true" : "false")
         << ",\"islands_contract\":" << (islands_contract ? "true" : "false")
         << ",\"z_contract\":" << (z_contract ? "true" : "false")
         << ",\"hit_contract\":" << (hit_contract ? "true" : "false")
         << ",\"pass\":" << (pass ? "true" : "false") << "}";
  return stream.str();
}

HWND CreateLayeredChild(DWORD extra_style, const wchar_t* class_name,
                        const wchar_t* title, const RECT& rect, HWND parent,
                        HINSTANCE instance, void* parameter) {
  HWND hwnd = CreateWindowExW(
      WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_LAYERED | extra_style,
      class_name, title, WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS,
      rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top,
      parent, nullptr, instance, parameter);
  if (hwnd == nullptr) {
    return nullptr;
  }
  if (!SetLayeredWindowAttributes(hwnd, 0, 238, LWA_ALPHA)) {
    DestroyWindow(hwnd);
    return nullptr;
  }
  return hwnd;
}

}  // namespace

int wmain(int argc, wchar_t** argv) {
  const auto options = ParseOptions(argc, argv);
  if (!options.has_value() || !options->run_lifecycle_test) {
    std::wcerr << L"Explicit --run-lifecycle-test and a valid --scenario are required.\n";
    return 2;
  }

  if (!SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)) {
    const DPI_AWARENESS_CONTEXT current_context = GetThreadDpiAwarenessContext();
    if (!AreDpiAwarenessContextsEqual(
            current_context, DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)) {
      std::wcerr << L"Failed to establish Per-Monitor v2 DPI awareness. Error="
                 << GetLastError() << L"\n";
      return 2;
    }
  }

  JsonlSink sink(options->log_path);
  if (!sink.IsValid(options->log_path)) {
    std::wcerr << L"Failed to open log path.\n";
    return 2;
  }

  Runtime runtime{&sink, Clock::now(), 0};
  g_runtime = &runtime;
  const ShellTarget target = DiscoverShellTarget();
  sink.Write(StartEventJson(target, options->duration_ms, options->scenario));
  if (target.topology != L"RAISED_PROGMAN" || target.icon_host == nullptr ||
      target.def_view == nullptr || target.icon_view == nullptr ||
      target.visible_def_views.size() != 1) {
    sink.Write(EventJson("degraded_no_desktop_host",
                         "requires_unambiguous_raised_progman"));
    g_runtime = nullptr;
    return 3;
  }

  HINSTANCE instance = GetModuleHandleW(nullptr);
  if (!RegisterProbeClasses(instance)) {
    sink.Write(EventJson("window_class_failed", "registration_failed"));
    g_runtime = nullptr;
    return 4;
  }

  const RECT visual_rect = DefaultHostRect(target.icon_host);
  const int left = visual_rect.left;
  const int top = visual_rect.top;
  const int width = visual_rect.right - visual_rect.left;
  const int height = visual_rect.bottom - visual_rect.top;
  const UINT dpi = GetDpiForWindow(target.icon_host);
  const auto px = [dpi](int value) { return MulDiv(value, static_cast<int>(dpi), 96); };

  HWND visual = CreateLayeredChild(
      WS_EX_TRANSPARENT, kVisualClass, L"TS-H01-D1 Transparent Visual",
      visual_rect, target.icon_host, instance, nullptr);

  std::array<IslandState, 3> islands{{
      {IslandRole::Title, nullptr, 0},
      {IslandRole::Item, nullptr, 0},
      {IslandRole::Action, nullptr, 0},
  }};
  const std::array<RECT, 3> island_rects{{
      RECT{left + px(24), top + px(18), left + px(382), top + px(66)},
      RECT{left + px(28), top + px(112), left + px(228), top + px(214)},
      RECT{left + px(430), top + px(18), left + px(510), top + px(66)},
  }};

  for (std::size_t index = 0; index < islands.size(); ++index) {
    islands[index].hwnd = CreateLayeredChild(
        0, kIslandClass, L"TS-H01-D1 Interaction Island", island_rects[index],
        target.icon_host, instance, &islands[index]);
  }

  const bool all_created = visual != nullptr &&
      std::all_of(islands.begin(), islands.end(),
                  [](const IslandState& island) { return island.hwnd != nullptr; });
  if (!all_created) {
    for (auto it = islands.rbegin(); it != islands.rend(); ++it) {
      if (it->hwnd != nullptr) DestroyWindow(it->hwnd);
    }
    if (visual != nullptr) DestroyWindow(visual);
    UnregisterClassW(kIslandClass, instance);
    UnregisterClassW(kVisualClass, instance);
    sink.Write(EventJson("window_create_failed", "layered_child_failed"));
    g_runtime = nullptr;
    return 4;
  }

  SetWindowPos(visual, HWND_TOP, left, top, width, height,
               SWP_NOACTIVATE | SWP_SHOWWINDOW);
  for (IslandState& island : islands) {
    SetWindowPos(island.hwnd, HWND_TOP, 0, 0, 0, 0,
                 SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    UpdateWindow(island.hwnd);
  }
  UpdateWindow(visual);
  sink.Write(EventJson("lifecycle_surface_created",
                       "fixed_attachment_no_repair_path"));

  const POINT blank_point{left + px(390), top + px(280)};
  const POINT title_point{left + px(180), top + px(42)};
  const POINT item_point{left + px(128), top + px(160)};
  const POINT action_point{left + px(470), top + px(42)};

  const auto started_at = Clock::now();
  runtime.started_at = started_at;
  const auto deadline = started_at + std::chrono::milliseconds(options->duration_ms);
  std::uint64_t sequence = 0;
  bool all_samples_passed = true;
  HANDLE sample_timer = CreateWaitableTimerExW(
      nullptr, nullptr, CREATE_WAITABLE_TIMER_HIGH_RESOLUTION,
      TIMER_ALL_ACCESS);
  LARGE_INTEGER first_due{};
  first_due.QuadPart = -10000;
  const bool timer_ready = sample_timer != nullptr &&
      SetWaitableTimerEx(sample_timer, &first_due,
                         static_cast<LONG>(kSampleIntervalMs), nullptr,
                         nullptr, nullptr, 0) != FALSE;
  if (!timer_ready) {
    all_samples_passed = false;
    sink.Write(EventJson("high_resolution_timer_failed",
                         "CreateWaitableTimerEx_or_SetWaitableTimerEx_failed"));
  }

  while (timer_ready && Clock::now() < deadline && IsWindow(visual)) {
    const DWORD wait_result = MsgWaitForMultipleObjectsEx(
        1, &sample_timer, 100, QS_ALLINPUT, MWMO_INPUTAVAILABLE);
    MSG message{};
    while (PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE)) {
      TranslateMessage(&message);
      DispatchMessageW(&message);
    }

    if (wait_result == WAIT_OBJECT_0) {
      const std::string sample = SampleJson(
          sequence++, started_at, target, visual, islands, blank_point,
          title_point, item_point, action_point);
      sink.Write(sample);
      all_samples_passed = all_samples_passed &&
          sample.find("\"pass\":true") != std::string::npos;
    } else if (wait_result == WAIT_FAILED) {
      all_samples_passed = false;
      sink.Write(EventJson("high_resolution_timer_failed",
                           "MsgWaitForMultipleObjectsEx_failed"));
      break;
    }
  }

  if (sample_timer != nullptr) {
    CancelWaitableTimer(sample_timer);
    CloseHandle(sample_timer);
  }

  bool destroyed = true;
  for (auto it = islands.rbegin(); it != islands.rend(); ++it) {
    destroyed = (DestroyWindow(it->hwnd) != FALSE) && destroyed;
  }
  destroyed = (DestroyWindow(visual) != FALSE) && destroyed;
  UnregisterClassW(kIslandClass, instance);
  UnregisterClassW(kVisualClass, instance);

  const std::uint64_t visual_pointer_messages = runtime.visual_pointer_messages;
  g_runtime = nullptr;
  if (!all_samples_passed || sequence == 0 || !destroyed ||
      visual_pointer_messages != 0) {
    sink.Write(EventJson("lifecycle_test_complete", "continuity_telemetry_fail"));
    return 4;
  }

  sink.Write(EventJson(
      "lifecycle_test_complete",
      "continuity_telemetry_pass_manual_cycles_pending"));
  return 0;
}
