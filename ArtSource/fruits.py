import math
from iconlib import *

GREEN = (92, 184, 70); DGREEN = (52, 130, 52); BROWN = (122, 80, 46)


def leaf(i, x, y, length, rot, col=GREEN, wid=0.45):
    pts = []
    for k in range(21):
        t = k / 20
        w = math.sin(t * math.pi) * length * wid * 0.5
        pts.append((t * length, -w))
    for k in range(20, -1, -1):
        t = k / 20
        w = math.sin(t * math.pi) * length * wid * 0.5
        pts.append((t * length, w))
    r = math.radians(rot)
    i.poly([(x + px * math.cos(r) - py * math.sin(r), y + px * math.sin(r) + py * math.cos(r)) for px, py in pts], col, "vert")
    i.stroke([(x, y), (x + length * 0.85 * math.cos(r), y + length * 0.85 * math.sin(r))], 0.008, dark(col, 0.25))


def stem(i, x0, y0, x1, y1, w=0.03, col=BROWN):
    i.curve((x0, y0), ((x0 + x1) / 2 + 0.03, (y0 + y1) / 2), (x1, y1), w, col)


def apple(col, leafcol=GREEN):
    def f(i):
        i.ell(0.40, 0.58, 0.27, 0.30, col); i.ell(0.60, 0.58, 0.27, 0.30, col)
        i.ell(0.50, 0.62, 0.30, 0.28, col)
        stem(i, 0.50, 0.34, 0.54, 0.18); leaf(i, 0.54, 0.26, 0.2, -25, leafcol)
        i.shine(0.34, 0.50, 0.05, 0.10)
    return f


def cherries(i):
    for (x, y) in ((0.32, 0.72), (0.68, 0.75)):
        i.curve((x, y - 0.14), ((x + 0.5) / 2 + 0.1, 0.35), (0.52, 0.15), 0.022, (96, 140, 60))
    leaf(i, 0.52, 0.17, 0.22, 20, GREEN)
    for (x, y) in ((0.32, 0.72), (0.68, 0.75)):
        i.ell(x, y, 0.19, 0.19, (206, 28, 52)); i.shine(x - 0.07, y - 0.07, 0.04, 0.07)


def strawberry(i):
    i.poly([(0.20, 0.38), (0.80, 0.38), (0.72, 0.64), (0.50, 0.92), (0.28, 0.64)], (226, 40, 60), "sphere")
    i.ell(0.35, 0.45, 0.17, 0.14, (226, 40, 60)); i.ell(0.65, 0.45, 0.17, 0.14, (226, 40, 60))
    i.ell(0.50, 0.50, 0.28, 0.22, (226, 40, 60))
    i.dots([(0.35, 0.50), (0.50, 0.46), (0.65, 0.50), (0.42, 0.62), (0.58, 0.62), (0.50, 0.74), (0.32, 0.40), (0.68, 0.40)], 0.014, (255, 226, 120))
    for k, a in enumerate(range(-150, -20, 26)):
        leaf(i, 0.5, 0.33, 0.17, a + 5, GREEN, 0.5)
    i.shine(0.30, 0.46, 0.04, 0.08)


def orange(i):
    i.ell(0.5, 0.56, 0.36, 0.36, (255, 148, 28))
    i.dots([(0.35, 0.45), (0.58, 0.38), (0.66, 0.60), (0.40, 0.70), (0.52, 0.55)], 0.008, (230, 110, 10))
    leaf(i, 0.5, 0.22, 0.22, -30, GREEN); i.ell(0.5, 0.22, 0.03, 0.02, BROWN, style="flat")
    i.shine(0.34, 0.42, 0.06, 0.10)


def lemon_like(col):
    def f(i):
        i.ell(0.5, 0.55, 0.38, 0.28, col, rot=-25)
        i.ell(0.17, 0.74, 0.05, 0.04, col, rot=-25, style="flat"); i.ell(0.83, 0.36, 0.05, 0.04, col, rot=-25, style="flat")
        leaf(i, 0.62, 0.28, 0.2, -50, GREEN)
        i.shine(0.40, 0.50, 0.06, 0.10, rot=-70)
    return f


