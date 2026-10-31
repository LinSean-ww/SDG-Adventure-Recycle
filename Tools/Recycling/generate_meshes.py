#!/usr/bin/env python3
"""Build real volume meshes/UVs from the owner's reference atlas coordinates.

Run from any directory with Python 3 + numpy. PNG bytes are never altered. UVs
sample inside the pictured objects, avoiding their transparent silhouettes.
This offline source asset check is NOT Unity/PhysX validation.
"""
from pathlib import Path
import hashlib
import json
import math
import uuid
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/_Modules_/02_Levels/Level03/Art/Recycling'
OUT = ART / 'Resources/RecyclingArt'
SIZES = {'08': (1402,1122), '09': (1672,941), '10': (1672,941), '07': (2048,682)}
FILES = {'07':'07_Three_Bales_on_Pallets.png','08':'08_Mixed_Sorting_Trash_Sheet.png',
         '09':'09_Three_Bale_Fronts.png','10':'10_Three_Bale_Angled.png'}
# Pixel rectangles lie INSIDE each photographed material, with a gutter. They
# are texture samples on volumetric meshes, not alpha-cutout sprite cards.
PATCH = {'red':(75,95,160,175),'tin':(350,130,440,220),'cap':(640,110,745,165),
         'pet':(112,395,147,425),'jug':(350,380,420,480),'cup':(640,390,710,475),
         'tray':(910,390,1000,430),'shampoo':(1230,400,1300,485),
         'news':(90,655,175,700),'card':(430,675,515,735),'bag':(640,635,725,720),'egg':(1260,690,1330,730)}
FRONT = {'Metal':(75,285,490,730),'Plastic':(650,285,1060,730),'Paper':(1190,285,1590,730)}
SIDE = {'Metal':(240,330,495,710),'Plastic':(790,330,1040,710),'Paper':(1350,330,1590,710)}
STOCK = {'Metal':(70,105,585,500),'Plastic':(770,105,1270,500),'Paper':(1460,105,1950,500)}
V = lambda *x: np.array(x,dtype=float)

