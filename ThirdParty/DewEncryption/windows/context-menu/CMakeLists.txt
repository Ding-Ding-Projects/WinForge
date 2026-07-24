cmake_minimum_required(VERSION 3.20)

project(DewEncryptionExplorerCommand LANGUAGES CXX)

add_library(DewEncryptionExplorerCommand SHARED
    DewExplorerCommand.cpp
    DewExplorerCommand.def
)

target_compile_definitions(DewEncryptionExplorerCommand PRIVATE
    UNICODE
    _UNICODE
    WIN32_LEAN_AND_MEAN
    NOMINMAX
)

target_compile_features(DewEncryptionExplorerCommand PRIVATE cxx_std_17)

target_link_libraries(DewEncryptionExplorerCommand PRIVATE
    advapi32
    ole32
    runtimeobject
    shell32
)
