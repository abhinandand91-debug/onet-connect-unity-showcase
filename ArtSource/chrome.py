"""Tile chrome (tile face, selection frame, path star) and per-theme backdrops."""
import math, os, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from iconlib import Icon

def rr_mask(size, box, r):
    m = Image.new("L", size, 0); ImageDraw.Draw(m).rounded_rectangle(box, radius=r, fill=255); return m

def vgrad(size, top, bot):
    h = size[1]; a = np.zeros((h, size[0], 4), np.uint8)
    for y in range(h):
        t = y / max(1, h - 1); a[y, :, :3] = [top[k] + (bot[k] - top[k]) * t for k in range(3)]; a[y, :, 3] = 255
    return Image.fromarray(a, "RGBA")

def tile_bg(path, S=1024, OUT=256):
    base = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    side = vgrad((S, S), (206, 190, 160), (176, 156, 126)); side.putalpha(rr_mask((S, S), (30, 60, S - 30, S - 20), 150))
    base.alpha_composite(side)
    face = vgrad((S, S), (255, 252, 240), (244, 236, 214)); face.putalpha(rr_mask((S, S), (30, 30, S - 30, S - 70), 150))
    base.alpha_composite(face)
    inner = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(inner)
    d.rounded_rectangle((60, 60, S - 60, S - 100), radius=125, outline=(255, 255, 255, 170), width=14)
    base.alpha_composite(inner.filter(ImageFilter.GaussianBlur(5)))
    out = Image.new("RGBA", (S, S), (0, 0, 0, 0)); out.alpha_composite(base)
    edge = Image.new("RGBA", (S, S), (110, 90, 70, 255)); a = rr_mask((S, S), (30, 60, S - 30, S - 20), 150)
    ring = Image.composite(Image.new("L", (S, S), 255), Image.new("L", (S, S), 0), a)
    ring = Image.eval(ring, lambda v: v); edge.putalpha(Image.fromarray(np.clip(np.array(a, np.int16) - np.array(a.filter(ImageFilter.MinFilter(9)), np.int16), 0, 255).astype(np.uint8)))
    out.alpha_composite(edge)
    out.resize((OUT, OUT), Image.LANCZOS).save(path)

def tile_frame(path, S=1024, OUT=256):
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0)); ImageDraw.Draw(glow).rounded_rectangle((24, 24, S - 24, S - 64), radius=150, outline=(255, 210, 60, 255), width=70)
    img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(22)))
    ImageDraw.Draw(img).rounded_rectangle((34, 34, S - 34, S - 74), radius=140, outline=(255, 196, 40, 255), width=34)
    ImageDraw.Draw(img).rounded_rectangle((52, 52, S - 52, S - 92), radius=125, outline=(255, 240, 150, 255), width=10)
    img.resize((OUT, OUT), Image.LANCZOS).save(path)

def path_star(path):
    ic = Icon(); ic.star(0.5, 0.52, 0.42, (255, 206, 50), n=5, inner=0.48); ic.star(0.5, 0.52, 0.27, (255, 238, 130), n=5, inner=0.48); ic.shine(0.42, 0.38, 0.04, 0.05)
    ic.finish(path, shadow=False)

def backdrop(path, top, bot, dot, seed, W=512, H=1024):
    rnd = random.Random(seed)
    img = vgrad((W, H), top, bot)
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(layer)
    for _ in range(26):
        r = rnd.randint(30, 110); x, y = rnd.randint(-20, W + 20), rnd.randint(-20, H + 20)
        d.ellipse((x - r, y - r, x + r, y + r), fill=dot + (rnd.randint(18, 38),))
    img.alpha_composite(layer.filter(ImageFilter.GaussianBlur(3)))
    img.convert("RGB").save(path)
