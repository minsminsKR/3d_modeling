"""Make a lighter browser copy without changing mesh, weights or animation."""
import argparse
import io
from pathlib import Path
from PIL import Image
from glb_io import read,write

def main(source,destination,size=2048):
    doc,binary=read(source)
    replacement={}
    for image in doc.get('images',[]):
        if 'bufferView' not in image:raise ValueError('Preview must be self-contained')
        index=image['bufferView'];view=doc['bufferViews'][index]
        data=binary[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']]
        im=Image.open(io.BytesIO(data));im.load()
        im.thumbnail((size,size),Image.Resampling.LANCZOS)
        encoded=io.BytesIO();im.save(encoded,format='PNG',compress_level=6)
        replacement[index]=encoded.getvalue();image['mimeType']='image/png'
        print('preview texture',image.get('name',index),im.size,flush=True)
    chunks=[];offset=0
    for i,view in enumerate(doc['bufferViews']):
        old=view.get('byteOffset',0)
        data=replacement.get(i,binary[old:old+view['byteLength']])
        view['byteOffset']=offset;view['byteLength']=len(data)
        padded=data+b'\0'*((-len(data))%4)
        chunks.append(padded);offset+=len(padded)
    Path(destination).parent.mkdir(parents=True,exist_ok=True)
    write(doc,b''.join(chunks),destination)
    print(Path(destination).name,Path(destination).stat().st_size,flush=True)

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('source');p.add_argument('destination');p.add_argument('--size',type=int,default=2048)
    a=p.parse_args();main(a.source,a.destination,a.size)
