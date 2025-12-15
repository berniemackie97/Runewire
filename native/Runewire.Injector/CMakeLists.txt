cmake_minimum_required(VERSION 3.21)
project(RunewireInjector LANGUAGES C CXX)

include(CTest)

if(NOT WIN32)
  message(FATAL_ERROR "Runewire.Injector currently only supports Windows builds.")
endif()

# Inherit C++ standard from parent, but enforce if someone builds standalone.
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

add_library(Runewire.Injector SHARED
    src/runewire_injector.cpp
    src/technique_dispatch.cpp
    src/param_parser.cpp
    src/process_utils.cpp
    src/payload_utils.cpp
    src/handler_threads.cpp
    src/thread_utils.cpp
    src/handler_payloads.cpp
    src/handler_redirects.cpp
)

target_include_directories(Runewire.Injector
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/include
        ${CMAKE_CURRENT_SOURCE_DIR}/third_party
)

# Produce exactly Runewire.Injector.dll (no 'lib' prefix)
set_target_properties(Runewire.Injector PROPERTIES
    OUTPUT_NAME "Runewire.Injector"
    PREFIX ""
)

if(BUILD_TESTING)
    enable_testing()
endif()

if(BUILD_TESTING)
    add_subdirectory(tests)
endif()
