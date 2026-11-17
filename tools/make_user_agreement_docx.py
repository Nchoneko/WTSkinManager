# -*- coding: utf-8 -*-
"""把 USER_AGREEMENT.md 转成排版良好的 Word 文档（USER_AGREEMENT.docx）。"""
import re
from docx import Document
from docx.shared import Pt, Cm, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml.ns import qn
from docx.oxml import OxmlElement

MD = "USER_AGREEMENT.md"
OUT = "USER_AGREEMENT.docx"
CN_FONT = "Microsoft YaHei"
EN_FONT = "Segoe UI"

doc = Document()
sec = doc.sections[0]
sec.page_width = Cm(21.0)
sec.page_height = Cm(29.7)
sec.left_margin = Cm(2.4)
sec.right_margin = Cm(2.4)
sec.top_margin = Cm(2.2)
sec.bottom_margin = Cm(2.2)

def set_style_font(style, size, bold=False, color=None):
    style.font.name = EN_FONT
    style.font.size = Pt(size)
    style.font.bold = bold
    if color:
        style.font.color.rgb = color
    rpr = style.element.get_or_add_rPr()
    rfonts = rpr.find(qn('w:rFonts'))
    if rfonts is None:
        rfonts = OxmlElement('w:rFonts')
        rpr.append(rfonts)
    rfonts.set(qn('w:eastAsia'), CN_FONT)

set_style_font(doc.styles['Normal'], 10.5)
doc.styles['Normal'].paragraph_format.line_spacing = 1.35
doc.styles['Normal'].paragraph_format.space_after = Pt(4)
for name, size in (('Heading 1', 15), ('Heading 2', 12.5)):
    st = doc.styles[name]
    set_style_font(st, size, True, RGBColor(0x1F, 0x3A, 0x5F))
    st.paragraph_format.space_before = Pt(12)
    st.paragraph_format.space_after = Pt(6)

def add_runs(par, text):
    """支持 **粗体** 与 `代码` 的简单内联解析。"""
    tokens = re.split(r'(\*\*.+?\*\*|`[^`]+`)', text)
    for tok in tokens:
        if not tok:
            continue
        if tok.startswith('**') and tok.endswith('**') and len(tok) > 4:
            r = par.add_run(tok[2:-2]); r.bold = True
        elif tok.startswith('`') and tok.endswith('`') and len(tok) > 2:
            r = par.add_run(tok[1:-1]); r.font.name = 'Consolas'
            r._element.rPr.rFonts.set(qn('w:eastAsia'), 'Consolas')
        else:
            par.add_run(tok)

def add_hr(par):
    pPr = par._p.get_or_add_pPr()
    pbdr = OxmlElement('w:pBdr')
    bottom = OxmlElement('w:bottom')
    bottom.set(qn('w:val'), 'single'); bottom.set(qn('w:sz'), '6')
    bottom.set(qn('w:space'), '1'); bottom.set(qn('w:color'), 'BBBBBB')
    pbdr.append(bottom); pPr.append(pbdr)

def make_table(rows):
    cols = len(rows[0])
    table = doc.add_table(rows=len(rows), cols=cols)
    table.style = 'Light Grid Accent 1'
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    for ri, row in enumerate(rows):
        for ci in range(cols):
            cell = table.cell(ri, ci)
            cell.text = ''
            p = cell.paragraphs[0]
            p.paragraph_format.space_after = Pt(2)
            add_runs(p, row[ci] if ci < len(row) else '')
            for r in p.runs:
                r.font.size = Pt(9)
                if ri == 0:
                    r.bold = True
    doc.add_paragraph()
    return table

lines = open(MD, encoding='utf-8').read().splitlines()
i = 0
first_h1 = True
while i < len(lines):
    line = lines[i].rstrip()
    # 表格
    if line.startswith('|') and i + 1 < len(lines) and re.match(r'^\|[\s:\-\|]+\|$', lines[i+1].strip()):
        rows = []
        header = [c.strip() for c in line.strip().strip('|').split('|')]
        rows.append(header)
        i += 2
        while i < len(lines) and lines[i].strip().startswith('|'):
            rows.append([c.strip() for c in lines[i].strip().strip('|').split('|')])
            i += 1
        make_table(rows)
        continue
    if line.strip() == '---':
        p = doc.add_paragraph(); add_hr(p)
        i += 1; continue
    if line.startswith('# '):
        text = line[2:].strip()
        if first_h1:
            p = doc.add_paragraph()
            p.alignment = WD_ALIGN_PARAGRAPH.CENTER
            r = p.add_run(text)
            r.font.name = EN_FONT; r.font.size = Pt(22); r.bold = True
            r.font.color.rgb = RGBColor(0x1F, 0x3A, 0x5F)
            r._element.rPr.rFonts.set(qn('w:eastAsia'), CN_FONT)
            p.paragraph_format.space_after = Pt(10)
            first_h1 = False
        else:
            doc.add_page_break()
            doc.add_heading(text, level=1)
        i += 1; continue
    if line.startswith('## '):
        doc.add_heading(line[3:].strip(), level=1)
        i += 1; continue
    if line.startswith('### '):
        doc.add_heading(line[4:].strip(), level=2)
        i += 1; continue
    if line.startswith('> '):
        p = doc.add_paragraph()
        p.paragraph_format.left_indent = Cm(0.6)
        p.paragraph_format.right_indent = Cm(0.6)
        p.paragraph_format.space_before = Pt(4)
        p.paragraph_format.space_after = Pt(8)
        add_runs(p, line[2:])
        for r in p.runs:
            r.font.color.rgb = RGBColor(0x8A, 0x5A, 0x00)
        add_hr(p)
        i += 1; continue
    if re.match(r'^\d+\.\d+\s', line) or re.match(r'^\d+\.\d+$', line):
        m = re.match(r'^(\d+\.\d+)\s*(.*)$', line)
        p = doc.add_paragraph()
        p.paragraph_format.left_indent = Cm(0.6)
        r = p.add_run(m.group(1) + '  '); r.bold = True
        add_runs(p, m.group(2))
        i += 1; continue
    if re.match(r'^\d+\.\s', line):
        p = doc.add_paragraph(style='List Number')
        add_runs(p, re.sub(r'^\d+\.\s*', '', line))
        i += 1; continue
    if line.startswith('- '):
        p = doc.add_paragraph(style='List Bullet')
        add_runs(p, line[2:])
        i += 1; continue
    if line.strip() == '':
        i += 1; continue
    p = doc.add_paragraph()
    add_runs(p, line.strip())
    if line.strip().startswith('**协议版本') or line.strip().startswith('**生效日期') \
       or line.strip().startswith('**适用软件') or line.strip().startswith('**开发者') \
       or line.strip().startswith('**适用对象'):
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    i += 1

# 页脚页码
footer = sec.footer.paragraphs[0]
footer.alignment = WD_ALIGN_PARAGRAPH.CENTER
run = footer.add_run()
fld1 = OxmlElement('w:fldChar'); fld1.set(qn('w:fldCharType'), 'begin')
instr = OxmlElement('w:instrText'); instr.set(qn('xml:space'), 'preserve'); instr.text = 'PAGE'
fld2 = OxmlElement('w:fldChar'); fld2.set(qn('w:fldCharType'), 'end')
run._r.append(fld1); run._r.append(instr); run._r.append(fld2)

doc.save(OUT)
print("SAVED", OUT)
