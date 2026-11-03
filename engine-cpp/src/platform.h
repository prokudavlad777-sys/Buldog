#pragma once
#include "bb_api.h"
#include <filesystem>
#include <string>
#include <vector>

namespace bb::platform {
double cpu_usage_percent();
bool   memory_info(BBMemoryInfo& out);
bool   set_high_performance();
bool   power_get_active(std::string& out_guid);
bool   power_set_active(const std::string& guid);
int    list_processes(BBProcessInfo* buf, int capacity);
std::vector<std::filesystem::path> temp_dirs();
std::vector<std::filesystem::path> deep_clean_dirs(bool include_browsers);
bool   trim_memory(BBTrimResult& out);
bool   flush_dns();
} // namespace bb::platform
