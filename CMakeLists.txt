cmake_minimum_required(VERSION 3.20)
project(BuldogBoost VERSION 0.2.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Усі DLL/exe в одну папку: build/bin/<Config>
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin/$<CONFIG>")
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin/$<CONFIG>")

if(MSVC)
    add_compile_options(/W4 /permissive- /utf-8)
else()
    add_compile_options(-Wall -Wextra)
endif()

enable_testing()

add_subdirectory(engine-cpp)       # Optimization Engine
if(WIN32)
    add_subdirectory(render-d2d)   # Direct2D (лише Windows)
endif()
