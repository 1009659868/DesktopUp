#include <windows.h>
#include <dwmapi.h>
#include <winternl.h>

#include <atomic>
#include <chrono>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <optional>
#include <sstream>
#include <string>
#include <string_view>
#include <vector>

#ifndef TS_H01A_BUILD_ID
#define TS_H01A_BUILD_ID "dev"
#endif

namespace {

std::atomic_bool g_running{true};

struct Options {
  std::uint32_t interval_ms = 0;
  std::uint64_t count = 1;
  std::optional<std::filesystem::path> log_path;
  bool show_help = false;
};

struct OsVersion {
  DWORD major = 0;
  DWORD minor = 0;
  DWORD build = 0;
};

struct WindowRecord {
  HWND hwnd = nullptr;
  HWND parent = nullptr;
  HWND owner = nullptr;
  HWND above = nullptr;
  HWND below = nullptr;
  DWORD pid = 0;
  DWORD tid = 0;
  LONG_PTR style = 0;
  LONG_PTR ex_style = 0;
  UINT dpi = 0;
  bool visible = false;
  bool iconic = false;
  bool enabled = false;
  bool active = false;
  bool topmost = false;
  bool cloak_known = false;
  DWORD cloak = 0;
  bool window_rect_known = false;
  bool client_rect_known = false;
  bool frame_rect_known = false;
  RECT window_rect{};
  RECT client_rect{};
  RECT frame_rect{};
  std::wstring class_name;
  std::wstring title;
  std::wstring process_image;
};

struct Topology {
  HWND progman = nullptr;
  HWND taskbar = nullptr;
  HWND icon_host = nullptr;
  DWORD explorer_pid = 0;
  std::vector<HWND> workerws;
  std::vector<HWND> defviews;
  std::vector<HWND> icon_views;
  std::wstring kind = L"UNKNOWN_NO_DEFVIEW";
};

std::string Utf8(std::wstring_view value) {
  if (value.empty()) return {};
  const int required = WideCharToMultiByte(
      CP_UTF8, WC_ERR_INVALID_CHARS, value.data(),
      static_cast<int>(value.size()), nullptr, 0, nullptr, nullptr);
  if (required <= 0) return {};
  std::string result(static_cast<std::size_t>(required), '\0');
  WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, value.data(),
                      static_cast<int>(value.size()), result.data(), required,
                      nullptr, nullptr);
  return result;
}

std::string Escape(std::string_view value) {
  std::ostringstream out;
  for (const unsigned char ch : value) {
    switch (ch) {
      case '"': out << "\\\""; break;
      case '\\': out << "\\\\"; break;
      case '\b': out << "\\b"; break;
      case '\f': out << "\\f"; break;
      case '\n': out << "\\n"; break;
      case '\r': out << "\\r"; break;
      case '\t': out << "\\t"; break;
      default:
        if (ch < 0x20) {
          out << "\\u" << std::hex << std::setw(4) << std::setfill('0')
              << static_cast<unsigned int>(ch) << std::dec;
        } else {
          out << static_cast<char>(ch);
        }
    }
  }
  return out.str();
}

std::string J(std::string_view value) { return "\"" + Escape(value) + "\""; }
std::string J(std::wstring_view value) { return J(Utf8(value)); }

std::string H(HWND hwnd) {
  std::ostringstream out;
  out << "0x" << std::hex << std::uppercase
      << reinterpret_cast<std::uintptr_t>(hwnd);
  return out.str();
}

std::string Hex(ULONG_PTR value) {
  std::ostringstream out;
  out << "0x" << std::hex << std::uppercase << value;
  return out.str();
}

std::wstring ClassOf(HWND hwnd) {
  wchar_t buffer[256]{};
  const int size = GetClassNameW(hwnd, buffer, static_cast<int>(_countof(buffer)));
  return size > 0 ? std::wstring(buffer, static_cast<std::size_t>(size)) : L"";
}

