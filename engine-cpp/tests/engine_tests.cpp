#include "bb_api.h"

#include <chrono>
#include <cstdio>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <string>
#include <thread>

namespace fs = std::filesystem;

#define CHECK(cond) do { if (!(cond)) { std::fprintf(stderr, "FAIL %s:%d  %s\n", __FILE__, __LINE__, #cond); std::exit(1); } } while (0)

int main() {
    // версія
    CHECK(bb_engine_version() != nullptr && bb_engine_version()[0] != '\0');

    // пам'ять
    BBMemoryInfo mem{};
    CHECK(bb_memory_info(&mem) == 1);
    CHECK(mem.total_bytes > 0);
    CHECK(mem.load_percent <= 100);
    CHECK(bb_memory_info(nullptr) == 0);

    // CPU
    bb_cpu_usage_percent();
    std::this_thread::sleep_for(std::chrono::milliseconds(200));
    const double cpu = bb_cpu_usage_percent();
    CHECK(cpu >= 0.0 && cpu <= 100.0);

    // очищення власної тимчасової папки (НЕ системного temp)
    const fs::path dir = fs::temp_directory_path() / "bb_engine_test_dir";
    fs::remove_all(dir);
    fs::create_directories(dir / "sub");
    { std::ofstream(dir / "a.txt") << "12345"; }
    { std::ofstream(dir / "sub" / "b.txt") << "1234567890"; }

    BBCleanResult res{};
    const std::string u8 = dir.string();
    CHECK(bb_clean_directory(u8.c_str(), &res) == 1);
    CHECK(res.removed_items == 2);
    CHECK(res.freed_bytes == 15);
    CHECK(fs::exists(dir) && fs::is_empty(dir));
    fs::remove_all(dir);

    // захист від небезпечних шляхів
    CHECK(bb_clean_directory("", &res) == 0);
    CHECK(bb_clean_directory(fs::path(fs::temp_directory_path()).root_path().string().c_str(), &res) == 0);
    CHECK(bb_clean_directory(nullptr, &res) == 0);

    // нові функції Boost: некоректні аргументи та поведінка поза Windows
    CHECK(bb_clean_deep(0, nullptr) == 0);
    CHECK(bb_trim_memory(nullptr) == 0);
#ifndef _WIN32
    BBTrimResult trim{};
    CHECK(bb_clean_deep(1, &res) == 0 && res.removed_items == 0);
    CHECK(bb_trim_memory(&trim) == 0);
    CHECK(bb_flush_dns() == 0);
#endif

    // схема живлення: некоректні аргументи відхиляються
    char guid[64] = {0};
    CHECK(bb_power_get_active(nullptr, 64) == 0);
    CHECK(bb_power_get_active(guid, 0) == 0);
    CHECK(bb_power_set_active(nullptr) == 0);
#ifdef _WIN32
    CHECK(bb_power_get_active(guid, sizeof(guid)) == 1);
    CHECK(guid[0] == '{');
#endif

    std::puts("engine tests: OK");
    return 0;
}