def grapes(i):
    stem(i, 0.5, 0.30, 0.5, 0.12); leaf(i, 0.5, 0.2, 0.28, -20, GREEN, 0.6)
    rows = [(0.5, 0.36, 3), (0.5, 0.50, 4), (0.5, 0.63, 3), (0.5, 0.75, 2), (0.5, 0.86, 1)]
    cols = [(150, 70, 190), (126, 52, 170), (170, 90, 205)]
    n = 0
    for (cx, cy, c) in [(0.5, 0.40, 3), (0.5, 0.53, 4), (0.5, 0.66, 3), (0.5, 0.78, 2), (0.5, 0.88, 1)]:
        for k in range(c):
            x = cx + (k - (c - 1) / 2) * 0.17
            i.ell(x, cy, 0.09, 0.09, cols[n % 3]); i.shine(x - 0.03, cy - 0.03, 0.02, 0.03, 0.8); n += 1


def banana(i):
    arc = [(0.5 + 0.36 * math.cos(math.radians(205 + 130 * t / 20)), 0.22 + 0.42 * -math.sin(math.radians(205 + 130 * t / 20)) * -1 * 0 + 0.36 * math.sin(math.radians(25 + 130 * t / 20))) for t in range(21)]
    arc = [(0.5 + 0.36 * math.cos(math.radians(200 + 140 * t / 20)), 0.30 - 0.50 * math.sin(math.radians(200 + 140 * t / 20))) for t in range(21)]
    i.stroke(arc, 0.17, (255, 214, 50), "flat")
    i.stroke(arc[2:19], 0.07, (255, 236, 130), "flat")
    i.stroke(arc[2:19], 0.012, (214, 160, 20), "flat")
    i.ell(arc[0][0] - 0.01, arc[0][1] - 0.01, 0.04, 0.04, BROWN, style="flat"); i.ell(arc[-1][0] + 0.01, arc[-1][1] - 0.01, 0.04, 0.04, BROWN, style="flat")


def pear(i):
    i.ell(0.5, 0.67, 0.28, 0.27, (170, 206, 60)); i.ell(0.5, 0.42, 0.17, 0.2, (170, 206, 60))
    i.ell(0.5, 0.52, 0.2, 0.2, (170, 206, 60))
    stem(i, 0.5, 0.25, 0.54, 0.12); leaf(i, 0.54, 0.2, 0.2, -20, DGREEN)
    i.dots([(0.42, 0.7), (0.58, 0.78), (0.55, 0.62)], 0.008, (120, 150, 30)); i.shine(0.40, 0.58, 0.05, 0.1)


def watermelon(i):
    i.poly([(0.08 + 0.84 * (k / 40), 0.30 + 0.55 * math.sin(k / 40 * math.pi) ** 0.9) for k in range(41)] + [(0.08, 0.30)], (60, 150, 60), "vert")
    i.poly([(0.13 + 0.74 * (k / 40), 0.30 + 0.47 * math.sin(k / 40 * math.pi) ** 0.9) for k in range(41)] + [(0.13, 0.30)], (240, 250, 220), "flat")
    i.poly([(0.16 + 0.68 * (k / 40), 0.30 + 0.42 * math.sin(k / 40 * math.pi) ** 0.9) for k in range(41)] + [(0.16, 0.30)], (240, 60, 76), "sphere", (0, -0.4))
    i.rrect(0.06, 0.26, 0.94, 0.33, 0.03, (240, 60, 76), "flat")
    for (x, y) in ((0.35, 0.50), (0.5, 0.58), (0.65, 0.50), (0.43, 0.68), (0.58, 0.72)):
        i.ell(x, y, 0.018, 0.03, (40, 30, 30), style="flat", rot=10)


