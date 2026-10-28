import os, sys, importlib
sys.path.insert(0, os.path.dirname(__file__))
from PIL import Image
from iconlib import Icon

def render(theme, out_root):
    mod = importlib.import_module(theme)
    items = getattr(mod, theme.upper())
    d = os.path.join(out_root, theme.capitalize())
    os.makedirs(d, exist_ok=True)
    imgs = []
    for idx, (name, fn) in enumerate(items, 1):
        ic = Icon(); fn(ic)
        imgs.append(ic.finish(os.path.join(d, f"{idx:02d}_{name}.png")))
    # contact sheet for review
    cols = 7; rows = (len(imgs) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * 256, rows * 256), (250, 246, 232, 255))
    for k, im in enumerate(imgs): sheet.alpha_composite(im, ((k % cols) * 256, (k // cols) * 256))
    sheet.convert("RGB").save(os.path.join(out_root, f"_sheet_{theme}.png"))
    print(theme, len(imgs))

if __name__ == "__main__":
    out = sys.argv[1]
    for t in sys.argv[2:]: render(t, out)
