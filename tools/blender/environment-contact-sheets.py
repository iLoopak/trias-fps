"""Assemble labeled contact sheets from the environment generator's Blender PNGs."""
import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def sheet(title, renders, target, columns=4):
    tile_width, tile_height = 300, 267
    margin, header = 18, 74
    rows = (len(renders)+columns-1)//columns
    panel = Image.new("RGB",(columns*tile_width+margin*2,rows*tile_height+header+margin),"#101820")
    draw = ImageDraw.Draw(panel)
    title_font = ImageFont.load_default(size=27)
    caption_font = ImageFont.load_default(size=15)
    draw.text((margin+7,20), title, font=title_font, fill="#f2e6d0")
    for i,(asset, view, path) in enumerate(renders):
        x, y = margin+(i%columns)*tile_width, header+(i//columns)*tile_height
        with Image.open(path) as image:
            image.thumbnail((tile_width-12,tile_height-30))
            panel.paste(image.convert("RGB"),(x+(tile_width-image.width)//2,y))
        suffix = "" if view=="quarter" else f"  /  {view}"
        draw.text((x+9,y+tile_height-29),asset+suffix,font=caption_font,fill="#dddfd9")
    panel.save(target,quality=93,optimize=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest",type=Path)
    parser.add_argument("renders",type=Path)
    args = parser.parse_args()
    inventory = json.loads(args.manifest.read_text())
    for name, collection in inventory["collections"].items():
        shots=[(asset["id"],"quarter",args.renders/f"{asset['id']}--quarter.png")
               for asset in collection["assets"]]
        sheet("TRIAS  /  "+name,shots,args.renders/f"{name.lower()}-contact.jpg")
    views=[(*file.stem.split("--"),file) for file in sorted(args.renders.glob("*--*.png"))
           if not file.stem.endswith("--quarter")]
    if views:
        sheet("TRIAS  /  Orthographic and material studies",views,args.renders/"views-contact.jpg")


if __name__=="__main__":
    main()
