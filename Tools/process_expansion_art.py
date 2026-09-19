from pathlib import Path
import json,shutil
import numpy as np
from scipy import ndimage
from PIL import Image
root=Path(__file__).resolve().parents[1];src=root/'ArtSource/AstralUI';out=root/'Assets/AstralUI/Art'
manifest=[]
for item in json.loads((src/'expansion-generation-manifest.json').read_text())['assets']:shutil.copy2(item['path'],src/(item['key']+'.png'))
def clean(im,threshold=95,min_area=100):
 a=np.array(im.convert('RGBA'));label,n=ndimage.label(a[:,:,3]>threshold);sizes=np.bincount(label.ravel());valid=sizes>min_area;valid[0]=False;mask=ndimage.binary_dilation(valid[label],iterations=1);a[:,:,3]=np.where(mask,a[:,:,3],0);return Image.fromarray(a)
def save(im,name,maxsize):
 im=im.convert('RGBA');im=im.crop(im.getbbox());im.thumbnail(maxsize,Image.Resampling.LANCZOS);pad=Image.new('RGBA',(im.width+4,im.height+4));pad.paste(im,(2,2));p=out/(name+'.png');pad.save(p,optimize=True,compress_level=9);manifest.append({'name':name,'width':pad.width,'height':pad.height,'alpha':pad.getextrema()[-1],'bytes':p.stat().st_size})
faces={'jiye':(320,30,725,460),'yaya':(390,30,800,450),'canglan':(425,0,820,390),'yaochen':(390,100,780,510)}
for name,box in faces.items():
 im=clean(Image.open(src/(name+'.png')),100,240);save(im,'character_'+name,(1024,1536));save(im.crop(box),'portrait_'+name,(256,256))
for atlas,prefix,columns,rows,size in [('battle_heroes','combat_hero_',4,2,384),('battle_enemies','combat_enemy_',3,2,384),('battle_skills','combat_skill_',4,3,192),('battle_fx','combat_fx_',4,2,256)]:
 im=Image.open(src/(atlas+'.png'))
 for i in range(columns*rows):
  x,y=i%columns,i//columns;crop=im.crop((x*im.width//columns,y*im.height//rows,(x+1)*im.width//columns,(y+1)*im.height//rows));save(clean(crop,85,100),prefix+str(i),(size,size))
im=Image.open(src/'battle_ground.png').convert('RGB');im.save(out/'combat_ground.jpg',quality=92,optimize=True)
(root/'Documentation/AstralUI/expansion-assets.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2));print('Generated',len(manifest),'RGBA sprites + arena.')
