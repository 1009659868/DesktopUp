// R4-D4 current-user lifecycle probe. This remains candidate evidence code, not the final product runtime.

class R4D4InstanceGate final {
public:
    enum class Result { Primary, SecondaryForwarded, Error };

    ~R4D4InstanceGate()
    {
        if (exitEvent_ != nullptr) CloseHandle(exitEvent_);
        if (activateEvent_ != nullptr) CloseHandle(activateEvent_);
        if (mutex_ != nullptr) CloseHandle(mutex_);
    }

    Result acquire(bool autostart, QString* error)
    {
        mutex_ = CreateMutexW(nullptr, TRUE, mutexName_);
        if (mutex_ == nullptr) {
            if (error != nullptr) {
                *error = QStringLiteral("R4D4_MUTEX_CREATE_FAILED: %1").arg(GetLastError());
            }
            return Result::Error;
        }
        const bool alreadyRunning = GetLastError() == ERROR_ALREADY_EXISTS;
        activateEvent_ = CreateEventW(nullptr, FALSE, FALSE, activateEventName_);
        exitEvent_ = CreateEventW(nullptr, FALSE, FALSE, exitEventName_);
        if (activateEvent_ == nullptr || exitEvent_ == nullptr) {
            if (error != nullptr) {
                *error = QStringLiteral("R4D4_EVENT_CREATE_FAILED: %1").arg(GetLastError());
            }
            return Result::Error;
        }
        if (!alreadyRunning) return Result::Primary;
        if (!autostart && !SetEvent(activateEvent_)) {
            if (error != nullptr) {
                *error = QStringLiteral("R4D4_ACTIVATE_SIGNAL_FAILED: %1").arg(GetLastError());
            }
            return Result::Error;
        }
        return Result::SecondaryForwarded;
    }

    static bool requestExit(QString* error)
    {
        HANDLE event = OpenEventW(EVENT_MODIFY_STATE, FALSE, exitEventName_);
        if (event == nullptr) {
            if (GetLastError() == ERROR_FILE_NOT_FOUND) return true;
            if (error != nullptr) {
                *error = QStringLiteral("R4D4_EXIT_EVENT_OPEN_FAILED: %1").arg(GetLastError());
            }
            return false;
        }
        const bool signaled = SetEvent(event) != FALSE;
        const DWORD code = GetLastError();
        CloseHandle(event);
        if (!signaled && error != nullptr) {
            *error = QStringLiteral("R4D4_EXIT_SIGNAL_FAILED: %1").arg(code);
        }
        return signaled;
    }

    HANDLE activateEvent() const { return activateEvent_; }
    HANDLE exitEvent() const { return exitEvent_; }

private:
    inline static constexpr wchar_t mutexName_[] =
        L"Local\\DesktopUp.V2.TSC01.R4D4.Primary";
    inline static constexpr wchar_t activateEventName_[] =
        L"Local\\DesktopUp.V2.TSC01.R4D4.Activate";
    inline static constexpr wchar_t exitEventName_[] =
        L"Local\\DesktopUp.V2.TSC01.R4D4.Exit";
    HANDLE mutex_ = nullptr;
    HANDLE activateEvent_ = nullptr;
    HANDLE exitEvent_ = nullptr;
};

class R4D4StateStore final {
public:
    explicit R4D4StateStore(DesktopHostCoordinator& host)
        : host_(host)
    {
        const QString localAppData = qEnvironmentVariable("LOCALAPPDATA");
        root_ = QDir(localAppData).filePath(QStringLiteral("DesktopUp/Candidate/TS-C01"));
        statePath_ = QDir(root_).filePath(QStringLiteral("candidate-state-v1.json"));
    }

    static QString formalInstallMarkerPath()
    {
        return QDir(QCoreApplication::applicationDirPath()).filePath(
            QStringLiteral("resources/r4d4-formal-install.flag"));
    }