std::wstring TitleOf(HWND hwnd) {
  const int reported = GetWindowTextLengthW(hwnd);
  const int capacity = (reported > 0 && reported < 4096) ? reported + 1 : 512;
  std::wstring value(static_cast<std::size_t>(capacity), L'\0');
  const int size = GetWindowTextW(hwnd, value.data(), capacity);
  if (size <= 0) return L"";
  value.resize(static_cast<std::size_t>(size));
  return value;
}

std::wstring ImageOf(DWORD pid) {
  HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
  if (!process) return L"";
  std::wstring value(32768, L'\0');
  DWORD size = static_cast<DWORD>(value.size());
  const BOOL ok = QueryFullProcessImageNameW(process, 0, value.data(), &size);
  CloseHandle(process);
  if (!ok) return L"";
  value.resize(size);
  return value;
}

BOOL CALLBACK Collect(HWND hwnd, LPARAM data) {
  reinterpret_cast<std::vector<HWND>*>(data)->push_back(hwnd);
  return TRUE;
}

std::vector<HWND> TopLevels() {
  std::vector<HWND> result;
  EnumWindows(Collect, reinterpret_cast<LPARAM>(&result));
  return result;
}

std::vector<HWND> Descendants(HWND root) {
  std::vector<HWND> result;
  if (root) EnumChildWindows(root, Collect, reinterpret_cast<LPARAM>(&result));
  return result;
}

void AddUnique(std::vector<HWND>& values, HWND hwnd) {
  if (!hwnd) return;
  for (const HWND value : values) {
    if (value == hwnd) return;
  }
  values.push_back(hwnd);
}

std::vector<HWND> WithClass(const std::vector<HWND>& windows,
                            std::wstring_view wanted) {
  std::vector<HWND> result;
  for (const HWND hwnd : windows) {
    if (ClassOf(hwnd) == wanted) result.push_back(hwnd);
  }
  return result;
}

Topology Discover(const std::vector<HWND>& top_levels) {
  Topology result;
  result.progman = FindWindowW(L"Progman", nullptr);
  result.taskbar = FindWindowW(L"Shell_TrayWnd", nullptr);
  if (result.taskbar) GetWindowThreadProcessId(result.taskbar, &result.explorer_pid);
  result.workerws = WithClass(top_levels, L"WorkerW");

  std::vector<HWND> roots = result.workerws;
  AddUnique(roots, result.progman);
  for (const HWND root : roots) {
    for (const HWND child : Descendants(root)) {
      const std::wstring name = ClassOf(child);
      if (name == L"SHELLDLL_DefView") AddUnique(result.defviews, child);
      if (name == L"SysListView32" || name == L"FolderView") {
        AddUnique(result.icon_views, child);
      }
    }
  }

  if (result.defviews.size() > 1) {
    result.kind = L"UNKNOWN_MULTIPLE_DEFVIEW";
  } else if (result.defviews.size() == 1) {
    result.icon_host = GetParent(result.defviews.front());
    if (result.icon_host == result.progman) {
      result.kind = L"RAISED_PROGMAN";
    } else if (ClassOf(result.icon_host) == L"WorkerW") {
      result.kind = L"TRADITIONAL_WORKERW";
    } else {
      result.kind = L"UNKNOWN_DEFVIEW_PARENT";
    }
  }
  return result;
}

bool ClientRectOnScreen(HWND hwnd, RECT* out) {
  RECT rect{};
  if (!GetClientRect(hwnd, &rect)) return false;
  POINT points[2]{{rect.left, rect.top}, {rect.right, rect.bottom}};
  SetLastError(ERROR_SUCCESS);
  const int mapped = MapWindowPoints(hwnd, nullptr, points, 2);
  if (mapped == 0 && GetLastError() != ERROR_SUCCESS) return false;
  *out = RECT{points[0].x, points[0].y, points[1].x, points[1].y};
  return true;
}

