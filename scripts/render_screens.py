#!/usr/bin/env python
"""
Convert each ASCII screen sketch in BankTellerOperations.md into a PNG that
looks like a real bank-teller application screenshot. Output goes to
docs/training/screens/screen-NN.png.

Detection: any fenced code block whose body contains a `+--` border row.

For each screen we render:
  * a window-chrome bar with three traffic-light dots
  * a GoldBank-blue MUI AppBar at the top
  * a white card with the screen content laid out in Segoe UI
  * `[ Button Text ]` tokens drawn as filled MUI-blue pill buttons over the text
  * `[___]` / `[       ]` tokens drawn as outlined input boxes
  * `[ X ▼ ]` tokens drawn as outlined dropdowns

Box-drawing characters at the edges are stripped so the text reads cleanly.
"""
import os, re, sys
from PIL import Image, ImageDraw, ImageFont

SRC = "docs/training/BankTellerOperations.md"
OUT_DIR = "docs/training/screens"
os.makedirs(OUT_DIR, exist_ok=True)

# ---------- Fonts ----------
def font(name, size):
    p = f"C:/Windows/Fonts/{name}"
    try:
        return ImageFont.truetype(p, size)
    except OSError:
        return ImageFont.load_default()

F_BODY    = font("consola.ttf", 13)            # MONOSPACE — keeps every column aligned to a grid
F_BODY_B  = font("consolab.ttf", 13)
F_TITLE   = font("seguisb.ttf", 18)
F_BAR     = font("seguisb.ttf", 16)
F_NAV     = font("segoeui.ttf", 13)
F_BTN     = font("seguisb.ttf", 12)
F_TINY    = font("segoeui.ttf", 11)

# ---------- Colors ----------
BG          = (244, 246, 248)         # MUI default background
CHROME      = (240, 240, 240)
CHROME_LINE = (210, 210, 210)
APPBAR      = (13, 58, 122)           # GoldBank brand blue
APPBAR_TEXT = (255, 255, 255)
CARD_BG     = (255, 255, 255)
CARD_BORDER = (220, 222, 226)
TEXT        = (32, 36, 44)
SUBTLE      = (110, 116, 130)
FIELD_BG    = (250, 250, 252)
FIELD_BORDER= (200, 204, 212)
BTN_BG      = APPBAR
BTN_TEXT    = (255, 255, 255)
WARN_BG     = (250, 173, 20)
SUCCESS     = (34, 139, 75)
DANGER      = (200, 50, 60)

# ---------- Text helpers ----------
def text_w(draw, txt, fnt):
    return draw.textlength(txt, font=fnt)

def round_rect(draw, box, radius, fill=None, outline=None, width=1):
    draw.rounded_rectangle(box, radius=radius, fill=fill, outline=outline, width=width)

