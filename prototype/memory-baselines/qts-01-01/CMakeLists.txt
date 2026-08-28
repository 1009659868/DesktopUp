cmake_minimum_required(VERSION 3.25)

project(desktopup_qts_01_01_memory_baselines VERSION 0.1.0 LANGUAGES CXX)

find_package(Qt6 6.8.3 EXACT REQUIRED COMPONENTS Gui Quick Qml)
qt_standard_project_setup(REQUIRES 6.8)

function(desktopup_qts_baseline target)
    target_compile_features(${target} PRIVATE cxx_std_20)
    target_compile_definitions(${target} PRIVATE
        UNICODE
        _UNICODE
        WIN32_LEAN_AND_MEAN
        NOMINMAX
    )
    if(MSVC)
        target_compile_options(${target} PRIVATE /W4 /permissive- /utf-8)
    endif()
    set_target_properties(${target} PROPERTIES WIN32_EXECUTABLE FALSE)
endfunction()

qt_add_executable(qts-01-01-qt-gui src/qt_gui_baseline.cpp)
target_include_directories(qts-01-01-qt-gui PRIVATE src)
target_link_libraries(qts-01-01-qt-gui PRIVATE Qt6::Gui)
desktopup_qts_baseline(qts-01-01-qt-gui)

qt_add_executable(qts-01-01-minimal-qt-quick src/minimal_qt_quick_baseline.cpp)
target_include_directories(qts-01-01-minimal-qt-quick PRIVATE src)
target_link_libraries(qts-01-01-minimal-qt-quick PRIVATE Qt6::Quick Qt6::Qml)
desktopup_qts_baseline(qts-01-01-minimal-qt-quick)

qt_add_executable(qts-01-01-offscreen-renderer src/offscreen_renderer_baseline.cpp)
target_include_directories(qts-01-01-offscreen-renderer PRIVATE src)
target_link_libraries(qts-01-01-offscreen-renderer PRIVATE Qt6::Quick Qt6::Qml)
desktopup_qts_baseline(qts-01-01-offscreen-renderer)
