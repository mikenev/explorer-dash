# blender -b -P ArtSource/render_previews.py -- <art_dir> <out_dir>
import bpy, sys, os, math
from mathutils import Vector
A, O = sys.argv[sys.argv.index("--")+1:][:2]; os.makedirs(O, exist_ok=True)
def setup(res=(640,480)):
    sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.samples=24; sc.cycles.device='CPU'
    sc.render.resolution_x, sc.render.resolution_y = res; sc.render.film_transparent=False
    w=bpy.data.worlds.new("w"); sc.world=w; w.use_nodes=True; w.node_tree.nodes["Background"].inputs[0].default_value=(0.62,0.74,0.58,1); w.node_tree.nodes["Background"].inputs[1].default_value=0.9
    sun=bpy.data.objects.new("sun",bpy.data.lights.new("sun",'SUN')); sun.data.energy=3.5; sun.rotation_euler=(math.radians(50),0,math.radians(-35)); sc.collection.objects.link(sun)
def cam(target, dist, elev=18, az=-35, lens=50):
    sc=bpy.context.scene; c=bpy.data.objects.new("cam",bpy.data.cameras.new("cam")); c.data.lens=lens; sc.collection.objects.link(c); sc.camera=c
    t=Vector(target); d=Vector((math.sin(math.radians(az))*math.cos(math.radians(elev)), -math.cos(math.radians(az))*math.cos(math.radians(elev)), math.sin(math.radians(elev))))
    c.location=t+d*dist; c.rotation_euler=(t-c.location).to_track_quat('-Z','Y').to_euler()
def bounds(objs):
    pts=[o.matrix_world@Vector(b) for o in objs if o.type=='MESH' for b in o.bound_box]
    mn=Vector([min(p[i] for p in pts) for i in range(3)]); mx=Vector([max(p[i] for p in pts) for i in range(3)]); return mn,mx
def render(path): bpy.context.scene.render.filepath=path; bpy.ops.render.render(write_still=True)
names=["Explorer","Boulder","GoldenIdol","Track_StonePath","Track_Wall","Foliage_Bush","Foliage_Palm","Foliage_Fern","Pit","Vine","Alligator"]
for n in names:
    bpy.ops.wm.open_mainfile(filepath=f"{A}/{n}.blend"); setup()
    meshes=[o for o in bpy.data.objects if o.type=='MESH']; mn,mx=bounds(meshes); c=(mn+mx)/2; r=(mx-mn).length
    cam(c, r*1.5, az=-35 if n!="Track_Wall" else 60); render(f"{O}/{n}.png")
# animation strips
for n,acts in (("Explorer",{"Run":[1,6,11,16],"Jump":[1,8,21],"Slide":[1,10],"Stumble":[5,12],"Caught":[10,20,31]}),("Alligator",{"Lunge":[1,7,12,25]})):
    for a,frames in acts.items():
        bpy.ops.wm.open_mainfile(filepath=f"{A}/{n}.blend"); setup((320,400) if n=="Explorer" else (480,320))
        arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
        for t in arm.animation_data.nla_tracks: t.mute=True
        arm.animation_data.action=bpy.data.actions[a]
        cam((0,0,0.9) if n=="Explorer" else (0,-0.5,0.3), 4.2 if n=="Explorer" else 4.5, az=-60 if n=="Explorer" else -70, elev=12)
        for f in frames: bpy.context.scene.frame_set(f); render(f"{O}/anim/{n}_{a}_{f:02d}.png")
