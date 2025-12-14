cmake_minimum_required(VERSION 3.21)

project(RunewireNative LANGUAGES CXX)

# Enable modern C++ and warnings. We can specialize per-compiler later.
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

include(CTest)

add_subdirectory(Runewire.Injector)
