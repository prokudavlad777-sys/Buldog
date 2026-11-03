/* Buldog Boost — публічний C-інтерфейс Optimization Engine.
 * Використовується з C# (P/Invoke) та Rust (FFI). Лише C-типи, без винятків через кордон DLL. */
#ifndef BB_API_H
#define BB_API_H

#include <stdint.h>

#ifdef _WIN32
  #ifdef BB_ENGINE_EXPORTS
    #define BB_API __declspec(dllexport)
  #else
    #define BB_API __declspec(dllimport)
  #endif
#else
  #define BB_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef struct BBMemoryInfo {
    uint64_t total_bytes;
    uint64_t available_bytes;
    uint32_t load_percent;
} BBMemoryInfo;

typedef struct BBCleanResult {
    uint32_t removed_items;
    uint64_t freed_bytes;
} BBCleanResult;

typedef struct BBTrimResult {
    uint32_t trimmed_processes;
    uint64_t freed_bytes;        /* зміна «доступної» RAM; може бути 0 — Windows повертає сторінки за потреби */
} BBTrimResult;

typedef struct BBProcessInfo {
    uint32_t pid;
    char     name[260];          /* ANSI (системна кодова сторінка) */
    uint64_t working_set_bytes;
} BBProcessInfo;

/* Версія рушія, напр. "0.2.0". Рядок статичний, звільняти не треба. */
BB_API const char* bb_engine_version(void);

/* Завантаження CPU у %, 0..100. Перший виклик повертає 0 (потрібні два виміри). */
BB_API double bb_cpu_usage_percent(void);

/* 1 — успіх, 0 — помилка. */
BB_API int bb_memory_info(BBMemoryInfo* out);

/* Очищення системних тимчасових папок. 1 — успіх, 0 — помилка. */
BB_API int bb_clean_temp(BBCleanResult* out);

/* Очищення вмісту довільної папки (UTF-8 шлях). Корінь диска та порожній шлях відхиляються. */
BB_API int bb_clean_directory(const char* utf8_path, BBCleanResult* out);

/* «Глибоке» очищення: дампи падінь, звіти WER, кеш інтернету, Minidump.
 * include_browsers != 0 — додатково кеш Chrome/Edge (лише Cache/Code Cache/GPUCache, без паролів і cookies).
 * 1 — хоча б одну папку оброблено, 0 — нічого / помилка. */
BB_API int bb_clean_deep(int include_browsers, BBCleanResult* out);

/* Скинути робочі набори процесів (EmptyWorkingSet). Системні PID<=4 і сам процес пропускаються.
 * Тимчасовий ефект: потрібні сторінки Windows підтягне назад. Лише Windows. 1 — успіх. */
BB_API int bb_trim_memory(BBTrimResult* out);

/* Очистити DNS-кеш (ipconfig /flushdns). Лише Windows. 1 — успіх. */
BB_API int bb_flush_dns(void);

/* Схема живлення «Висока продуктивність» (Windows). 1 — успіх. */
BB_API int bb_set_high_performance(void);

/* GUID активної схеми живлення у форматі "{xxxxxxxx-xxxx-...}". 1 — успіх (Windows). */
BB_API int bb_power_get_active(char* out_guid, int capacity);

/* Увімкнути схему живлення за GUID (для відкату після Boost). 1 — успіх. */
BB_API int bb_power_set_active(const char* guid);

/* Заповнює buf процесами, відсортованими за пам'яттю (від більших). Повертає кількість. */
BB_API int bb_list_processes(BBProcessInfo* buf, int capacity);

#ifdef __cplusplus
}
#endif
#endif /* BB_API_H */