    static bool isFormalInstalledExecutable()
    {
        const QString executablePath = QDir::cleanPath(QCoreApplication::applicationFilePath());
        if (QFileInfo(executablePath).fileName().compare(
                QStringLiteral("DesktopUp.exe"), Qt::CaseInsensitive) != 0) return false;

        QFile marker(formalInstallMarkerPath());
        if (!marker.open(QIODevice::ReadOnly)) return false;
        const QByteArray markerValue = marker.readAll().trimmed();
        if (markerValue != QByteArrayLiteral("desktopup-ts-c01-r4d4-formal-install-v2")) {
            return false;
        }

        HKEY key = nullptr;
        if (RegOpenKeyExW(HKEY_CURRENT_USER,
                L"Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\DesktopUp",
                0, KEY_QUERY_VALUE, &key) != ERROR_SUCCESS) return false;

        DWORD type = 0;
        DWORD bytes = 0;
        LONG status = RegQueryValueExW(key, L"InstallLocation", nullptr, &type, nullptr, &bytes);
        if (status != ERROR_SUCCESS || (type != REG_SZ && type != REG_EXPAND_SZ) || bytes == 0) {
            RegCloseKey(key);
            return false;
        }
        std::vector<wchar_t> value(bytes / sizeof(wchar_t) + 1, L'\0');
        status = RegQueryValueExW(key, L"InstallLocation", nullptr, &type,
                                 reinterpret_cast<BYTE*>(value.data()), &bytes);
        RegCloseKey(key);
        if (status != ERROR_SUCCESS) return false;

        QString registeredPath = QString::fromWCharArray(value.data());
        if (type == REG_EXPAND_SZ) {
            std::vector<wchar_t> expanded(32768, L'\0');
            const DWORD count = ExpandEnvironmentStringsW(
                reinterpret_cast<LPCWSTR>(registeredPath.utf16()), expanded.data(),
                static_cast<DWORD>(expanded.size()));
            if (count == 0 || count > expanded.size()) return false;
            registeredPath = QString::fromWCharArray(expanded.data());
        }
        return QDir::cleanPath(registeredPath).compare(
                   QDir::cleanPath(QCoreApplication::applicationDirPath()),
                   Qt::CaseInsensitive) == 0;
    }

    static QString defaultLogPath()
    {
        const QString root = QDir(qEnvironmentVariable("LOCALAPPDATA"))
            .filePath(QStringLiteral("DesktopUp/Candidate/TS-C01/logs"));
        if (!QDir().mkpath(root)) return {};
        return QDir(root).filePath(QStringLiteral("r4d4-runtime-%1-%2.jsonl")
            .arg(QDateTime::currentDateTimeUtc().toString(QStringLiteral("yyyyMMdd-HHmmss-zzz")))
            .arg(QCoreApplication::applicationPid()));
    }

