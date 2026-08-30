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

# ---- And64InlineHook (tek dosya inline hook; Dobby'nin arm64 asm build sorunu yok) ----
FetchContent_Declare(a64hook
  GIT_REPOSITORY https://github.com/Rprop/And64InlineHook.git
  GIT_TAG        master)
FetchContent_GetProperties(a64hook)
if(NOT a64hook_POPULATED)
  FetchContent_Populate(a64hook)
endif()

# ---- Mod kütüphanesi ----
add_library(few1nmod SHARED
  jni/main.cpp
  jni/il2cpp.cpp
  jni/features.cpp
  jni/feat_vehicle.cpp
  jni/feat_tuning.cpp
  jni/overlay.cpp
  ${a64hook_SOURCE_DIR}/And64InlineHook.cpp)

target_include_directories(few1nmod PRIVATE
  ${imgui_SOURCE_DIR} ${imgui_SOURCE_DIR}/backends
  ${a64hook_SOURCE_DIR})

target_link_libraries(few1nmod
  imgui
  EGL GLESv3 android log dl)
