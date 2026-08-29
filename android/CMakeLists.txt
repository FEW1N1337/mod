# FEW1N Mod — Android native kütüphane (libfew1nmod.so, arm64-v8a)
cmake_minimum_required(VERSION 3.22)
project(few1nmod CXX C)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
add_compile_options(-fvisibility=hidden -fno-exceptions -fno-rtti -O2 -Wall)

include(FetchContent)

# ---- Dear ImGui (CMakeLists'i yok -> sadece indir, target'i biz kurarız) ----
FetchContent_Declare(imgui
  GIT_REPOSITORY https://github.com/ocornut/imgui.git
  GIT_TAG        v1.90.9)
FetchContent_GetProperties(imgui)
if(NOT imgui_POPULATED)
  FetchContent_Populate(imgui)
endif()

add_library(imgui STATIC
  ${imgui_SOURCE_DIR}/imgui.cpp
  ${imgui_SOURCE_DIR}/imgui_draw.cpp
  ${imgui_SOURCE_DIR}/imgui_tables.cpp
  ${imgui_SOURCE_DIR}/imgui_widgets.cpp
  ${imgui_SOURCE_DIR}/backends/imgui_impl_opengl3.cpp
  ${imgui_SOURCE_DIR}/backends/imgui_impl_android.cpp)
target_include_directories(imgui PUBLIC ${imgui_SOURCE_DIR} ${imgui_SOURCE_DIR}/backends)
target_compile_definitions(imgui PUBLIC IMGUI_IMPL_OPENGL_ES3)
# ImGui exception'sız derlensin diye rtti/exceptions kapalı bırakıyoruz (backend'ler uyumlu)

# ---- Dobby (inline hook) ----
set(DOBBY_GENERATE_SHARED OFF CACHE BOOL "" FORCE)
set(DOBBY_DEBUG OFF CACHE BOOL "" FORCE)
FetchContent_Declare(dobby
  GIT_REPOSITORY https://github.com/jmpews/Dobby.git
  GIT_TAG        master)
FetchContent_MakeAvailable(dobby)

# ---- Mod kütüphanesi ----
add_library(few1nmod SHARED
  jni/main.cpp
  jni/il2cpp.cpp
  jni/features.cpp
  jni/overlay.cpp)

target_include_directories(few1nmod PRIVATE
  ${imgui_SOURCE_DIR} ${imgui_SOURCE_DIR}/backends
  ${dobby_SOURCE_DIR}/include)

target_link_libraries(few1nmod
  imgui dobby
  EGL GLESv3 android log dl)
