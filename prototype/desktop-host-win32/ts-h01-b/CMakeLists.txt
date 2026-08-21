cmake_minimum_required(VERSION 3.25)

project(desktop_host_layer_probe LANGUAGES CXX RC)

add_executable(desktop-host-layer-probe
  src/main.cpp
  src/probe.rc
)

target_compile_features(desktop-host-layer-probe PRIVATE cxx_std_20)
target_compile_definitions(desktop-host-layer-probe PRIVATE
  UNICODE
  _UNICODE
  WIN32_LEAN_AND_MEAN
  NOMINMAX
)

target_compile_options(desktop-host-layer-probe PRIVATE
  /W4
  /permissive-
  /utf-8
)

target_link_options(desktop-host-layer-probe PRIVATE
  /MANIFEST:NO
)

target_link_libraries(desktop-host-layer-probe PRIVATE
  dwmapi
  shcore
)
