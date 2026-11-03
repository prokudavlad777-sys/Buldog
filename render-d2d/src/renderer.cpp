#include "bb_render.h"

#include <d2d1.h>
#include <wrl/client.h>

#include <algorithm>
#include <chrono>
#include <new>
#include <vector>

using Microsoft::WRL::ComPtr;
using Clock = std::chrono::steady_clock;

namespace {
constexpr size_t kVisibleIntervals = 60;   // скільки секунд видно на графіку
constexpr size_t kMaxSamples = kVisibleIntervals + 3;
}

struct BBRender {
    HWND hwnd = nullptr;
    UINT width = 1, height = 1;

    ComPtr<ID2D1Factory> factory;
    ComPtr<ID2D1HwndRenderTarget> rt;
    ComPtr<ID2D1SolidColorBrush> lineBrush, fillBrush, gridBrush;

    std::vector<float> samples;
    Clock::time_point lastPush = Clock::now();
    double intervalMs = 1000.0;

    bool ensureTarget() {
        if (rt) return true;
        HRESULT hr = factory->CreateHwndRenderTarget(
            D2D1::RenderTargetProperties(),
            D2D1::HwndRenderTargetProperties(hwnd, D2D1::SizeU(width, height),
                                             D2D1_PRESENT_OPTIONS_IMMEDIATELY), // темп задає хост (WPF), без блокування
            &rt);
        if (FAILED(hr)) return false;
        rt->CreateSolidColorBrush(D2D1::ColorF(0.30f, 0.80f, 0.45f, 1.0f), &lineBrush);
        rt->CreateSolidColorBrush(D2D1::ColorF(0.30f, 0.80f, 0.45f, 0.18f), &fillBrush);
        rt->CreateSolidColorBrush(D2D1::ColorF(1.0f, 1.0f, 1.0f, 0.07f), &gridBrush);
        return lineBrush && fillBrush && gridBrush;
    }

    void releaseTarget() {
        lineBrush.Reset(); fillBrush.Reset(); gridBrush.Reset(); rt.Reset();
    }

    ComPtr<ID2D1PathGeometry> buildGeometry(const std::vector<D2D1_POINT_2F>& pts, bool filled) {
        ComPtr<ID2D1PathGeometry> geo;
        if (pts.size() < 2 || FAILED(factory->CreatePathGeometry(&geo))) return nullptr;
        ComPtr<ID2D1GeometrySink> sink;
        if (FAILED(geo->Open(&sink))) return nullptr;
        const float h = static_cast<float>(height);
        if (filled) {
            sink->BeginFigure(D2D1::Point2F(pts.front().x, h), D2D1_FIGURE_BEGIN_FILLED);
            for (const auto& p : pts) sink->AddLine(p);
            sink->AddLine(D2D1::Point2F(pts.back().x, h));
            sink->EndFigure(D2D1_FIGURE_END_CLOSED);
        } else {
            sink->BeginFigure(pts.front(), D2D1_FIGURE_BEGIN_HOLLOW);
            for (size_t i = 1; i < pts.size(); ++i) sink->AddLine(pts[i]);
            sink->EndFigure(D2D1_FIGURE_END_OPEN);
        }
        sink->Close();
        return geo;
    }
};

extern "C" {

BBRender* bb_render_create(void* hwnd) {
    if (!hwnd) return nullptr;
    auto* r = new (std::nothrow) BBRender();
    if (!r) return nullptr;
    r->hwnd = static_cast<HWND>(hwnd);
    RECT rc{};
    GetClientRect(r->hwnd, &rc);
    r->width = std::max<LONG>(1, rc.right - rc.left);
    r->height = std::max<LONG>(1, rc.bottom - rc.top);
    if (FAILED(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, r->factory.GetAddressOf()))) {
        delete r;
        return nullptr;
    }
    return r;
}

void bb_render_destroy(BBRender* r) { delete r; }

void bb_render_resize(BBRender* r, uint32_t w, uint32_t h) {
    if (!r) return;
    r->width = std::max<uint32_t>(1, w);
    r->height = std::max<uint32_t>(1, h);
    if (r->rt) r->rt->Resize(D2D1::SizeU(r->width, r->height));
}

void bb_render_push_sample(BBRender* r, float v) {
    if (!r) return;
    const auto now = Clock::now();
    if (!r->samples.empty()) {
        const double dt = std::chrono::duration<double, std::milli>(now - r->lastPush).count();
        r->intervalMs = 0.8 * r->intervalMs + 0.2 * std::clamp(dt, 100.0, 5000.0); // згладжений інтервал
    }
    r->lastPush = now;
    r->samples.push_back(std::clamp(v, 0.0f, 1.0f));
    if (r->samples.size() > kMaxSamples)
        r->samples.erase(r->samples.begin(), r->samples.begin() + (r->samples.size() - kMaxSamples));
}

int bb_render_draw(BBRender* r) {
    if (!r || !r->ensureTarget()) return 0;

    const float w = static_cast<float>(r->width), h = static_cast<float>(r->height);
    const float spacing = w / static_cast<float>(kVisibleIntervals);
    const double elapsed = std::chrono::duration<double, std::milli>(Clock::now() - r->lastPush).count();
    const float progress = static_cast<float>(std::clamp(elapsed / r->intervalMs, 0.0, 1.0));

    // Найновіший вимір їде від правого краю (+spacing) до краю (w) — рух безперервний.
    std::vector<D2D1_POINT_2F> pts;
    const size_t n = r->samples.size();
    pts.reserve(n);
    for (size_t i = 0; i < n; ++i) {
        const float x = w + spacing - (static_cast<float>(n - 1 - i) + progress) * spacing;
        const float y = h - 6.0f - r->samples[i] * (h - 12.0f);
        pts.push_back(D2D1::Point2F(x, y));
    }

    r->rt->BeginDraw();
    r->rt->Clear(D2D1::ColorF(0.11f, 0.12f, 0.15f, 1.0f));
    for (int i = 1; i < 4; ++i) {
        const float y = h * static_cast<float>(i) / 4.0f;
        r->rt->DrawLine(D2D1::Point2F(0, y), D2D1::Point2F(w, y), r->gridBrush.Get(), 1.0f);
    }
    if (auto fill = r->buildGeometry(pts, true)) r->rt->FillGeometry(fill.Get(), r->fillBrush.Get());
    if (auto line = r->buildGeometry(pts, false)) r->rt->DrawGeometry(line.Get(), r->lineBrush.Get(), 2.0f);

    const HRESULT hr = r->rt->EndDraw();
    if (hr == D2DERR_RECREATE_TARGET) { r->releaseTarget(); return 0; } // пристрій втрачено — перестворимо
    return SUCCEEDED(hr) ? 1 : 0;
}

} // extern "C"
