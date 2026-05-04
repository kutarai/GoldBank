#!/usr/bin/env python
"""Generate fake handwritten-style signature PNGs for every account and emit SQL."""
import io, math, random, sys, subprocess, json
from PIL import Image, ImageDraw, ImageFont

# Pull (Id, first, last) from DB via podman psql
out = subprocess.check_output([
    "podman", "exec", "goldbank-postgres", "psql", "-U", "goldbank", "-d", "goldbank",
    "-At", "-F", "|",
    "-c", "SELECT \"Id\", COALESCE(first_name,''), COALESCE(last_name,'') FROM bank.accounts WHERE first_name IS NOT NULL;"
]).decode()

rows = [line.split("|") for line in out.strip().splitlines() if line.strip()]
print(f"-- {len(rows)} accounts", file=sys.stderr)

# Try to find a script-like font on Windows
font_candidates = [
    "C:/Windows/Fonts/segoesc.ttf",      # Segoe Script
    "C:/Windows/Fonts/MTCORSVA.TTF",     # Monotype Corsiva
    "C:/Windows/Fonts/ITCEDSCR.TTF",
    "C:/Windows/Fonts/BRUSHSCI.TTF",
    "C:/Windows/Fonts/segoescb.ttf",
]
font = None
for f in font_candidates:
    try:
        font = ImageFont.truetype(f, 44)
        print(f"-- using font {f}", file=sys.stderr)
        break
    except OSError:
        continue
if font is None:
    font = ImageFont.load_default()

W, H = 360, 110

def make_sig(name, seed):
    rng = random.Random(seed)
    img = Image.new("RGBA", (W, H), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    # squiggly baseline flourish
    baseline = 75 + rng.randint(-5, 5)
    pts = []
    for x in range(20, W - 20, 6):
        y = baseline + math.sin((x + seed) * 0.05) * 4 + rng.uniform(-1.5, 1.5)
        pts.append((x, y))
    # draw text slightly slanted
    txt = name
    angle = rng.uniform(-6, -2)
    txt_img = Image.new("RGBA", (W, H), (255, 255, 255, 0))
    td = ImageDraw.Draw(txt_img)
    td.text((20, 18), txt, font=font, fill=(20, 30, 90, 255))
    txt_img = txt_img.rotate(angle, resample=Image.BICUBIC, center=(W/2, H/2))
    img.alpha_composite(txt_img)
    # baseline flourish line
    d.line(pts, fill=(20, 30, 90, 220), width=2)
    # underline curl
    cx, cy = W - 60, baseline + 6
    d.arc([cx - 30, cy - 8, cx + 30, cy + 12], 0, 180, fill=(20, 30, 90, 220), width=2)
    buf = io.BytesIO()
    img.save(buf, format="PNG", optimize=True)
    return buf.getvalue()

print("BEGIN;")
for i, (acc_id, first, last) in enumerate(rows):
    name = (first + " " + last).strip() or "Customer"
    png = make_sig(name, seed=i + 1)
    hexstr = png.hex()
    print(
        f"UPDATE bank.accounts SET signature_image = decode('{hexstr}', 'hex'), "
        f"signature_verified_by = 'teller', signature_verified_at = NOW() "
        f"WHERE \"Id\" = '{acc_id}';"
    )
print("COMMIT;")
