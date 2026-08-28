#include "adapters/desktop-host-win32/desktop_host_fixture.h"
#include "adapters/diagnostics/resource_ledger.h"
#include "adapters/renderer-qpainter/qpainter_renderer.h"
#include "core/runtime_core.h"

#include <QGuiApplication>

#include <iostream>

namespace {

int failures{};

void expect(const bool condition, const char* message) {
  if (!condition) {
    std::cerr << "FAIL: " << message << '\n';
    ++failures;
  }
}

}  // namespace

int main(int argc, char** argv) {
  QGuiApplication application(argc, argv);
  using namespace desktopup;

  adapters::ResourceLedger ledger;
  adapters::Win32DesktopHostFixture host;
  adapters::QPainterRenderer renderer;
  core::FrameGate gate;
  constexpr contracts::PixelSize frameSize{240, 360};
  constexpr contracts::PixelRect screenBounds{100, 120, 240, 360};

  expect(host.attach(11, screenBounds), "host fixture must attach");
  ledger.acquire("host");
  gate.configure(7, 11, frameSize);
  const auto frame = renderer.render({7, 11, frameSize});
  const auto pixels = renderer.frameView(frame.buffer);
  const contracts::HitRegionSet hits{7, 7, frameSize, {}};
  expect(renderer.hasBuffer(), "QPainter renderer must own an ARGB32 premultiplied buffer");
  expect(renderer.image().format() == QImage::Format_ARGB32_Premultiplied,
         "renderer format must match frozen contract");
  expect(pixels.valid(), "renderer must expose a valid premultiplied frame view");
  ledger.acquire("frame");
  expect(gate.accept(frame) && host.present(screenBounds, frame, pixels, hits),         "matching frame must publish after first-frame gate");
  expect(!host.present(screenBounds, {7, 10, frameSize, frame.buffer}, pixels, hits),         "host must reject stale surface epoch");

  gate.beginExit();
  host.beginExit();
  renderer.beginExit();
  const auto lateFrame = renderer.render({8, 12, frameSize});
  expect(lateFrame.buffer == 0, "renderer must not create late buffer after BeginExit");
  expect(!gate.accept(frame), "gate must reject late delivery after BeginExit");
  renderer.release(frame.buffer);
  ledger.release("frame");
  host.detach();
  ledger.release("host");
  expect(!host.attached(), "host must detach");
  expect(ledger.total() == 0, "resource ownership ledger must return to zero");

  const auto redacted = adapters::redactDiagnosticText("failed at C:\\Users\\Example\\secret.txt");
  expect(redacted.find("secret.txt") == std::string::npos,
         "diagnostics must redact local paths");

  if (failures == 0) {
    std::cout << "QTS03_LIFECYCLE_PASS\n";
  }
  return failures == 0 ? 0 : 1;
}
