cmake_minimum_required(VERSION 3.25)

project(desktopup_ts_c01_qt_entry VERSION 0.1.0 LANGUAGES CXX RC)

find_package(Qt6 6.8.3 EXACT REQUIRED COMPONENTS Quick Qml)

qt_standard_project_setup(REQUIRES 6.8)

qt_add_executable(ts-c01-qt-entry
    src/main.cpp
)

qt_add_qml_module(ts-c01-qt-entry
    URI DesktopUp.TSC01.Entry
    VERSION 1.0
    QML_FILES
        qml/Main.qml
)

target_compile_features(ts-c01-qt-entry PRIVATE cxx_std_20)
target_compile_definitions(ts-c01-qt-entry PRIVATE UNICODE _UNICODE)

if(MSVC)
    target_compile_options(ts-c01-qt-entry PRIVATE /W4 /permissive- /utf-8)
endif()

target_link_libraries(ts-c01-qt-entry PRIVATE Qt6::Quick Qt6::Qml)

# R0 keeps a console subsystem so the structured self-check is observable.
# The real R1 adapter chooses its subsystem only after the DesktopHost boundary exists.
set_target_properties(ts-c01-qt-entry PROPERTIES WIN32_EXECUTABLE FALSE)

qt_add_executable(ts-c01-qt-r1
    src/r1_main.cpp
    adapter/desktop_host_contract.cpp
    adapter/desktop_host_contract.h
)

qt_add_qml_module(ts-c01-qt-r1
    URI DesktopUp.TSC01.R1
    VERSION 1.0
    QML_FILES
        qml/R1Surface.qml
)

target_include_directories(ts-c01-qt-r1 PRIVATE adapter)
target_compile_features(ts-c01-qt-r1 PRIVATE cxx_std_20)
target_compile_definitions(ts-c01-qt-r1 PRIVATE
    UNICODE
    _UNICODE
    WIN32_LEAN_AND_MEAN
    NOMINMAX
)

if(MSVC)
    target_compile_options(ts-c01-qt-r1 PRIVATE /W4 /permissive- /utf-8)
endif()

target_link_libraries(ts-c01-qt-r1 PRIVATE Qt6::Quick Qt6::Qml dwmapi)
set_target_properties(ts-c01-qt-r1 PROPERTIES WIN32_EXECUTABLE FALSE)

qt_add_executable(ts-c01-qt-r2
    src/r2_main.cpp
    src/r4d3_controller.inc
    src/r4d4_lifecycle.inc
    src/r2.rc
    adapter/desktop_host_contract.cpp
    adapter/desktop_host_contract.h
)

qt_add_qml_module(ts-c01-qt-r2
    URI DesktopUp.TSC01.R2
    VERSION 1.0
    QML_FILES
        qml/R2Surface.qml
)

target_include_directories(ts-c01-qt-r2 PRIVATE adapter)
target_compile_features(ts-c01-qt-r2 PRIVATE cxx_std_20)
target_compile_definitions(ts-c01-qt-r2 PRIVATE
    UNICODE
    _UNICODE
    WIN32_LEAN_AND_MEAN
    NOMINMAX
    DESKTOPUP_TS_C01_BUILD_ID=\"ts-c01-qt-6.8.3-r4d4-20260826-02\"
)

if(MSVC)
    target_compile_options(ts-c01-qt-r2 PRIVATE
        /W4
        /permissive-
        /utf-8
        /Brepro
        /experimental:deterministic
        "/pathmap:${CMAKE_BINARY_DIR}=."
    )
endif()

target_link_options(ts-c01-qt-r2 PRIVATE /MANIFEST:NO /Brepro)
target_link_libraries(ts-c01-qt-r2 PRIVATE Qt6::Quick Qt6::Qml dwmapi shell32 advapi32)
set_target_properties(ts-c01-qt-r2 PROPERTIES WIN32_EXECUTABLE FALSE)
