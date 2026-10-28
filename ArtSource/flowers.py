import math
from iconlib import *
from fruits import leaf, stem, GREEN, DGREEN, BROWN


def petals(i, cx, cy, n, length, width, col, offset=0.0, rot0=-90, style="sphere"):
    for k in range(n):
        a = math.radians(rot0 + k * 360 / n)
        mx, my = cx + math.cos(a) * length * 0.5, cy + math.sin(a) * length * 0.5
        i.ell(mx, my, length * 0.5, width * 0.5, col, rot=math.degrees(a), style=style, lightpos=(-0.2, -0.3))


def flower(n, length, width, col, center, ccol, r2=None, col2=None, stemmed=True):
    def f(i):
        if stemmed:
            i.curve((0.5, 0.95), (0.55, 0.78), (0.5, 0.58), 0.03, DGREEN); leaf(i, 0.52, 0.86, 0.2, -30, GREEN, 0.5)
        cy = 0.4 if stemmed else 0.5
        s = 1.0 if not stemmed else 0.88
        petals(i, 0.5, cy, n, length * s, width * s, col)
        if r2: petals(i, 0.5, cy, n, length * s * 0.6, width * s * 0.9, col2, rot0=-90 + 180 / n)
        i.ell(0.5, cy, center, center, ccol)
        i.dots([(0.5 + math.cos(k * 2.4) * center * 0.55 * (k % 3) / 2, cy + math.sin(k * 2.4) * center * 0.55 * (k % 3) / 2) for k in range(8)], center * 0.09, dark(ccol, 0.4))
    return f


def tulip(col, col2):
    def f(i):
        i.curve((0.5, 0.96), (0.52, 0.76), (0.5, 0.56), 0.035, DGREEN)
        leaf(i, 0.5, 0.95, 0.4, -125, GREEN, 0.4); leaf(i, 0.5, 0.95, 0.4, -55, GREEN, 0.4)
        i.ell(0.36, 0.40, 0.14, 0.26, col2, rot=-12); i.ell(0.64, 0.40, 0.14, 0.26, col2, rot=12)
        i.ell(0.5, 0.40, 0.17, 0.30, col)
        for (x, y, rot) in ((0.38, 0.17, -10), (0.5, 0.12, 0), (0.62, 0.17, 10)):
            i.ell(x, y, 0.07, 0.13, col, rot=rot)
        i.shine(0.43, 0.34, 0.03, 0.09)
    return f


def rose(col):
    def f(i):
        i.curve((0.5, 0.96), (0.46, 0.78), (0.5, 0.62), 0.03, DGREEN); leaf(i, 0.5, 0.84, 0.24, -30, GREEN, 0.5); leaf(i, 0.48, 0.74, 0.22, -150, GREEN, 0.5)
        i.ell(0.5, 0.40, 0.34, 0.32, dark(col, 0.25))
        i.ell(0.5, 0.40, 0.30, 0.28, col)
        i.ell(0.5, 0.44, 0.22, 0.2, light(col, 0.15))
        pts = [(0.5 + (0.02 + t * 0.18) * math.cos(t * 12), 0.40 + (0.02 + t * 0.16) * math.sin(t * 12)) for t in [k / 60 for k in range(61)]]
        i.stroke(pts, 0.03, dark(col, 0.35), "flat")
        i.shine(0.34, 0.28, 0.04, 0.07)
    return f


def lotus(i):
    i.ell(0.5, 0.78, 0.40, 0.10, (70, 160, 100))
    for a, c in ((-70, (255, 150, 190)), (70, (255, 150, 190)), (-40, (255, 170, 205)), (40, (255, 170, 205))):
        i.ell(0.5 + math.sin(math.radians(a)) * 0.2, 0.52, 0.10, 0.28, c, rot=a)
    i.ell(0.5, 0.48, 0.12, 0.32, (255, 190, 220)); i.ell(0.5, 0.52, 0.06, 0.24, (255, 230, 240), style="flat")


def hibiscus(i):
    petals(i, 0.5, 0.52, 5, 0.40, 0.30, (240, 50, 70), rot0=-90)
    i.ell(0.5, 0.52, 0.10, 0.10, (170, 20, 50)); i.curve((0.5, 0.52), (0.62, 0.40), (0.72, 0.30), 0.02, (255, 214, 90))
    i.dots([(0.72, 0.30), (0.69, 0.33), (0.74, 0.34)], 0.02, (255, 190, 40))


def blossom(i):
    for k in range(5):
        a = math.radians(-90 + k * 72)
        cx, cy = 0.5 + math.cos(a) * 0.20, 0.5 + math.sin(a) * 0.20
        i.ell(cx, cy, 0.17, 0.17, (255, 190, 210)); 
        i.poly([(cx + math.cos(a) * 0.17 - 0.03, cy + math.sin(a) * 0.17 - 0.03), (cx + math.cos(a) * 0.1, cy + math.sin(a) * 0.1), (cx + math.cos(a) * 0.17 + 0.03, cy + math.sin(a) * 0.17 + 0.03)], (250, 244, 240), "flat")
    i.ell(0.5, 0.5, 0.07, 0.07, (240, 100, 140))
    for k in range(5):
        a = math.radians(-72 + k * 72); i.stroke([(0.5, 0.5), (0.5 + 0.12 * math.cos(a), 0.5 + 0.12 * math.sin(a))], 0.008, (200, 60, 100)); i.ell(0.5 + 0.12 * math.cos(a), 0.5 + 0.12 * math.sin(a), 0.014, 0.014, (255, 214, 90), style="flat")


