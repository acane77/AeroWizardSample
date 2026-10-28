cmake_minimum_required(VERSION 3.20)

project(AeroWizard LANGUAGES CXX)

if(NOT WIN32)
    message(FATAL_ERROR "Aero Wizard 示例只能在 Windows 上构建。")
endif()

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# 清单由 app.rc 以 RT_MANIFEST 嵌入。comctl32 6.0 和 PerMonitorV2 都写在 app.manifest 里。
add_executable(AeroWizard WIN32
    src/main.cpp
    src/AeroWizard.cpp
    app.rc
)

target_include_directories(AeroWizard PRIVATE
    "${CMAKE_CURRENT_SOURCE_DIR}/src"
    "${CMAKE_CURRENT_SOURCE_DIR}"
)

target_compile_definitions(AeroWizard PRIVATE
    UNICODE
    _UNICODE
    WIN32_LEAN_AND_MEAN
    NOMINMAX
    _WIN32_WINNT=0x0A00
    WINVER=0x0A00
)

target_link_libraries(AeroWizard PRIVATE
    comctl32
    user32
    ole32
)

if(MSVC)
    target_compile_options(AeroWizard PRIVATE /W4 /utf-8)
    # 链接器默认清单会和 app.rc 里的 RT_MANIFEST 冲突。
    target_link_options(AeroWizard PRIVATE "/MANIFEST:NO")
endif()
