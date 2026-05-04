#!/usr/bin/env python
"""
Render docs/training/BankTellerOperations.md to PDF using ReportLab.
Handles headings (H1-H4), paragraphs, fenced code blocks (with the ASCII screen
sketches preserved as monospace), bullet lists, ordered lists, blockquotes,
inline code, bold, and pipe tables.
"""
import re, sys, os
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import mm
from reportlab.lib import colors
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Preformatted, PageBreak, Table, TableStyle,
    KeepTogether, ListFlowable, ListItem, Image as RLImage,
)
from reportlab.lib.utils import ImageReader
from reportlab.lib.enums import TA_LEFT

SRC = "docs/training/BankTellerOperations.md"
OUT = "docs/training/BankTellerOperations.pdf"
SCREENS_DIR = "docs/training/screens"

# ---------- Styles ----------
styles = getSampleStyleSheet()
def s(name, **kw):
    base = ParagraphStyle(name, parent=styles["Normal"])
    for k, v in kw.items():
        setattr(base, k, v)
    return base

H1 = s("H1", fontName="Helvetica-Bold", fontSize=22, leading=26, spaceBefore=18, spaceAfter=10, textColor=colors.HexColor("#0d3a7a"))
H2 = s("H2", fontName="Helvetica-Bold", fontSize=16, leading=20, spaceBefore=14, spaceAfter=8,  textColor=colors.HexColor("#0d3a7a"))
H3 = s("H3", fontName="Helvetica-Bold", fontSize=13, leading=16, spaceBefore=10, spaceAfter=6,  textColor=colors.HexColor("#1c1c1c"))
H4 = s("H4", fontName="Helvetica-Bold", fontSize=11, leading=14, spaceBefore=8,  spaceAfter=4,  textColor=colors.HexColor("#333"))
BODY = s("Body", fontName="Helvetica", fontSize=9.5, leading=13, spaceAfter=4, alignment=TA_LEFT)
QUOTE = s("Quote", fontName="Helvetica-Oblique", fontSize=9.5, leading=13,
          leftIndent=12, rightIndent=6, spaceAfter=6,
          borderColor=colors.HexColor("#999"), borderPadding=6,
          backColor=colors.HexColor("#fff8dc"))
CODE = ParagraphStyle("Code", parent=styles["Code"], fontName="Courier",
                      fontSize=7.2, leading=8.6, leftIndent=4, rightIndent=4,
                      backColor=colors.HexColor("#f5f5f5"),
                      borderColor=colors.HexColor("#ddd"), borderWidth=0.5,
                      borderPadding=4, spaceBefore=4, spaceAfter=8)

def inline(md):
    """Convert inline markdown (`code`, **bold**, *italic*) to ReportLab markup, escape <>&."""
    md = md.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
    md = re.sub(r"`([^`]+)`", r'<font face="Courier" size="9">\1</font>', md)
    md = re.sub(r"\*\*([^*]+)\*\*", r"<b>\1</b>", md)
    md = re.sub(r"(?<!\*)\*([^*]+)\*(?!\*)", r"<i>\1</i>", md)
    md = re.sub(r"\[([^\]]+)\]\(([^)]+)\)", r'<font color="#0645ad">\1</font>', md)
    return md

