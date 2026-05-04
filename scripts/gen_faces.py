#!/usr/bin/env python
"""Generate cartoon avatar selfies for every account and emit SQL upserts into bank.kyc_documents."""
import io, hashlib, random, sys, subprocess, uuid
from PIL import Image, ImageDraw, ImageFont

out = subprocess.check_output([
    "podman", "exec", "goldbank-postgres", "psql", "-U", "goldbank", "-d", "goldbank",
    "-At", "-F", "|",
    "-c", "SELECT \"Id\", tenant_id, COALESCE(first_name,''), COALESCE(last_name,'') FROM bank.accounts WHERE first_name IS NOT NULL;"
]).decode()

rows = [line.split("|") for line in out.strip().splitlines() if line.strip()]
print(f"-- {len(rows)} accounts", file=sys.stderr)

try:
    font = ImageFont.truetype("C:/Windows/Fonts/seguisb.ttf", 96)
except OSError:
    font = ImageFont.load_default()

W, H = 200, 260  # passport-style 35x45 ratio
SKIN = [
    (245, 213, 180), (231, 188, 145), (210, 161, 116), (181, 128, 84),
    (141, 89, 53), (96, 56, 35), (255, 224, 196), (200, 150, 110),
]
BG = [
    (52, 73, 94), (41, 128, 185), (39, 174, 96), (192, 57, 43),
    (142, 68, 173), (211, 84, 0), (44, 62, 80), (127, 140, 141),
]

def make_face(first, last, seed):
    rng = random.Random(seed)
    bg = (235, 235, 240)  # plain passport background
    img = Image.new("RGB", (W, H), bg)
    d = ImageDraw.Draw(img)
    skin = rng.choice(SKIN)
    # shoulders
    d.ellipse([-20, H - 70, W + 20, H + 120], fill=rng.choice(BG))
    # head
    cx, cy = W // 2, H // 2 - 10
    rx, ry = 60, 75
    d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=skin, outline=(0, 0, 0), width=2)
    # hair cap
    hair_color = rng.choice([(30, 20, 15), (60, 35, 20), (120, 80, 40), (20, 20, 20), (180, 140, 90)])
    d.chord([cx - rx, cy - ry - 8, cx + rx, cy + ry - 30], 180, 360, fill=hair_color)
    # eyes
    eye_y = cy - 8
    for dx in (-20, 20):
        d.ellipse([cx + dx - 8, eye_y - 5, cx + dx + 8, eye_y + 5], fill="white", outline="black")
        pupil = rng.choice([(30, 30, 30), (70, 50, 30), (40, 80, 120)])
        d.ellipse([cx + dx - 3, eye_y - 3, cx + dx + 3, eye_y + 3], fill=pupil)
    # nose
    d.line([(cx, eye_y + 6), (cx - 3, cy + 14), (cx + 3, cy + 14)], fill=(0, 0, 0), width=2)
    # mouth
    d.arc([cx - 18, cy + 14, cx + 18, cy + 36], 0, 180, fill=(120, 30, 30), width=2)
    buf = io.BytesIO()
    img.save(buf, format="JPEG", quality=82)
    return buf.getvalue()

print("BEGIN;")
print("DELETE FROM bank.kyc_documents WHERE document_type='selfie';")
for i, (acc_id, tenant_id, first, last) in enumerate(rows):
    jpg = make_face(first, last, seed=i + 1)
    hexstr = jpg.hex()
    sha = hashlib.sha256(jpg).hexdigest()
    doc_id = str(uuid.uuid4())
    fname = f"{first}_{last}_selfie.jpg".replace(' ', '_')
    print(
        f"INSERT INTO bank.kyc_documents "
        f"(\"Id\", account_id, document_type, file_name, content_type, file_size_bytes, file_path, "
        f" encryption_key_ref, checksum_sha256, status, tenant_id, created_at, file_data, verified_at) "
        f"VALUES ('{doc_id}', '{acc_id}', 'selfie', '{fname}', 'image/jpeg', {len(jpg)}, "
        f" 'inline://selfie/{doc_id}', 'none', '{sha}', 'verified', '{tenant_id}', NOW(), "
        f" decode('{hexstr}', 'hex'), NOW());"
    )
print("COMMIT;")
