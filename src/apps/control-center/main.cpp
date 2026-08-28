#include "adapters/runtime-ipc-win32/runtime_ipc.h"

#include <QGuiApplication>
#include <QQmlApplicationEngine>
#include <QTimer>

#include <optional>
#include <string>

namespace {

std::optional<std::string> optionValue(const int argc, char** argv, const std::string& option) {
  for (int index = 1; index < argc; ++index) {
    const std::string argument = argv[index];
    if (argument == option && index + 1 < argc) {
      return std::string(argv[index + 1]);
    }
    const auto prefix = option + "=";
    if (argument.rfind(prefix, 0) == 0) {
      return argument.substr(prefix.size());
    }
  }
  return std::nullopt;
}

}  // namespace

int main(int argc, char** argv) {
  QGuiApplication application(argc, argv);
  QCoreApplication::setApplicationName(QStringLiteral("DesktopUp V2 Control Center"));
  QCoreApplication::setOrganizationName(QStringLiteral("DesktopUp"));

  if (const auto pipe = optionValue(argc, argv, "--pipe-name"); pipe.has_value()) {
    const std::wstring pipeName(pipe->begin(), pipe->end());
    const auto result = desktopup::adapters::RuntimeIpcClient::sendIntent(
        pipeName, desktopup::contracts::LaunchIntent::OpenControlCenter, 1);
    if (!result.accepted) {
      return 20;
    }
  }

  QQmlApplicationEngine engine;
  engine.loadFromModule("DesktopUp.ControlCenter", "Main");
  if (engine.rootObjects().isEmpty()) {
    return 21;
  }

  if (const auto smokeMs = optionValue(argc, argv, "--smoke-ms"); smokeMs.has_value()) {
    QTimer::singleShot(std::stoi(*smokeMs), &application, &QCoreApplication::quit);
  }
  return application.exec();
}