def peach(i):
    i.ell(0.5, 0.57, 0.33, 0.33, (255, 168, 120))
    i.ell(0.60, 0.60, 0.22, 0.28, (255, 128, 120), style="flat")
    i.ell(0.5, 0.57, 0.33, 0.33, (255, 168, 120), style="sphere")
    i.curve((0.5, 0.30), (0.46, 0.55), (0.52, 0.86), 0.012, (214, 110, 90))
    leaf(i, 0.52, 0.27, 0.22, -20, GREEN); leaf(i, 0.50, 0.27, 0.18, -150, DGREEN)
    i.shine(0.34, 0.48, 0.05, 0.1)


def plum(i):
    i.ell(0.5, 0.57, 0.33, 0.34, (110, 44, 130))
    i.curve((0.5, 0.28), (0.48, 0.55), (0.5, 0.86), 0.01, (70, 24, 90))
    stem(i, 0.5, 0.26, 0.55, 0.12); leaf(i, 0.55, 0.18, 0.18, -20, GREEN)
    i.shine(0.35, 0.47, 0.05, 0.1)


def blueberry(i):
    i.ell(0.5, 0.56, 0.34, 0.34, (66, 90, 190))
    i.star(0.5, 0.40, 0.11, dark((66, 90, 190), 0.4), n=5, inner=0.45)
    i.ell(0.5, 0.40, 0.03, 0.03, (40, 50, 120), style="flat")
    i.shine(0.34, 0.50, 0.05, 0.09)
    leaf(i, 0.58, 0.22, 0.2, -20, GREEN)


def kiwi(i):
    i.ell(0.5, 0.5, 0.40, 0.40, (130, 90, 50), style="flat")
    i.ell(0.5, 0.5, 0.36, 0.36, (128, 190, 60), style="flat")
    i.ell(0.5, 0.5, 0.28, 0.28, (170, 220, 90), style="sphere", lightpos=(0, 0))
    i.ell(0.5, 0.5, 0.10, 0.12, (245, 250, 220), style="flat")
    for k in range(16):
        a = k / 16 * math.tau
        i.ell(0.5 + 0.17 * math.cos(a), 0.5 + 0.17 * math.sin(a), 0.014, 0.022, (30, 40, 20), rot=math.degrees(a) + 90, style="flat")


def pineapple(i):
    for a in range(-60, 61, 20):
        leaf(i, 0.5, 0.36, 0.30, -90 + a, DGREEN if a % 40 else GREEN, 0.3)
    i.ell(0.5, 0.64, 0.25, 0.30, (250, 190, 40))
    for r in range(6):
        for c in range(4):
            x = 0.5 + (c - 1.5) * 0.115 + (0.0575 if r % 2 else 0); y = 0.43 + r * 0.085
            if ((x - 0.5) / 0.22) ** 2 + ((y - 0.64) / 0.26) ** 2 < 1:
                i.ell(x, y, 0.04, 0.04, (255, 214, 80), style="sphere"); i.ell(x, y + 0.004, 0.012, 0.012, (170, 100, 20), style="flat")
    i.shine(0.40, 0.56, 0.04, 0.10)


def tomato(i):
    i.ell(0.5, 0.58, 0.36, 0.31, (232, 50, 40))
    i.star(0.5, 0.36, 0.17, GREEN, n=5, inner=0.4)
    i.ell(0.5, 0.36, 0.035, 0.035, DGREEN, style="flat")
    i.shine(0.33, 0.52, 0.05, 0.09)


def carrot(i):
    i.poly([(0.24, 0.34), (0.58, 0.20), (0.90, 0.82), (0.86, 0.90), (0.80, 0.93)], (255, 140, 30), "sphere")
    i.poly([(0.20, 0.30), (0.62, 0.14), (0.66, 0.36), (0.30, 0.46)], (255, 140, 30), "flat")
    i.poly([(0.22, 0.34), (0.60, 0.20), (0.92, 0.84), (0.84, 0.92)], (255, 140, 30), "sphere")
    for (a, b) in [(0.42, 0.34), (0.52, 0.52), (0.62, 0.50), (0.70, 0.66)]:
        i.stroke([(a, b), (a + 0.07, b - 0.02)], 0.01, (210, 100, 10))
    for a in (-170, -140, -110):
        leaf(i, 0.24, 0.30, 0.26, a + 20, GREEN, 0.3)


