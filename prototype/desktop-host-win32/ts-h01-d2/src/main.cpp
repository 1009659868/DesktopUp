#define wmain TsH01D1UnusedEntry
#include "../../ts-h01-d1/src/main.cpp"
#undef wmain

#include <set>

namespace {

constexpr wchar_t kD2VisualClass[] = L"DesktopUp.TS-H01-D2.VisualSurface";
constexpr wchar_t kD2IslandClass[] = L"DesktopUp.TS-H01-D2.InteractionIsland";
constexpr wchar_t kD2BuildId[] = L"TS-H01-D2-20260821-01-RECOVERY";
constexpr DWORD kD2DefaultDurationMs = 600000;
constexpr DWORD kD2MaximumDurationMs = 900000;
constexpr DWORD kD2SampleIntervalMs = 8;
constexpr std::uint32_t kD2StableDiscoverySamples = 32;
constexpr DWORD kD2FinalStableMs = 1500;

struct RecoveryOptions {
  bool run_recovery_test = false;
  DWORD duration_ms = kD2DefaultDurationMs;
  std::wstring scenario;
  std::uint32_t expected_recoveries = 10;
  std::optional<std::filesystem::path> log_path;
};

enum class RecoveryHostState {
  AttachedSafe,
  DegradedNoDesktopHost,
};

struct RecoveryCounters {
  std::uint64_t attach = 0;
  std::uint64_t generation_loss_cleanup = 0;
  std::uint64_t recreate = 0;
  std::uint64_t recovery = 0;
  std::uint64_t degraded_entries = 0;
};

struct RecoverySurfaceGroup {
  ShellTarget target;
  HWND visual = nullptr;
  std::array<IslandState, 3> islands{{
      {IslandRole::Title, nullptr, 0},
      {IslandRole::Item, nullptr, 0},
      {IslandRole::Action, nullptr, 0},
  }};
  RECT visual_rect{};
  POINT blank_point{};
  POINT title_point{};
  POINT item_point{};
  POINT action_point{};
  std::uint64_t generation = 0;
};

struct SelfWindowCounts {
  int total = 0;
  int top_level = 0;
};

const char* RecoveryStateName(RecoveryHostState state) {
  return state == RecoveryHostState::AttachedSafe
      ? "ATTACHED_SAFE"
      : "DEGRADED_NO_DESKTOP_HOST";
}

bool IsSafeShellTarget(const ShellTarget& target) {
  return target.topology == L"RAISED_PROGMAN" &&
      target.progman != nullptr && target.icon_host == target.progman &&
      target.def_view != nullptr && target.icon_view != nullptr &&
      target.explorer_pid != 0 && target.visible_def_views.size() == 1 &&
      IsWindow(target.icon_host) && IsWindow(target.def_view) &&
      IsWindow(target.icon_view) && GetParent(target.def_view) == target.icon_host;
}

bool SameShellTarget(const ShellTarget& left, const ShellTarget& right) {
  return IsSafeShellTarget(left) && IsSafeShellTarget(right) &&
      left.progman == right.progman && left.icon_host == right.icon_host &&
      left.def_view == right.def_view && left.icon_view == right.icon_view &&
      left.explorer_pid == right.explorer_pid && left.topology == right.topology;
}

std::string ShellSignature(const ShellTarget& target) {
  std::ostringstream stream;
  stream << target.explorer_pid << ':'
         << reinterpret_cast<std::uintptr_t>(target.icon_host) << ':'
         << reinterpret_cast<std::uintptr_t>(target.def_view) << ':'
         << reinterpret_cast<std::uintptr_t>(target.icon_view);
  return stream.str();
}

std::optional<RecoveryOptions> ParseRecoveryOptions(int argc, wchar_t** argv) {
  RecoveryOptions options;
  for (int index = 1; index < argc; ++index) {
    const std::wstring argument = argv[index];
    if (argument == L"--run-recovery-test") {
      options.run_recovery_test = true;
    } else if (argument == L"--scenario" && index + 1 < argc) {
      options.scenario = argv[++index];
      if (options.scenario != L"explorer-restart") {
        return std::nullopt;
      }
    } else if (argument == L"--duration-ms" && index + 1 < argc) {
      try {
        const unsigned long value = std::stoul(argv[++index]);
        if (value == 0 || value > kD2MaximumDurationMs) {
          return std::nullopt;
        }
        options.duration_ms = static_cast<DWORD>(value);
      } catch (...) {
        return std::nullopt;
      }
    } else if (argument == L"--expected-recoveries" && index + 1 < argc) {
      try {
        const unsigned long value = std::stoul(argv[++index]);
        if (value > 20) {
          return std::nullopt;
        }
        options.expected_recoveries = static_cast<std::uint32_t>(value);
      } catch (...) {
        return std::nullopt;
      }
    } else if (argument == L"--log" && index + 1 < argc) {
      options.log_path = std::filesystem::path(argv[++index]);
    } else {
      return std::nullopt;
    }
  }
  if (!options.run_recovery_test || options.scenario.empty()) {
    return std::nullopt;
  }
  return options;
}

void D2LogPointerEvent(const char* event, HWND hwnd, const char* role,
                       std::uint64_t count) {
  if (g_runtime == nullptr || g_runtime->sink == nullptr) {
    return;
  }
  const double elapsed_ms = std::chrono::duration<double, std::milli>(
      Clock::now() - g_runtime->started_at).count();
  std::ostringstream stream;
  stream << "{\"schema\":\"desktopup.ts-h01-d2.input.v1\""
         << ",\"event\":\"" << event << "\""
         << ",\"elapsed_ms\":" << std::fixed << std::setprecision(3)
         << elapsed_ms
         << ",\"hwnd\":" << HwndJson(hwnd)
         << ",\"role\":\"" << role << "\""
         << ",\"count\":" << count << "}";
  g_runtime->sink->Write(stream.str());
}

LRESULT CALLBACK D2VisualWindowProc(HWND hwnd, UINT message, WPARAM w_param,
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
        D2LogPointerEvent("unexpected_visual_input", hwnd, "visual",
                          g_runtime->visual_pointer_messages);
      }
      return message == WM_MOUSEACTIVATE ? MA_NOACTIVATE : 0;
    case WM_ERASEBKGND:
      return 1;
    case WM_PAINT: {
      PAINTSTRUCT paint{};
      HDC dc = BeginPaint(hwnd, &paint);
      RECT client{};
      GetClientRect(hwnd, &client);
      HBRUSH background = CreateSolidBrush(RGB(11, 29, 39));
      FillRect(dc, &client, background);
      DeleteObject(background);

      HPEN border = CreatePen(PS_SOLID, 3, RGB(244, 185, 66));
      HGDIOBJ old_pen = SelectObject(dc, border);
      HGDIOBJ old_brush = SelectObject(dc, GetStockObject(HOLLOW_BRUSH));
      Rectangle(dc, 1, 1, client.right - 1, client.bottom - 1);
      HPEN hatch = CreatePen(PS_DOT, 1, RGB(91, 118, 126));
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
      SetTextColor(dc, RGB(255, 214, 128));
      RECT heading{24, 78, client.right - 24, 103};
      DrawTextW(dc, L"TS-H01-D2  EXPLORER GENERATION RECOVERY", -1,
                &heading, DT_LEFT | DT_TOP | DT_NOPREFIX);
      RECT blank_text{blank.left + 16, blank.top + 18, blank.right - 16,
                      blank.bottom - 16};
      DrawTextW(dc,
                L"SAFE ATTACH ONLY\n\nNO TOP-LEVEL FALLBACK\n"
                L"NO TOPMOST\nONE SURFACE GROUP PER SHELL GENERATION",
                -1, &blank_text,
                DT_LEFT | DT_TOP | DT_WORDBREAK | DT_NOPREFIX);
      EndPaint(hwnd, &paint);
      return 0;
    }
    default:
      return DefWindowProcW(hwnd, message, w_param, l_param);
  }
}

