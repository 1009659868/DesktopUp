set(DESKTOPUP_DESKTOP_HOST_MANIFEST_RESOURCE platform/windows/desktop_host.rc)

add_library(desktopup_contracts INTERFACE)
target_include_directories(desktopup_contracts INTERFACE "${CMAKE_CURRENT_SOURCE_DIR}")

add_library(desktopup_core STATIC
  core/display_geometry.cpp
  core/display_geometry.h
  core/geometry_gesture.cpp
  core/geometry_gesture.h
  core/runtime_core.cpp
  core/runtime_core.h
)
target_link_libraries(desktopup_core PUBLIC desktopup_contracts)

add_library(desktopup_state_store_sqlite STATIC
  adapters/state-store-sqlite/sqlite_state_store.cpp
  adapters/state-store-sqlite/sqlite_state_store.h
  adapters/state-store-sqlite/state_store_worker.cpp
  adapters/state-store-sqlite/state_store_worker.h
)
target_link_libraries(desktopup_state_store_sqlite PUBLIC desktopup_contracts PRIVATE winsqlite3)

add_library(desktopup_runtime_ipc_win32 STATIC
  adapters/runtime-ipc-win32/runtime_ipc.cpp
  adapters/runtime-ipc-win32/runtime_ipc.h
)
target_compile_definitions(desktopup_runtime_ipc_win32 PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)
target_link_libraries(desktopup_runtime_ipc_win32 PUBLIC desktopup_contracts PRIVATE advapi32)

add_library(desktopup_desktop_host_win32 STATIC
  adapters/desktop-host-win32/desktop_host_fixture.cpp
  adapters/desktop-host-win32/desktop_host_fixture.h
  adapters/desktop-host-win32/win32_desktop_host.cpp
  adapters/desktop-host-win32/win32_desktop_host.h
  adapters/desktop-host-win32/win32_input_islands.cpp
  adapters/desktop-host-win32/win32_input_islands.h
)
target_compile_definitions(desktopup_desktop_host_win32 PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)
target_link_libraries(desktopup_desktop_host_win32 PUBLIC desktopup_contracts PRIVATE user32 gdi32 dwmapi)

add_library(desktopup_renderer_qpainter STATIC
  adapters/renderer-qpainter/qpainter_renderer.cpp
  adapters/renderer-qpainter/qpainter_renderer.h
)
target_link_libraries(desktopup_renderer_qpainter PUBLIC desktopup_contracts PRIVATE Qt6::Gui)

add_library(desktopup_renderer_qtquick STATIC
  adapters/renderer-qtquick/qtquick_renderer.cpp
  adapters/renderer-qtquick/qtquick_renderer.h
)
qt_add_resources(desktopup_renderer_qtquick desktopup_renderer_qml
  PREFIX "/DesktopUp/Renderer"
  BASE "${CMAKE_CURRENT_SOURCE_DIR}/adapters/renderer-qtquick"
  FILES adapters/renderer-qtquick/NightOpsPoster.qml
)
target_link_libraries(desktopup_renderer_qtquick
  PUBLIC desktopup_contracts
  PRIVATE
    Qt6::Core
    Qt6::Gui
    Qt6::Quick
    Qt6::Qml
    desktopup_renderer_qpainter
)

add_library(desktopup_diagnostics STATIC
  adapters/diagnostics/resource_ledger.cpp
  adapters/diagnostics/resource_ledger.h
)
target_link_libraries(desktopup_diagnostics PUBLIC desktopup_contracts)

add_executable(desktopup-runtime
  apps/runtime/main.cpp
  ${DESKTOPUP_DESKTOP_HOST_MANIFEST_RESOURCE}
)
target_link_libraries(desktopup-runtime PRIVATE
  Qt6::Core
  Qt6::Gui
  desktopup_contracts
  desktopup_core
  desktopup_state_store_sqlite
  desktopup_runtime_ipc_win32
  desktopup_desktop_host_win32
  desktopup_renderer_qtquick
  desktopup_diagnostics
)
if(MSVC)
  target_link_options(desktopup-runtime PRIVATE /MANIFEST:NO)
endif()

qt_add_executable(desktopup-control-center
  apps/control-center/main.cpp
)
qt_add_qml_module(desktopup-control-center
  URI DesktopUp.ControlCenter
  VERSION 1.0
  QML_FILES apps/control-center/Main.qml
)
target_link_libraries(desktopup-control-center PRIVATE
  Qt6::Core
  Qt6::Gui
  Qt6::Quick
  Qt6::Qml
  Qt6::QuickControls2
  desktopup_contracts
  desktopup_runtime_ipc_win32
)