def lavender(i):
    i.curve((0.5, 0.96), (0.5, 0.7), (0.5, 0.2), 0.025, DGREEN)
    for k in range(9):
        y = 0.16 + k * 0.07
        i.ell(0.5 - 0.05 - (0.02 if k < 3 else 0), y + 0.01, 0.045, 0.03, (150, 100, 230), rot=-30); i.ell(0.5 + 0.05 + (0.02 if k < 3 else 0), y + 0.01, 0.045, 0.03, (170, 120, 240), rot=30)
        i.ell(0.5, y - 0.01, 0.04, 0.03, (190, 140, 250))
    leaf(i, 0.5, 0.92, 0.26, -140, GREEN, 0.3); leaf(i, 0.5, 0.92, 0.26, -40, GREEN, 0.3)


def clover(i):
    for a in (0, 90, 180, 270):
        r = math.radians(a - 45)
        for s in (-1, 1):
            i.ell(0.5 + math.cos(r + s * 0.5) * 0.17, 0.46 + math.sin(r + s * 0.5) * 0.17, 0.14, 0.14, GREEN if a % 180 else (110, 200, 80))
    i.curve((0.5, 0.5), (0.54, 0.74), (0.62, 0.94), 0.03, DGREEN)
    for a in (-135, -45, 45, 135):
        r = math.radians(a); i.stroke([(0.5, 0.46), (0.5 + 0.2 * math.cos(r), 0.46 + 0.2 * math.sin(r))], 0.01, DGREEN)


def sprout(i):
    i.ell(0.5, 0.88, 0.34, 0.10, (140, 90, 56)); i.ell(0.5, 0.82, 0.28, 0.08, (170, 116, 70))
    i.curve((0.5, 0.82), (0.5, 0.6), (0.5, 0.42), 0.035, DGREEN)
    leaf(i, 0.5, 0.5, 0.34, -150, (130, 210, 90), 0.55); leaf(i, 0.5, 0.42, 0.34, -30, GREEN, 0.55)


def cactus(i):
    i.rrect(0.32, 0.18, 0.60, 0.82, 0.14, (80, 170, 90), "sphere")
    i.rrect(0.14, 0.38, 0.26, 0.62, 0.06, (80, 170, 90), "sphere"); i.rrect(0.14, 0.52, 0.40, 0.62, 0.05, (80, 170, 90), "sphere")
    i.rrect(0.66, 0.30, 0.78, 0.52, 0.06, (80, 170, 90), "sphere"); i.rrect(0.58, 0.44, 0.78, 0.54, 0.05, (80, 170, 90), "sphere")
    for x in (0.40, 0.46, 0.52): i.stroke([(x, 0.2), (x, 0.8)], 0.008, (50, 120, 60))
    i.poly([(0.26, 0.80), (0.74, 0.80), (0.68, 0.95), (0.32, 0.95)], (210, 110, 80), "vert")
    i.ell(0.46, 0.16, 0.05, 0.04, (255, 120, 170))


def maple(i):
    pts = [(0.5, 0.06), (0.58, 0.26), (0.74, 0.18), (0.70, 0.42), (0.92, 0.40), (0.78, 0.58), (0.86, 0.66), (0.56, 0.70), (0.54, 0.94), (0.46, 0.94), (0.44, 0.70), (0.14, 0.66), (0.22, 0.58), (0.08, 0.40), (0.30, 0.42), (0.26, 0.18), (0.42, 0.26)]
    i.poly(pts, (232, 90, 36), "sphere")
    i.stroke([(0.5, 0.9), (0.5, 0.2)], 0.015, (150, 40, 20)); i.stroke([(0.5, 0.66), (0.78, 0.5)], 0.01, (150, 40, 20)); i.stroke([(0.5, 0.66), (0.22, 0.5)], 0.01, (150, 40, 20))


def potted(i):
    leaf(i, 0.5, 0.55, 0.40, -140, GREEN, 0.5); leaf(i, 0.5, 0.55, 0.40, -40, (110, 200, 80), 0.5); leaf(i, 0.5, 0.55, 0.40, -90, DGREEN, 0.4)
    i.poly([(0.26, 0.56), (0.74, 0.56), (0.66, 0.94), (0.34, 0.94)], (220, 110, 70), "vert"); i.rrect(0.22, 0.52, 0.78, 0.64, 0.03, (236, 140, 90), "vert")


