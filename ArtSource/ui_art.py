"""HUD art in the Cute Onet pack's style: raised buttons, glass cards, coin, heart, chevron, hilltop backdrops."""
import math, os, random, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

K = 4  # supersample


def rrect(draw, box, r, fill=None, outline=None, width=0):
    draw.rounded_rectangle([v * K for v in box], radius=r * K, fill=fill, outline=outline, width=width * K)


def lerp(a, b, t): return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))


def gradient_fill(size, box, r, top, bottom):
    w, h = size
    base = Image.new("RGBA", (w * K, h * K), (0, 0, 0, 0))
    col = Image.new("RGBA", (w * K, h * K))
    px = np.zeros((h * K, w * K, 4), np.uint8)
    y0, y1 = box[1] * K, box[3] * K
    for y in range(h * K):
        t = min(1, max(0, (y - y0) / max(1, y1 - y0)))
        px[y, :, :3] = lerp(top, bottom, t)[:3]; px[y, :, 3] = 255
    col = Image.fromarray(px, "RGBA")
    m = Image.new("L", (w * K, h * K), 0)
    ImageDraw.Draw(m).rounded_rectangle([v * K for v in box], radius=r * K, fill=255)
    col.putalpha(m)
    return col


def raised_button(path, face_top, face_bottom, lip, size=128):
    img = Image.new("RGBA", (size * K, size * K), (0, 0, 0, 0))
    lipimg = Image.new("RGBA", (size * K, size * K), (0, 0, 0, 0))
    rrect(ImageDraw.Draw(lipimg), (4, 12, size - 4, size - 4), 36, fill=lip + (255,))
    img.alpha_composite(lipimg)
    img.alpha_composite(gradient_fill((size, size), (4, 4, size - 4, size - 14), 36, face_top, face_bottom))
    hi = Image.new("RGBA", (size * K, size * K), (0, 0, 0, 0))
    rrect(ImageDraw.Draw(hi), (10, 8, size - 10, size - 20), 30, outline=(255, 255, 255, 120), width=3)
    img.alpha_composite(hi.filter(ImageFilter.GaussianBlur(K)))
    img.resize((size, size), Image.LANCZOS).save(path)