def corn(i):
    for a in (-60, 60):
        leaf(i, 0.5, 0.92, 0.55, -90 + a * 0.6, GREEN, 0.35)
    i.ell(0.5, 0.5, 0.20, 0.38, (255, 210, 50))
    for r in range(6):
        for c in range(3):
            i.ell(0.40 + c * 0.10, 0.20 + r * 0.115, 0.04, 0.045, (255, 236, 110), style="flat")
    leaf(i, 0.5, 0.95, 0.5, -120, GREEN, 0.4); leaf(i, 0.5, 0.95, 0.5, -60, DGREEN, 0.4)


def eggplant(i):
    i.ell(0.5, 0.62, 0.22, 0.38, (110, 50, 140), rot=30)
    i.ell(0.34, 0.34, 0.12, 0.14, (110, 50, 140), rot=30)
    i.star(0.31, 0.27, 0.15, GREEN, n=5, inner=0.5)
    stem(i, 0.30, 0.24, 0.22, 0.10, 0.04, DGREEN)
    i.shine(0.60, 0.66, 0.035, 0.12, rot=30)


def avocado(i):
    i.ell(0.5, 0.64, 0.30, 0.30, (70, 120, 50)); i.ell(0.5, 0.38, 0.19, 0.24, (70, 120, 50)); i.ell(0.5, 0.52, 0.22, 0.24, (70, 120, 50))
    i.ell(0.5, 0.64, 0.25, 0.25, (190, 220, 90), style="flat"); i.ell(0.5, 0.40, 0.14, 0.2, (190, 220, 90), style="flat"); i.ell(0.5, 0.52, 0.17, 0.2, (190, 220, 90), style="flat")
    i.ell(0.5, 0.64, 0.12, 0.12, (150, 90, 50)); i.shine(0.46, 0.60, 0.03, 0.04)


def coconut(i):
    i.ell(0.5, 0.5, 0.40, 0.40, (130, 82, 50))
    for k in range(10):
        a = k / 10 * math.tau; i.stroke([(0.5 + 0.1 * math.cos(a), 0.5 + 0.1 * math.sin(a)), (0.5 + 0.38 * math.cos(a), 0.5 + 0.38 * math.sin(a))], 0.01, dark((130, 82, 50), 0.3))
    i.ell(0.5, 0.5, 0.40, 0.40, (130, 82, 50), style="sphere")
    i.dots([(0.42, 0.40), (0.58, 0.40), (0.5, 0.54)], 0.04, (50, 30, 20))
    i.shine(0.33, 0.36, 0.05, 0.09)


def mango(i):
    i.ell(0.5, 0.56, 0.34, 0.28, (255, 168, 40), rot=-30)
    i.ell(0.60, 0.50, 0.2, 0.2, (236, 90, 60), rot=-30, style="flat")
    i.ell(0.5, 0.56, 0.34, 0.28, (255, 168, 40), rot=-30, style="sphere")
    stem(i, 0.34, 0.36, 0.30, 0.22); leaf(i, 0.32, 0.30, 0.24, -150, GREEN)
    i.shine(0.40, 0.64, 0.04, 0.09, rot=-30)


def raspberry(i):
    base = (218, 40, 90)
    pts = [(0.5, 0.42), (0.36, 0.50), (0.64, 0.50), (0.28, 0.62), (0.5, 0.60), (0.72, 0.62), (0.38, 0.74), (0.62, 0.74), (0.5, 0.82), (0.32, 0.38), (0.68, 0.38), (0.5, 0.30)]
    for (x, y) in pts: i.ell(x, y, 0.095, 0.095, base)
    for (x, y) in pts: i.shine(x - 0.03, y - 0.03, 0.015, 0.025, 0.7)
    for a in range(-150, -20, 30): leaf(i, 0.5, 0.26, 0.16, a, GREEN, 0.4)


