"""Bake the polished fault section used at specimen and landscape scale in Unity."""
from pathlib import Path
import numpy as np
from PIL import Image

size = 768
y, x = np.mgrid[-1:1:complex(size), -1:1:complex(size)]
# An off-centre folded inclusion under pressure from the lower right.
u = x + .33*y + .19 + .045*np.sin(5*y+2*x)
v = y - .12 + .14*np.sin(2.3*x) + .035*np.cos(8*x-2*y)
radius = (abs(u*1.17)**2.7 + abs(v*.84)**2.7)**(1/2.7)
bed = radius + .022*np.sin(11*u+5*v) + .009*np.sin(26*v-7*u)
stops = [0,.19,.25,.263,.29,.34,.355,.42,.435,.47,.49,.55,.574,.63,.649,.68,.72,.79,.81,.85,1,1.5]
colors = ['17272e','203239','305657','b9d1b2','527c76','49716b','cebe91',
          '56847a','abc1a0','638d80','365f58','9ba88d','ebc698','99715b',
          'e5c39e','76594e','b68369','795a54','c19c81','625053','493d45','3d3740']
palette = np.array([[int(c[i:i+2],16) for i in (0,2,4)] for c in colors])
rgb = np.stack([np.interp(bed,stops,ch) for ch in palette.T],axis=-1)
grain = np.random.default_rng(1947).normal(0,1.35,x.shape)
lamina = np.sin(145*bed + .8*np.sin(15*u)) * np.clip(1-abs(bed-.48)*2.9,0,1)*4
# Cleaved crystal terminations in the cavity, softer chalcedony around them.
rng = np.random.default_rng(711)
nearest = np.full(x.shape,np.inf)
facet = np.zeros_like(x)
for bx,by,tone in zip(rng.uniform(-.38,.38,55),rng.uniform(-.44,.44,55),rng.uniform(.2,1,55)):
    dx,dy = u-bx,v-by
    distance = dx*dx + dy*dy*.72
    inside = distance < nearest
    facet = np.where(inside, tone * np.where(dx+.48*dy>0,1,.38), facet)
    nearest = np.minimum(nearest,distance)
core = np.clip((.25-bed)*13,0,1)
rgb += (facet*core)[...,None]*np.array([47,70,69])
rgb += (grain+lamina)[...,None]
out = Path(__file__).resolve().parents[1]/'unity-vfx-preview/Assets/Resources/TRIAS/meridian-agate.png'
Image.fromarray(np.uint8(np.clip(rgb,0,255))).save(out)
