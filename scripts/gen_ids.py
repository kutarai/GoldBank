#!/usr/bin/env python
"""Generate sample national ID card images for every account."""
import io, hashlib, random, sys, subprocess, uuid
from PIL import Image, ImageDraw, ImageFont

out = subprocess.check_output([
    "podman", "exec", "goldbank-postgres", "psql", "-U", "goldbank", "-d", "goldbank",
    "-At", "-F", "|",
    "-c", "SELECT \"Id\", tenant_id, COALESCE(first_name,''), COALESCE(last_name,''), COALESCE(national_id,''), COALESCE(date_of_birth,'') FROM bank.accounts WHERE first_name IS NOT NULL;"
]).decode()

rows = [line.split("|") for line in out.strip().splitlines() if line.strip()]
print(f"-- {len(rows)} accounts", file=sys.stderr)

def font(size, bold=False):
    path = "C:/Windows/Fonts/seguisb.ttf" if bold else "C:/Windows/Fonts/segoeui.ttf"
    try:
        return ImageFont.truetype(path, size)
    except OSError:
        return ImageFont.load_default()

F_TITLE = font(22, bold=True)
F_LABEL = font(11)
F_VALUE = font(14, bold=True)
F_BIG   = font(16, bold=True)

W, H = 540, 340  # ID-1 card aspect ~1.586

SKIN = [(245,213,180),(231,188,145),(210,161,116),(181,128,84),(141,89,53),(96,56,35)]

def make_face(seed, w=120, h=150):
    rng = random.Random(seed)
    img = Image.new("RGB", (w, h), (235, 235, 240))
    d = ImageDraw.Draw(img)
    skin = rng.choice(SKIN)
    cx, cy = w//2, h//2 - 4
    rx, ry = 34, 42
    d.ellipse([-10, h-30, w+10, h+60], fill=(60,80,140))
    d.ellipse([cx-rx, cy-ry, cx+rx, cy+ry], fill=skin, outline=(0,0,0), width=1)
    hair = rng.choice([(30,20,15),(60,35,20),(120,80,40),(20,20,20)])
    d.chord([cx-rx, cy-ry-4, cx+rx, cy+ry-18], 180, 360, fill=hair)
    for dx in (-12, 12):
        d.ellipse([cx+dx-4, cy-6, cx+dx+4, cy+2], fill="white", outline="black")
        d.ellipse([cx+dx-2, cy-4, cx+dx+2, cy], fill=(30,30,30))
    d.arc([cx-10, cy+6, cx+10, cy+20], 0, 180, fill=(120,30,30), width=1)
    return img

def make_id(first, last, nid, dob, seed):
    rng = random.Random(seed)
    img = Image.new("RGB", (W, H), (240, 245, 250))
    d = ImageDraw.Draw(img)
    # header bar
    d.rectangle([0, 0, W, 56], fill=(20, 60, 120))
    d.text((16, 14), "REPUBLIC OF ZIMBABWE", font=F_TITLE, fill="white")
    d.text((16, 38), "NATIONAL IDENTITY CARD", font=F_LABEL, fill=(220, 230, 255))
    # border
    d.rectangle([4, 4, W - 5, H - 5], outline=(20, 60, 120), width=2)
    # photo
    face = make_face(seed)
    img.paste(face, (20, 80))
    d.rectangle([20, 80, 20 + face.width, 80 + face.height], outline=(80, 80, 80), width=1)
    # signature line
    d.text((20, 240), "Signature", font=F_LABEL, fill=(80, 80, 80))
    d.line([(20, 270), (140, 270)], fill=(0, 0, 0), width=1)
    # right column fields
    x = 170
    fields = [
        ("Surname", (last or "").upper()),
        ("Given Names", (first or "").upper()),
        ("ID Number", nid or f"{rng.randint(10,99)}-{rng.randint(100000,999999)} A {rng.randint(10,99)}"),
        ("Date of Birth", dob or f"{rng.randint(1,28):02d}-{rng.randint(1,12):02d}-{rng.randint(1965,2002)}"),
        ("Sex", rng.choice(["M", "F"])),
        ("Nationality", "ZIMBABWEAN"),
    ]
    y = 78
    for label, value in fields:
        d.text((x, y), label.upper(), font=F_LABEL, fill=(100, 100, 110))
        d.text((x, y + 13), value, font=F_VALUE, fill=(20, 20, 30))
        y += 38
    # MRZ-style strip
    d.rectangle([0, H - 36, W, H], fill=(225, 230, 240))
    mrz = f"IDZWE{(last or 'X')[:8].upper():<8}<<{(first or 'X')[:6].upper():<6}<<<<<<<<<<<<<{rng.randint(10000000, 99999999)}"
    d.text((10, H - 28), mrz[:60], font=font(14), fill=(40, 40, 60))
    return img

def to_jpeg(img):
    buf = io.BytesIO()
    img.save(buf, format="JPEG", quality=85)
    return buf.getvalue()

print("BEGIN;")
print("DELETE FROM bank.kyc_documents WHERE document_type='national_id';")
for i, (acc_id, tenant_id, first, last, nid, dob) in enumerate(rows):
    img = make_id(first, last, nid, dob, seed=i + 101)
    jpg = to_jpeg(img)
    sha = hashlib.sha256(jpg).hexdigest()
    doc_id = str(uuid.uuid4())
    fname = f"{first}_{last}_id.jpg".replace(' ', '_')
    print(
        f"INSERT INTO bank.kyc_documents "
        f"(\"Id\", account_id, document_type, file_name, content_type, file_size_bytes, file_path, "
        f" encryption_key_ref, checksum_sha256, status, tenant_id, created_at, file_data, verified_at) "
        f"VALUES ('{doc_id}', '{acc_id}', 'national_id', '{fname}', 'image/jpeg', {len(jpg)}, "
        f" 'inline://id/{doc_id}', 'none', '{sha}', 'verified', '{tenant_id}', NOW(), "
        f" decode('{jpg.hex()}', 'hex'), NOW());"
    )
print("COMMIT;")
