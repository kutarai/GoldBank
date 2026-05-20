#!/usr/bin/env python
"""
Render the docs/system/*.md set into PDFs.

Two outputs:
  docs/system/executive-summary.pdf            executive-summary.md only
  docs/system/GoldBank-system-documentation.pdf   all 9 system docs combined

Usage:
  python scripts/system_docs_to_pdf.py
  python scripts/system_docs_to_pdf.py exec      # exec summary only
  python scripts/system_docs_to_pdf.py full      # combined doc only

Markdown features supported: headings (H1-H4), paragraphs, fenced code blocks,
ordered + unordered lists, pipe tables, blockquotes, inline code, **bold**,
*italic*, [links](url) (rendered as the link text in blue), horizontal rules.
"""
from __future__ import annotations
import os, re, sys
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import mm
from reportlab.lib import colors
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Preformatted, PageBreak, Table, TableStyle,
    ListFlowable, ListItem,
)
from reportlab.lib.enums import TA_LEFT

ROOT  = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SYSDIR = os.path.join(ROOT, "docs", "system")

# Reading order — exec summary first so a non-technical reader gets the
# whole picture in the first few pages and can stop there; technical depth
# follows for engineers who need more.
FULL_ORDER = [
    "executive-summary.md",
    "README.md",
    "architecture.md",
    "data-model.md",
    "server.md",
    "switch.md",
    "mobile.md",
    "bank-client.md",
    "bank-teller.md",
    "operations.md",
]

# ── Styles ─────────────────────────────────────────────────────────────────────
styles = getSampleStyleSheet()
def s(name, **kw):
    base = ParagraphStyle(name, parent=styles["Normal"])
    for k, v in kw.items():
        setattr(base, k, v)
    return base

GOLD = colors.HexColor("#b8860b")
INK  = colors.HexColor("#1a1a2e")

H1 = s("H1", fontName="Helvetica-Bold", fontSize=22, leading=26,
       spaceBefore=18, spaceAfter=12, textColor=INK)
H2 = s("H2", fontName="Helvetica-Bold", fontSize=16, leading=20,
       spaceBefore=14, spaceAfter=8,  textColor=INK)
H3 = s("H3", fontName="Helvetica-Bold", fontSize=12.5, leading=16,
       spaceBefore=10, spaceAfter=5,  textColor=colors.HexColor("#333"))
H4 = s("H4", fontName="Helvetica-Bold", fontSize=10.5, leading=14,
       spaceBefore=8,  spaceAfter=3,  textColor=colors.HexColor("#555"))
BODY = s("Body", fontName="Helvetica", fontSize=9.5, leading=13,
         spaceAfter=4, alignment=TA_LEFT)
QUOTE = s("Quote", fontName="Helvetica-Oblique", fontSize=9.5, leading=13,
          leftIndent=12, rightIndent=6, spaceAfter=8,
          borderColor=colors.HexColor("#999"), borderPadding=8, borderWidth=0,
          leftBorderColor=GOLD, leftBorderWidth=2,
          backColor=colors.HexColor("#fdf8e8"))
CODE = ParagraphStyle("Code", parent=styles["Code"], fontName="Courier",
                      fontSize=7.4, leading=9.0, leftIndent=4, rightIndent=4,
                      backColor=colors.HexColor("#f5f5f5"),
                      borderColor=colors.HexColor("#ddd"), borderWidth=0.5,
                      borderPadding=4, spaceBefore=4, spaceAfter=8)

# ── Inline markdown → ReportLab markup ────────────────────────────────────────
def inline(md: str) -> str:
    md = md.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
    # 1. Pull every `code` span out into a placeholder so bold/italic don't
    #    fire on underscores or asterisks that live inside identifiers.
    placeholders = []
    def stash_code(m):
        placeholders.append(m.group(1))
        return f"\x00CODE{len(placeholders)-1}\x00"
    md = re.sub(r"`([^`]+)`", stash_code, md)
    # 2. Bold, italic, links on the remaining (code-free) text.
    md = re.sub(r"\*\*([^*]+)\*\*", r"<b>\1</b>", md)
    md = re.sub(r"(?<!\*)\*([^*]+)\*(?!\*)", r"<i>\1</i>", md)
    md = re.sub(r"\[([^\]]+)\]\(([^)]+)\)", r'<font color="#0645ad">\1</font>', md)
    # 3. Put the code spans back.
    def restore_code(m):
        return f'<font face="Courier" size="9">{placeholders[int(m.group(1))]}</font>'
    md = re.sub(r"\x00CODE(\d+)\x00", restore_code, md)
    return md

