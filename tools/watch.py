"""watch.py: grabs the desktop every <step> s for <total> s, crops the game window, writes contact sheets gen/watch/sheet_N.png
usage: python tools/watch.py <total_seconds> <step_seconds>   (start the preview first)"""
import sys, time, os
from PIL import ImageGrab, Image, ImageDraw
total, step = float(sys.argv[1]), float(sys.argv[2])
root = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'gen', 'watch')
os.makedirs(root, exist_ok=True)
for f in os.listdir(root): os.remove(os.path.join(root, f))
frames, t0 = [], time.time()
while time.time() - t0 < total:
    t = time.time() - t0
    im = ImageGrab.grab()
    w, h = im.size
    box = (int(w * 160 / 1280), int(h * 130 / 800), int(w * 1120 / 1280), int(h * 670 / 800))
    full = im.crop(box).resize((960, 540)); full.save(os.path.join(root, 'f_%03d.png' % len(frames)))
    g = full.resize((480, 270))
    ImageDraw.Draw(g).text((6, 252), 't=%.0fs' % t, fill=(255, 255, 0))
    frames.append(g)
    time.sleep(max(0, step - ((time.time() - t0) - t)))
per = 12
for i in range(0, len(frames), per):
    sheet = Image.new('RGB', (480 * 3, 270 * 4), (0, 0, 0))
    for k, f in enumerate(frames[i:i + per]):
        sheet.paste(f, ((k % 3) * 480, (k // 3) * 270))
    sheet.save(os.path.join(root, 'sheet_%d.png' % (i // per)))
print(len(frames), 'frames')
