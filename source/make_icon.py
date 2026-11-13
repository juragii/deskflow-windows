from pathlib import Path
from PIL import Image,ImageDraw
p=Path(__file__).parent
size=256
im=Image.new('RGBA',(size,size),(0,0,0,0));d=ImageDraw.Draw(im)
d.rounded_rectangle((8,8,248,248),radius=59,fill='#3182F6')
d.rounded_rectangle((53,59,203,213),radius=22,fill='white')
d.rectangle((53,94,203,102),fill='#D7E8FF')
for x in (91,165):d.rounded_rectangle((x-6,39,x+6,76),radius=6,fill='white')
for y in (126,156,186):
 for x in (81,113,145,177):d.rounded_rectangle((x-5,y-5,x+5,y+5),radius=3,fill='#B8D7FF')
d.rounded_rectangle((130,141,160,171),radius=8,fill='#3182F6')
im.save(p/'DeskFlow.ico',sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
im.save(p/'DeskFlow_icon.png')