def pomegranate(i):
    i.ell(0.5, 0.58, 0.35, 0.33, (200, 30, 56))
    i.poly([(0.38, 0.30), (0.42, 0.18), (0.50, 0.26), (0.58, 0.18), (0.62, 0.30)], (200, 30, 56), "vert")
    i.dots([(0.4, 0.5), (0.5, 0.58), (0.6, 0.5), (0.45, 0.7), (0.58, 0.70)], 0.012, (255, 150, 160))
    i.shine(0.34, 0.50, 0.05, 0.1)


def pepper(i):
    i.ell(0.36, 0.60, 0.17, 0.27, (50, 170, 70)); i.ell(0.64, 0.60, 0.17, 0.27, (50, 170, 70)); i.ell(0.5, 0.64, 0.17, 0.27, (50, 170, 70))
    i.ell(0.5, 0.58, 0.28, 0.27, (50, 170, 70))
    i.curve((0.4, 0.38), (0.4, 0.62), (0.4, 0.86), 0.012, (30, 110, 40)); i.curve((0.6, 0.38), (0.6, 0.62), (0.6, 0.86), 0.012, (30, 110, 40))
    stem(i, 0.5, 0.32, 0.54, 0.14, 0.04, DGREEN)
    i.shine(0.34, 0.52, 0.04, 0.12)


def mushroom(i):
    i.rrect(0.38, 0.54, 0.62, 0.90, 0.08, (245, 230, 205), "sphere")
    i.poly([(0.10 + 0.8 * (k / 40), 0.62 - 0.46 * math.sin(k / 40 * math.pi) ** 0.8) for k in range(41)], (220, 40, 50), "sphere", (-0.3, -0.5))
    i.rrect(0.10, 0.56, 0.90, 0.68, 0.05, (200, 30, 44), "flat")
    for (x, y, r) in ((0.30, 0.46, 0.06), (0.50, 0.34, 0.07), (0.70, 0.46, 0.06), (0.5, 0.52, 0.04)):
        i.ell(x, y, r, r * 0.9, (255, 245, 240), style="flat")


def pumpkin(i):
    for dx, rx in ((-0.22, 0.17), (0.22, 0.17), (-0.1, 0.18), (0.1, 0.18)):
        i.ell(0.5 + dx, 0.60, rx, 0.30, (255, 130, 30))
    i.ell(0.5, 0.60, 0.1, 0.30, (255, 150, 50))
    for dx in (-0.14, 0.0, 0.14): i.curve((0.5 + dx, 0.34), (0.5 + dx * 1.5, 0.6), (0.5 + dx, 0.86), 0.01, (200, 90, 10))
    stem(i, 0.5, 0.32, 0.54, 0.16, 0.05, (90, 130, 50)); i.shine(0.30, 0.52, 0.04, 0.09)


FRUITS = [
    ("apple_red", apple((222, 36, 48))), ("apple_green", apple((130, 200, 50), DGREEN)), ("cherries", cherries),
    ("strawberry", strawberry), ("orange", orange), ("lemon", lemon_like((255, 232, 60))), ("lime", lemon_like((120, 200, 60))),
    ("grapes", grapes), ("banana", banana), ("pear", pear), ("watermelon", watermelon), ("peach", peach), ("plum", plum),
    ("blueberry", blueberry), ("kiwi", kiwi), ("pineapple", pineapple), ("tomato", tomato), ("carrot", carrot), ("corn", corn),
    ("eggplant", eggplant), ("avocado", avocado), ("coconut", coconut), ("mango", mango), ("raspberry", raspberry),
    ("pomegranate", pomegranate), ("pepper", pepper), ("mushroom", mushroom), ("pumpkin", pumpkin),
]