LRESULT CALLBACK D2IslandWindowProc(HWND hwnd, UINT message, WPARAM w_param,
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
        D2LogPointerEvent("island_click", hwnd, RoleName(state->role),
                          state->click_count);
        InvalidateRect(hwnd, nullptr, FALSE);
      }
      return 0;
    case WM_RBUTTONUP:
      if (state != nullptr) {
        D2LogPointerEvent("island_right_click", hwnd, RoleName(state->role),
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
      HPEN border = CreatePen(PS_SOLID, 2, RGB(255, 236, 194));
      HGDIOBJ old_pen = SelectObject(dc, border);
      HGDIOBJ old_brush = SelectObject(dc, GetStockObject(HOLLOW_BRUSH));
      Rectangle(dc, 1, 1, client.right - 1, client.bottom - 1);
      SelectObject(dc, old_brush);
      SelectObject(dc, old_pen);
      DeleteObject(border);
      SetBkMode(dc, TRANSPARENT);
      SetTextColor(dc, RGB(255, 250, 238));
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

bool RegisterD2ProbeClasses(HINSTANCE instance) {
  WNDCLASSEXW visual_class{};
  visual_class.cbSize = sizeof(visual_class);
  visual_class.lpfnWndProc = D2VisualWindowProc;
  visual_class.hInstance = instance;
  visual_class.hCursor = LoadCursorW(nullptr, IDC_ARROW);
  visual_class.lpszClassName = kD2VisualClass;

  WNDCLASSEXW island_class{};
  island_class.cbSize = sizeof(island_class);
  island_class.lpfnWndProc = D2IslandWindowProc;
  island_class.hInstance = instance;
  island_class.hCursor = LoadCursorW(nullptr, IDC_HAND);
  island_class.lpszClassName = kD2IslandClass;

  return RegisterClassExW(&visual_class) != 0 &&
         RegisterClassExW(&island_class) != 0;
}

bool IsD2Window(HWND hwnd) {
  const std::wstring class_name = ClassName(hwnd);
  return class_name == kD2VisualClass || class_name == kD2IslandClass;
}
bool IsOwnedD2Window(HWND hwnd) {
  if (!IsD2Window(hwnd)) {
    return false;
  }
  DWORD process_id = 0;
  GetWindowThreadProcessId(hwnd, &process_id);
  return process_id == GetCurrentProcessId();
}


BOOL CALLBACK CountChildSelfWindows(HWND hwnd, LPARAM parameter) {
  auto* counts = reinterpret_cast<SelfWindowCounts*>(parameter);
  if (IsOwnedD2Window(hwnd)) {
    ++counts->total;
  }
  return TRUE;
}

BOOL CALLBACK CountTopSelfWindows(HWND hwnd, LPARAM parameter) {
  auto* counts = reinterpret_cast<SelfWindowCounts*>(parameter);
  if (IsOwnedD2Window(hwnd)) {
    ++counts->total;
    ++counts->top_level;
  }
  EnumChildWindows(hwnd, CountChildSelfWindows, parameter);
  return TRUE;
}

SelfWindowCounts CountSelfWindows() {
  SelfWindowCounts counts;
  EnumWindows(CountTopSelfWindows, reinterpret_cast<LPARAM>(&counts));
  return counts;
}

bool CreateRecoveryGroup(const ShellTarget& target, HINSTANCE instance,
                         std::uint64_t generation,
                         RecoverySurfaceGroup* group) {
  if (group == nullptr || !IsSafeShellTarget(target)) {
    return false;
  }
  *group = RecoverySurfaceGroup{};
  group->target = target;
  group->generation = generation;
  group->visual_rect = DefaultHostRect(target.icon_host);
  const int left = group->visual_rect.left;
  const int top = group->visual_rect.top;
  const int width = group->visual_rect.right - group->visual_rect.left;
  const int height = group->visual_rect.bottom - group->visual_rect.top;
  const UINT dpi = GetDpiForWindow(target.icon_host);
  const auto px = [dpi](int value) {
    return MulDiv(value, static_cast<int>(dpi), 96);
  };

  group->visual = CreateLayeredChild(
      WS_EX_TRANSPARENT, kD2VisualClass, L"TS-H01-D2 Transparent Visual",
      group->visual_rect, target.icon_host, instance, nullptr);
  const std::array<RECT, 3> island_rects{{
      RECT{left + px(24), top + px(18), left + px(382), top + px(66)},
      RECT{left + px(28), top + px(112), left + px(228), top + px(214)},
      RECT{left + px(430), top + px(18), left + px(510), top + px(66)},
  }};
  for (std::size_t index = 0; index < group->islands.size(); ++index) {
    group->islands[index].hwnd = CreateLayeredChild(
        0, kD2IslandClass, L"TS-H01-D2 Interaction Island",
        island_rects[index], target.icon_host, instance,
        &group->islands[index]);
  }

  const bool all_created = group->visual != nullptr &&
      std::all_of(group->islands.begin(), group->islands.end(),
                  [](const IslandState& island) {
                    return island.hwnd != nullptr;
                  });
  if (!all_created) {
    for (auto it = group->islands.rbegin(); it != group->islands.rend(); ++it) {
      if (it->hwnd != nullptr && IsOwnedD2Window(it->hwnd)) {
        DestroyWindow(it->hwnd);
      }
      it->hwnd = nullptr;
    }
    if (group->visual != nullptr && IsOwnedD2Window(group->visual)) {
      DestroyWindow(group->visual);
    }
    group->visual = nullptr;
    return false;
  }

  SetWindowPos(group->visual, HWND_TOP, left, top, width, height,
               SWP_NOACTIVATE | SWP_SHOWWINDOW);
  for (IslandState& island : group->islands) {
    SetWindowPos(island.hwnd, HWND_TOP, 0, 0, 0, 0,
                 SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    UpdateWindow(island.hwnd);
  }
  UpdateWindow(group->visual);

  group->blank_point = POINT{left + px(390), top + px(280)};
  group->title_point = POINT{left + px(180), top + px(42)};
  group->item_point = POINT{left + px(128), top + px(160)};
  group->action_point = POINT{left + px(470), top + px(42)};
  return true;
}

bool DestroyRecoveryGroup(RecoverySurfaceGroup* group) {
  if (group == nullptr) {
    return false;
  }
  bool destroyed = true;
  for (auto it = group->islands.rbegin(); it != group->islands.rend(); ++it) {
    if (it->hwnd != nullptr && IsOwnedD2Window(it->hwnd)) {
      destroyed = DestroyWindow(it->hwnd) != FALSE && destroyed;
    }
    it->hwnd = nullptr;
    it->click_count = 0;
  }
  if (group->visual != nullptr && IsOwnedD2Window(group->visual)) {
    destroyed = DestroyWindow(group->visual) != FALSE && destroyed;
  }
  group->visual = nullptr;
  group->target = ShellTarget{};
  group->generation = 0;
  return destroyed;
}

bool RecoveryGroupHandlesExist(const RecoverySurfaceGroup& group) {
  return IsOwnedD2Window(group.visual) &&
      std::all_of(group.islands.begin(), group.islands.end(),
                  [](const IslandState& island) {
                    return IsOwnedD2Window(island.hwnd);
                  });
}

std::string RecoveryStartJson(const RecoveryOptions& options,
                              const ShellTarget& target) {
  std::ostringstream stream;
  stream << "{\"schema\":\"desktopup.ts-h01-d2.event.v1\""
         << ",\"event\":\"recovery_test_start\""
         << ",\"build_id\":\"" << JsonEscape(Utf8(kD2BuildId)) << "\""
         << ",\"duration_ms\":" << options.duration_ms
         << ",\"scenario\":\"" << JsonEscape(Utf8(options.scenario)) << "\""
         << ",\"expected_recoveries\":" << options.expected_recoveries
         << ",\"strategy\":\"safe_rediscovery_no_top_level_fallback\""
         << ",\"initial_topology\":\""
         << JsonEscape(Utf8(target.topology)) << "\""
         << ",\"initial_explorer_pid\":" << target.explorer_pid
         << ",\"initial_icon_host\":" << HwndJson(target.icon_host)
         << ",\"initial_def_view\":" << HwndJson(target.def_view)
         << ",\"initial_icon_view\":" << HwndJson(target.icon_view)
         << "}";
  return stream.str();
}

std::string RecoveryEventJson(const char* event, const char* result,
                              RecoveryHostState state,
                              const ShellTarget& target,
                              const RecoveryCounters& counters,
                              std::uint64_t generation,
                              Clock::time_point started_at) {
  const double elapsed_ms = std::chrono::duration<double, std::milli>(
      Clock::now() - started_at).count();
  const SelfWindowCounts self = CountSelfWindows();
  std::ostringstream stream;
  stream << "{\"schema\":\"desktopup.ts-h01-d2.event.v1\""
         << ",\"event\":\"" << event << "\""
         << ",\"result\":\"" << result << "\""
         << ",\"elapsed_ms\":" << std::fixed << std::setprecision(3)
         << elapsed_ms
         << ",\"host_state\":\"" << RecoveryStateName(state) << "\""
         << ",\"generation\":" << generation
         << ",\"topology\":\"" << JsonEscape(Utf8(target.topology)) << "\""
         << ",\"explorer_pid\":" << target.explorer_pid
         << ",\"icon_host\":" << HwndJson(target.icon_host)
         << ",\"def_view\":" << HwndJson(target.def_view)
         << ",\"icon_view\":" << HwndJson(target.icon_view)
         << ",\"attach_count\":" << counters.attach
         << ",\"generation_loss_cleanup_count\":"
         << counters.generation_loss_cleanup
         << ",\"recreate_count\":" << counters.recreate
         << ",\"recovery_count\":" << counters.recovery
         << ",\"degraded_entry_count\":" << counters.degraded_entries
         << ",\"self_window_count\":" << self.total
         << ",\"self_top_level_count\":" << self.top_level
         << "}";
  return stream.str();
}

std::string RecoverySampleJson(std::uint64_t sequence,
                               Clock::time_point started_at,
                               RecoveryHostState state,
                               const ShellTarget& live_target,
                               const RecoverySurfaceGroup& group,
                               const RecoveryCounters& counters,
                               bool signatures_unique) {
  constexpr UINT kHitFlags =
      CWP_SKIPINVISIBLE | CWP_SKIPDISABLED | CWP_SKIPTRANSPARENT;
  const SelfWindowCounts self = CountSelfWindows();
  bool target_contract = true;
  bool visual_contract = true;
  bool islands_contract = true;
  bool z_contract = true;
  bool hit_contract = true;

  HWND blank_target = nullptr;
  HWND title_target = nullptr;
  HWND item_target = nullptr;
  HWND action_target = nullptr;
  int visual_index = -1;
  int title_index = -1;
  int item_index = -1;
  int action_index = -1;
  int def_view_index = -1;

  if (state == RecoveryHostState::AttachedSafe) {
    target_contract = SameShellTarget(live_target, group.target) &&
        RecoveryGroupHandlesExist(group);
    blank_target = ChildWindowFromPointEx(
        group.target.icon_host, group.blank_point, kHitFlags);
    title_target = ChildWindowFromPointEx(
        group.target.icon_host, group.title_point, kHitFlags);
    item_target = ChildWindowFromPointEx(
        group.target.icon_host, group.item_point, kHitFlags);
    action_target = ChildWindowFromPointEx(
        group.target.icon_host, group.action_point, kHitFlags);
    visual_index = ChildIndex(group.target.icon_host, group.visual);
    def_view_index = ChildIndex(group.target.icon_host, group.target.def_view);
    title_index = ChildIndex(group.target.icon_host, group.islands[0].hwnd);
    item_index = ChildIndex(group.target.icon_host, group.islands[1].hwnd);
    action_index = ChildIndex(group.target.icon_host, group.islands[2].hwnd);

    const LONG_PTR visual_style =
        GetWindowLongPtrW(group.visual, GWL_STYLE);
    const LONG_PTR visual_ex_style =
        GetWindowLongPtrW(group.visual, GWL_EXSTYLE);
    visual_contract = GetParent(group.visual) == group.target.icon_host &&
        (visual_style & WS_CHILD) != 0 &&
        (visual_ex_style & WS_EX_LAYERED) != 0 &&
        (visual_ex_style & WS_EX_TRANSPARENT) != 0 &&
        (visual_ex_style & WS_EX_TOPMOST) == 0 &&
        IsWindowVisible(group.visual) != FALSE && !IsIconic(group.visual) &&
        !IsCloaked(group.visual);

    for (const IslandState& island : group.islands) {
      const LONG_PTR style = GetWindowLongPtrW(island.hwnd, GWL_STYLE);
      const LONG_PTR ex_style = GetWindowLongPtrW(island.hwnd, GWL_EXSTYLE);
      islands_contract = islands_contract &&
          GetParent(island.hwnd) == group.target.icon_host &&
          (style & WS_CHILD) != 0 && (ex_style & WS_EX_LAYERED) != 0 &&
          (ex_style & WS_EX_TRANSPARENT) == 0 &&
          (ex_style & WS_EX_TOPMOST) == 0 &&
          IsWindowVisible(island.hwnd) != FALSE && !IsIconic(island.hwnd) &&
          !IsCloaked(island.hwnd);
    }
    z_contract = action_index >= 0 && item_index >= 0 && title_index >= 0 &&
        visual_index >= 0 && def_view_index >= 0 &&
        action_index < visual_index && item_index < visual_index &&
        title_index < visual_index && visual_index < def_view_index;
    hit_contract = IsNativeDesktopTarget(blank_target, group.target) &&
        title_target == group.islands[0].hwnd &&
        item_target == group.islands[1].hwnd &&
        action_target == group.islands[2].hwnd;
  } else {
    target_contract = group.visual == nullptr &&
        std::all_of(group.islands.begin(), group.islands.end(),
                    [](const IslandState& island) {
                      return island.hwnd == nullptr;
                    });
    visual_contract = self.total == 0;
    islands_contract = self.total == 0;
    z_contract = self.top_level == 0;
    hit_contract = true;
  }

  const bool self_contract = state == RecoveryHostState::AttachedSafe
      ? self.total == 4 && self.top_level == 0
      : self.total == 0 && self.top_level == 0;
  const bool pass = target_contract && visual_contract && islands_contract &&
      z_contract && hit_contract && self_contract && signatures_unique &&
      g_runtime != nullptr && g_runtime->visual_pointer_messages == 0;
  const double elapsed_ms = std::chrono::duration<double, std::milli>(
      Clock::now() - started_at).count();

  std::ostringstream stream;
  stream << "{\"schema\":\"desktopup.ts-h01-d2.sample.v1\""
         << ",\"sequence\":" << sequence
         << ",\"elapsed_ms\":" << std::fixed << std::setprecision(3)
         << elapsed_ms
         << ",\"host_state\":\"" << RecoveryStateName(state) << "\""
         << ",\"generation\":" << group.generation
         << ",\"live_topology\":\""
         << JsonEscape(Utf8(live_target.topology)) << "\""
         << ",\"live_explorer_pid\":" << live_target.explorer_pid
         << ",\"live_icon_host\":" << HwndJson(live_target.icon_host)
         << ",\"live_def_view\":" << HwndJson(live_target.def_view)
         << ",\"visual\":" << HwndJson(group.visual)
         << ",\"title\":" << HwndJson(group.islands[0].hwnd)
         << ",\"item\":" << HwndJson(group.islands[1].hwnd)
         << ",\"action\":" << HwndJson(group.islands[2].hwnd)
         << ",\"blank_target\":" << HwndJson(blank_target)
         << ",\"title_target\":" << HwndJson(title_target)
         << ",\"item_target\":" << HwndJson(item_target)
         << ",\"action_target\":" << HwndJson(action_target)
         << ",\"visual_index\":" << visual_index
         << ",\"title_index\":" << title_index
         << ",\"item_index\":" << item_index
         << ",\"action_index\":" << action_index
         << ",\"def_view_index\":" << def_view_index
         << ",\"visual_state\":"
         << WindowStateJson(group.visual, group.target.icon_host)
         << ",\"title_state\":"
         << WindowStateJson(group.islands[0].hwnd, group.target.icon_host)
         << ",\"item_state\":"
         << WindowStateJson(group.islands[1].hwnd, group.target.icon_host)
         << ",\"action_state\":"
         << WindowStateJson(group.islands[2].hwnd, group.target.icon_host)
         << ",\"def_view_state\":"
         << WindowStateJson(group.target.def_view, group.target.icon_host)
         << ",\"attach_count\":" << counters.attach
         << ",\"generation_loss_cleanup_count\":"
         << counters.generation_loss_cleanup
         << ",\"recreate_count\":" << counters.recreate
         << ",\"recovery_count\":" << counters.recovery
         << ",\"degraded_entry_count\":" << counters.degraded_entries
         << ",\"self_window_count\":" << self.total
         << ",\"self_top_level_count\":" << self.top_level
         << ",\"target_contract\":"
         << (target_contract ? "true" : "false")
         << ",\"visual_contract\":"
         << (visual_contract ? "true" : "false")
         << ",\"islands_contract\":"
         << (islands_contract ? "true" : "false")
         << ",\"z_contract\":" << (z_contract ? "true" : "false")
         << ",\"hit_contract\":" << (hit_contract ? "true" : "false")
         << ",\"self_contract\":" << (self_contract ? "true" : "false")
         << ",\"signatures_unique\":"
         << (signatures_unique ? "true" : "false")
         << ",\"visual_pointer_messages\":"
         << (g_runtime != nullptr ? g_runtime->visual_pointer_messages : 0)
         << ",\"pass\":" << (pass ? "true" : "false") << "}";
  return stream.str();
}

}  // namespace

int wmain(int argc, wchar_t** argv) {
  const auto options = ParseRecoveryOptions(argc, argv);
  if (!options.has_value()) {
    std::wcerr << L"Explicit --run-recovery-test, --scenario explorer-restart, "
                  L"and valid bounded options are required.\n";
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
  const auto started_at = Clock::now();
  runtime.started_at = started_at;
  ShellTarget live_target = DiscoverShellTarget();
  sink.Write(RecoveryStartJson(*options, live_target));

  HINSTANCE instance = GetModuleHandleW(nullptr);
  if (!RegisterD2ProbeClasses(instance)) {
    sink.Write("{\"schema\":\"desktopup.ts-h01-d2.event.v1\","
               "\"event\":\"window_class_failed\","
               "\"result\":\"registration_failed\"}");
    g_runtime = nullptr;
    return 4;
  }

  RecoverySurfaceGroup group;
  RecoveryCounters counters;
  RecoveryHostState state = RecoveryHostState::DegradedNoDesktopHost;
  std::set<std::string> attached_signatures;
  ShellTarget stable_candidate;
  std::uint32_t stable_candidate_samples = 0;
  std::uint64_t next_generation = 1;
  bool signatures_unique = true;
  bool all_samples_passed = true;
  bool cleanup_passed = true;
  auto last_attachment_at = started_at;

  if (IsSafeShellTarget(live_target)) {
    const std::string signature = ShellSignature(live_target);
    if (!CreateRecoveryGroup(live_target, instance, next_generation++, &group)) {
      sink.Write(RecoveryEventJson(
          "initial_attach_failed", "layered_child_failed", state,
          live_target, counters, 0, started_at));
      UnregisterClassW(kD2IslandClass, instance);
      UnregisterClassW(kD2VisualClass, instance);
      g_runtime = nullptr;
      return 4;
    }
    attached_signatures.insert(signature);
    ++counters.attach;
    state = RecoveryHostState::AttachedSafe;
    last_attachment_at = Clock::now();
    sink.Write(RecoveryEventJson(
        "initial_attached_safe", "one_child_surface_group", state,
        live_target, counters, group.generation, started_at));
  } else {
    ++counters.degraded_entries;
    sink.Write(RecoveryEventJson(
        "degraded_enter", "initial_safe_target_unavailable", state,
        live_target, counters, 0, started_at));
  }

  HANDLE sample_timer = CreateWaitableTimerExW(
      nullptr, nullptr, CREATE_WAITABLE_TIMER_HIGH_RESOLUTION,
      TIMER_ALL_ACCESS);
  LARGE_INTEGER first_due{};
  first_due.QuadPart = -10000;
  const bool timer_ready = sample_timer != nullptr &&
      SetWaitableTimerEx(sample_timer, &first_due,
                         static_cast<LONG>(kD2SampleIntervalMs), nullptr,
                         nullptr, nullptr, 0) != FALSE;
  if (!timer_ready) {
    sink.Write(RecoveryEventJson(
        "high_resolution_timer_failed", "timer_initialization_failed", state,
        live_target, counters, group.generation, started_at));
    all_samples_passed = false;
  }

  const auto deadline =
      started_at + std::chrono::milliseconds(options->duration_ms);
  std::uint64_t sequence = 0;
  bool completed_required_recoveries = false;

  while (timer_ready && Clock::now() < deadline && all_samples_passed) {
    const DWORD wait_result = MsgWaitForMultipleObjectsEx(
        1, &sample_timer, 100, QS_ALLINPUT, MWMO_INPUTAVAILABLE);
    MSG message{};
    while (PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE)) {
      TranslateMessage(&message);
      DispatchMessageW(&message);
    }
    if (wait_result == WAIT_FAILED) {
      sink.Write(RecoveryEventJson(
          "high_resolution_timer_failed", "wait_failed", state,
          live_target, counters, group.generation, started_at));
      all_samples_passed = false;
      break;
    }
    if (wait_result != WAIT_OBJECT_0) {
      continue;
    }

    live_target = DiscoverShellTarget();
    if (state == RecoveryHostState::AttachedSafe &&
        (!SameShellTarget(live_target, group.target) ||
         !RecoveryGroupHandlesExist(group))) {
      const ShellTarget lost_target = group.target;
      sink.Write(RecoveryEventJson(
          "shell_generation_lost", "old_parent_tree_invalid", state,
          lost_target, counters, group.generation, started_at));
      cleanup_passed = DestroyRecoveryGroup(&group) && cleanup_passed;
      ++counters.generation_loss_cleanup;
      ++counters.degraded_entries;
      state = RecoveryHostState::DegradedNoDesktopHost;
      stable_candidate = ShellTarget{};
      stable_candidate_samples = 0;
      const SelfWindowCounts after_cleanup = CountSelfWindows();
      cleanup_passed = cleanup_passed && after_cleanup.total == 0 &&
          after_cleanup.top_level == 0;
      sink.Write(RecoveryEventJson(
          "degraded_enter",
          cleanup_passed ? "old_surface_group_cleared"
                         : "old_surface_cleanup_contract_failed",
          state, live_target, counters, 0, started_at));
    }

    if (state == RecoveryHostState::DegradedNoDesktopHost) {
      if (IsSafeShellTarget(live_target)) {
        if (SameShellTarget(stable_candidate, live_target)) {
          ++stable_candidate_samples;
        } else {
          stable_candidate = live_target;
          stable_candidate_samples = 1;
        }
      } else {
        stable_candidate = ShellTarget{};
        stable_candidate_samples = 0;
      }

      if (stable_candidate_samples >= kD2StableDiscoverySamples) {
        const std::string signature = ShellSignature(stable_candidate);
        signatures_unique =
            attached_signatures.find(signature) == attached_signatures.end();
        sink.Write(RecoveryEventJson(
            "safe_target_rediscovered",
            signatures_unique ? "stable_unique_target"
                              : "duplicate_generation_signature",
            state, stable_candidate, counters, 0, started_at));
        if (!signatures_unique ||
            !CreateRecoveryGroup(stable_candidate, instance,
                                 next_generation++, &group)) {
          all_samples_passed = false;
        } else {
          attached_signatures.insert(signature);
          const bool is_recovery = counters.attach > 0;
          ++counters.attach;
          if (is_recovery) {
            ++counters.recreate;
            ++counters.recovery;
          }
          state = RecoveryHostState::AttachedSafe;
          last_attachment_at = Clock::now();
          stable_candidate = ShellTarget{};
          stable_candidate_samples = 0;
          sink.Write(RecoveryEventJson(
              "attached_safe",
              is_recovery ? "unique_recovery_surface_group"
                          : "initial_delayed_surface_group",
              state, group.target, counters, group.generation, started_at));
          if (counters.recovery > options->expected_recoveries) {
            all_samples_passed = false;
          }
        }
      }
    }

    const std::string sample = RecoverySampleJson(
        sequence++, started_at, state, live_target, group, counters,
        signatures_unique);
    sink.Write(sample);
    all_samples_passed = all_samples_passed &&
        sample.find("\"pass\":true") != std::string::npos;

    if (options->expected_recoveries > 0 &&
        counters.recovery == options->expected_recoveries &&
        state == RecoveryHostState::AttachedSafe &&
        std::chrono::duration_cast<std::chrono::milliseconds>(
            Clock::now() - last_attachment_at).count() >= kD2FinalStableMs) {
      completed_required_recoveries = true;
      break;
    }
  }

  if (sample_timer != nullptr) {
    CancelWaitableTimer(sample_timer);
    CloseHandle(sample_timer);
  }

  if (options->expected_recoveries == 0) {
    completed_required_recoveries = counters.recovery == 0 &&
        state == RecoveryHostState::AttachedSafe;
  }
  const bool expected_recovery_contract = completed_required_recoveries &&
      counters.recovery == options->expected_recoveries &&
      counters.recreate == counters.recovery &&
      counters.generation_loss_cleanup == counters.recovery;

  cleanup_passed = DestroyRecoveryGroup(&group) && cleanup_passed;
  const SelfWindowCounts final_self = CountSelfWindows();
  cleanup_passed = cleanup_passed && final_self.total == 0 &&
      final_self.top_level == 0;
  UnregisterClassW(kD2IslandClass, instance);
  UnregisterClassW(kD2VisualClass, instance);

  const bool final_pass = all_samples_passed && cleanup_passed &&
      expected_recovery_contract && sequence > 0 && signatures_unique &&
      runtime.visual_pointer_messages == 0;
  sink.Write(RecoveryEventJson(
      "recovery_test_complete",
      final_pass ? "telemetry_pass_manual_observation_pending"
                 : "recovery_contract_fail",
      RecoveryHostState::DegradedNoDesktopHost, live_target, counters, 0,
      started_at));
  g_runtime = nullptr;
  return final_pass ? 0 : 4;
}