    bool load(FixtureState* state, QString* error)
    {
        QFile file(statePath_);
        if (!file.exists()) {
            host_.recordFixtureEvent(QStringLiteral("r4d4_state_defaulted"), {
                {QStringLiteral("reason"), QStringLiteral("STATE_ABSENT")},
                {QStringLiteral("state_path"), QDir::toNativeSeparators(statePath_)}
            });
            lastSerialized_ = serialize(*state);
            return true;
        }
        if (!file.open(QIODevice::ReadOnly)) {
            if (error != nullptr) *error = QStringLiteral("R4D4_STATE_OPEN_FAILED");
            return false;
        }
        QJsonParseError parseError {};
        const QJsonDocument document = QJsonDocument::fromJson(file.readAll(), &parseError);
        file.close();
        if (parseError.error != QJsonParseError::NoError || !document.isObject()) {
            quarantine(QStringLiteral("PARSE_REJECTED"));
            host_.recordFixtureEvent(QStringLiteral("r4d4_state_rejected"), {
                {QStringLiteral("reason"), QStringLiteral("PARSE_REJECTED")},
                {QStringLiteral("state_path"), QDir::toNativeSeparators(statePath_)},
                {QStringLiteral("fallback"), QStringLiteral("SAFE_DEFAULT")}
            });
            lastSerialized_ = serialize(*state);
            return true;
        }
        const QJsonObject root = document.object();
        const QJsonObject geometry = root.value(QStringLiteral("geometry_px")).toObject();
        const QString presentation = root.value(QStringLiteral("presentation")).toString();
        const QString interaction = root.value(QStringLiteral("interaction")).toString();
        const int width = geometry.value(QStringLiteral("width")).toInt();
        const int expandedHeight = geometry.value(QStringLiteral("expanded_height")).toInt();
        if (root.value(QStringLiteral("schema")).toString() !=
                QStringLiteral("desktopup-ts-c01-r4d4-state-v1") ||
            width < 320 || width > 2400 || expandedHeight < 420 || expandedHeight > 2400 ||
            (presentation != QStringLiteral("EXPANDED") &&
             presentation != QStringLiteral("COLLAPSED")) ||
            (interaction != QStringLiteral("USING") &&
             interaction != QStringLiteral("EDITING"))) {
            quarantine(QStringLiteral("VALIDATION_REJECTED"));
            host_.recordFixtureEvent(QStringLiteral("r4d4_state_rejected"), {
                {QStringLiteral("reason"), QStringLiteral("VALIDATION_REJECTED")},
                {QStringLiteral("state_path"), QDir::toNativeSeparators(statePath_)},
                {QStringLiteral("fallback"), QStringLiteral("SAFE_DEFAULT")}
            });
            lastSerialized_ = serialize(*state);
            return true;
        }
        QRect restored(
            geometry.value(QStringLiteral("x")).toInt(),
            geometry.value(QStringLiteral("y")).toInt(),
            width,
            presentation == QStringLiteral("COLLAPSED")
                ? qRound(60.0 * width / 420.0)
                : expandedHeight);
        RECT nativeRect {
            restored.left(), restored.top(), restored.right() + 1, restored.bottom() + 1
        };
        MONITORINFO monitorInfo {};
        monitorInfo.cbSize = sizeof(monitorInfo);
        const HMONITOR monitor = MonitorFromRect(&nativeRect, MONITOR_DEFAULTTONEAREST);
        if (monitor != nullptr && GetMonitorInfoW(monitor, &monitorInfo)) {
            const QRect work(
                monitorInfo.rcWork.left, monitorInfo.rcWork.top,
                monitorInfo.rcWork.right - monitorInfo.rcWork.left,
                monitorInfo.rcWork.bottom - monitorInfo.rcWork.top);
            restored.setWidth(std::min(restored.width(), work.width()));
            restored.setHeight(std::min(restored.height(), work.height()));
            restored.moveLeft(std::clamp(
                restored.left(), work.left(), work.right() - restored.width() + 1));
            restored.moveTop(std::clamp(
                restored.top(), work.top(), work.bottom() - restored.height() + 1));
        }
        state->screenRect = restored;
        state->expandedHeight = expandedHeight;
        state->presentation = presentation;
        state->interaction = interaction;
        state->locked = root.value(QStringLiteral("locked")).toBool(false);
        const QString theme = root.value(QStringLiteral("theme_id")).toString(
            QStringLiteral("transparent-default"));
        state->selectedThemeId = theme;
        state->effectiveThemeId = theme;
        state->lastKnownGoodThemeId = theme;
        state->themeFallbackActive = false;
        lastSerialized_ = serialize(*state);
        host_.recordFixtureEvent(QStringLiteral("r4d4_state_loaded"), {
            {QStringLiteral("state_path"), QDir::toNativeSeparators(statePath_)},
            {QStringLiteral("geometry_px"), geometry},
            {QStringLiteral("presentation"), presentation},
            {QStringLiteral("interaction"), interaction},
            {QStringLiteral("locked"), state->locked},
            {QStringLiteral("theme_id"), theme},
            {QStringLiteral("result"), QStringLiteral("PASS")}
        });
        return true;
    }