if(BUILD_TESTING)
  add_executable(desktopup-contract-tests tests/contracts/contract_tests.cpp)
  target_link_libraries(desktopup-contract-tests PRIVATE desktopup_core)
  add_test(NAME qts03.contracts COMMAND desktopup-contract-tests)

  add_executable(desktopup-geometry-gesture-tests
    tests/core/geometry_gesture_tests.cpp)
  target_link_libraries(desktopup-geometry-gesture-tests PRIVATE desktopup_core)
  add_test(NAME qts04.geometry-gesture COMMAND desktopup-geometry-gesture-tests)

  add_executable(desktopup-state-store-tests tests/contracts/state_store_tests.cpp)
  target_link_libraries(desktopup-state-store-tests PRIVATE desktopup_state_store_sqlite)
  add_test(NAME qts03.state-store COMMAND desktopup-state-store-tests)

  add_executable(desktopup-ipc-tests tests/contracts/ipc_tests.cpp)
  target_link_libraries(desktopup-ipc-tests PRIVATE desktopup_runtime_ipc_win32)
  add_test(NAME qts03.ipc COMMAND desktopup-ipc-tests)

  add_executable(desktopup-lifecycle-tests tests/lifecycle/lifecycle_tests.cpp)
  target_link_libraries(desktopup-lifecycle-tests PRIVATE
    Qt6::Core
    Qt6::Gui
    desktopup_core
    desktopup_desktop_host_win32
    desktopup_renderer_qpainter
    desktopup_diagnostics
  )
  add_test(NAME qts03.lifecycle COMMAND desktopup-lifecycle-tests)

  add_executable(desktopup-unrelated-window-event-source WIN32
    tests/desktop-host/unrelated_window_event_source.cpp
  )
  target_compile_definitions(desktopup-unrelated-window-event-source PRIVATE
    UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)

  add_executable(desktopup-host-attach-reconnect-tests
    tests/desktop-host/attach_reconnect_tests.cpp
    ${DESKTOPUP_DESKTOP_HOST_MANIFEST_RESOURCE}
  )
  target_compile_definitions(desktopup-host-attach-reconnect-tests PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)
  target_link_libraries(desktopup-host-attach-reconnect-tests PRIVATE
    Qt6::Core
    Qt6::Gui
    desktopup_desktop_host_win32
    desktopup_renderer_qpainter
    user32
  )
  if(MSVC)
    target_link_options(desktopup-host-attach-reconnect-tests PRIVATE /MANIFEST:NO)
  endif()
  add_test(NAME qts04.host-attach-reconnect
    COMMAND desktopup-host-attach-reconnect-tests
      --duration-ms 80
      --event-source "$<TARGET_FILE:desktopup-unrelated-window-event-source>")

  add_executable(desktopup-input-islands-tests
    tests/desktop-host/input_islands_tests.cpp
    ${DESKTOPUP_DESKTOP_HOST_MANIFEST_RESOURCE}
  )
  target_compile_definitions(desktopup-input-islands-tests PRIVATE
    UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)
  target_link_libraries(desktopup-input-islands-tests PRIVATE
    Qt6::Core
    Qt6::Gui
    desktopup_desktop_host_win32
    desktopup_renderer_qpainter
    user32
  )
  if(MSVC)
    target_link_options(desktopup-input-islands-tests PRIVATE /MANIFEST:NO)
  endif()
  add_test(NAME qts04.input-islands COMMAND desktopup-input-islands-tests)

  add_executable(desktopup-night-ops-renderer-tests
    tests/renderer/night_ops_renderer_tests.cpp
  )
  target_link_libraries(desktopup-night-ops-renderer-tests PRIVATE
    Qt6::Core
    Qt6::Gui
    desktopup_renderer_qpainter
  )
  add_test(NAME qts04.night-ops-renderer
    COMMAND desktopup-night-ops-renderer-tests)

  add_executable(desktopup-qtquick-renderer-tests
    tests/renderer/qtquick_renderer_tests.cpp
  )
  target_link_libraries(desktopup-qtquick-renderer-tests PRIVATE
    Qt6::Core
    Qt6::Gui
    Qt6::Quick
    Qt6::Qml
    desktopup_renderer_qtquick
  )
  add_test(NAME qts04.qtquick-renderer
    COMMAND desktopup-qtquick-renderer-tests)
  set_tests_properties(qts04.qtquick-renderer PROPERTIES
    ENVIRONMENT "QT_QUICK_BACKEND=software")

  add_test(
    NAME qts03.architecture
    COMMAND powershell -NoProfile -ExecutionPolicy Bypass
      -File "${CMAKE_CURRENT_SOURCE_DIR}/tests/architecture/verify_boundaries.ps1"
      -SourceRoot "${CMAKE_SOURCE_DIR}"
  )

  set(QTS03_FIXTURE_DIR "${CMAKE_BINARY_DIR}/qts03-exit-fixture")
  add_test(
    NAME qts03.runtime-exit
    COMMAND desktopup-runtime
      --state-db "${QTS03_FIXTURE_DIR}/state.sqlite"
      --pipe-name "DesktopUp.QTS03.ExitFixture"
      --instance-name "DesktopUp.QTS03.ExitFixture"
      --ledger "${QTS03_FIXTURE_DIR}/ledger.json"
      --exit-after-ms 150
  )
  set_tests_properties(qts03.runtime-exit PROPERTIES
    FIXTURES_SETUP qts03_runtime_exited
  )

  add_test(
    NAME qts03.exit-ledger
    COMMAND powershell -NoProfile -ExecutionPolicy Bypass
      -File "${CMAKE_CURRENT_SOURCE_DIR}/tests/lifecycle/verify_exit_ledger.ps1"
      -LedgerPath "${QTS03_FIXTURE_DIR}/ledger.json"
  )
  set_tests_properties(qts03.exit-ledger PROPERTIES
    FIXTURES_REQUIRED qts03_runtime_exited
  )

  add_test(NAME qts03.control-center-close COMMAND desktopup-control-center --smoke-ms 100)

  add_test(
    NAME qts03.process-roles
    COMMAND powershell -NoProfile -ExecutionPolicy Bypass
      -File "${CMAKE_CURRENT_SOURCE_DIR}/tests/lifecycle/verify_process_roles.ps1"
      -RuntimePath "$<TARGET_FILE:desktopup-runtime>"
      -ControlCenterPath "$<TARGET_FILE:desktopup-control-center>"
      -FixtureRoot "${CMAKE_BINARY_DIR}/qts03-process-roles"
  )
endif()
