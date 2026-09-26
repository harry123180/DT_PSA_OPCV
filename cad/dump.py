"""列出 STEP 裡每個實體的包圍盒、體積、面型（給分群用）。

    python cad/dump.py <組立.STEP> <輸出.json>
"""
import cadquery as cq, json, sys, time
t=time.time()
shape=cq.importers.importStep(sys.argv[1])
solids=shape.solids().vals()
out=[]
for i,s in enumerate(solids):
    bb=s.BoundingBox()
    faces=s.Faces()
    kinds={}
    for f in faces:
        k=f.geomType(); kinds[k]=kinds.get(k,0)+1
    out.append(dict(i=i,vol=round(s.Volume(),1),min=[round(bb.xmin,1),round(bb.ymin,1),round(bb.zmin,1)],max=[round(bb.xmax,1),round(bb.ymax,1),round(bb.zmax,1)],faces=len(faces),kinds=kinds))
json.dump(out,open(sys.argv[2],'w'),indent=0)
print(len(solids),'solids',round(time.time()-t,1),'s')