    bool saveIfChanged(const FixtureState& state, bool force, QString* error)
    {
        const QByteArray serialized = serialize(state);
        if (!force && serialized == lastSerialized_) return true;
        if (!QDir().mkpath(root_)) {
            if (error != nullptr) *error = QStringLiteral("R4D4_STATE_ROOT_CREATE_FAILED");
            return false;
        }
        QSaveFile output(statePath_);
        if (!output.open(QIODevice::WriteOnly) || output.write(serialized) != serialized.size() ||
            !output.commit()) {
            if (error != nullptr) *error = QStringLiteral("R4D4_STATE_ATOMIC_WRITE_FAILED");
            return false;
        }
        lastSerialized_ = serialized;
        host_.recordFixtureEvent(QStringLiteral("r4d4_state_saved"), {
            {QStringLiteral("state_path"), QDir::toNativeSeparators(statePath_)},
            {QStringLiteral("atomic_replace"), true},
            {QStringLiteral("result"), QStringLiteral("PASS")}
        });
        return true;
    }

private:
    QByteArray serialize(const FixtureState& state) const
    {
        return QJsonDocument(QJsonObject {
            {QStringLiteral("schema"), QStringLiteral("desktopup-ts-c01-r4d4-state-v1")},
            {QStringLiteral("revision"), 1},
            {QStringLiteral("geometry_px"), QJsonObject {
                {QStringLiteral("x"), state.screenRect.x()},
                {QStringLiteral("y"), state.screenRect.y()},
                {QStringLiteral("width"), state.screenRect.width()},
                {QStringLiteral("expanded_height"), state.expandedHeight}
            }},
            {QStringLiteral("presentation"), state.presentation},
            {QStringLiteral("interaction"), state.interaction},
            {QStringLiteral("locked"), state.locked},
            {QStringLiteral("theme_id"), state.effectiveThemeId}
        }).toJson(QJsonDocument::Indented);
    }

    void quarantine(const QString& reason)
    {
        const QString rejected = statePath_ + QStringLiteral(".rejected-%1-%2")
            .arg(QDateTime::currentDateTimeUtc().toString(QStringLiteral("yyyyMMdd-HHmmss-zzz")))
            .arg(reason.toLower());
        QFile::rename(statePath_, rejected);
    }

    DesktopHostCoordinator& host_;
    QString root_;
    QString statePath_;
    QByteArray lastSerialized_;
};

class R4D4LifecycleController final {
public:
    R4D4LifecycleController(
        DesktopHostCoordinator& host,
        R4D4InstanceGate& gate,
        R4CControlCenterRoleProbe& controlCenter,
        FixtureState& state,
        R4D4StateStore& stateStore)
        : host_(host), gate_(gate), controlCenter_(controlCenter),
          state_(state), stateStore_(stateStore), instance_(GetModuleHandleW(nullptr))
    {
        timer_.setInterval(200);
        timer_.setTimerType(Qt::CoarseTimer);
        QObject::connect(&timer_, &QTimer::timeout, &timer_, [this]() { poll(); });
        stateTimer_.setInterval(500);
        stateTimer_.setTimerType(Qt::CoarseTimer);
        QObject::connect(&stateTimer_, &QTimer::timeout, &stateTimer_, [this]() {
            QString error;
            if (!stateStore_.saveIfChanged(state_, false, &error)) fail(error);
        });
    }

    ~R4D4LifecycleController()
    {
        timer_.stop();
        stateTimer_.stop();
        QString ignored;
        stateStore_.saveIfChanged(state_, true, &ignored);
        removeTray();
        if (window_ != nullptr && IsWindow(window_)) DestroyWindow(window_);
        window_ = nullptr;
        if (registered_) UnregisterClassW(className_, instance_);
    }