WindowRecord Inspect(HWND hwnd) {
  WindowRecord r;
  r.hwnd = hwnd;
  r.parent = GetParent(hwnd);
  r.owner = GetWindow(hwnd, GW_OWNER);
  r.above = GetWindow(hwnd, GW_HWNDPREV);
  r.below = GetWindow(hwnd, GW_HWNDNEXT);
  r.tid = GetWindowThreadProcessId(hwnd, &r.pid);
  r.style = GetWindowLongPtrW(hwnd, GWL_STYLE);
  r.ex_style = GetWindowLongPtrW(hwnd, GWL_EXSTYLE);
  r.dpi = GetDpiForWindow(hwnd);
  r.visible = IsWindowVisible(hwnd) != FALSE;
  r.iconic = IsIconic(hwnd) != FALSE;
  r.enabled = IsWindowEnabled(hwnd) != FALSE;
  r.active = GetForegroundWindow() == hwnd;
  r.topmost = (r.ex_style & WS_EX_TOPMOST) != 0;
  r.class_name = ClassOf(hwnd);
  r.title = TitleOf(hwnd);
  r.process_image = ImageOf(r.pid);
  r.window_rect_known = GetWindowRect(hwnd, &r.window_rect) != FALSE;
  r.client_rect_known = ClientRectOnScreen(hwnd, &r.client_rect);
  r.cloak_known = SUCCEEDED(DwmGetWindowAttribute(
      hwnd, DWMWA_CLOAKED, &r.cloak, sizeof(r.cloak)));
  r.frame_rect_known = SUCCEEDED(DwmGetWindowAttribute(
      hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, &r.frame_rect, sizeof(r.frame_rect)));
  return r;
}

void WriteRect(std::ostream& out, bool known, const RECT& r) {
  if (!known) { out << "null"; return; }
  out << "{\"left\":" << r.left << ",\"top\":" << r.top
      << ",\"right\":" << r.right << ",\"bottom\":" << r.bottom
      << ",\"width\":" << r.right - r.left
      << ",\"height\":" << r.bottom - r.top << '}';
}

void WriteWindow(std::ostream& out, const WindowRecord& r,
                 std::optional<std::size_t> z_index = std::nullopt) {
  out << "{\"hwnd\":" << J(H(r.hwnd))
      << ",\"class\":" << J(r.class_name)
      << ",\"title\":" << J(r.title)
      << ",\"pid\":" << r.pid << ",\"tid\":" << r.tid
      << ",\"process_image\":" << J(r.process_image)
      << ",\"parent\":" << J(H(r.parent))
      << ",\"owner\":" << J(H(r.owner))
      << ",\"z_above\":" << J(H(r.above))
      << ",\"z_below\":" << J(H(r.below));
  if (z_index) out << ",\"top_level_z_index\":" << *z_index;
  out << ",\"style\":" << J(Hex(static_cast<ULONG_PTR>(r.style)))
      << ",\"ex_style\":" << J(Hex(static_cast<ULONG_PTR>(r.ex_style)))
      << ",\"visible\":" << (r.visible ? "true" : "false")
      << ",\"iconic\":" << (r.iconic ? "true" : "false")
      << ",\"enabled\":" << (r.enabled ? "true" : "false")
      << ",\"active\":" << (r.active ? "true" : "false")
      << ",\"topmost\":" << (r.topmost ? "true" : "false")
      << ",\"cloaked\":";
  if (r.cloak_known) out << r.cloak; else out << "null";
  out << ",\"dpi\":" << r.dpi << ",\"window_rect\":";
  WriteRect(out, r.window_rect_known, r.window_rect);
  out << ",\"client_rect_screen\":";
  WriteRect(out, r.client_rect_known, r.client_rect);
  out << ",\"extended_frame_bounds\":";
  WriteRect(out, r.frame_rect_known, r.frame_rect);
  out << '}';
}

void WriteHandles(std::ostream& out, const std::vector<HWND>& values) {
  out << '[';
  for (std::size_t i = 0; i < values.size(); ++i) {
    if (i) out << ',';
    out << J(H(values[i]));
  }
  out << ']';
}

