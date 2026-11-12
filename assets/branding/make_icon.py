"""Package the original SCP emblem with the requested corner label.

Requires resvg-py and Pillow only for asset authoring, never at app runtime.
"""
from pathlib import Path
import xml.etree.ElementTree as ET
import resvg_py
from PIL import Image

ROOT = Path(__file__).resolve().parent
source = ET.parse(ROOT / "scp-source.svg").getroot()
ns = "{http://www.w3.org/2000/svg}"
tile = ET.Element(ns + "svg", {"width": "1024", "height": "1024", "viewBox": "0 0 1024 1024"})
ET.SubElement(tile, ns + "rect", {"x": "16", "y": "16", "width": "992", "height": "992", "rx": "130", "fill": "white"})
group = ET.SubElement(tile, ns + "g", {"transform": "translate(85 36) scale(6.3)"})
for child in source:
    group.append(child)
label = ET.SubElement(tile, ns + "text", {"x": "950", "y": "942", "text-anchor": "end", "font-family": "Segoe UI", "font-size": "72", "font-weight": "600", "fill": "#2763eb"})
label.text = "rxsend tool"
svg = ET.tostring(tile, encoding="unicode")
(ROOT / "scp-rxsend-tool.svg").write_text(svg, encoding="utf-8", newline="\n")
png = resvg_py.svg_to_bytes(svg_string=svg, width=1024, height=1024)
(ROOT / "scp-rxsend-tool.png").write_bytes(png)
(ROOT.parent / "app.png").write_bytes(png)
with Image.open(ROOT / "scp-rxsend-tool.png") as icon:
    icon.save(ROOT.parent / "app.ico", sizes=[(n, n) for n in (16, 24, 32, 48, 64, 128, 256)])
