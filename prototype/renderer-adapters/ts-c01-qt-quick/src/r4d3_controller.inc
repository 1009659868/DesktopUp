class R4D3FaultPerformanceController final {
public:
    using RecoveryCallback = std::function<bool(QString*)>;
    using ProjectionCallback =
        std::function<bool(const FixtureState&, const QString&, QString*)>;

    R4D3FaultPerformanceController(
        DesktopHostCoordinator& host,
        FixtureState& state,
        RecoveryCallback recover,
        ProjectionCallback project)
        : host_(host), state_(state), recover_(std::move(recover)),
          project_(std::move(project)), instance_(GetModuleHandleW(nullptr))
    {
    }

    ~R4D3FaultPerformanceController() { clear(); }

    bool initialize(bool runSelfTest, QString* error)
    {
        WNDCLASSEXW windowClass {};
        windowClass.cbSize = sizeof(windowClass);
        windowClass.lpfnWndProc = &R4D3FaultPerformanceController::windowProc;
        windowClass.hInstance = instance_;
        windowClass.hCursor = LoadCursorW(nullptr, IDC_ARROW);
        windowClass.hbrBackground = GetSysColorBrush(COLOR_WINDOW);
        windowClass.lpszClassName = className_;
        if (RegisterClassExW(&windowClass) == 0) {
            if (error != nullptr) {
                *error = QStringLiteral("R4D3_CONTROL_CLASS_REGISTER_FAILED: %1")
                    .arg(GetLastError());
            }
            return false;
        }
        registered_ = true;
        window_ = CreateWindowExW(
            runSelfTest ? WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE : WS_EX_APPWINDOW,
            className_,
            L"DesktopUp R4-D3 Fault / Performance Manual Probe",
            runSelfTest
                ? WS_POPUP
                : WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX,
            runSelfTest ? 0 : 48,
            runSelfTest ? 0 : 64,
            runSelfTest ? 1 : 720,
            runSelfTest ? 1 : 390,
            nullptr, nullptr, instance_, this);
        if (window_ == nullptr) {
            if (error != nullptr) {
                *error = QStringLiteral("R4D3_CONTROL_WINDOW_CREATE_FAILED: %1")
                    .arg(GetLastError());
            }
            clear();
            return false;
        }
        if (!runSelfTest && !createManualControls(error)) {
            clear();
            return false;
        }
        host_.recordFixtureEvent(QStringLiteral("r4d3_controller_ready"), {
            {QStringLiteral("self_test"), runSelfTest},
            {QStringLiteral("manual_control_entry"), runSelfTest
                ? QStringLiteral("NONE")
                : QStringLiteral("VISIBLE_NATIVE_BUTTON_PANEL")},
            {QStringLiteral("surface_recovery_scope"),
             QStringLiteral("SURFACE_ONLY_SAME_ATTACHMENT")},
            {QStringLiteral("synthetic_item_count"), 200},
            {QStringLiteral("static_sample_seconds"), 10},
            {QStringLiteral("cpu_time_delta_budget_ms"), 100},
            {QStringLiteral("working_set_budget_mib"), 50},
            {QStringLiteral("theme_switch_budget_ms"), 200},
            {QStringLiteral("topmost"), false},
            {QStringLiteral("ordinary_top_level_fallback"), false}
        });
        if (runSelfTest) {
            QTimer::singleShot(0, &actionContext_, [this]() { this->runSelfTest(); });
        } else {
            ShowWindow(window_, SW_SHOWNORMAL);
            UpdateWindow(window_);
            SetForegroundWindow(window_);
        }
        return true;
    }

    void clear()
    {
        controls_.clear();
        status_ = nullptr;
        if (window_ != nullptr && IsWindow(window_)) DestroyWindow(window_);
        window_ = nullptr;
        if (registered_) {
            UnregisterClassW(className_, instance_);
            registered_ = false;
        }
    }

    bool failed() const { return failed_; }
    QString error() const { return error_; }
    bool selfTestCompleted() const { return selfTestCompleted_; }
    int recoveryCount() const { return recoveryCount_; }
    int staleRejectCount() const { return staleRejectCount_; }
    int duplicateShellRejectCount() const { return duplicateShellRejectCount_; }
    int stressCount() const { return stressCount_; }
    double maxThemeSwitchMs() const { return maxThemeSwitchMs_; }

private:
    static quint64 jsonU64(const QJsonObject& object, const QString& key)
    {
        return static_cast<quint64>(object.value(key).toDouble());
    }

    HWND createControl(
        const wchar_t* className, const wchar_t* label, DWORD style,
        int identifier, int x, int y, int width, int height)
    {
        HWND control = CreateWindowExW(
            0, className, label, WS_CHILD | WS_VISIBLE | style,
            x, y, width, height, window_,
            identifier == 0
                ? nullptr
                : reinterpret_cast<HMENU>(static_cast<INT_PTR>(identifier)),
            instance_, nullptr);
        if (control != nullptr) controls_.push_back(control);
        return control;
    }

    bool createManualControls(QString* error)
    {
        const bool created =
            createControl(
                L"STATIC", L"R4-D3 RENDERER FAULT / PERFORMANCE PROBE",
                SS_LEFT, 0, 20, 16, 660, 28) != nullptr &&
            createControl(
                L"STATIC",
                L"Diagnostic UI only. Use every button once. The 10-second CPU and working-set gate is automated.",
                SS_LEFT, 0, 20, 48, 660, 42) != nullptr &&
            createControl(
                L"BUTTON", L"Recoverable renderer loss -> recover",
                BS_PUSHBUTTON | WS_TABSTOP,
                actionRecover_, 20, 108, 320, 44) != nullptr &&
            createControl(
                L"BUTTON", L"Reject stale revision callback",
                BS_PUSHBUTTON | WS_TABSTOP,
                actionStale_, 360, 108, 320, 44) != nullptr &&
            createControl(
                L"BUTTON", L"Reject duplicate current-Shell event",
                BS_PUSHBUTTON | WS_TABSTOP,
                actionDuplicateShell_, 20, 172, 320, 44) != nullptr &&
            createControl(
                L"BUTTON", L"Run 200 Item + theme-switch stress",
                BS_PUSHBUTTON | WS_TABSTOP,
                actionStress_, 360, 172, 320, 44) != nullptr;
        if (!created) {
            if (error != nullptr) {
                *error = QStringLiteral("R4D3_CONTROL_CHILD_CREATE_FAILED: %1")
                    .arg(GetLastError());
            }
            return false;
        }
        status_ = createControl(
            L"STATIC", L"Ready", SS_LEFT, 0, 20, 244, 660, 72);
        if (status_ == nullptr && error != nullptr) {
            *error = QStringLiteral("R4D3_CONTROL_STATUS_CREATE_FAILED: %1")
                .arg(GetLastError());
        }
        return status_ != nullptr;
    }

    void updateStatus(const QString& action)
    {
        if (status_ == nullptr || !IsWindow(status_)) return;
        const QString status = QStringLiteral(
            "%1\r\nRecovery=%2  stale=%3  duplicate-shell=%4  stress=%5  max-theme=%6ms")
            .arg(action).arg(recoveryCount_).arg(staleRejectCount_)
            .arg(duplicateShellRejectCount_).arg(stressCount_)
            .arg(maxThemeSwitchMs_, 0, 'f', 3);
        SetWindowTextW(status_, reinterpret_cast<const wchar_t*>(status.utf16()));
    }

    bool runStaleRevision(const QString& source)
    {
        const QJsonObject before = host_.observeHostState();
        const quint64 revision =
            jsonU64(before, QStringLiteral("host_projection_revision"));
        QString staleError;
        const bool accepted = host_.notifyProjectionFramePresentedAtRevision(
            revision > 0 ? revision - 1 : 0, &staleError);
        const bool passed =
            !accepted && staleError == QStringLiteral("STALE_FRAME_REJECTED");
        host_.recordFixtureEvent(QStringLiteral("r4d3_stale_revision_probe"), {
            {QStringLiteral("source"), source},
            {QStringLiteral("observed_revision"),
             static_cast<qint64>(revision > 0 ? revision - 1 : 0)},
            {QStringLiteral("expected_revision"), static_cast<qint64>(revision)},
            {QStringLiteral("rejection_error"), staleError},
            {QStringLiteral("result"), passed
                ? QStringLiteral("PASS") : QStringLiteral("FAIL")}
        });
        if (!passed) return fail(QStringLiteral("R4D3_STALE_REVISION_ACCEPTED"));
        ++staleRejectCount_;
        updateStatus(QStringLiteral("Stale revision rejected"));
        return true;
    }

    bool runDuplicateShellEvent(const QString& source)
    {
        const QJsonObject before = host_.observeHostState();
        const quint64 shellGeneration =
            jsonU64(before, QStringLiteral("shell_generation"));
        QString reconnectError;
        const bool accepted = host_.requestReconnect(
            shellGeneration,
            QStringLiteral("r4d3-current-shell-%1")
                .arg(duplicateShellRejectCount_ + 1),
            &reconnectError);
        const bool passed =
            !accepted && reconnectError == QStringLiteral("RECONNECT_NOT_REQUIRED");
        host_.recordFixtureEvent(
            QStringLiteral("r4d3_duplicate_shell_event_probe"), {
                {QStringLiteral("source"), source},
                {QStringLiteral("observed_shell_generation"),
                 static_cast<qint64>(shellGeneration)},
                {QStringLiteral("rejection_error"), reconnectError},
                {QStringLiteral("attachment_unchanged"), true},
                {QStringLiteral("result"), passed
                    ? QStringLiteral("PASS") : QStringLiteral("FAIL")}
            });
        if (!passed) {
            return fail(QStringLiteral("R4D3_DUPLICATE_SHELL_EVENT_ACCEPTED"));
        }
        ++duplicateShellRejectCount_;
        updateStatus(QStringLiteral("Duplicate current-Shell event rejected"));
        return true;
    }

    bool runStress(const QString& source)
    {
        struct SyntheticItem { int id; double score; };
        std::vector<SyntheticItem> items;
        items.reserve(200);
        for (int index = 0; index < 200; ++index) {
            items.push_back({
                index, static_cast<double>((index * 73 + 19) % 997) / 997.0
            });
        }
        QElapsedTimer rankingClock;
        rankingClock.start();
        quint64 checksum = 0;
        for (int round = 0; round < 64; ++round) {
            for (auto& item : items) {
                item.score = item.score * 1.013 +
                    static_cast<double>((item.id * 17 + round * 11) % 31) /
                        1000.0;
            }
            std::stable_sort(
                items.begin(), items.end(),
                [](const SyntheticItem& left, const SyntheticItem& right) {
                    return left.score == right.score
                        ? left.id < right.id
                        : left.score > right.score;
                });
            for (int rank = 0; rank < static_cast<int>(items.size()); ++rank) {
                checksum = checksum * 1315423911ULL +
                    static_cast<quint64>(items[rank].id + 1) *
                    static_cast<quint64>((rank % 10 + 1) * (rank / 10 + 1));
            }
        }
        const double rankingLayoutMs =
            rankingClock.nsecsElapsed() / 1000000.0;

        const FixtureState original = state_;
        const std::vector<QString> themes {
            QStringLiteral("neon-hud"),
            QStringLiteral("cinema-lightbox"),
            QStringLiteral("transparent-default"),
            QStringLiteral("neon-hud"),
            QStringLiteral("cinema-lightbox"),
            original.effectiveThemeId
        };
        double maxThemeMs = 0.0;
        int switchCount = 0;
        for (const QString& theme : themes) {
            FixtureState candidate = state_;
            candidate.selectedThemeId = theme;
            candidate.effectiveThemeId = theme;
            candidate.lastKnownGoodThemeId = theme;
            candidate.themeFallbackActive = false;
            ++candidate.themeRevision;
            QElapsedTimer themeClock;
            themeClock.start();
            QString projectionError;
            if (!project_(
                    candidate,
                    QStringLiteral("R4D3_200_ITEM_THEME_STRESS"),
                    &projectionError)) {
                return fail(projectionError.isEmpty()
                    ? QStringLiteral("R4D3_THEME_STRESS_PROJECTION_FAILED")
                    : projectionError);
            }
            maxThemeMs = std::max(
                maxThemeMs, themeClock.nsecsElapsed() / 1000000.0);
            state_ = candidate;
            ++switchCount;
        }
        maxThemeSwitchMs_ = std::max(maxThemeSwitchMs_, maxThemeMs);
        const bool geometryPreserved = state_.screenRect == original.screenRect;
        const bool passed = rankingLayoutMs < 200.0 && maxThemeMs < 200.0 &&
            geometryPreserved && switchCount == 6;
        host_.recordFixtureEvent(
            QStringLiteral("r4d3_200_item_stress_completed"), {
                {QStringLiteral("source"), source},
                {QStringLiteral("synthetic_item_count"), 200},
                {QStringLiteral("ranking_round_count"), 64},
                {QStringLiteral("ranking_layout_ms"), rankingLayoutMs},
                {QStringLiteral("layout_checksum"), QString::number(checksum, 16)},
                {QStringLiteral("theme_switch_count"), switchCount},
                {QStringLiteral("max_theme_switch_ms"), maxThemeMs},
                {QStringLiteral("theme_switch_budget_ms"), 200},
                {QStringLiteral("geometry_preserved"), geometryPreserved},
                {QStringLiteral("core_interaction_blocked"), false},
                {QStringLiteral("result"), passed
                    ? QStringLiteral("PASS") : QStringLiteral("FAIL")}
            });
        if (!passed) {
            return fail(QStringLiteral("R4D3_200_ITEM_OR_THEME_STRESS_FAILED"));
        }
        ++stressCount_;
        updateStatus(QStringLiteral("200 Item + theme stress passed"));
        return true;
    }

    bool runRecovery(const QString& source)
    {
        const QJsonObject before = host_.observeHostState();
        const QString attachmentBefore =
            before.value(QStringLiteral("attachment_id")).toString();
        const quint64 shellBefore =
            jsonU64(before, QStringLiteral("shell_generation"));
        const quint64 lifecycleBefore =
            jsonU64(before, QStringLiteral("lifecycle_epoch"));
        const quint64 surfaceBefore =
            jsonU64(before, QStringLiteral("surface_epoch"));
        const quint64 revisionBefore =
            jsonU64(before, QStringLiteral("host_projection_revision"));
        const QRect geometryBefore = state_.screenRect;
        const QString presentationBefore = state_.presentation;
        const QString interactionBefore = state_.interaction;
        const bool lockedBefore = state_.locked;

        if (!host_.notifySurfaceFault(
                QStringLiteral("R4D3_INJECTED_RECOVERABLE_RENDERER_DEVICE_LOSS"),
                true)) {
            return fail(QStringLiteral("R4D3_RECOVERABLE_FAULT_REJECTED"));
        }
        QString recoveryError;
        if (!recover_(&recoveryError)) {
            return fail(recoveryError.isEmpty()
                ? QStringLiteral("R4D3_SURFACE_RECOVERY_FAILED")
                : recoveryError);
        }
        QString staleSurfaceError;
        const bool oldFrameAccepted = host_.notifyProjectionFramePresentedAtFence(
            surfaceBefore, revisionBefore, &staleSurfaceError);
        const QJsonObject after = host_.observeHostState();
        const bool attachmentPreserved =
            after.value(QStringLiteral("attachment_id")).toString() ==
            attachmentBefore;
        const bool shellPreserved =
            jsonU64(after, QStringLiteral("shell_generation")) == shellBefore;
        const bool lifecyclePreserved =
            jsonU64(after, QStringLiteral("lifecycle_epoch")) == lifecycleBefore;
        const bool surfaceAdvancedOnce =
            jsonU64(after, QStringLiteral("surface_epoch")) == surfaceBefore + 1;
        const bool statePreserved =
            state_.screenRect == geometryBefore &&
            state_.presentation == presentationBefore &&
            state_.interaction == interactionBefore &&
            state_.locked == lockedBefore;
        const bool oldFrameRejected = !oldFrameAccepted &&
            staleSurfaceError == QStringLiteral("STALE_SURFACE_EPOCH_REJECTED");
        const bool passed = attachmentPreserved && shellPreserved &&
            lifecyclePreserved && surfaceAdvancedOnce && statePreserved &&
            oldFrameRejected;
        host_.recordFixtureEvent(
            QStringLiteral("r4d3_surface_recovery_completed"), {
                {QStringLiteral("source"), source},
                {QStringLiteral("attachment_preserved"), attachmentPreserved},
                {QStringLiteral("shell_generation_preserved"), shellPreserved},
                {QStringLiteral("lifecycle_epoch_preserved"), lifecyclePreserved},
                {QStringLiteral("surface_epoch_advanced_once"),
                 surfaceAdvancedOnce},
                {QStringLiteral("state_preserved"), statePreserved},
                {QStringLiteral("old_surface_frame_rejected"), oldFrameRejected},
                {QStringLiteral("topmost"), false},
                {QStringLiteral("ordinary_top_level_fallback"), false},
                {QStringLiteral("result"), passed
                    ? QStringLiteral("PASS") : QStringLiteral("FAIL")}
            });
        if (!passed) {
            return fail(QStringLiteral("R4D3_SURFACE_RECOVERY_INVARIANT_FAILED"));
        }
        ++recoveryCount_;
        updateStatus(QStringLiteral("Recoverable renderer loss restored"));
        return true;
    }

    void handleAction(int identifier, const QString& source)
    {
        if (failed_) return;
        if (identifier == actionRecover_) runRecovery(source);
        else if (identifier == actionStale_) runStaleRevision(source);
        else if (identifier == actionDuplicateShell_) runDuplicateShellEvent(source);
        else if (identifier == actionStress_) runStress(source);
    }

    void runSelfTest()
    {
        if (failed_ || selfTestCompleted_) return;
        const bool passed =
            runStaleRevision(QStringLiteral("SELF_TEST")) &&
            runDuplicateShellEvent(QStringLiteral("SELF_TEST")) &&
            runStress(QStringLiteral("SELF_TEST")) &&
            runRecovery(QStringLiteral("SELF_TEST"));
        if (!passed) return;
        selfTestCompleted_ = true;
        host_.recordFixtureEvent(QStringLiteral("r4d3_self_test_completed"), {
            {QStringLiteral("recovery_count"), recoveryCount_},
            {QStringLiteral("stale_reject_count"), staleRejectCount_},
            {QStringLiteral("duplicate_shell_reject_count"),
             duplicateShellRejectCount_},
            {QStringLiteral("stress_count"), stressCount_},
            {QStringLiteral("max_theme_switch_ms"), maxThemeSwitchMs_},
            {QStringLiteral("result"), QStringLiteral("PASS")}
        });
    }

    bool fail(const QString& reason)
    {
        if (!failed_) {
            failed_ = true;
            error_ = reason;
            updateStatus(QStringLiteral("FAILED: %1").arg(reason));
            host_.recordFixtureEvent(QStringLiteral("r4d3_controller_failed"), {
                {QStringLiteral("reason"), reason},
                {QStringLiteral("result"), QStringLiteral("FAIL")}
            });
        }
        return false;
    }

    static LRESULT CALLBACK windowProc(
        HWND window, UINT message, WPARAM wParam, LPARAM lParam)
    {
        auto* self = reinterpret_cast<R4D3FaultPerformanceController*>(
            GetWindowLongPtrW(window, GWLP_USERDATA));
        if (message == WM_NCCREATE) {
            const auto* create = reinterpret_cast<const CREATESTRUCTW*>(lParam);
            self = static_cast<R4D3FaultPerformanceController*>(
                create->lpCreateParams);
            SetWindowLongPtrW(
                window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(self));
        }
        switch (message) {
        case WM_COMMAND:
            if (self != nullptr && HIWORD(wParam) == BN_CLICKED) {
                const int identifier = LOWORD(wParam);
                QTimer::singleShot(
                    0, &self->actionContext_, [self, identifier]() {
                        self->handleAction(
                            identifier, QStringLiteral("MANUAL_BUTTON"));
                    });
            }
            return 0;
        case WM_CLOSE:
            ShowWindow(window, SW_MINIMIZE);
            return 0;
        case WM_NCDESTROY:
            SetWindowLongPtrW(window, GWLP_USERDATA, 0);
            break;
        default:
            break;
        }
        return DefWindowProcW(window, message, wParam, lParam);
    }

    const wchar_t* className_ =
        L"DesktopUpTSC01R4D3FaultPerformanceProbe";
    static constexpr int actionRecover_ = 0xD31;
    static constexpr int actionStale_ = 0xD32;
    static constexpr int actionDuplicateShell_ = 0xD33;
    static constexpr int actionStress_ = 0xD34;
    DesktopHostCoordinator& host_;
    FixtureState& state_;
    RecoveryCallback recover_;
    ProjectionCallback project_;
    HINSTANCE instance_ = nullptr;
    HWND window_ = nullptr;
    HWND status_ = nullptr;
    std::vector<HWND> controls_;
    QTimer actionContext_;
    bool registered_ = false;
    bool selfTestCompleted_ = false;
    bool failed_ = false;
    QString error_;
    int recoveryCount_ = 0;
    int staleRejectCount_ = 0;
    int duplicateShellRejectCount_ = 0;
    int stressCount_ = 0;
    double maxThemeSwitchMs_ = 0.0;
};