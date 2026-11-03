/* Buldog Boost — Direct2D рендерер плавного графіка навантаження (лише Windows). */
#ifndef BB_RENDER_H
#define BB_RENDER_H

#include <stdint.h>

#ifdef BB_RENDER_EXPORTS
  #define BB_RENDER_API __declspec(dllexport)
#else
  #define BB_RENDER_API __declspec(dllimport)
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef struct BBRender BBRender;

/* hwnd — дочірнє вікно, у яке малюємо. Повертає NULL при помилці. */
BB_RENDER_API BBRender* bb_render_create(void* hwnd);
BB_RENDER_API void      bb_render_destroy(BBRender* r);
BB_RENDER_API void      bb_render_resize(BBRender* r, uint32_t width_px, uint32_t height_px);
/* Додає новий вимір (0..1). Викликати приблизно раз на секунду. */
BB_RENDER_API void      bb_render_push_sample(BBRender* r, float value01);
/* Малює кадр; викликати кожен кадр (60+ FPS). Графік плавно «їде» між вимірами. 1 — успіх. */
BB_RENDER_API int       bb_render_draw(BBRender* r);

#ifdef __cplusplus
}
#endif
#endif /* BB_RENDER_H */
