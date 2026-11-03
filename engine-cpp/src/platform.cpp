#include "platform.h"

#include <algorithm>
#include <cstring>
#include <mutex>
#include <system_error>

namespace fs = std::filesystem;

#ifdef _WIN32
// ============================== WINDOWS ==============================
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#include <tlhelp32.h>
#include <psapi.h>
#include <powrprof.h>
#include <objbase.h>

namespace bb::platform {

static uint64_t to_u64(const FILETIME& f) {
    return (static_cast<uint64_t>(f.dwHighDateTime) << 32) | f.dwLowDateTime;
}

double cpu_usage_percent() {
    static std::mutex m;
    static uint64_t prev_idle = 0, prev_kernel = 0, prev_user = 0;
    static bool has_prev = false;
    std::lock_guard<std::mutex> lock(m);

    FILETIME idle, kernel, user;
    if (!GetSystemTimes(&idle, &kernel, &user)) return 0.0;
    const uint64_t i = to_u64(idle), k = to_u64(kernel), u = to_u64(user);

    double result = 0.0;
    if (has_prev) {
        const uint64_t d_idle = i - prev_idle;
        const uint64_t d_total = (k - prev_kernel) + (u - prev_user); // kernel вже містить idle
        if (d_total > 0) result = 100.0 * static_cast<double>(d_total - d_idle) / static_cast<double>(d_total);
    }
    prev_idle = i; prev_kernel = k; prev_user = u; has_prev = true;
    return std::clamp(result, 0.0, 100.0);
}

bool memory_info(BBMemoryInfo& out) {
    MEMORYSTATUSEX ms{};
    ms.dwLength = sizeof(ms);
    if (!GlobalMemoryStatusEx(&ms)) return false;
    out.total_bytes = ms.ullTotalPhys;
    out.available_bytes = ms.ullAvailPhys;
    out.load_percent = ms.dwMemoryLoad;
    return true;
}

// Запускає службову команду без вікна й чекає завершення. true — код виходу 0.
static bool run_hidden(wchar_t* cmd, DWORD timeout_ms) {
    STARTUPINFOW si{}; si.cb = sizeof(si);
    PROCESS_INFORMATION pi{};
    if (!CreateProcessW(nullptr, cmd, nullptr, nullptr, FALSE, CREATE_NO_WINDOW, nullptr, nullptr, &si, &pi))
        return false;
    DWORD code = 1;
    if (WaitForSingleObject(pi.hProcess, timeout_ms) == WAIT_OBJECT_0) GetExitCodeProcess(pi.hProcess, &code);
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    return code == 0;
}

bool set_high_performance() {
    wchar_t cmd[] = L"powercfg.exe /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    return run_hidden(cmd, 10000);
}

bool flush_dns() {
    wchar_t cmd[] = L"ipconfig.exe /flushdns";
    return run_hidden(cmd, 10000);
}

bool trim_memory(BBTrimResult& out) {
    BBMemoryInfo before{}, after{};
    if (!memory_info(before)) return false;
    HANDLE snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snap == INVALID_HANDLE_VALUE) return false;

    const DWORD self = GetCurrentProcessId();
    PROCESSENTRY32W pe{}; pe.dwSize = sizeof(pe);
    for (BOOL ok = Process32FirstW(snap, &pe); ok; ok = Process32NextW(snap, &pe)) {
        if (pe.th32ProcessID <= 4 || pe.th32ProcessID == self) continue;
        HANDLE h = OpenProcess(PROCESS_SET_QUOTA | PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pe.th32ProcessID);
        if (!h) continue;
        if (EmptyWorkingSet(h)) out.trimmed_processes++;
        CloseHandle(h);
    }
    CloseHandle(snap);

    Sleep(300);
    if (memory_info(after) && after.available_bytes > before.available_bytes)
        out.freed_bytes = after.available_bytes - before.available_bytes;
    return true;
}

static fs::path env_path(const wchar_t* name) {
    wchar_t buf[MAX_PATH];
    const DWORD n = GetEnvironmentVariableW(name, buf, MAX_PATH);
    if (n == 0 || n >= MAX_PATH) return {};
    return fs::path(buf);
}

std::vector<fs::path> deep_clean_dirs(bool include_browsers) {
    std::vector<fs::path> candidates;
    std::error_code ec;

    const fs::path local = env_path(L"LOCALAPPDATA");
    if (!local.empty()) {
        candidates.push_back(local / L"CrashDumps");
        candidates.push_back(local / L"Microsoft" / L"Windows" / L"WER" / L"ReportArchive");
        candidates.push_back(local / L"Microsoft" / L"Windows" / L"WER" / L"ReportQueue");
        candidates.push_back(local / L"Microsoft" / L"Windows" / L"INetCache");
        if (include_browsers) {
            for (const wchar_t* base : { L"Google\\Chrome\\User Data\\Default", L"Microsoft\\Edge\\User Data\\Default" })
                for (const wchar_t* sub : { L"Cache", L"Code Cache", L"GPUCache" })
                    candidates.push_back(local / base / sub);
        }
    }
    wchar_t win[MAX_PATH];
    if (GetWindowsDirectoryW(win, MAX_PATH)) candidates.push_back(fs::path(win) / L"Minidump");

    std::vector<fs::path> dirs;
    for (auto& d : candidates) if (fs::is_directory(d, ec)) dirs.push_back(d);
    return dirs;
}