# ── Block tokenizer ───────────────────────────────────────────────────────────
def tokenize(md_text: str):
    lines = md_text.splitlines()
    tokens, i, n = [], 0, len(lines)
    while i < n:
        line = lines[i]
        if line.startswith("```"):
            buf = []
            i += 1
            while i < n and not lines[i].startswith("```"):
                buf.append(lines[i])
                i += 1
            i += 1  # closing fence
            tokens.append(("code", "\n".join(buf)))
            continue
        m = re.match(r"^(#{1,6})\s+(.*)$", line)
        if m:
            level = len(m.group(1))
            tokens.append((f"h{min(level,4)}", m.group(2).strip()))
            i += 1; continue
        if re.match(r"^---+\s*$", line):
            tokens.append(("hr", None)); i += 1; continue
        if line.startswith(">"):
            buf = []
            while i < n and lines[i].startswith(">"):
                buf.append(lines[i].lstrip("> ").rstrip())
                i += 1
            tokens.append(("quote", " ".join(buf))); continue
        # Pipe table
        if "|" in line and i+1 < n and re.match(r"^\s*\|?\s*[-:|\s]+\s*\|?\s*$", lines[i+1]):
            header = [c.strip() for c in line.strip().strip("|").split("|")]
            i += 2
            rows = []
            while i < n and "|" in lines[i] and lines[i].strip():
                row = [c.strip() for c in lines[i].strip().strip("|").split("|")]
                rows.append(row)
                i += 1
            tokens.append(("table", (header, rows))); continue
        if re.match(r"^\s*[-*]\s+", line):
            items = []
            while i < n and re.match(r"^\s*[-*]\s+", lines[i]):
                items.append(re.sub(r"^\s*[-*]\s+", "", lines[i])); i += 1
            tokens.append(("ul", items)); continue
        if re.match(r"^\s*\d+\.\s+", line):
            items = []
            while i < n and re.match(r"^\s*\d+\.\s+", lines[i]):
                items.append(re.sub(r"^\s*\d+\.\s+", "", lines[i])); i += 1
            tokens.append(("ol", items)); continue
        if not line.strip():
            tokens.append(("blank", None)); i += 1; continue
        # Paragraph
        buf = [line]; i += 1
        while i < n and lines[i].strip() and not re.match(
                r"^(#{1,6}\s|```|---+\s*$|\s*[-*]\s|\s*\d+\.\s|>)", lines[i]):
            buf.append(lines[i]); i += 1
        tokens.append(("p", " ".join(buf)))
    return tokens

def build_table(header, rows):
    data = [header] + rows
    cell_style = ParagraphStyle("cell", fontName="Helvetica", fontSize=8, leading=10)
    head_style = ParagraphStyle("head", fontName="Helvetica-Bold", fontSize=8, leading=10,
                                textColor=colors.white)
    wrapped = []
    for r_idx, row in enumerate(data):
        wrapped.append([Paragraph(inline(c), head_style if r_idx == 0 else cell_style) for c in row])
    t = Table(wrapped, repeatRows=1, hAlign="LEFT", colWidths=None)
    t.setStyle(TableStyle([
        ("BACKGROUND",   (0,0), (-1,0), INK),
        ("TEXTCOLOR",    (0,0), (-1,0), colors.white),
        ("GRID",         (0,0), (-1,-1), 0.25, colors.HexColor("#aaaaaa")),
        ("VALIGN",       (0,0), (-1,-1), "TOP"),
        ("LEFTPADDING",  (0,0), (-1,-1), 4),
        ("RIGHTPADDING", (0,0), (-1,-1), 4),
        ("TOPPADDING",   (0,0), (-1,-1), 3),
        ("BOTTOMPADDING",(0,0), (-1,-1), 3),
    ]))
    return t

def to_flowables(tokens, first_h1_no_break=False):
    out = []
    seen_h1 = False
    for kind, payload in tokens:
        if kind == "h1":
            if seen_h1 or (not first_h1_no_break and out):
                out.append(PageBreak())
            out.append(Paragraph(inline(payload), H1))
            seen_h1 = True
        elif kind == "h2": out.append(Paragraph(inline(payload), H2))
        elif kind == "h3": out.append(Paragraph(inline(payload), H3))
        elif kind == "h4": out.append(Paragraph(inline(payload), H4))
        elif kind == "p":  out.append(Paragraph(inline(payload), BODY))
        elif kind == "code": out.append(Preformatted(payload, CODE))
        elif kind == "quote": out.append(Paragraph(inline(payload), QUOTE))
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
        elif kind == "hr": out.append(Spacer(1, 6))
        elif kind == "blank": out.append(Spacer(1, 2))
    return out

# ── Page furniture ────────────────────────────────────────────────────────────
def on_page(canvas, doc):
    canvas.saveState()
    canvas.setFont("Helvetica", 8)
    canvas.setFillColor(colors.HexColor("#888"))
    canvas.drawString(20*mm, 12*mm, f"GoldBank Documentation")
    canvas.drawRightString(190*mm, 12*mm, f"Page {doc.page}")
    canvas.setStrokeColor(GOLD)
    canvas.setLineWidth(0.5)
    canvas.line(20*mm, 14*mm, 190*mm, 14*mm)
    canvas.restoreState()

def render(input_md_paths, output_pdf):
    flowables = []
    for idx, path in enumerate(input_md_paths):
        with open(path, "r", encoding="utf-8") as f:
            md = f.read()
        tokens = tokenize(md)
        flowables.extend(to_flowables(tokens, first_h1_no_break=(idx == 0)))

    doc = SimpleDocTemplate(
        output_pdf, pagesize=A4,
        leftMargin=20*mm, rightMargin=20*mm,
        topMargin=18*mm, bottomMargin=18*mm,
        title="GoldBank System Documentation",
        author="GoldBank platform team",
    )
    doc.build(flowables, onFirstPage=on_page, onLaterPages=on_page)
    print(f"wrote {output_pdf} ({os.path.getsize(output_pdf)//1024} KB)")

def main():
    arg = sys.argv[1] if len(sys.argv) > 1 else "both"
    if arg in ("exec", "both"):
        render(
            [os.path.join(SYSDIR, "executive-summary.md")],
            os.path.join(SYSDIR, "executive-summary.pdf"),
        )
    if arg in ("full", "both"):
        render(
            [os.path.join(SYSDIR, f) for f in FULL_ORDER if os.path.exists(os.path.join(SYSDIR, f))],
            os.path.join(SYSDIR, "GoldBank-system-documentation.pdf"),
        )

if __name__ == "__main__":
    main()