# ---------- Screen renderer ----------
def render_screen(ascii_text, idx):
    raw_lines = ascii_text.splitlines()

    # Strip horizontal frame rows (+----+) and side pipes
    body_lines = []
    for ln in raw_lines:
        if re.match(r"^\s*\+[-+]+\+?\s*$", ln):
            continue  # +---+ row
        # Strip leading/trailing | (with adjacent spaces)
        s = ln
        s = re.sub(r"^\s*\|\s?", "", s)
        s = re.sub(r"\s?\|\s*$", "", s)
        body_lines.append(s.rstrip())

    # Trim leading/trailing blank lines
    while body_lines and not body_lines[0].strip():
        body_lines.pop(0)
    while body_lines and not body_lines[-1].strip():
        body_lines.pop()

    # ----- Layout dimensions -----
    # Fixed character cell so every column lines up perfectly
    cell_w = 8           # px per char column (Consolas 13pt)
    line_h = 20          # px per row
    chrome_h = 28
    appbar_h = 48
    card_pad_x = 22
    card_pad_y = 18

    max_chars = max((len(l) for l in body_lines), default=60)
    width = max(900, card_pad_x * 2 + max_chars * cell_w + 32)
    n_lines = max(1, len(body_lines))
    card_inner_h = card_pad_y * 2 + n_lines * line_h
    card_top = chrome_h + appbar_h + 14
    card_bottom = card_top + card_inner_h
    height = card_bottom + 18
    if height < 240:
        height = 240

    img = Image.new("RGB", (width, height), BG)
    d = ImageDraw.Draw(img)

    # ----- Window chrome -----
    d.rectangle([0, 0, width, chrome_h], fill=CHROME)
    d.line([0, chrome_h, width, chrome_h], fill=CHROME_LINE)
    for i, c in enumerate([(255, 95, 86), (255, 189, 46), (39, 201, 63)]):
        cx = 16 + i * 18
        d.ellipse([cx, 9, cx + 11, 20], fill=c)
    d.text((width // 2 - 60, 7), "GoldBank Teller — Branch", font=F_TINY, fill=(110, 116, 130))

    # ----- MUI AppBar -----
    d.rectangle([0, chrome_h, width, chrome_h + appbar_h], fill=APPBAR)
    d.text((22, chrome_h + 14), "GoldBank Teller", font=F_BAR, fill=APPBAR_TEXT)
    nav_x = 200
    for label in ("Customers", "Drawer", "Vault"):
        d.text((nav_x, chrome_h + 16), label, font=F_NAV, fill=APPBAR_TEXT)
        nav_x += int(text_w(d, label, F_NAV)) + 24
    # Drawer Open chip
    chip_x = nav_x + 12
    chip_w = 88
    round_rect(d, [chip_x, chrome_h + 12, chip_x + chip_w, chrome_h + 36], 12,
               fill=(34, 139, 75))
    d.text((chip_x + 10, chrome_h + 16), "Drawer Open", font=F_NAV, fill=(255, 255, 255))
    # Right-side user
    d.text((width - 110, chrome_h + 8),  "J. Doe",     font=F_NAV, fill=APPBAR_TEXT)
    d.text((width - 110, chrome_h + 26), "Borrowdale", font=F_TINY, fill=(200, 215, 240))

    # ----- Card -----
    card_box = [16, card_top, width - 16, card_bottom]
    # subtle shadow
    shadow = [card_box[0] + 2, card_box[1] + 2, card_box[2] + 2, card_box[3] + 2]
    round_rect(d, shadow, 10, fill=(225, 228, 232))
    round_rect(d, card_box, 10, fill=CARD_BG, outline=CARD_BORDER, width=1)

    # ----- Render body lines -----
    x0 = card_box[0] + card_pad_x
    y  = card_box[1] + card_pad_y

    # Pattern matchers
    button_re   = re.compile(r"\[\s+([^\[\]]+?)\s+\]")          # [ Label With Spaces ]
    field_re    = re.compile(r"\[\s*_+\s*\]|\[\s{4,}\]")         # [____] or [        ]
    dropdown_re = re.compile(r"\[\s*([A-Za-z0-9 .,/]+?)\s*▼\s*\]")
    checkbox_re = re.compile(r"\[\s*[ x☐☑X✓]\s*\]")

    def col_x(col):
        return x0 + col * cell_w

    def blank_chars(line_y, start_col, end_col):
        """Erase a character range so we can draw a widget over it."""
        d.rectangle(
            [col_x(start_col) - 1, line_y - 2, col_x(end_col) + 1, line_y + line_h - 2],
            fill=CARD_BG,
        )

    def render_line(line, y):
        nonlocal first_content
        # First non-empty line gets rendered as a screen title in the brand font
        if first_content and line.strip():
            d.text((x0, y), line.strip(), font=F_TITLE, fill=TEXT)
            first_content = False
            return

        # Render the entire line as monospace text first — this guarantees that
        # every column lines up exactly to a fixed grid (cell_w pixels per char).
        if line:
            d.text((x0, y), line, font=F_BODY, fill=TEXT)

        # Then walk the line for widget tokens and overlay them at exact char
        # positions. We blank the underlying text first so it isn't visible
        # behind the widget.
        spans = []
        for m in dropdown_re.finditer(line):
            spans.append(("dropdown", m.group(1), m.start(), m.end()))
        for m in button_re.finditer(line):
            if any(a <= m.start() < b for _, _, a, b in spans):
                continue
            label = m.group(1)
            if re.match(r"^[\d\s.,]+$", label):  # numeric placeholder, not a button
                continue
            spans.append(("button", label, m.start(), m.end()))
        for m in field_re.finditer(line):
            if any(a <= m.start() < b for _, _, a, b in spans):
                continue
            spans.append(("field", "", m.start(), m.end()))
        for m in checkbox_re.finditer(line):
            if any(a <= m.start() < b for _, _, a, b in spans):
                continue
            spans.append(("checkbox", "", m.start(), m.end()))

        spans.sort(key=lambda t: t[2])

        for kind, label, a, b in spans:
            x_start = col_x(a)
            x_end   = col_x(b)
            cw = x_end - x_start  # widget width in pixels — fits the char span
            ch = line_h - 2

            blank_chars(y, a, b)

            if kind == "button":
                color = BTN_BG
                ll = label.lower()
                if "deposit" in ll or "confirm deposit" in ll or "open drawer" in ll:
                    color = SUCCESS
                if any(k in ll for k in ("withdraw", "reverse", "spot", "variance", "approve", "warning", "close drawer")):
                    color = WARN_BG
                if any(k in ll for k in ("cancel", "recount")):
                    color = (110, 116, 130)
                round_rect(d, [x_start, y - 2, x_end, y + ch], 6, fill=color)
                # Center the label
                tw = text_w(d, label, F_BTN)
                tx = x_start + (cw - tw) / 2
                d.text((tx, y), label, font=F_BTN, fill=BTN_TEXT)

            elif kind == "field":
                round_rect(d, [x_start, y, x_end, y + ch - 2], 4,
                           fill=FIELD_BG, outline=FIELD_BORDER, width=1)

            elif kind == "dropdown":
                round_rect(d, [x_start, y, x_end, y + ch - 2], 4,
                           fill=FIELD_BG, outline=FIELD_BORDER, width=1)
                d.text((x_start + 6, y + 1), label, font=F_BODY, fill=TEXT)
                d.polygon(
                    [(x_end - 12, y + 7), (x_end - 4, y + 7), (x_end - 8, y + 13)],
                    fill=SUBTLE,
                )

            elif kind == "checkbox":
                cb = 14
                cx = x_start + 2
                cy = y + 1
                d.rectangle([cx, cy, cx + cb, cy + cb], outline=FIELD_BORDER, width=1, fill=FIELD_BG)

    first_content = True
    for line in body_lines:
        render_line(line, y)
        y += line_h

    out = os.path.join(OUT_DIR, f"screen-{idx:02d}.png")
    img.save(out, "PNG", optimize=True)
    return out


def main():
    with open(SRC, "r", encoding="utf-8") as f:
        text = f.read()

    pattern = re.compile(r"```\n(.*?)\n```", re.DOTALL)
    idx = 0
    out_paths = []
    for m in pattern.finditer(text):
        body = m.group(1)
        if "+--" not in body:
            continue
        idx += 1
        path = render_screen(body, idx)
        out_paths.append(path)
        print(f"  {path}")
    print(f"Rendered {idx} screens")

if __name__ == "__main__":
    main()