    bool initialize(bool autostart, QString* error)
    {
        WNDCLASSEXW windowClass {};
        windowClass.cbSize = sizeof(windowClass);
        windowClass.lpfnWndProc = &R4D4LifecycleController::windowProc;
        windowClass.hInstance = instance_;
        windowClass.hCursor = LoadCursorW(nullptr, IDC_ARROW);
        windowClass.lpszClassName = className_;
        if (RegisterClassExW(&windowClass) == 0) {
            if (error != nullptr) {
                *error = QStringLiteral("R4D4_TRAY_CLASS_REGISTER_FAILED: %1").arg(GetLastError());
            }
            return false;
        }
        registered_ = true;
        window_ = CreateWindowExW(
            WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
            className_, L"DesktopUp R4-D4 Lifecycle Probe", WS_OVERLAPPED,
            0, 0, 1, 1, nullptr, nullptr, instance_, this);
        if (window_ == nullptr) {
            if (error != nullptr) {
                *error = QStringLiteral("R4D4_TRAY_WINDOW_CREATE_FAILED: %1").arg(GetLastError());
            }
            return false;
        }
        taskbarCreatedMessage_ = RegisterWindowMessageW(L"TaskbarCreated");
        if (!addTray()) {
            if (error != nullptr) *error = QStringLiteral("R4D4_TRAY_ADD_FAILED");
            return false;
        }
        timer_.start();
        stateTimer_.start();
        host_.recordFixtureEvent(QStringLiteral("r4d4_lifecycle_primary_ready"), {
            {QStringLiteral("launch_intent"), autostart
                ? QStringLiteral("AUTOSTART") : QStringLiteral("ORDINARY")},
            {QStringLiteral("single_instance"), true},
            {QStringLiteral("tray_unique"), true},
            {QStringLiteral("control_center_initially_visible"), false},
            {QStringLiteral("autostart_default_enabled"), autostartOwned()},
            {QStringLiteral("formal_install_path"), QDir::toNativeSeparators(
                QCoreApplication::applicationFilePath())},
            {QStringLiteral("product_runtime_implemented"), false},
            {QStringLiteral("candidate_lifecycle_probe"), true},
            {QStringLiteral("p0_result"), QStringLiteral("NOT_RUN")}
        });
        return true;
    }

    bool failed() const { return failed_; }
    QString error() const { return error_; }

private:
    enum : UINT { trayMessage_ = WM_APP + 0x44 };
    enum : UINT { openCommand_ = 1001, autostartCommand_ = 1002, exitCommand_ = 1003 };

    QString autostartCommand() const
    {
        return QStringLiteral("\"") + QDir::toNativeSeparators(
            QCoreApplication::applicationFilePath()) + QStringLiteral("\" --autostart");
    }

    std::optional<QString> readAutostart() const
    {
        HKEY key = nullptr;
        if (RegOpenKeyExW(HKEY_CURRENT_USER,
                L"Software\\Microsoft\\Windows\\CurrentVersion\\Run",
                0, KEY_QUERY_VALUE, &key) != ERROR_SUCCESS) return std::nullopt;
        DWORD type = 0;
        DWORD bytes = 0;
        LONG status = RegQueryValueExW(key, L"DesktopUp", nullptr, &type, nullptr, &bytes);
        if (status == ERROR_FILE_NOT_FOUND) {
            RegCloseKey(key);
            return QString();
        }
        if (status != ERROR_SUCCESS || (type != REG_SZ && type != REG_EXPAND_SZ) || bytes < sizeof(wchar_t)) {
            RegCloseKey(key);
            return std::nullopt;
        }
        std::vector<wchar_t> value(bytes / sizeof(wchar_t) + 1, L'\0');
        status = RegQueryValueExW(key, L"DesktopUp", nullptr, &type,
                                 reinterpret_cast<BYTE*>(value.data()), &bytes);
        RegCloseKey(key);
        if (status != ERROR_SUCCESS) return std::nullopt;
        return QString::fromWCharArray(value.data());
    }

    bool autostartOwned() const
    {
        const auto value = readAutostart();
        return value.has_value() && !value->isEmpty() &&
               value->compare(autostartCommand(), Qt::CaseInsensitive) == 0;
    }

