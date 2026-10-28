"""Tiny procedural illustration kit: shaded, outlined, supersampled cartoon icons (no external art)."""
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

S = 1024          # working size (downsampled to 256)
OUT = 256
OUTLINE = (74, 44, 36)


def lerp(a, b, t): return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))
def light(c, k=0.35): return lerp(c, (255, 255, 255), k)
def dark(c, k=0.35): return lerp(c, (30, 10, 20), k)
def P(x, y): return (x * S, y * S)


class Icon:
    def __init__(self):
        self.img = Image.new("RGBA", (S, S), (0, 0, 0, 0))

    # ---- masks
    @staticmethod
    def _mask(fn):
        m = Image.new("L", (S, S), 0)
        fn(ImageDraw.Draw(m))
        return m

    def _paint(self, mask, base, style="sphere", lightpos=(-0.3, -0.35)):
        bb = mask.getbbox()
        if not bb: return
        x0, y0, x1, y1 = bb
        w, h = x1 - x0, y1 - y0
        ys, xs = np.mgrid[y0:y1, x0:x1].astype(np.float32)
        if style == "flat":
            t = np.zeros_like(xs)
        elif style == "vert":
            t = (ys - y0) / max(h, 1)
        else:
            cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
            lx, ly = cx + lightpos[0] * w, cy + lightpos[1] * h
            t = np.sqrt(((xs - lx) / (0.95 * w)) ** 2 + ((ys - ly) / (0.95 * h)) ** 2)
            t = np.clip(t, 0, 1)
        hi, lo = np.array(light(base, 0.38), np.float32), np.array(dark(base, 0.32), np.float32)
        b = np.array(base, np.float32)
        # three-stop ramp: highlight -> base -> shade
        t3 = t[..., None]
        col = np.where(t3 < 0.5, hi + (b - hi) * (t3 / 0.5), b + (lo - b) * ((t3 - 0.5) / 0.5))
        if style == "flat": col = np.broadcast_to(b, col.shape)
        rgba = np.zeros((y1 - y0, x1 - x0, 4), np.uint8)
        rgba[..., :3] = np.clip(col, 0, 255).astype(np.uint8)
        rgba[..., 3] = 255
        layer = Image.fromarray(rgba, "RGBA")
        full = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        full.paste(layer, (x0, y0))
        full.putalpha(mask)
        self.img = Image.alpha_composite(self.img, full)

    # ---- primitives (coords 0..1)
    def ell(self, cx, cy, rx, ry, col, rot=0, style="sphere", lightpos=(-0.3, -0.35)):
        pts = []
        for i in range(120):
            a = i / 120 * math.tau
            x, y = rx * math.cos(a), ry * math.sin(a)
            r = math.radians(rot)
            pts.append(P(cx + x * math.cos(r) - y * math.sin(r), cy + x * math.sin(r) + y * math.cos(r)))
        self.poly_px(pts, col, style, lightpos)

    def poly(self, pts, col, style="vert", lightpos=(-0.3, -0.35)):
        self.poly_px([P(*p) for p in pts], col, style, lightpos)

    def poly_px(self, pts, col, style="vert", lightpos=(-0.3, -0.35)):
        self._paint(self._mask(lambda d: d.polygon(pts, fill=255)), col, style, lightpos)

    def rrect(self, x0, y0, x1, y1, r, col, style="vert"):
        self._paint(self._mask(lambda d: d.rounded_rectangle([x0 * S, y0 * S, x1 * S, y1 * S], radius=r * S, fill=255)), col, style)

    def stroke(self, pts, w, col, style="flat"):
        def f(d):
            px = [P(*p) for p in pts]
            d.line(px, fill=255, width=int(w * S), joint="curve")
            for p in (px[0], px[-1]):
                r = w * S / 2
                d.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=255)
        self._paint(self._mask(f), col, style)

    def curve(self, p0, p1, p2, w, col, n=24):
        pts = [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0],
                (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]) for t in [i / n for i in range(n + 1)]]
        self.stroke(pts, w, col)

    def shine(self, cx, cy, rx, ry, a=0.85, rot=-30):
        m = Image.new("L", (S, S), 0)
        d = ImageDraw.Draw(m)
        pts = []
        for i in range(60):
            t = i / 60 * math.tau
            x, y = rx * math.cos(t), ry * math.sin(t)
            r = math.radians(rot)
            pts.append(P(cx + x * math.cos(r) - y * math.sin(r), cy + x * math.sin(r) + y * math.cos(r)))
        d.polygon(pts, fill=int(255 * a))
        m = m.filter(ImageFilter.GaussianBlur(S * 0.006))
        white = Image.new("RGBA", (S, S), (255, 255, 255, 255))
        white.putalpha(m)
        # clip to existing art
        alpha = self.img.split()[3]
        clipped = Image.composite(m, Image.new("L", (S, S), 0), alpha)
        white.putalpha(clipped)
        self.img = Image.alpha_composite(self.img, white)

    def dots(self, pts, r, col):
        for (x, y) in pts: self.ell(x, y, r, r, col, style="flat")

    def star(self, cx, cy, r, col, n=5, inner=0.45, rot=-90):
        pts = []
        for i in range(n * 2):
            a = math.radians(rot + i * 180 / n)
            rr = r if i % 2 == 0 else r * inner
            pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
        self.poly(pts, col, "flat")

    # ---- output
    def finish(self, path, shadow=True, recenter=True):
        a = self.img.split()[3]
        grow = a.filter(ImageFilter.MaxFilter(2 * int(S * 0.012) + 1))
        grow = grow.filter(ImageFilter.GaussianBlur(S * 0.002))
        outline = Image.new("RGBA", (S, S), OUTLINE + (255,))
        outline.putalpha(grow)
        base = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        if shadow:
            sh = grow.filter(ImageFilter.GaussianBlur(S * 0.015))
            sh = sh.point(lambda v: int(v * 0.28))
            shl = Image.new("RGBA", (S, S), (40, 20, 10, 255))
            shl.putalpha(sh)
            base.alpha_composite(shl, (int(S * 0.006), int(S * 0.018)))
        base = Image.alpha_composite(base, outline)
        base = Image.alpha_composite(base, self.img)
        if not recenter:
            base.resize((OUT, OUT), Image.LANCZOS).save(path)
            return base
        bb = base.getbbox()
        crop = base.crop(bb)
        side = max(crop.size)
        scale = S * 0.9 / side
        crop = crop.resize((max(1, int(crop.width * scale)), max(1, int(crop.height * scale))), Image.LANCZOS)
        final = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        final.paste(crop, ((S - crop.width) // 2, (S - crop.height) // 2), crop)
        final = final.resize((OUT, OUT), Image.LANCZOS)
        final.save(path)
        return final