def parse(md_text):
    """Parse markdown into a flat list of (kind, payload) tokens."""
    lines = md_text.splitlines()
    tokens = []
    i, n = 0, len(lines)
    while i < n:
        line = lines[i]
        # Fenced code block
        if line.startswith("```"):
            buf = []
            i += 1
            while i < n and not lines[i].startswith("```"):
                buf.append(lines[i])
                i += 1
            i += 1  # closing fence
            body = "\n".join(buf)
            # Screen sketches contain the box-drawing border row "+--"; render as image.
            if "+--" in body:
                tokens.append(("screen", body))
            else:
                tokens.append(("code", body))
            continue
        # Heading
        m = re.match(r"^(#{1,6})\s+(.*)$", line)
        if m:
            level = len(m.group(1))
            tokens.append((f"h{min(level,4)}", m.group(2).strip()))
            i += 1
            continue
        # Horizontal rule
        if re.match(r"^---+\s*$", line):
            tokens.append(("hr", None))
            i += 1
            continue
        # Blockquote
        if line.startswith(">"):
            buf = []
            while i < n and lines[i].startswith(">"):
                buf.append(lines[i].lstrip("> ").rstrip())
                i += 1
            tokens.append(("quote", " ".join(buf)))
            continue
        # Pipe table (header | --- |)
        if "|" in line and i+1 < n and re.match(r"^\s*\|?\s*[-:|\s]+\s*\|?\s*$", lines[i+1]):
            header = [c.strip() for c in line.strip().strip("|").split("|")]
            i += 2
            rows = []
            while i < n and "|" in lines[i] and lines[i].strip():
                row = [c.strip() for c in lines[i].strip().strip("|").split("|")]
                rows.append(row)
                i += 1
            tokens.append(("table", (header, rows)))
            continue
        # Unordered list
        if re.match(r"^\s*[-*]\s+", line):
            items = []
            while i < n and re.match(r"^\s*[-*]\s+", lines[i]):
                items.append(re.sub(r"^\s*[-*]\s+", "", lines[i]))
                i += 1
            tokens.append(("ul", items))
            continue
        # Ordered list
        if re.match(r"^\s*\d+\.\s+", line):
            items = []
            while i < n and re.match(r"^\s*\d+\.\s+", lines[i]):
                items.append(re.sub(r"^\s*\d+\.\s+", "", lines[i]))
                i += 1
            tokens.append(("ol", items))
            continue
        # Blank
        if not line.strip():
            tokens.append(("blank", None))
            i += 1
            continue
        # Paragraph (collect until blank/special)
        buf = [line]
        i += 1
        while i < n and lines[i].strip() and not re.match(
                r"^(#{1,6}\s|```|---+\s*$|\s*[-*]\s|\s*\d+\.\s|>)", lines[i]):
            buf.append(lines[i])
            i += 1
        tokens.append(("p", " ".join(buf)))
    return tokens

def build_table(header, rows):
    data = [header] + rows
    # Use simple word-wrapping by converting cells to Paragraphs
    cell_style = ParagraphStyle("cell", fontName="Helvetica", fontSize=8, leading=10)
    head_style = ParagraphStyle("head", fontName="Helvetica-Bold", fontSize=8, leading=10, textColor=colors.white)
    wrapped = []
    for r_idx, row in enumerate(data):
        wrapped_row = []
        for c in row:
            txt = inline(c)
            wrapped_row.append(Paragraph(txt, head_style if r_idx == 0 else cell_style))
        wrapped.append(wrapped_row)
    t = Table(wrapped, repeatRows=1, hAlign="LEFT", colWidths=None)
    t.setStyle(TableStyle([
        ("BACKGROUND", (0,0), (-1,0), colors.HexColor("#0d3a7a")),
        ("TEXTCOLOR",  (0,0), (-1,0), colors.white),
        ("GRID", (0,0), (-1,-1), 0.25, colors.HexColor("#aaaaaa")),
        ("VALIGN", (0,0), (-1,-1), "TOP"),
        ("LEFTPADDING", (0,0), (-1,-1), 4),
        ("RIGHTPADDING", (0,0), (-1,-1), 4),
        ("TOPPADDING", (0,0), (-1,-1), 3),
        ("BOTTOMPADDING", (0,0), (-1,-1), 3),
    ]))
    return t

