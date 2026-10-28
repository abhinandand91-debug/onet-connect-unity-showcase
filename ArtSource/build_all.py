import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import render, chrome
out = sys.argv[1]
os.makedirs(out, exist_ok=True)
for t in ("fruits", "sweets", "flowers"): render.render(t, out)
c = os.path.join(out, "Common"); os.makedirs(c, exist_ok=True)
chrome.tile_bg(os.path.join(c, "tile_bg.png")); chrome.tile_frame(os.path.join(c, "tile_frame.png")); chrome.path_star(os.path.join(c, "path_star.png"))
chrome.backdrop(os.path.join(out, "Fruits", "bg.png"), (190, 226, 170), (232, 244, 196), (255, 255, 255), 1)
chrome.backdrop(os.path.join(out, "Sweets", "bg.png"), (255, 214, 226), (255, 240, 214), (255, 255, 255), 2)
chrome.backdrop(os.path.join(out, "Flowers", "bg.png"), (200, 214, 250), (250, 226, 240), (255, 255, 255), 3)
