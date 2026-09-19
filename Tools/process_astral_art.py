"""Split only freshly generated atlases; preserve/clean their alpha; lossless PNG optimization."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import numpy as np
from scipy import ndimage
import json

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / 'ArtSource/AstralUI'
OUT = ROOT / 'Assets/AstralUI/Art'
OUT.mkdir(parents=True, exist_ok=True)
manifest = []

def clean(im, threshold=85, min_area=120):
    a = np.array(im.convert('RGBA'))
    labels, n = ndimage.label(a[:, :, 3] > threshold)
    sizes = np.bincount(labels.ravel())
    valid = sizes > min_area
    valid[0] = False
    mask = ndimage.binary_dilation(valid[labels], iterations=1)
    a[:, :, 3] = np.where(mask, a[:, :, 3], 0)
    return Image.fromarray(a)

def save(im, name, maxsize, border=None):
    im = im.convert('RGBA')
    box = im.getbbox()
    if box: im = im.crop(box)
    im.thumbnail(maxsize, Image.Resampling.LANCZOS)
    # Small safety gutter keeps bilinear filtering away from neighboring sprite edges.
    padded = Image.new('RGBA', (im.width+4, im.height+4))
    padded.paste(im,(2,2))
    path = OUT/(name+'.png')
    padded.save(path, optimize=True, compress_level=9)
    manifest.append({'name':name,'width':padded.width,'height':padded.height,'bytes':path.stat().st_size,'border':border or [0,0,0,0]})

for name in ['seiri','miku','luna','ember']:
    im = clean(Image.open(SRC/(name+'.png')), 100, 240)
    save(im,'character_'+name,(1024,1536))
    # Portraits are crops of the newly generated character artwork, never the references.
    face={'seiri':(465,95,825,465),'miku':(350,60,755,480),'luna':(380,15,775,435),'ember':(410,60,815,480)}[name]
    save(im.crop(face),'portrait_'+name,(256,256))

im = Image.open(SRC/'weapons.png')
for i in range(12):
    x,y = i%3,i//3
    crop=im.crop((x*im.width//3,y*im.height//4,(x+1)*im.width//3,(y+1)*im.height//4))
    sprite = clean(crop,90,80)
    pixels = np.array(sprite)
    labels, _ = ndimage.label(pixels[:, :, 3] > 120)
    sizes = np.bincount(labels.ravel()); sizes[0] = 0
    keep = ndimage.binary_dilation(labels == sizes.argmax(), iterations=2)
    pixels[:, :, 3] = np.where(keep, pixels[:, :, 3], 0)
    save(Image.fromarray(pixels),'equipment_%02d'%i,(512,512))

names=['home','quest','mail','event','gallery','settings','sword','enhance','star','set','dismantle','shield','heart','skill','talent','costume','friends','menu','crystal','energy','coin','lock','next','close']
im=Image.open(SRC/'icons.png')
for i,name in enumerate(names):
    x,y=i%6,i//6
    crop=im.crop((x*256,y*256,(x+1)*256,(y+1)*256))
    save(clean(crop,155,110),'icon_'+name,(96,96))

# Precisely mask the generated panel silhouettes, removing imperfect automatic alpha fringes.
panels=[('panel_light',(36,92,492,299),22),('panel_dark',(540,92,998,299),24),('button_primary',(1050,123,1501,274),22),('button_secondary',(39,429,492,590),24),('card_gold',(541,411,998,602),25),('card_purple',(1048,411,1500,602),23),('card_selected',(37,711,494,906),25),('tab',(541,752,1012,873),22),('divider',(1049,795,1502,829),14)]
im=Image.open(SRC/'panels.png').convert('RGBA')
for name,rect,c in panels:
    crop=im.crop(rect);w,h=crop.size
    mask=Image.new('L',(w,h));d=ImageDraw.Draw(mask)
    if name=='tab': points=[(c,0),(w-60,0),(w-1,h//2),(w-60,h-1),(c,h-1),(0,h-c),(0,c)]
    else: points=[(c,0),(w-c,0),(w-1,c),(w-1,h-c),(w-c,h-1),(c,h-1),(0,h-c),(0,c)]
    d.polygon(points,fill=255);crop.putalpha(mask)
    save(crop,name,(384,200),[32,32,32,32] if name!='divider' else [20,4,20,4])

for name in ['ruins']:
    im=Image.open(SRC/(name+'.png')).convert('RGB')
    im.save(OUT/('background_'+name+'.jpg'),quality=91,optimize=True)
im=Image.open(SRC/'regions.png')
for i,name in enumerate(['forest','frost','desert','void']):
    x,y=i%2,i//2
    crop=im.crop((x*im.width//2,y*im.height//2,(x+1)*im.width//2,(y+1)*im.height//2)).convert('RGB')
    crop.save(OUT/('background_'+name+'.jpg'),quality=91,optimize=True)

(ROOT/'Documentation/AstralUI/asset-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2))
print('Saved',len(manifest),'transparent sprites and 5 backgrounds.')