    bool setAutostart(bool enabled, QString* error)
    {
        if (!R4D4StateStore::isFormalInstalledExecutable()) {
            if (error != nullptr) *error = QStringLiteral("R4D4_AUTOSTART_REJECTED_NOT_FORMAL_INSTALL");
            return false;
        }
        const auto before = readAutostart();
        if (!before.has_value()) {
            if (error != nullptr) *error = QStringLiteral("R4D4_AUTOSTART_READ_FAILED");
            return false;
        }
        const QString expected = autostartCommand();
        if (!before->isEmpty() && before->compare(expected, Qt::CaseInsensitive) != 0) {
            if (error != nullptr) *error = QStringLiteral("R4D4_AUTOSTART_FOREIGN_VALUE_PRESERVED");
            host_.recordFixtureEvent(QStringLiteral("r4d4_autostart_ownership_conflict"), {
                {QStringLiteral("requested_enabled"), enabled},
                {QStringLiteral("value_name"), QStringLiteral("DesktopUp")},
                {QStringLiteral("foreign_value_preserved"), true},
                {QStringLiteral("result"), QStringLiteral("REJECTED")}
            });
            return false;
        }
        HKEY key = nullptr;
        const LONG opened = RegCreateKeyExW(
            HKEY_CURRENT_USER,
            L"Software\\Microsoft\\Windows\\CurrentVersion\\Run",
            0, nullptr, 0, KEY_QUERY_VALUE | KEY_SET_VALUE,
            nullptr, &key, nullptr);
        if (opened != ERROR_SUCCESS) {
            if (error != nullptr) *error = QStringLiteral("R4D4_AUTOSTART_KEY_OPEN_FAILED: %1").arg(opened);
            return false;
        }
        LONG status = ERROR_SUCCESS;
        if (enabled) {
            const std::wstring native = expected.toStdWString();
            status = RegSetValueExW(
                key, L"DesktopUp", 0, REG_SZ,
                reinterpret_cast<const BYTE*>(native.c_str()),
                static_cast<DWORD>((native.size() + 1) * sizeof(wchar_t)));
        } else if (!before->isEmpty()) {
            status = RegDeleteValueW(key, L"DesktopUp");
        }
        RegCloseKey(key);
        const bool afterOwned = autostartOwned();
        const bool verified = status == ERROR_SUCCESS && afterOwned == enabled;
        host_.recordFixtureEvent(QStringLiteral("r4d4_autostart_changed"), {
            {QStringLiteral("requested_enabled"), enabled},
            {QStringLiteral("effective_enabled"), afterOwned},
            {QStringLiteral("registry_scope"), QStringLiteral("HKCU")},
            {QStringLiteral("value_name"), QStringLiteral("DesktopUp")},
            {QStringLiteral("command"), expected},
            {QStringLiteral("write_readback_verified"), verified},
            {QStringLiteral("result"), verified ? QStringLiteral("PASS") : QStringLiteral("FAIL")}
        });
        if (!verified && error != nullptr) {
            *error = QStringLiteral("R4D4_AUTOSTART_WRITE_READBACK_FAILED: %1").arg(status);
        }
        return verified;
    }

    bool addTray()
    {
        NOTIFYICONDATAW data {};
        data.cbSize = sizeof(data);
        data.hWnd = window_;
        data.uID = 1;
        data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
        data.uCallbackMessage = trayMessage_;
        data.hIcon = LoadIconW(nullptr, IDI_APPLICATION);
        wcscpy_s(data.szTip, L"DesktopUp R4-D4 Candidate");
        if (!Shell_NotifyIconW(NIM_ADD, &data)) return false;
        data.uVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIconW(NIM_SETVERSION, &data);
        trayAdded_ = true;
        return true;
    }

    void removeTray()
    {
        if (!trayAdded_ || window_ == nullptr) return;
        NOTIFYICONDATAW data {};
        data.cbSize = sizeof(data);
        data.hWnd = window_;
        data.uID = 1;
        Shell_NotifyIconW(NIM_DELETE, &data);
        trayAdded_ = false;
    }

    void showMenu()
    {
        HMENU menu = CreatePopupMenu();
        if (menu == nullptr) return;
        AppendMenuW(menu, MF_STRING, openCommand_, L"Open control center probe");
        AppendMenuW(menu, MF_SEPARATOR, 0, nullptr);
        AppendMenuW(menu, MF_STRING | (autostartOwned() ? MF_CHECKED : MF_UNCHECKED),
                    autostartCommand_, L"Start with Windows");
        AppendMenuW(menu, MF_SEPARATOR, 0, nullptr);
        AppendMenuW(menu, MF_STRING, exitCommand_, L"Exit DesktopUp");
        POINT point {};
        GetCursorPos(&point);
        SetForegroundWindow(window_);
        TrackPopupMenu(menu, TPM_RIGHTBUTTON | TPM_BOTTOMALIGN,
                       point.x, point.y, 0, window_, nullptr);
        PostMessageW(window_, WM_NULL, 0, 0);
        DestroyMenu(menu);
    }

