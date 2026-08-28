#include <windows.h>

namespace {

constexpr wchar_t kClassName[] =
    L"DesktopUp.V2.QTS04.UnrelatedWindowEventSource";

LRESULT CALLBACK windowProcedure(HWND window,
                                 UINT message,
                                 WPARAM wParam,
                                 LPARAM lParam) {
  return DefWindowProcW(window, message, wParam, lParam);
}

}  // namespace

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int) {
  WNDCLASSEXW windowClass{};
  windowClass.cbSize = sizeof(windowClass);
  windowClass.hInstance = instance;
  windowClass.lpfnWndProc = windowProcedure;
  windowClass.lpszClassName = kClassName;
  if (RegisterClassExW(&windowClass) == 0) {
    return 2;
  }

  const HWND window =
      CreateWindowExW(WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
                      kClassName,
                      L"QTS-04-04 unrelated lifecycle source",
                      WS_POPUP,
                      -32000,
                      -32000,
                      1,
                      1,
                      nullptr,
                      nullptr,
                      instance,
                      nullptr);
  if (window == nullptr) {
    return 3;
  }

  Sleep(220);
  DestroyWindow(window);
  UnregisterClassW(kClassName, instance);
  return 0;
}
