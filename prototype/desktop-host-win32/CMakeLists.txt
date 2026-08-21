cmake_minimum_required(VERSION 3.25)

project(desktop_host_probe VERSION 0.1.0 LANGUAGES CXX)

add_executable(desktop-host-probe src/main.cpp)
target_compile_features(desktop-host-probe PRIVATE cxx_std_20)
target_compile_definitions(desktop-host-probe PRIVATE
  UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX
  TS_H01A_BUILD_ID="${PROJECT_VERSION}"
)

if(MSVC)
  target_compile_options(desktop-host-probe PRIVATE /W4 /permissive- /EHsc /utf-8)
  set_property(TARGET desktop-host-probe PROPERTY
    MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>DLL")
endif()

target_link_libraries(desktop-host-probe PRIVATE dwmapi user32)