bool power_get_active(std::string& out) {
    GUID* g = nullptr;
    if (PowerGetActiveScheme(nullptr, &g) != ERROR_SUCCESS || !g) return false;
    wchar_t wide[64] = {0};
    const int n = StringFromGUID2(*g, wide, 64);
    LocalFree(g);
    if (n <= 0) return false;
    char narrow[64] = {0};
    WideCharToMultiByte(CP_ACP, 0, wide, -1, narrow, sizeof(narrow) - 1, nullptr, nullptr);
    out = narrow;
    return true;
}

bool power_set_active(const std::string& guid) {
    wchar_t wide[64] = {0};
    if (!MultiByteToWideChar(CP_ACP, 0, guid.c_str(), -1, wide, 63)) return false;
    GUID g{};
    if (FAILED(CLSIDFromString(wide, &g))) return false;
    return PowerSetActiveScheme(nullptr, &g) == ERROR_SUCCESS;
}

int list_processes(BBProcessInfo* buf, int capacity) {
    if (!buf || capacity <= 0) return 0;
    HANDLE snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snap == INVALID_HANDLE_VALUE) return 0;

    std::vector<BBProcessInfo> all;
    PROCESSENTRY32W pe{}; pe.dwSize = sizeof(pe);
    for (BOOL ok = Process32FirstW(snap, &pe); ok; ok = Process32NextW(snap, &pe)) {
        BBProcessInfo info{};
        info.pid = pe.th32ProcessID;
        WideCharToMultiByte(CP_ACP, 0, pe.szExeFile, -1, info.name, sizeof(info.name) - 1, nullptr, nullptr);
        HANDLE h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pe.th32ProcessID);
        if (h) {
            PROCESS_MEMORY_COUNTERS pmc{};
            if (GetProcessMemoryInfo(h, &pmc, sizeof(pmc))) info.working_set_bytes = pmc.WorkingSetSize;
            CloseHandle(h);
        }
        all.push_back(info);
    }
    CloseHandle(snap);

    std::sort(all.begin(), all.end(), [](const BBProcessInfo& a, const BBProcessInfo& b) {
        return a.working_set_bytes > b.working_set_bytes;
    });
    const int n = std::min<int>(capacity, static_cast<int>(all.size()));
    for (int i = 0; i < n; ++i) buf[i] = all[i];
    return n;
}

std::vector<fs::path> temp_dirs() {
    std::vector<fs::path> dirs;
    std::error_code ec;
    fs::path t = fs::temp_directory_path(ec);
    if (!ec) dirs.push_back(t);
    wchar_t win[MAX_PATH];
    if (GetWindowsDirectoryW(win, MAX_PATH)) {
        fs::path w = fs::path(win) / L"Temp";
        if (fs::is_directory(w, ec)) dirs.push_back(w);
    }
    return dirs;
}

} // namespace bb::platform

#else
// ============================== POSIX (для тестів/CI) ==============================
#include <fstream>
#include <sstream>
#include <string>
#include <unistd.h>

namespace bb::platform {

double cpu_usage_percent() {
    static std::mutex m;
    static uint64_t prev_total = 0, prev_idle = 0;
    static bool has_prev = false;
    std::lock_guard<std::mutex> lock(m);

    std::ifstream f("/proc/stat");
    std::string label;
    uint64_t v[8] = {0};
    if (!(f >> label)) return 0.0;
    for (auto& x : v) f >> x;
    uint64_t total = 0;
    for (auto x : v) total += x;
    const uint64_t idle = v[3] + v[4];

    double result = 0.0;
    if (has_prev && total > prev_total)
        result = 100.0 * static_cast<double>((total - prev_total) - (idle - prev_idle)) / static_cast<double>(total - prev_total);
    prev_total = total; prev_idle = idle; has_prev = true;
    return std::clamp(result, 0.0, 100.0);
}

bool memory_info(BBMemoryInfo& out) {
    const long page = sysconf(_SC_PAGESIZE);
    const long pages = sysconf(_SC_PHYS_PAGES);
    const long avail = sysconf(_SC_AVPHYS_PAGES);
    if (page <= 0 || pages <= 0) return false;
    out.total_bytes = static_cast<uint64_t>(pages) * page;
    out.available_bytes = static_cast<uint64_t>(avail > 0 ? avail : 0) * page;
    out.load_percent = static_cast<uint32_t>(100 - (100 * out.available_bytes / out.total_bytes));
    return true;
}

bool set_high_performance() { return false; }
bool flush_dns() { return false; }
bool trim_memory(BBTrimResult&) { return false; }
std::vector<fs::path> deep_clean_dirs(bool) { return {}; }
bool power_get_active(std::string&) { return false; }
bool power_set_active(const std::string&) { return false; }
int list_processes(BBProcessInfo*, int) { return 0; }

std::vector<fs::path> temp_dirs() {
    std::vector<fs::path> dirs;
    std::error_code ec;
    fs::path t = fs::temp_directory_path(ec);
    if (!ec) dirs.push_back(t);
    return dirs;
}

} // namespace bb::platform
#endif