OsVersion GetOsVersion() {
  using Fn = LONG(WINAPI*)(PRTL_OSVERSIONINFOW);
  OsVersion result;
  const HMODULE module = GetModuleHandleW(L"ntdll.dll");
  const auto fn = module ? reinterpret_cast<Fn>(GetProcAddress(module, "RtlGetVersion")) : nullptr;
  if (!fn) return result;
  RTL_OSVERSIONINFOW info{};
  info.dwOSVersionInfoSize = sizeof(info);
  if (fn(&info) == 0) {
    result = {info.dwMajorVersion, info.dwMinorVersion, info.dwBuildNumber};
  }
  return result;
}

std::string UtcNow() {
  SYSTEMTIME t{};
  GetSystemTime(&t);
  std::ostringstream out;
  out << std::setfill('0') << std::setw(4) << t.wYear << '-'
      << std::setw(2) << t.wMonth << '-' << std::setw(2) << t.wDay << 'T'
      << std::setw(2) << t.wHour << ':' << std::setw(2) << t.wMinute << ':'
      << std::setw(2) << t.wSecond << '.' << std::setw(3) << t.wMilliseconds << 'Z';
  return out.str();
}

std::uint64_t MonotonicUs() {
  LARGE_INTEGER counter{}, frequency{};
  QueryPerformanceCounter(&counter);
  QueryPerformanceFrequency(&frequency);
  if (!frequency.QuadPart) return 0;
  return static_cast<std::uint64_t>(
      static_cast<long double>(counter.QuadPart) * 1000000.0L /
      static_cast<long double>(frequency.QuadPart));
}

std::string Awareness() {
  switch (GetAwarenessFromDpiAwarenessContext(GetThreadDpiAwarenessContext())) {
    case DPI_AWARENESS_UNAWARE: return "UNAWARE";
    case DPI_AWARENESS_SYSTEM_AWARE: return "SYSTEM_AWARE";
    case DPI_AWARENESS_PER_MONITOR_AWARE: return "PER_MONITOR_AWARE";
    default: return "INVALID";
  }
}

std::vector<HWND> Related(const Topology& t) {
  std::vector<HWND> result;
  AddUnique(result, t.progman);
  AddUnique(result, t.taskbar);
  for (const HWND h : t.workerws) AddUnique(result, h);
  for (const HWND h : t.defviews) AddUnique(result, h);
  for (const HWND h : t.icon_views) AddUnique(result, h);
  for (const HWND root : t.workerws) {
    for (const HWND h : Descendants(root)) AddUnique(result, h);
  }
  for (const HWND h : Descendants(t.progman)) AddUnique(result, h);
  return result;
}

void Snapshot(std::ostream& out, std::uint64_t sequence,
              bool dpi_set, DWORD dpi_error) {
  const auto top_levels = TopLevels();
  const Topology topology = Discover(top_levels);
  const auto related = Related(topology);
  const OsVersion os = GetOsVersion();

  out << "{\"schema\":\"desktopup.ts-h01-a.snapshot.v1\""
      << ",\"sequence\":" << sequence
      << ",\"utc\":" << J(UtcNow())
      << ",\"monotonic_us\":" << MonotonicUs()
      << ",\"probe_build\":" << J(TS_H01A_BUILD_ID)
      << ",\"os\":{\"major\":" << os.major << ",\"minor\":" << os.minor
      << ",\"build\":" << os.build << "}"
      << ",\"process_dpi\":{\"set_pm_v2\":" << (dpi_set ? "true" : "false")
      << ",\"set_error\":" << dpi_error << ",\"awareness\":" << J(Awareness()) << "}"
      << ",\"explorer\":{\"taskbar\":" << J(H(topology.taskbar))
      << ",\"pid\":" << topology.explorer_pid << ",\"generation\":"
      << J(std::to_string(topology.explorer_pid) + ":" + H(topology.taskbar)) << "}"
      << ",\"topology\":{\"kind\":" << J(topology.kind)
      << ",\"progman\":" << J(H(topology.progman))
      << ",\"icon_host\":" << J(H(topology.icon_host))
      << ",\"workerws\":";
  WriteHandles(out, topology.workerws);
  out << ",\"defviews\":"; WriteHandles(out, topology.defviews);
  out << ",\"icon_views\":"; WriteHandles(out, topology.icon_views);
  out << "},\"desktop_windows\":[";
  for (std::size_t i = 0; i < related.size(); ++i) {
    if (i) out << ',';
    WriteWindow(out, Inspect(related[i]));
  }
  out << "],\"top_level_windows\":[";
  for (std::size_t i = 0; i < top_levels.size(); ++i) {
    if (i) out << ',';
    WriteWindow(out, Inspect(top_levels[i]), i);
  }
  out << "]}\n";
  out.flush();
}

