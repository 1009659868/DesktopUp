#include <windows.h>
#include <dwmapi.h>

#include <algorithm>
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

constexpr wchar_t kProbeClass[] = L"DesktopUp.TS-H01-B.HostLayerProbe";
constexpr wchar_t kBuildId[] = L"TS-H01-B-20260820-02-LAYERED";
constexpr DWORD kDefaultDurationMs = 4000;
constexpr DWORD kMaximumDurationMs = 60000;
constexpr DWORD kSampleIntervalMs = 50;

using Clock = std::chrono::steady_clock;

struct Options {
  bool run_host_test = false;
  DWORD duration_ms = kDefaultDurationMs;
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

struct Sample {
  std::uint64_t sequence = 0;
  double elapsed_ms = 0.0;
  HWND host = nullptr;
  HWND parent = nullptr;
  HWND owner = nullptr;
  HWND z_above = nullptr;
  HWND z_below = nullptr;
  HWND def_view_z_above = nullptr;
  LONG_PTR style = 0;
  LONG_PTR ex_style = 0;
  bool visible = false;
  bool iconic = false;
  bool cloaked = false;
  bool host_above_def_view = false;
  int host_child_index = -1;
  int def_view_child_index = -1;
  int visible_top_level_windows_above_desktop = 0;
  RECT window_rect{};
  RECT client_rect{};
  RECT extended_frame_bounds{};
  UINT dpi = 0;
  bool pass = false;
};

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

std::string RectJson(const RECT& rect) {
  std::ostringstream stream;
  stream << "{\"left\":" << rect.left << ",\"top\":" << rect.top
         << ",\"right\":" << rect.right << ",\"bottom\":" << rect.bottom
         << ",\"width\":" << rect.right - rect.left
         << ",\"height\":" << rect.bottom - rect.top << "}";
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

RECT ExtendedFrameBounds(HWND hwnd) {
  RECT rect{};
  if (FAILED(DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, &rect,
                                   sizeof(rect)))) {
    GetWindowRect(hwnd, &rect);
  }
  return rect;
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

int CountVisibleTopLevelWindowsAbove(HWND desktop_root) {
  int count = 0;
  for (HWND current = GetTopWindow(nullptr); current != nullptr;
       current = GetWindow(current, GW_HWNDNEXT)) {
    if (current == desktop_root) {
      break;
    }
    if (!IsWindowVisible(current) || IsIconic(current) || IsCloaked(current)) {
      continue;
    }
    const std::wstring class_name = ClassName(current);
    if (class_name == L"Shell_TrayWnd" || class_name == L"Shell_SecondaryTrayWnd" ||
        class_name == L"Progman" || class_name == L"WorkerW") {
      continue;
    }
    ++count;
  }
  return count;
}

std::optional<Options> ParseOptions(int argc, wchar_t** argv) {
  Options options;
  for (int index = 1; index < argc; ++index) {
    const std::wstring argument = argv[index];
    if (argument == L"--run-host-test") {
      options.run_host_test = true;
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
  return options;
}

class JsonlSink {
 public:
  explicit JsonlSink(const std::optional<std::filesystem::path>& path) {
    if (path.has_value()) {
      file_.open(*path, std::ios::out | std::ios::trunc | std::ios::binary);
    }
  }

  bool IsValid(const std::optional<std::filesystem::path>& path) const {
    return !path.has_value() || file_.is_open();
  }

  void Write(const std::string& line) {
    std::cout << line << '\n';
    if (file_.is_open()) {
      file_ << line << '\n';
      file_.flush();
    }
  }

 private:
  std::ofstream file_;
};

std::string StartEventJson(const ShellTarget& target, DWORD duration_ms) {
  std::ostringstream stream;
  stream << "{\"schema\":\"desktopup.ts-h01-b.event.v1\""
         << ",\"event\":\"host_test_start\""
         << ",\"build_id\":\"" << JsonEscape(Utf8(kBuildId)) << "\""
         << ",\"duration_ms\":" << duration_ms
         << ",\"surface_mode\":\"layered_gdi_child\""
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

std::string EventJson(const char* event, HWND host, const char* result) {
  std::ostringstream stream;
  stream << "{\"schema\":\"desktopup.ts-h01-b.event.v1\""
         << ",\"event\":\"" << event << "\""
         << ",\"host\":" << HwndJson(host)
         << ",\"result\":\"" << result << "\"}";
  return stream.str();
}

std::string SampleJson(const Sample& sample) {
  std::ostringstream stream;
  stream << "{\"schema\":\"desktopup.ts-h01-b.sample.v1\""
         << ",\"sequence\":" << sample.sequence
         << ",\"elapsed_ms\":" << std::fixed << std::setprecision(3)
         << sample.elapsed_ms
         << ",\"host\":" << HwndJson(sample.host)
         << ",\"parent\":" << HwndJson(sample.parent)
         << ",\"owner\":" << HwndJson(sample.owner)
         << ",\"z_above\":" << HwndJson(sample.z_above)
         << ",\"z_below\":" << HwndJson(sample.z_below)
         << ",\"def_view_z_above\":" << HwndJson(sample.def_view_z_above)
         << ",\"style\":\"0x" << std::uppercase << std::hex
         << static_cast<std::uintptr_t>(sample.style) << "\""
         << ",\"ex_style\":\"0x" << static_cast<std::uintptr_t>(sample.ex_style)
         << "\"" << std::dec
         << ",\"visible\":" << (sample.visible ? "true" : "false")
         << ",\"iconic\":" << (sample.iconic ? "true" : "false")
         << ",\"cloaked\":" << (sample.cloaked ? "true" : "false")
         << ",\"host_child_index\":" << sample.host_child_index
         << ",\"def_view_child_index\":" << sample.def_view_child_index
         << ",\"host_above_def_view\":"
         << (sample.host_above_def_view ? "true" : "false")
         << ",\"visible_top_level_windows_above_desktop\":"
         << sample.visible_top_level_windows_above_desktop
         << ",\"window_rect\":" << RectJson(sample.window_rect)
         << ",\"client_rect\":" << RectJson(sample.client_rect)
         << ",\"extended_frame_bounds\":"
         << RectJson(sample.extended_frame_bounds)
         << ",\"dpi\":" << sample.dpi
         << ",\"pass\":" << (sample.pass ? "true" : "false") << "}";
  return stream.str();
}

LRESULT CALLBACK ProbeWindowProc(HWND hwnd, UINT message, WPARAM w_param,
                                 LPARAM l_param) {
  switch (message) {
    case WM_MOUSEACTIVATE:
      return MA_NOACTIVATE;
    case WM_ERASEBKGND:
      return 1;
    case WM_PAINT: {
      PAINTSTRUCT paint{};
      HDC dc = BeginPaint(hwnd, &paint);
      RECT client{};
      GetClientRect(hwnd, &client);

      HBRUSH background = CreateSolidBrush(RGB(7, 25, 34));
      FillRect(dc, &client, background);
      DeleteObject(background);

      HPEN border = CreatePen(PS_SOLID, 3, RGB(78, 224, 235));
      HGDIOBJ old_pen = SelectObject(dc, border);
      HGDIOBJ old_brush = SelectObject(dc, GetStockObject(HOLLOW_BRUSH));
      Rectangle(dc, 1, 1, client.right - 1, client.bottom - 1);
      SelectObject(dc, old_brush);
      SelectObject(dc, old_pen);
      DeleteObject(border);

      SetBkMode(dc, TRANSPARENT);
      SetTextColor(dc, RGB(229, 250, 252));
      RECT text_rect = client;
      text_rect.left += 24;
      text_rect.top += 22;
      DrawTextW(dc,
                L"DESKTOP-UP  TS-H01-B\nNATIVE HOST LAYER PROBE\n"
                L"STRUCTURE ONLY — AUTO CLOSE",
                -1, &text_rect, DT_LEFT | DT_TOP | DT_NOPREFIX);
      EndPaint(hwnd, &paint);
      return 0;
    }
    case WM_DESTROY:
      return 0;
    default:
      return DefWindowProcW(hwnd, message, w_param, l_param);
  }
}

bool RegisterProbeClass(HINSTANCE instance) {
  WNDCLASSEXW window_class{};
  window_class.cbSize = sizeof(window_class);
  window_class.lpfnWndProc = ProbeWindowProc;
  window_class.hInstance = instance;
  window_class.hCursor = LoadCursorW(nullptr, IDC_ARROW);
  window_class.lpszClassName = kProbeClass;
  return RegisterClassExW(&window_class) != 0;
}

RECT DefaultHostRect(HWND parent) {
  POINT points[2]{};
  MONITORINFO monitor_info{};
  monitor_info.cbSize = sizeof(monitor_info);
  GetMonitorInfoW(MonitorFromPoint(POINT{0, 0}, MONITOR_DEFAULTTOPRIMARY),
                  &monitor_info);

  const UINT dpi = GetDpiForWindow(parent);
  const int width = MulDiv(360, static_cast<int>(dpi), 96);
  const int height = MulDiv(220, static_cast<int>(dpi), 96);
  const RECT work = monitor_info.rcWork;
  const int screen_x = work.left + ((work.right - work.left) - width) / 2;
  const int screen_y = work.top + ((work.bottom - work.top) - height) / 2;

  points[0] = POINT{screen_x, screen_y};
  points[1] = POINT{screen_x + width, screen_y + height};
  MapWindowPoints(nullptr, parent, points, 2);
  return RECT{points[0].x, points[0].y, points[1].x, points[1].y};
}

Sample CaptureSample(HWND host, const ShellTarget& target,
                     std::uint64_t sequence, Clock::time_point started_at) {
  Sample sample;
  sample.sequence = sequence;
  sample.elapsed_ms =
      std::chrono::duration<double, std::milli>(Clock::now() - started_at).count();
  sample.host = host;
  sample.parent = GetParent(host);
  sample.owner = GetWindow(host, GW_OWNER);
  sample.z_above = GetWindow(host, GW_HWNDPREV);
  sample.z_below = GetWindow(host, GW_HWNDNEXT);
  sample.def_view_z_above = GetWindow(target.def_view, GW_HWNDPREV);
  sample.style = GetWindowLongPtrW(host, GWL_STYLE);
  sample.ex_style = GetWindowLongPtrW(host, GWL_EXSTYLE);
  sample.visible = IsWindowVisible(host) != FALSE;
  sample.iconic = IsIconic(host) != FALSE;
  sample.cloaked = IsCloaked(host);
  sample.host_child_index = ChildIndex(target.icon_host, host);
  sample.def_view_child_index = ChildIndex(target.icon_host, target.def_view);
  sample.host_above_def_view = sample.host_child_index >= 0 &&
                               sample.def_view_child_index >= 0 &&
                               sample.host_child_index < sample.def_view_child_index;
  sample.visible_top_level_windows_above_desktop =
      CountVisibleTopLevelWindowsAbove(target.icon_host);
  GetWindowRect(host, &sample.window_rect);
  GetClientRect(host, &sample.client_rect);
  sample.extended_frame_bounds = ExtendedFrameBounds(host);
  sample.dpi = GetDpiForWindow(host);

  const bool is_child = (sample.style & WS_CHILD) != 0;
  const bool has_caption = (sample.style & WS_CAPTION) != 0;
  const bool is_topmost = (sample.ex_style & WS_EX_TOPMOST) != 0;
  const bool is_layered = (sample.ex_style & WS_EX_LAYERED) != 0;
  sample.pass = sample.parent == target.icon_host && sample.owner == nullptr &&
                is_child && !has_caption && !is_topmost && is_layered &&
                sample.visible &&
                !sample.iconic && !sample.cloaked && sample.host_above_def_view &&
                sample.host_child_index == 0;
  return sample;
}

}  // namespace

int wmain(int argc, wchar_t** argv) {
  const auto options = ParseOptions(argc, argv);
  if (!options.has_value() || !options->run_host_test) {
    std::wcerr << L"Explicit --run-host-test is required.\n";
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

  const ShellTarget target = DiscoverShellTarget();
  sink.Write(StartEventJson(target, options->duration_ms));

  const bool supported_topology = target.topology == L"RAISED_PROGMAN" ||
                                  target.topology == L"TRADITIONAL_WORKERW";
  if (!supported_topology || target.icon_host == nullptr ||
      target.def_view == nullptr || target.icon_view == nullptr ||
      target.visible_def_views.size() != 1) {
    sink.Write(EventJson("degraded_no_desktop_host", nullptr,
                         "ambiguous_or_unsupported_topology"));
    return 3;
  }

  HINSTANCE instance = GetModuleHandleW(nullptr);
  if (!RegisterProbeClass(instance)) {
    sink.Write(EventJson("host_create_failed", nullptr,
                         "window_class_registration_failed"));
    return 4;
  }

  const RECT host_rect = DefaultHostRect(target.icon_host);
  const int width = host_rect.right - host_rect.left;
  const int height = host_rect.bottom - host_rect.top;
  HWND host = CreateWindowExW(
      WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_LAYERED, kProbeClass,
      L"Desktop-Up TS-H01-B Host Layer Probe",
      WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS,
      host_rect.left, host_rect.top, width, height, target.icon_host, nullptr,
      instance, nullptr);

  if (host == nullptr) {
    sink.Write(EventJson("host_create_failed", nullptr, "CreateWindowExW_failed"));
    return 4;
  }
  if (!SetLayeredWindowAttributes(host, 0, 255, LWA_ALPHA)) {
    sink.Write(EventJson("host_layer_failed", host,
                         "SetLayeredWindowAttributes_failed"));
    DestroyWindow(host);
    return 4;
  }


  if (!SetWindowPos(host, HWND_TOP, host_rect.left, host_rect.top, width, height,
                    SWP_NOACTIVATE | SWP_SHOWWINDOW)) {
    sink.Write(EventJson("host_position_failed", host, "SetWindowPos_failed"));
    DestroyWindow(host);
    return 4;
  }

  UpdateWindow(host);
  sink.Write(EventJson("host_created", host, "created_as_desktop_child"));

  const auto started_at = Clock::now();
  const auto deadline = started_at + std::chrono::milliseconds(options->duration_ms);
  auto next_sample_at = started_at;
  std::uint64_t sequence = 0;
  bool all_samples_passed = true;

  while (Clock::now() < deadline && IsWindow(host)) {
    MSG message{};
    while (PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE)) {
      TranslateMessage(&message);
      DispatchMessageW(&message);
    }

    const auto now = Clock::now();
    if (now >= next_sample_at) {
      const Sample sample = CaptureSample(host, target, sequence++, started_at);
      sink.Write(SampleJson(sample));
      all_samples_passed = all_samples_passed && sample.pass;
      next_sample_at += std::chrono::milliseconds(kSampleIntervalMs);
    }
    std::this_thread::sleep_for(std::chrono::milliseconds(5));
  }

  const bool destroyed = DestroyWindow(host) != FALSE;
  sink.Write(EventJson("host_destroyed", host,
                       destroyed ? "clean" : "DestroyWindow_failed"));
  UnregisterClassW(kProbeClass, instance);

  if (!all_samples_passed || sequence == 0 || !destroyed) {
    sink.Write(EventJson("host_test_complete", nullptr, "structural_fail"));
    return 4;
  }

  sink.Write(EventJson("host_test_complete", nullptr,
                       "automated_structure_pass_manual_visual_pending"));
  return 0;
}