def to_flowables(tokens):
    # Counter for screen images so we can pull them in order from disk
    screen_idx = [0]
    out = []
    for kind, payload in tokens:
        if kind == "h1":
            out.append(PageBreak() if out else Spacer(1, 1))
            out.append(Paragraph(inline(payload), H1))
        elif kind == "h2":
            out.append(Paragraph(inline(payload), H2))
        elif kind == "h3":
            out.append(Paragraph(inline(payload), H3))
        elif kind == "h4":
            out.append(Paragraph(inline(payload), H4))
        elif kind == "p":
            out.append(Paragraph(inline(payload), BODY))
        elif kind == "code":
            out.append(Preformatted(payload, CODE))
        elif kind == "screen":
            screen_idx[0] += 1
            path = os.path.join(SCREENS_DIR, f"screen-{screen_idx[0]:02d}.png")
            if os.path.exists(path):
                ir = ImageReader(path)
                iw, ih = ir.getSize()
                # Fit to content width (about 175mm)
                target_w = 175 * mm
                scale = target_w / iw
                img = RLImage(path, width=target_w, height=ih * scale)
                img.hAlign = "CENTER"
                out.append(Spacer(1, 4))
                out.append(img)
                out.append(Spacer(1, 8))
            else:
                out.append(Preformatted(payload, CODE))
        elif kind == "quote":
            out.append(Paragraph(inline(payload), QUOTE))
        elif kind == "ul":
            items = [ListItem(Paragraph(inline(it), BODY), leftIndent=12) for it in payload]
            out.append(ListFlowable(items, bulletType="bullet", start="•", leftIndent=14))
            out.append(Spacer(1, 4))
        elif kind == "ol":
            items = [ListItem(Paragraph(inline(it), BODY), leftIndent=12) for it in payload]
            out.append(ListFlowable(items, bulletType="1", leftIndent=14))
            out.append(Spacer(1, 4))
        elif kind == "table":
            out.append(build_table(*payload))
            out.append(Spacer(1, 6))
        elif kind == "hr":
            out.append(Spacer(1, 6))
        elif kind == "blank":
            out.append(Spacer(1, 3))
    return out

def header_footer(canvas, doc):
    canvas.saveState()
    canvas.setFont("Helvetica", 8)
    canvas.setFillColor(colors.HexColor("#666"))
    canvas.drawString(15*mm, 10*mm, "GoldBank Branch Operations Manual · v1.0 · April 2026")
    canvas.drawRightString(A4[0] - 15*mm, 10*mm, f"Page {doc.page}")
    canvas.restoreState()

SAMPLES = [
    ("Sample — Cash Deposit Receipt (A6)",        "docs/training/samples/sample-deposit-receipt.pdf"),
    ("Sample — Cash Withdrawal Receipt (A6)",     "docs/training/samples/sample-withdrawal-receipt.pdf"),
    ("Sample — Teller End-of-Day Report (A4)",    "docs/training/samples/sample-teller-eod.pdf"),
    ("Sample — Branch Vault End-of-Day Report (A4)", "docs/training/samples/sample-vault-eod.pdf"),
]

def main():
    with open(SRC, "r", encoding="utf-8") as f:
        text = f.read()
    tokens = parse(text)
    flow = to_flowables(tokens)
    doc = SimpleDocTemplate(
        OUT, pagesize=A4,
        leftMargin=15*mm, rightMargin=15*mm,
        topMargin=15*mm, bottomMargin=18*mm,
        title="GoldBank Branch Operations Manual",
        author="GoldBank Branch Operations / Training",
    )
    doc.build(flow, onFirstPage=header_footer, onLaterPages=header_footer)

    # Merge sample report PDFs as appendix pages so trainees see real output
    # without needing to chase external files.
    from pypdf import PdfWriter, PdfReader
    writer = PdfWriter()
    base = PdfReader(OUT)
    for page in base.pages:
        writer.add_page(page)

    # Build a single-page divider for each sample, then append the sample's pages.
    from io import BytesIO
    for title, path in SAMPLES:
        if not os.path.exists(path):
            continue
        # Divider page
        buf = BytesIO()
        d = SimpleDocTemplate(
            buf, pagesize=A4,
            leftMargin=15*mm, rightMargin=15*mm,
            topMargin=40*mm, bottomMargin=18*mm,
            title=title,
        )
        d.build([
            Paragraph("Sample Report", H1),
            Spacer(1, 8),
            Paragraph(title, H2),
            Spacer(1, 12),
            Paragraph(
                "The following pages are the actual PDF produced by the live GoldBank gateway. "
                "They are bit-for-bit identical to what tellers, vault managers, and supervisors "
                "will print in production.",
                BODY),
        ], onFirstPage=header_footer, onLaterPages=header_footer)
        buf.seek(0)
        for page in PdfReader(buf).pages:
            writer.add_page(page)
        for page in PdfReader(path).pages:
            writer.add_page(page)

    with open(OUT, "wb") as f:
        writer.write(f)
    print(f"Wrote {OUT} ({os.path.getsize(OUT)} bytes, {len(writer.pages)} pages)")

if __name__ == "__main__":
    main()