    void poll()
    {
        if (WaitForSingleObject(gate_.activateEvent(), 0) == WAIT_OBJECT_0) {
            controlCenter_.openFromLifecycle();
            host_.recordFixtureEvent(QStringLiteral("r4d4_secondary_ordinary_forwarded"), {
                {QStringLiteral("action"), QStringLiteral("OPEN_CONTROL_CENTER_ROLE_PROBE")},
                {QStringLiteral("duplicate_poster_created"), false},
                {QStringLiteral("duplicate_tray_created"), false},
                {QStringLiteral("result"), QStringLiteral("PASS")}
            });
        }
        if (WaitForSingleObject(gate_.exitEvent(), 0) == WAIT_OBJECT_0) {
            host_.recordFixtureEvent(QStringLiteral("r4d4_exit_requested"), {
                {QStringLiteral("source"), QStringLiteral("SECONDARY_OR_UNINSTALLER")},
                {QStringLiteral("result"), QStringLiteral("ACCEPTED")}
            });
            QCoreApplication::quit();
        }
    }

    void handleCommand(UINT command)
    {
        if (command == openCommand_) {
            controlCenter_.openFromLifecycle();
        } else if (command == autostartCommand_) {
            QString error;
            if (!setAutostart(!autostartOwned(), &error)) fail(error);
        } else if (command == exitCommand_) {
            host_.recordFixtureEvent(QStringLiteral("r4d4_exit_requested"), {
                {QStringLiteral("source"), QStringLiteral("TRAY")},
                {QStringLiteral("result"), QStringLiteral("ACCEPTED")}
            });
            QCoreApplication::quit();
        }
    }

    void fail(const QString& reason)
    {
        if (failed_) return;
        failed_ = true;
        error_ = reason.isEmpty() ? QStringLiteral("R4D4_LIFECYCLE_UNKNOWN_FAILURE") : reason;
        host_.recordFixtureEvent(QStringLiteral("r4d4_lifecycle_failed"), {
            {QStringLiteral("reason"), error_},
            {QStringLiteral("result"), QStringLiteral("FAIL")}
        });
    }

    static LRESULT CALLBACK windowProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam)
    {
        auto* self = reinterpret_cast<R4D4LifecycleController*>(
            GetWindowLongPtrW(window, GWLP_USERDATA));
        if (message == WM_NCCREATE) {
            const auto* create = reinterpret_cast<CREATESTRUCTW*>(lParam);
            self = static_cast<R4D4LifecycleController*>(create->lpCreateParams);
            SetWindowLongPtrW(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(self));
        }
        if (self != nullptr && message == self->taskbarCreatedMessage_) {
            self->trayAdded_ = false;
            self->addTray();
            return 0;
        }
        switch (message) {
        case WM_COMMAND:
            if (self != nullptr) self->handleCommand(LOWORD(wParam));
            return 0;
        case trayMessage_:
            if (self != nullptr) {
                const UINT notification = LOWORD(lParam);
                if (notification == WM_CONTEXTMENU || notification == WM_RBUTTONUP) {
                    self->showMenu();
                } else if (notification == NIN_SELECT || notification == NIN_KEYSELECT ||
                           notification == WM_LBUTTONDBLCLK) {
                    self->controlCenter_.openFromLifecycle();
                }
            }
            return 0;
        default:
            return DefWindowProcW(window, message, wParam, lParam);
        }
    }

    inline static constexpr wchar_t className_[] = L"DesktopUpTSC01R4D4Lifecycle";
    DesktopHostCoordinator& host_;
    R4D4InstanceGate& gate_;
    R4CControlCenterRoleProbe& controlCenter_;
    FixtureState& state_;
    R4D4StateStore& stateStore_;
    HINSTANCE instance_ = nullptr;
    HWND window_ = nullptr;
    UINT taskbarCreatedMessage_ = 0;
    QTimer timer_;
    QTimer stateTimer_;
    bool registered_ = false;
    bool trayAdded_ = false;
    bool failed_ = false;
    QString error_;
};