def guid(path, importer='DefaultImporter', extra=''):
    meta=path.with_name(path.name+'.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'+importer+':\n'+extra+
                        '  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    return next(x.split(': ',1)[1] for x in meta.read_text().splitlines() if x.startswith('guid: '))

def uv(rect, u, v, atlas):
    x0,y0,x1,y1=rect;w,h=SIZES[atlas]
    return ((x0+(x1-x0)*u)/w,1-(y1-(y1-y0)*v)/h)

class Mesh:
    def __init__(self,atlas='08',rect=None):
        self.atlas,self.rect=atlas,rect or PATCH['tin'];self.faces=[]
    def tri(self,a,b,c,tex=None,normal=None):
        a,b,c=map(np.asarray,(a,b,c));n=np.cross(b-a,c-a)
        if np.linalg.norm(n)<1e-10:return
        if normal is not None and np.dot(n,normal)<0:b,c=c,b;tex=(tex[0],tex[2],tex[1]) if tex else None;n=-n
        self.faces.append(([a.copy(),b.copy(),c.copy()],tex or [uv(self.rect,0,0,self.atlas),uv(self.rect,1,0,self.atlas),uv(self.rect,1,1,self.atlas)],n/np.linalg.norm(n)))
    def quad(self,a,b,c,d,normal,rect=None):
        rect=rect or self.rect;t=[uv(rect,u,v,self.atlas) for u,v in ((0,0),(1,0),(1,1),(0,1))]
        self.tri(a,b,c,t[:3],normal);self.tri(a,c,d,[t[0],t[2],t[3]],normal)
    def box(self,center,size,rect=None,rotation=None):
        center=np.array(center);h=np.array(size)/2;r=np.eye(3) if rotation is None else rotation
        for axis in range(3):
            for sign in (-1,1):
                normal=np.eye(3)[axis]*sign;u=np.eye(3)[(axis+1)%3]*h[(axis+1)%3];v=np.eye(3)[(axis+2)%3]*h[(axis+2)%3]
                face=np.eye(3)[axis]*h[axis]*sign
                points=[center+r@(face+du*u+dv*v) for du,dv in ((-1,-1),(1,-1),(1,1),(-1,1))]
                self.quad(*points,r@normal,rect)
    def lathe(self,profile,center=(0,0,0),ellipse=(1,1),dent=0,n=32,caps=True,rect=None):
        rect=rect or self.rect;center=np.array(center);rings=[]
        for j,(y,radius) in enumerate(profile):
            ring=[]
            for i in range(n):
                theta=2*math.pi*i/n
                wrinkle=1+dent*math.sin(theta*5+j*.9)*math.sin(math.pi*j/max(1,len(profile)-1))
                ring.append(center+V(math.cos(theta)*radius*ellipse[0]*wrinkle,y,math.sin(theta)*radius*ellipse[1]*wrinkle))
            rings.append(ring)
        for j in range(len(rings)-1):
            for i in range(n):
                k=(i+1)%n;a,b,c,d=rings[j][i],rings[j][k],rings[j+1][k],rings[j+1][i]
                normal=V(a[0]-center[0],0,a[2]-center[2]);t=[uv(rect,i/n,j/(len(rings)-1),self.atlas),uv(rect,(i+1)/n,j/(len(rings)-1),self.atlas),uv(rect,(i+1)/n,(j+1)/(len(rings)-1),self.atlas),uv(rect,i/n,(j+1)/(len(rings)-1),self.atlas)]
                self.tri(a,b,c,t[:3],normal);self.tri(a,c,d,[t[0],t[2],t[3]],normal)
        if caps:
            for j,sign in ((0,-1),(-1,1)):
                mid=center+V(0,profile[j][0],0)
                for i in range(n):self.tri(mid,rings[j][i],rings[j][(i+1)%n],normal=V(0,sign,0))
    def torus(self,center,major,minor,rotation=None,rect=None):
        r=np.eye(3) if rotation is None else rotation;c=np.array(center)
        def p(i,j):
            a=2*math.pi*i/24;b=2*math.pi*j/8
            return c+r@V((major+minor*math.cos(b))*math.cos(a),minor*math.sin(b),(major+minor*math.cos(b))*math.sin(a))
        for i in range(24):
            for j in range(8):
                normal=r@V(math.cos(2*math.pi*i/24)*math.cos(2*math.pi*j/8),math.sin(2*math.pi*j/8),math.sin(2*math.pi*i/24)*math.cos(2*math.pi*j/8))
                self.quad(p(i,j),p(i+1,j),p(i+1,j+1),p(i,j+1),normal,rect)
    def points(self):return np.array([p for f,_,_ in self.faces for p in f])
    def transform(self,scale=1,offset=(0,0,0)):
        for face,_,_ in self.faces:
            for p in face:p[:]=p*scale+offset
    def write(self,name):
        path=OUT/'Meshes'/f'{name}.obj';lines=['# Owner PNG reference -> actual volume geometry; metres, Y up',f'o {name}']
        for face,tex,normal in self.faces:
            for p in face:lines.append('v '+' '.join(f'{x:.8f}' for x in p))
            for t in tex:lines.append('vt '+' '.join(f'{x:.8f}' for x in t))
            for _ in face:lines.append('vn '+' '.join(f'{x:.8f}' for x in normal))
        for i in range(len(self.faces)):
            ids=[i*3+j+1 for j in range(3)];lines.append('f '+' '.join(f'{n}/{n}/{n}' for n in ids))
        path.write_text('\n'.join(lines)+'\n');model_meta(path)
        pts=self.points(); assert np.isfinite(pts).all()
        assert all(np.linalg.norm(n)>.99 for _,_,n in self.faces)
        return dict(model=name,triangles=len(self.faces),min=pts.min(axis=0).round(8).tolist(),max=pts.max(axis=0).round(8).tolist(),max_radius=float(np.linalg.norm(pts,axis=1).max()),atlas=self.atlas)

def rot_x(deg):
    t=math.radians(deg);return np.array([[1,0,0],[0,math.cos(t),-math.sin(t)],[0,math.sin(t),math.cos(t)]])

def raw_models():
    result=[]
    for kind in ('Metal','Plastic','Paper'):
        names={'Metal':['AluminumCan','CrushedCan','TinCan','CrownCap','FoodTin'],
               'Plastic':['PETBottle','DrinkCup','DetergentBottle','Clamshell','CrushedBottle'],
               'Paper':['Newspaper','CardboardBox','FoldedCarton','PaperBag','EggCarton']}[kind]
        for variant,name in enumerate(names):
            patch={'Metal':['red','red','tin','cap','tin'],'Plastic':['pet','cup','jug','tray','pet'],
                   'Paper':['news','card','card','bag','egg']}[kind][variant]
            body=Mesh(rect=PATCH[patch]);detail=Mesh(rect=PATCH[patch])
            if kind=='Metal' and variant in (0,1,2,4):
                height=.15 if variant in (0,2) else .085;r=.045 if variant!=4 else .05
                profile=[(-height/2,.038),(-height*.44,r),(-height*.28,r),(0,r),(height*.25,r),(height*.44,r),(height/2,.038)]
                if variant==2:
                    profile += [(height/2,.034),(height*.35,.034)]
                body.lathe(profile,ellipse=(1.3,.8) if variant==4 else (1,1),dent=.23 if variant==1 else .025)
                for y in (-height*.47,height*.47):detail.torus((0,y,0),.039,.003,rect=PATCH['tin'])
                detail.torus((.004,height*.5+.002,0),.012,.003,rect=PATCH['tin'])
                if variant==2:detail.lathe([(-.001,.037),(.001,.037)],center=(0,height*.58,-.03),rect=PATCH['tin'])
            elif kind=='Metal':
                body.lathe([(-.015,.054),(-.01,.062),(.005,.061),(.011,.050)],dent=.12)
                detail.torus((0,.008,0),.054,.003)
            elif kind=='Plastic' and variant in (0,4):
                body.lathe([(-.09,.023),(-.082,.038),(-.06,.036),(-.025,.039),(.015,.036),(.045,.033),(.063,.014),(.080,.012)],dent=.21 if variant==4 else .055)
                detail.lathe([(.078,.014),(.092,.014)],rect=PATCH['shampoo'])
                for y in (-.06,-.035,-.01,.015):detail.torus((0,y,0),.037,.002)
            elif kind=='Plastic' and variant==1:
                body.lathe([(-.065,.032),(.055,.047),(.060,.047),(.062,.043),(.055,.041)])
                detail.torus((0,.058,0),.047,.003)
                detail.lathe([(-.01,.003),(.105,.003)],center=(.008,0,0),rect=PATCH['pet'])
            elif kind=='Plastic' and variant==2:
                body.lathe([(-.075,.035),(-.06,.05),(.025,.05),(.055,.027),(.070,.018)],ellipse=(1.1,.63))
                detail.lathe([(.070,.021),(.086,.021)],rect=PATCH['shampoo'])
                detail.torus((.050,.033,0),.022,.006,rot_x(90))
            elif kind=='Plastic':
                body.box((0,-.01,0),(.17,.045,.11));detail.box((0,.020,0),(.18,.010,.12))
                for x in np.linspace(-.07,.07,9):detail.box((x,.026,0),(.004,.004,.108))
            elif variant==0:
                for i in range(6):body.box((0,-.02+i*.007,0),(.17,.005,.12))
                detail.box((0,.025,-.01),(.15,.003,.11),rotation=rot_x(-9))
            elif variant==1:
                body.box((0,-.055,0),(.13,.008,.11))
                for x in (-.065,.065):body.box((x,0,0),(.006,.11,.11))
                for z in (-.055,.055):body.box((0,0,z),(.13,.11,.006))
                for z in (-1,1):detail.box((0,.061,z*.072),(.13,.005,.052),rotation=rot_x(z*35))
                for x in (-1,1):detail.box((x*.068,.035,0),(.009,.012,.10))
            elif variant==2:
                for i in range(5):
                    body.box((0,-.025+i*.012,0),(.17,.008,.115))
                    for x in np.linspace(-.08,.08,12):detail.box((x,-.02+i*.012,-.058),(.006,.005,.004))
            elif variant==3:
                body.lathe([(-.075,.034),(-.060,.048),(.050,.046),(.075,.032)],ellipse=(1.1,.64),dent=.07)
                detail.box((0,-.065,0),(.071,.009,.045));detail.box((0,.062,0),(.067,.007,.009))
            else:
                body.box((0,-.025,0),(.17,.013,.105))
                for x in (-.055,0,.055):
                    for z in (-.027,.027):body.lathe([(-.021,.016),(.015,.023)],center=(x,0,z),n=16)
                detail.box((0,.025,.061),(.17,.009,.10),rotation=rot_x(60))
            # The TWO actual meshes share one normalization; every vertex of
            # every component stays inside the same 0.12m transport proxy.
            pts=np.concatenate([body.points(),detail.points()]);center=(pts.min(0)+pts.max(0))/2
            radius=np.linalg.norm(pts-center,axis=1).max();scale=.118/radius
            for mesh in (body,detail):mesh.transform(scale,-center*scale)
            for suffix,mesh in (('Body',body),('Details',detail)):
                info=mesh.write(f'{kind}_{name}_{suffix}');assert info['max_radius']<=.118001
                info.update(kind=kind,variant=variant,role='RawVisual');result.append(info)
    return result

def bale_face(mesh,axis,sign,half,rect,front=False):
    uaxis,vaxis={0:(2,1),1:(0,2),2:(0,1)}[axis]
    # More subdivisions on the paper layer direction; small inward relief keeps
    # ALL visible geometry within ProductSize, including the solid straps.
    nu,nv=18,14
    def p(i,j):
        u,v=i/nu,j/nv;depth=.003*(.5+.5*math.sin(i*1.7+j*3.2))*math.sin(math.pi*u)*math.sin(math.pi*v)
        a=np.zeros(3);a[axis]=sign*(half[axis]-depth);a[uaxis]=(u*2-1)*half[uaxis];a[vaxis]=(v*2-1)*half[vaxis]
        return a
    for i in range(nu):
        for j in range(nv):
            a,b,c,d=p(i,j),p(i+1,j),p(i+1,j+1),p(i,j+1)
            tex=[uv(rect,i/nu,j/nv,mesh.atlas),uv(rect,(i+1)/nu,j/nv,mesh.atlas),uv(rect,(i+1)/nu,(j+1)/nv,mesh.atlas),uv(rect,i/nu,(j+1)/nv,mesh.atlas)]
            normal=np.eye(3)[axis]*sign
            mesh.tri(a,b,c,tex[:3],normal);mesh.tri(a,c,d,[tex[0],tex[2],tex[3]],normal)

def bale_models():
    out=[]
    for kind in ('Metal','Plastic','Paper'):
        shell=Mesh('10',SIDE[kind]);front=Mesh('09',FRONT[kind]);stock=Mesh('07',STOCK[kind])
        for axis in range(3):
            for sign in (-1,1):
                bale_face(stock,axis,sign,V(.26,.21,.25),STOCK[kind])
                target=front if axis==2 and sign==-1 else shell
                bale_face(target,axis,sign,V(.26,.21,.25),FRONT[kind] if target==front else SIDE[kind])
        for name,mesh in ((f'Bale_{kind}_Shell',shell),(f'Bale_{kind}_Front',front),(f'Stock_{kind}',stock)):
            info=mesh.write(name);info.update(kind=kind,role='DecorativeStock' if name.startswith('Stock') else 'OfficialBale');out.append(info)
    strap=Mesh('07',(175,100,195,200))
    for x in (-.16,.16):
        for z in (-.251,.251):strap.box((x,0,z),(.025,.43,.008))
        for y in (-.211,.211):strap.box((x,y,0),(.025,.008,.51))
    info=strap.write('Bale_Straps');info['role']='SharedStraps';out.append(info)
    pallet=Mesh('07',(40,555,580,575))
    for x in (-.39,0,.39):
        for z in (-.31,.31):pallet.box((x,.067,z),(.15,.09,.15))
    for z in (-.34,0,.34):pallet.box((0,.018,z),(.96,.036,.16))
    for z in np.linspace(-.34,.34,5):pallet.box((0,.121,z),(.96,.038,.14))
    info=pallet.write('Wood_Pallet');info['role']='DecorativePallet';out.append(info)
    return out

def model_meta(path):
    meta=path.with_name(path.name+'.meta')
    ident=guid(path)
    template=(ROOT/'Assets/_Modules_/02_Levels/Level03/Resources/C3V3Models/V9_StairGatewayArch.obj.meta').read_text()
    import re
    template=re.sub(r'^guid: .*$',f'guid: {ident}',template,flags=re.M)
    template=template.replace('materialImportMode: 2','materialImportMode: 0').replace('isReadable: 0','isReadable: 1')
    template=template.replace('useFileUnits: 1','useFileUnits: 0').replace('useFileScale: 1','useFileScale: 0')
    template=template.replace('importAnimation: 1','importAnimation: 0').replace('preserveHierarchy: 0','preserveHierarchy: 1')
    meta.write_text(template)

def material(name,atlas=None,metal=0,gloss=.3,color=(1,1,1)):
    path=OUT/'Materials'/f'{name}.mat'
    tex='{fileID: 2800000, guid: '+image_guids[atlas]+', type: 3}' if atlas else '{fileID: 0}'
    text=f'''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 46, guid: 0000000000000000f000000000000000, type: 0}}
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 1
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {{}}
  disabledShaderPasses: []
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _MainTex:
        m_Texture: {tex}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    m_Ints: []
    m_Floats:
    - _Metallic: {metal}
    - _Glossiness: {gloss}
    - _Mode: 0
    - _SrcBlend: 1
    - _DstBlend: 0
    - _ZWrite: 1
    - _Cull: 2
    m_Colors:
    - _Color: {{r: {color[0]}, g: {color[1]}, b: {color[2]}, a: 1}}
    - _EmissionColor: {{r: 0, g: 0, b: 0, a: 1}}
  m_BuildTextureStacks: []
'''
    path.write_text(text);guid(path,'NativeFormatImporter','  mainObjectFileID: 2100000\n')

if __name__=='__main__':
    image_guids={}
    for key,file in FILES.items():
        path=ART/'SourceImages'/file;assert path.exists(),path
        image_guids[key]=guid(path)
        path.with_name(path.name+'.meta').write_text('fileFormatVersion: 2\nguid: '+image_guids[key]+'''\nTextureImporter:
  serializedVersion: 12
  internalIDToNameTable: []
  externalObjects: {}
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 1
  isReadable: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 0
  textureShape: 1
  npotScale: 0
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 4
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  userData: Owner reference; UVs use interior samples; opaque Standard material
  assetBundleName: 
  assetBundleVariant: 
''')
    for kind,metal,gloss in (('Metal',.75,.5),('Plastic',.03,.43),('Paper',0,.18)):
        for prefix,atlas in (('Raw','08'),('BaleFront','09'),('BaleSide','10'),('Stock','07')):
            material(prefix+'_'+kind,atlas,metal,gloss)
    material('Binding_Steel',metal=.65,gloss=.35,color=(.25,.29,.27));material('Pallet_Wood','07',gloss=.2)
    manifest=raw_models()+bale_models()
    for info in manifest:
        path=OUT/'Meshes'/(info['model']+'.obj');info['sha256']=hashlib.sha256(path.read_bytes()).hexdigest()
    (ART/'MeshManifest.json').write_text(json.dumps(dict(units='metres',raw_proxy_radius=.12,product_size=[.52,.43,.51],
        render_pipeline='Built-in / Standard opaque',image_sha256={file:hashlib.sha256((ART/'SourceImages'/file).read_bytes()).hexdigest() for file in FILES.values()},models=manifest),indent=2)+'\n')
    guid(ART/'MeshManifest.json','TextScriptImporter')
    print(json.dumps(dict(models=len(manifest),triangles=sum(i['triangles'] for i in manifest),raw_max_radius=max(i['max_radius'] for i in manifest if i['role']=='RawVisual'),unity_import='NOT RUN')))
