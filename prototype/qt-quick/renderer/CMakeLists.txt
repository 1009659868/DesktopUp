cmake_minimum_required(VERSION 3.21)
project(poster_renderer LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_AUTOMOC ON)

find_package(Qt6 6.8 REQUIRED COMPONENTS Quick)

qt_standard_project_setup(REQUIRES 6.8)

qt_add_executable(poster-renderer
    src/main.cpp
)

qt_add_qml_module(poster-renderer
    URI PosterRenderer
    VERSION 1.0
    QML_FILES
        qml/Main.qml
)

target_link_libraries(poster-renderer PRIVATE Qt6::Quick)
