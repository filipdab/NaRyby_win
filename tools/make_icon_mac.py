#!/usr/bin/env python3
"""Ikona NaRyby dla macOS (.icns) i okna Avalonii (.png).

Rysuje ten sam motyw co tools/make_icon.ps1 (ryba na tle wody), ale w rozdzielczości 1024 px
i z marginesem wymaganym przez szablon ikon macOS (tło 824×824 px wyśrodkowane na 1024×1024).

Wymaga Pillow:  python3 -m pip install pillow
Użycie:         python3 tools/make_icon_mac.py
"""
import os

from PIL import Image, ImageChops, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ICNS = os.path.join(ROOT, "NaRyby_mac", "macos", "NaRyby.icns")
PNG = os.path.join(ROOT, "NaRyby_mac", "Assets", "icon.png")

SS = 4                      # nadpróbkowanie dla gładkich krawędzi
SIZE = 1024 * SS
SCALE = 824 / 236 * SS      # tło z make_icon.ps1 (10..246) → 824 px
OFFSET = 100 * SS


def p(x, y):
    """Punkt z układu 256×256 (make_icon.ps1) na płótno macOS."""
    return (OFFSET + (x - 10) * SCALE, OFFSET + (y - 10) * SCALE)


def box(x, y, w, h):
    return [p(x, y), p(x + w, y + h)]


def vertical_gradient(top, bottom, y0, y1):
    """Pełnoekranowy gradient pionowy między wierszami y0 i y1 (współrzędne 256)."""
    grad = Image.new("RGBA", (1, SIZE))
    a, b = p(0, y0)[1], p(0, y1)[1]
    for y in range(SIZE):
        t = min(1.0, max(0.0, (y - a) / (b - a)))
        grad.putpixel((0, y), tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,))
    return grad.resize((SIZE, SIZE))


def mask(draw_fn):
    m = Image.new("L", (SIZE, SIZE), 0)
    draw_fn(ImageDraw.Draw(m))
    return m


def bezier(p0, p1, p2, p3, n=64):
    pts = []
    for i in range(n + 1):
        t = i / n
        u = 1 - t
        pts.append(tuple(u ** 3 * p0[k] + 3 * u * u * t * p1[k] + 3 * u * t * t * p2[k] + t ** 3 * p3[k] for k in range(2)))
    return pts


def layer(fill, alpha):
    """Warstwa o danym kolorze/gradiencie i masce przezroczystości."""
    out = (fill if isinstance(fill, Image.Image) else Image.new("RGBA", (SIZE, SIZE), fill)).copy()
    out.putalpha(alpha)
    return out


def render():
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))

    # Tło: zaokrąglony kwadrat w kolorze wody.
    bg = mask(lambda d: d.rounded_rectangle(box(10, 10, 236, 236), radius=52 * SCALE, fill=255))
    img.alpha_composite(layer(vertical_gradient((70, 170, 235), (18, 80, 160), 0, 256), bg))

    # Fale na dole (przycięte do tła).
    wave_pts = bezier((10, 205), (55, 185), (85, 225), (128, 205)) + bezier((128, 205), (170, 185), (200, 225), (246, 205))
    wave_pts += [(246, 246), (10, 246)]
    wave = mask(lambda d: d.polygon([p(x, y) for x, y in wave_pts], fill=60))
    img.alpha_composite(layer((255, 255, 255), ImageChops.multiply(wave, bg)))

    # Bąbelki przy pysku.
    bubbles = mask(lambda d: [d.ellipse(box(x, y, s, s), outline=220, width=int(6 * SCALE))
                              for x, y, s in ((30, 62, 18), (46, 30, 12))])
    img.alpha_composite(layer((255, 255, 255), bubbles))

    orange = (235, 105, 20)
    img.alpha_composite(layer(orange, mask(lambda d: d.polygon([p(176, 124), p(236, 76), p(222, 124), p(236, 172)], fill=255))))  # ogon
    img.alpha_composite(layer(orange, mask(lambda d: d.polygon([p(96, 84), p(140, 46), p(160, 88)], fill=255))))                  # płetwa

    # Tułów z gradientem.
    body = mask(lambda d: d.ellipse(box(40, 72, 156, 104), fill=255))
    img.alpha_composite(layer(vertical_gradient((255, 200, 60), (245, 120, 20), 72, 176), body))

    # Pokrywa skrzelowa i oko.
    img.alpha_composite(layer((190, 80, 10), mask(lambda d: d.arc(box(70, 92, 50, 64), start=-60, end=60, fill=200, width=int(6 * SCALE)))))
    img.alpha_composite(layer((255, 255, 255), mask(lambda d: d.ellipse(box(64, 98, 26, 26), fill=255))))
    img.alpha_composite(layer((25, 35, 60), mask(lambda d: d.ellipse(box(69, 103, 15, 15), fill=255))))

    return img.resize((1024, 1024), Image.LANCZOS)


def main():
    icon = render()
    os.makedirs(os.path.dirname(ICNS), exist_ok=True)
    os.makedirs(os.path.dirname(PNG), exist_ok=True)
    icon.save(ICNS, format="ICNS")
    icon.resize((256, 256), Image.LANCZOS).save(PNG, optimize=True)
    print("Zapisano", ICNS, "i", PNG)


if __name__ == "__main__":
    main()
