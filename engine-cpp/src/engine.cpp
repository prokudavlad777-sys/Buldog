#include "bb_api.h"
#include "platform.h"

#include <algorithm>
#include <cstring>
#include <filesystem>
#include <string>
#include <system_error>
#include <vector>

namespace fs = std::filesystem;

#ifndef BB_VERSION
#define BB_VERSION "0.0.0"
#endif

namespace {

fs::path from_utf8(const char* s) {
    const auto* u = reinterpret_cast<const char8_t*>(s);
    return fs::path(std::u8string(u));
}

uint64_t entry_size(const fs::path& p) {
    std::error_code ec;
    if (fs::is_symlink(p, ec)) return 0;
    if (fs::is_regular_file(p, ec)) {
        const auto s = fs::file_size(p, ec);
        return ec ? 0 : static_cast<uint64_t>(s);
    }
    uint64_t total = 0;
    if (fs::is_directory(p, ec)) {
        fs::recursive_directory_iterator it(p, fs::directory_options::skip_permission_denied, ec), end;
        for (; !ec && it != end; it.increment(ec)) {
            std::error_code e2;
            if (it->is_regular_file(e2) && !e2) {
                const auto s = it->file_size(e2);
                if (!e2) total += static_cast<uint64_t>(s);
            }
        }
    }
    return total;
}

// Видаляє ВМІСТ папки (саму папку не чіпає). Повертає false, якщо шлях небезпечний/недоступний.
bool clean_dir_contents(const fs::path& dir, BBCleanResult& res) {
    std::error_code ec;
    if (dir.empty() || !fs::is_directory(dir, ec)) return false;
    if (dir.relative_path().empty() || dir == dir.root_path()) return false; // корінь диска — ні

    std::vector<fs::path> entries;
    for (fs::directory_iterator it(dir, fs::directory_options::skip_permission_denied, ec), end;
         !ec && it != end; it.increment(ec))
        entries.push_back(it->path());

    for (const auto& p : entries) {
        const uint64_t size = entry_size(p);
        std::error_code rec;
        fs::remove_all(p, rec);
        if (!rec) { res.removed_items++; res.freed_bytes += size; } // зайняті файли пропускаємо
    }
    return true;
}

} // namespace

extern "C" {

const char* bb_engine_version(void) { return BB_VERSION; }

double bb_cpu_usage_percent(void) {
    try { return bb::platform::cpu_usage_percent(); } catch (...) { return 0.0; }
}

int bb_memory_info(BBMemoryInfo* out) {
    if (!out) return 0;
    try {
        std::memset(out, 0, sizeof(*out));
        return bb::platform::memory_info(*out) ? 1 : 0;
    } catch (...) { return 0; }
}

int bb_clean_directory(const char* utf8_path, BBCleanResult* out) {
    if (!utf8_path || !out) return 0;
    try {
        std::memset(out, 0, sizeof(*out));
        return clean_dir_contents(from_utf8(utf8_path), *out) ? 1 : 0;
    } catch (...) { return 0; }
}

int bb_clean_temp(BBCleanResult* out) {
    if (!out) return 0;
    try {
        std::memset(out, 0, sizeof(*out));
        bool any = false;
        for (const auto& d : bb::platform::temp_dirs()) any |= clean_dir_contents(d, *out);
        return any ? 1 : 0;
    } catch (...) { return 0; }
}

int bb_clean_deep(int include_browsers, BBCleanResult* out) {
    if (!out) return 0;
    try {
        std::memset(out, 0, sizeof(*out));
        bool any = false;
        for (const auto& d : bb::platform::deep_clean_dirs(include_browsers != 0)) any |= clean_dir_contents(d, *out);
        return any ? 1 : 0;
    } catch (...) { return 0; }
}

int bb_trim_memory(BBTrimResult* out) {
    if (!out) return 0;
    try {
        std::memset(out, 0, sizeof(*out));
        return bb::platform::trim_memory(*out) ? 1 : 0;
    } catch (...) { return 0; }
}

int bb_flush_dns(void) {
    try { return bb::platform::flush_dns() ? 1 : 0; } catch (...) { return 0; }
}

int bb_set_high_performance(void) {
    try { return bb::platform::set_high_performance() ? 1 : 0; } catch (...) { return 0; }
}

int bb_power_get_active(char* out_guid, int capacity) {
    if (!out_guid || capacity <= 0) return 0;
    try {
        std::string s;
        if (!bb::platform::power_get_active(s) || static_cast<int>(s.size()) + 1 > capacity) return 0;
        std::memcpy(out_guid, s.c_str(), s.size() + 1);
        return 1;
    } catch (...) { return 0; }
}

int bb_power_set_active(const char* guid) {
    if (!guid) return 0;
    try { return bb::platform::power_set_active(guid) ? 1 : 0; } catch (...) { return 0; }
}

int bb_list_processes(BBProcessInfo* buf, int capacity) {
    try { return bb::platform::list_processes(buf, capacity); } catch (...) { return 0; }
}

} // extern "C"