bool ParseNumber(std::wstring_view text, std::uint64_t* out) {
  if (text.empty()) return false;
  std::uint64_t result = 0;
  for (const wchar_t ch : text) {
    if (ch < L'0' || ch > L'9') return false;
    const auto digit = static_cast<std::uint64_t>(ch - L'0');
    if (result > (UINT64_MAX - digit) / 10) return false;
    result = result * 10 + digit;
  }
  *out = result;
  return true;
}

bool Parse(int argc, wchar_t** argv, Options* options) {
  for (int i = 1; i < argc; ++i) {
    const std::wstring_view arg(argv[i]);
    if (arg == L"--help" || arg == L"-h") {
      options->show_help = true;
      continue;
    }
    if (arg != L"--interval-ms" && arg != L"--count" && arg != L"--log") {
      std::wcerr << L"Unknown argument: " << arg << L'\n';
      return false;
    }
    if (++i >= argc) {
      std::wcerr << L"Missing value after " << arg << L'\n';
      return false;
    }
    const std::wstring_view value(argv[i]);
    if (arg == L"--log") {
      options->log_path = std::filesystem::path(value);
      continue;
    }
    std::uint64_t parsed = 0;
    if (!ParseNumber(value, &parsed)) return false;
    if (arg == L"--count") {
      options->count = parsed;
    } else {
      if (parsed > UINT32_MAX) return false;
      options->interval_ms = static_cast<std::uint32_t>(parsed);
    }
  }
  if (options->count == 0 && options->interval_ms == 0) return false;
  return true;
}

BOOL WINAPI CtrlHandler(DWORD signal) {
  if (signal == CTRL_C_EVENT || signal == CTRL_BREAK_EVENT || signal == CTRL_CLOSE_EVENT) {
    g_running.store(false);
    return TRUE;
  }
  return FALSE;
}

void Usage() {
  std::cout << "desktop-host-probe [--interval-ms N] [--count N] [--log PATH]\n"
            << "Default: one read-only JSONL snapshot. --count 0 runs until Ctrl+C.\n";
}

}  // namespace

int wmain(int argc, wchar_t** argv) {
  std::ios::sync_with_stdio(false);
  Options options;
  if (!Parse(argc, argv, &options)) { Usage(); return 2; }
  if (options.show_help) { Usage(); return 0; }

  SetLastError(ERROR_SUCCESS);
  const bool dpi_set = SetProcessDpiAwarenessContext(
      DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2) != FALSE;
  const DWORD dpi_error = dpi_set ? ERROR_SUCCESS : GetLastError();
  SetConsoleCtrlHandler(CtrlHandler, TRUE);

  std::ofstream file;
  std::ostream* output = &std::cout;
  if (options.log_path) {
    file.open(*options.log_path, std::ios::out | std::ios::app | std::ios::binary);
    if (!file.is_open()) return 3;
    output = &file;
  }

  std::uint64_t sequence = 0;
  while (g_running.load() && (options.count == 0 || sequence < options.count)) {
    Snapshot(*output, sequence++, dpi_set, dpi_error);
    if (options.count != 0 && sequence >= options.count) break;
    const auto deadline = std::chrono::steady_clock::now() +
                          std::chrono::milliseconds(options.interval_ms);
    while (g_running.load() && std::chrono::steady_clock::now() < deadline) Sleep(10);
  }
  return 0;
}