def card(path, size=128, fill=(255, 255, 250, 236), border=(255, 255, 255, 255), radius=38):
    img = Image.new("RGBA", (size * K, size * K), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    rrect(d, (2, 2, size - 2, size - 2), radius, fill=border)
    rrect(d, (5, 5, size - 5, size - 5), radius - 3, fill=fill)
    img.resize((size, size), Image.LANCZOS).save(path)


def coin(path, size=128):
    img = Image.new("RGBA", (size * K, size * K), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    c = size * K / 2
    d.ellipse((6 * K, 12 * K, (size - 6) * K, (size - 2) * K), fill=(214, 150, 20, 255))       # edge
    d.ellipse((6 * K, 6 * K, (size - 6) * K, (size - 10) * K), fill=(255, 200, 60, 255))        # face
    d.ellipse((20 * K, 20 * K, (size - 20) * K, (size - 24) * K), outline=(232, 160, 30, 255), width=5 * K)
    d.arc((38 * K, 34 * K, (size - 38) * K, (size - 38) * K), 40, 320, fill=(255, 244, 190, 255), width=9 * K)   # stylised glyph
    d.line((c, 28 * K, c, (size - 32) * K), fill=(255, 244, 190, 255), width=8 * K)
    img.resize((size, size), Image.LANCZOS).save(path)


def heart(path, size=128):
    img = Image.new("RGBA", (size * K, size * K), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    pts = []
    for i in range(200):
        t = i / 200 * math.tau
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts.append((size * K / 2 + x * size * K / 40, size * K * 0.46 - y * size * K / 40))
    d.polygon(pts, fill=(255, 92, 106, 255))
    sh = Image.new("RGBA", (size * K, size * K), (0, 0, 0, 0)); ImageDraw.Draw(sh).ellipse((28 * K, 28 * K, 50 * K, 44 * K), fill=(255, 255, 255, 150))
    img.alpha_composite(sh.filter(ImageFilter.GaussianBlur(3 * K)))
    img.resize((size, size), Image.LANCZOS).save(path)


def chevron(path, size=128):
    img = Image.new("RGBA", (size * K, size * K), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    pts = [(78 * K, 28 * K), (46 * K, 64 * K), (78 * K, 100 * K)]
    d.line(pts, fill=(36, 92, 96, 255), width=14 * K, joint="curve")
    for p in (pts[0], pts[2]): d.ellipse((p[0] - 7 * K, p[1] - 7 * K, p[0] + 7 * K, p[1] + 7 * K), fill=(36, 92, 96, 255))
    img.resize((size, size), Image.LANCZOS).save(path)


def backdrop(path, top, bottom, hill1, hill2, seed, W=540, H=960):
    """Rendered at 2x on an opaque gradient and downsampled once, so soft shapes never pick up a dark fringe."""
    S = 2
    w, h = W * S, H * S
    px = np.zeros((h, w, 3), np.uint8)
    for y in range(h):
        px[y, :, :] = lerp(top, bottom, y / h)[:3]
    img = Image.fromarray(px).convert("RGBA")
    mask = Image.new("L", (w, h), 0); d = ImageDraw.Draw(mask)
    for (cx, cy, sc) in ((0.04, 0.13, 1.0), (0.96, 0.46, 0.9), (0.2, 0.70, 0.6)):
        for k in range(4):
            r = int(w * 0.07 * sc * (1 - 0.15 * k)); x = int(w * cx + k * r * 0.9); y = int(h * cy + (k % 2) * r * 0.3)
            d.ellipse((x - r, y - r * 0.8, x + r, y + r * 0.8), fill=120)
    mask = mask.filter(ImageFilter.GaussianBlur(14))
    white = Image.new("RGBA", (w, h), (255, 255, 255, 255)); white.putalpha(mask)      # white RGB everywhere: blur only changes alpha
    img.alpha_composite(white)
    for (cx, cy, rx, ry, col) in ((0.15, 1.02, 0.80, 0.20, hill1), (0.92, 1.05, 0.70, 0.17, hill2)):
        layer = Image.new("RGBA", (w, h), col + (255,)); m = Image.new("L", (w, h), 0)
        ImageDraw.Draw(m).ellipse(((cx - rx) * w, (cy - ry) * h, (cx + rx) * w, (cy + ry) * h), fill=255)
        layer.putalpha(m); img.alpha_composite(layer)
    img.convert("RGB").resize((W, H), Image.LANCZOS).save(path)


def bar(path, w=96, h=32):
    img = Image.new("RGBA", (w * K, h * K), (0, 0, 0, 0))
    ImageDraw.Draw(img).rounded_rectangle((0, 0, w * K - 1, h * K - 1), radius=h * K // 2, fill=(255, 255, 255, 255))
    img.resize((w, h), Image.LANCZOS).save(path)


def padlock(path, size=128):
    img = Image.new("RGBA", (size * K, size * K), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    d.arc((34 * K, 14 * K, 94 * K, 74 * K), 180, 360, fill=(232, 240, 242, 255), width=11 * K)       # shackle
    d.line((34 * K, 44 * K, 34 * K, 62 * K), fill=(232, 240, 242, 255), width=11 * K)
    d.line((94 * K, 44 * K, 94 * K, 62 * K), fill=(232, 240, 242, 255), width=11 * K)
    rrect(d, (22, 56, 106, 116), 16, fill=(108, 128, 140, 255))                                          # body
    rrect(d, (22, 56, 106, 106), 16, fill=(138, 160, 172, 255))
    d.ellipse((56 * K, 72 * K, 72 * K, 88 * K), fill=(70, 86, 96, 255)); d.rectangle((61 * K, 82 * K, 67 * K, 98 * K), fill=(70, 86, 96, 255))
    img.resize((size, size), Image.LANCZOS).save(path)


def padlock_open(path, size=128):
    """padlock() with the shackle lifted out of the body: the right leg stays seated, the left one hangs free."""
    img = Image.new("RGBA", (size * K, size * K), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    lift = 14
    d.arc((34 * K, (14 - lift) * K, 94 * K, (74 - lift) * K), 180, 360, fill=(232, 240, 242, 255), width=11 * K)
    d.line((34 * K, (44 - lift) * K, 34 * K, 42 * K), fill=(232, 240, 242, 255), width=11 * K)          # free leg, gap above body
    d.line((94 * K, (44 - lift) * K, 94 * K, 62 * K), fill=(232, 240, 242, 255), width=11 * K)          # seated leg
    rrect(d, (22, 56, 106, 116), 16, fill=(108, 128, 140, 255))
    rrect(d, (22, 56, 106, 106), 16, fill=(138, 160, 172, 255))
    d.ellipse((56 * K, 72 * K, 72 * K, 88 * K), fill=(70, 86, 96, 255)); d.rectangle((61 * K, 82 * K, 67 * K, 98 * K), fill=(70, 86, 96, 255))
    img.resize((size, size), Image.LANCZOS).save(path)


def main(out):
    os.makedirs(out, exist_ok=True)
    raised_button(os.path.join(out, "ui9_btn_cream.png"), (255, 255, 250), (244, 250, 244), (160, 204, 190))
    raised_button(os.path.join(out, "ui9_btn_coral.png"), (248, 150, 134), (238, 128, 116), (210, 98, 92))
    card(os.path.join(out, "ui9_card.png"))
    card(os.path.join(out, "ui9_tray.png"), fill=(255, 255, 255, 70), border=(255, 255, 255, 190))
    card(os.path.join(out, "ui9_pill.png"), fill=(255, 255, 255, 255), border=(255, 255, 255, 255), radius=62)
    padlock(os.path.join(out, "padlock.png")); padlock_open(os.path.join(out, "padlock_open.png")); bar(os.path.join(out, "ui9_bar.png"))
    coin(os.path.join(out, "coin.png")); heart(os.path.join(out, "heart.png")); chevron(os.path.join(out, "chevron.png"))


def backdrops(out):
    os.makedirs(out, exist_ok=True)
    backdrop(os.path.join(out, "bg_meadow.png"), (196, 238, 218), (158, 218, 198), (134, 208, 176), (112, 190, 160), 21)
    backdrop(os.path.join(out, "bg_sunset.png"), (255, 226, 206), (250, 206, 190), (240, 178, 150), (226, 156, 134), 22)
    backdrop(os.path.join(out, "bg_dream.png"), (218, 222, 252), (190, 196, 238), (160, 168, 224), (140, 148, 208), 23)


if __name__ == "__main__":
    main(sys.argv[1]); backdrops(sys.argv[2])