def lily(i):
    i.curve((0.5, 0.96), (0.52, 0.78), (0.5, 0.58), 0.03, DGREEN)
    for k in range(6):
        a = math.radians(-90 + k * 60)
        mx, my = 0.5 + math.cos(a) * 0.19, 0.42 + math.sin(a) * 0.19
        i.poly([(0.5, 0.42), (mx + math.cos(a + 1.57) * 0.07, my + math.sin(a + 1.57) * 0.07), (0.5 + math.cos(a) * 0.40, 0.42 + math.sin(a) * 0.40), (mx - math.cos(a + 1.57) * 0.07, my - math.sin(a + 1.57) * 0.07)], (255, 140, 60) if k % 2 else (255, 170, 90), "vert")
        i.ell(mx, my, 0.014, 0.014, (150, 40, 30), style="flat")
    i.ell(0.5, 0.42, 0.045, 0.045, (255, 220, 100))


def daffodil(i):
    i.curve((0.5, 0.96), (0.52, 0.78), (0.5, 0.58), 0.03, DGREEN)
    petals(i, 0.5, 0.42, 6, 0.40, 0.2, (255, 232, 70))
    i.ell(0.5, 0.42, 0.13, 0.13, (255, 160, 40)); i.ell(0.5, 0.42, 0.08, 0.08, (255, 190, 70), style="flat")


def sunflower(i):
    i.curve((0.5, 0.95), (0.54, 0.78), (0.5, 0.62), 0.035, DGREEN); leaf(i, 0.52, 0.86, 0.24, -30, GREEN, 0.5)
    petals(i, 0.5, 0.42, 14, 0.40, 0.14, (255, 200, 20)); petals(i, 0.5, 0.42, 14, 0.32, 0.14, (255, 220, 60), rot0=-90 + 12.9)
    i.ell(0.5, 0.42, 0.14, 0.14, (110, 64, 30))
    i.dots([(0.5 + 0.08 * math.cos(k), 0.42 + 0.08 * math.sin(k)) for k in range(0, 7)], 0.01, (60, 34, 14))


def poppy(i):
    i.curve((0.5, 0.96), (0.52, 0.78), (0.5, 0.58), 0.025, DGREEN)
    for dx, dy, r in ((-0.12, 0, 0.24), (0.12, 0, 0.24), (0, -0.1, 0.24), (0, 0.1, 0.22)):
        i.ell(0.5 + dx, 0.42 + dy, r, r, (236, 50, 40))
    i.ell(0.5, 0.42, 0.07, 0.07, (40, 30, 40)); i.dots([(0.5 + 0.09 * math.cos(k * 0.9), 0.42 + 0.09 * math.sin(k * 0.9)) for k in range(7)], 0.012, (40, 30, 40))


def pansy(i):
    i.ell(0.34, 0.38, 0.2, 0.22, (120, 70, 200)); i.ell(0.66, 0.38, 0.2, 0.22, (120, 70, 200))
    i.ell(0.34, 0.66, 0.2, 0.2, (160, 110, 230)); i.ell(0.66, 0.66, 0.2, 0.2, (160, 110, 230)); i.ell(0.5, 0.74, 0.18, 0.18, (255, 214, 70))
    i.ell(0.5, 0.52, 0.09, 0.09, (255, 236, 110))
    for a in (-30, 90, 210): i.stroke([(0.5, 0.52), (0.5 + 0.2 * math.cos(math.radians(a + 90)), 0.52 + 0.2 * math.sin(math.radians(a + 90)))], 0.015, (60, 30, 110))


FLOWERS = [
    ("daisy", flower(12, 0.34, 0.10, (255, 255, 255), 0.09, (255, 210, 40))), ("sunflower", sunflower), ("tulip_red", tulip((230, 40, 70), (210, 30, 60))),
    ("tulip_yellow", tulip((255, 210, 50), (250, 190, 30))), ("rose_red", rose((220, 30, 60))), ("rose_pink", rose((255, 130, 170))), ("lotus", lotus),
    ("hibiscus", hibiscus), ("blossom", blossom), ("poppy", poppy), ("pansy", pansy), ("lavender", lavender),
    ("cosmos", flower(8, 0.34, 0.20, (255, 120, 190), 0.07, (255, 214, 60))), ("forgetmenot", flower(5, 0.30, 0.20, (110, 170, 255), 0.06, (255, 230, 90))),
    ("marigold", flower(18, 0.34, 0.12, (255, 150, 30), 0.1, (230, 100, 10), True, (255, 190, 60))), ("daffodil", daffodil), ("lily", lily),
    ("violet_flower", flower(5, 0.32, 0.22, (170, 110, 230), 0.06, (255, 230, 90))), ("camellia", flower(10, 0.30, 0.2, (255, 90, 120), 0.07, (255, 220, 80), True, (255, 140, 160))),
    ("clover", clover), ("sprout", sprout), ("cactus", cactus), ("maple", maple), ("potted", potted),
    ("dahlia", flower(16, 0.36, 0.14, (200, 60, 150), 0.08, (255, 190, 60), True, (230, 110, 190))), ("peony", flower(12, 0.34, 0.24, (255, 180, 200), 0.08, (255, 214, 90), True, (255, 220, 230))),
    ("bluebell", flower(6, 0.34, 0.16, (80, 120, 240), 0.06, (230, 240, 255))), ("whitelily", flower(6, 0.38, 0.14, (250, 250, 255), 0.06, (255, 214, 80))),
]
