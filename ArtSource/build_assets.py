# Explorer Dash M3 art generator. Run: blender -b -P ArtSource/build_assets.py -- <out_dir>
# All geometry is procedural and original (no third-party assets).
import bpy, bmesh, math, sys, os, random
from mathutils import Vector, Matrix, Euler
OUT = sys.argv[sys.argv.index("--")+1] if "--" in sys.argv else "/tmp/ed_art"
os.makedirs(OUT, exist_ok=True)
random.seed(7)
PAL = {  # name: rgb (flat palette, shared with the Unity material remap)
 "Skin":(0.80,0.56,0.40),"Hair":(0.18,0.10,0.06),"Helmet":(0.86,0.66,0.30),"HelmetBand":(0.10,0.42,0.42),
 "Teal":(0.07,0.48,0.47),"Shirt":(0.93,0.86,0.68),"Ochre":(0.80,0.52,0.14),"Shorts":(0.45,0.36,0.22),
 "Sock":(0.93,0.88,0.75),"Boot":(0.28,0.17,0.10),"Satchel":(0.55,0.30,0.14),"Brass":(0.85,0.70,0.30),
 "Stone":(0.50,0.48,0.42),"StoneDark":(0.33,0.32,0.29),"Moss":(0.30,0.45,0.18),"Gold":(1.0,0.76,0.15),
 "Gem":(0.10,0.75,0.55),"Leaf":(0.16,0.46,0.18),"LeafLight":(0.32,0.62,0.22),"Vine":(0.20,0.36,0.12),
 "Gator":(0.24,0.36,0.18),"GatorBelly":(0.70,0.68,0.45),"Mouth":(0.70,0.25,0.25),"Tooth":(0.95,0.93,0.85),
 "Void":(0.03,0.03,0.03),"Dust":(0.82,0.74,0.58),"Fletch":(0.75,0.18,0.12),"Wood":(0.62,0.48,0.30),"Eye":(0.95,0.85,0.20),
}
def mat(n):
    m = bpy.data.materials.get(n)
    if not m:
        m = bpy.data.materials.new(n); m.use_nodes = True
        b = m.node_tree.nodes["Principled BSDF"]; c = PAL[n]
        b.inputs["Base Color"].default_value = (*c, 1); b.inputs["Roughness"].default_value = 0.8
        if n in ("Gold","Brass"): b.inputs["Metallic"].default_value = 0.8; b.inputs["Roughness"].default_value = 0.35
        m.diffuse_color = (*c, 1)
    return m
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
class Builder:
    """Accumulates flat-shaded parts into one bmesh; each part gets a material and (optionally) a bone group."""
    def __init__(s, name): s.name=name; s.bm=bmesh.new(); s.mats=[]; s.groups=[]; s.vg={}
    def _mi(s,m):
        if m not in s.mats: s.mats.append(m)
        return s.mats.index(m)
    def add(s, kind, m, loc=(0,0,0), size=(1,1,1), rot=(0,0,0), bone=None, seg=8, taper=1.0, jitter=0.0):
        tmp = bmesh.new()
        if kind=="box": bmesh.ops.create_cube(tmp, size=1.0)
        elif kind=="cyl": bmesh.ops.create_cone(tmp, cap_ends=True, segments=seg, radius1=0.5, radius2=0.5*taper, depth=1.0)
        elif kind=="sphere": bmesh.ops.create_icosphere(tmp, subdivisions=1 if seg<=8 else 2, radius=0.5)
        elif kind=="cone": bmesh.ops.create_cone(tmp, cap_ends=True, segments=seg, radius1=0.5, radius2=0.0, depth=1.0)
        if jitter:
            for v in tmp.verts: v.co += Vector((random.uniform(-1,1) for _ in range(3)))*jitter
        M = Matrix.Translation(loc) @ Euler([math.radians(a) for a in rot]).to_matrix().to_4x4() @ Matrix.Diagonal((*size,1))
        bmesh.ops.transform(tmp, matrix=M, verts=tmp.verts)
        mi = s._mi(m)
        for f in tmp.faces: f.material_index = mi; f.smooth = False
        me = bpy.data.meshes.new("t"); tmp.to_mesh(me); tmp.free()
        off = len(s.bm.verts); s.bm.from_mesh(me); bpy.data.meshes.remove(me)
        s.bm.verts.ensure_lookup_table()
        s.groups.append((bone, list(range(off, len(s.bm.verts)))))
        # from_mesh resets material indices? keep explicit
        return s
    def build(s):
        me = bpy.data.meshes.new(s.name); s.bm.to_mesh(me)
        ob = bpy.data.objects.new(s.name, me); bpy.context.scene.collection.objects.link(ob)
        for m in s.mats: me.materials.append(mat(m))
        for bone, idx in s.groups:
            if bone:
                g = ob.vertex_groups.get(bone) or ob.vertex_groups.new(name=bone)
                g.add(idx, 1.0, 'REPLACE')
        return ob
    def tris(s): return sum(len(f.verts)-2 for f in s.bm.faces)
# fix: bmesh.from_mesh keeps material_index from mesh polys; our tmp mesh has no materials so indices are preserved as set.

def armature(name, bones):
    """bones: list of (name, head, tail, parent)"""
    ad = bpy.data.armatures.new(name+"_Rig"); ao = bpy.data.objects.new(name+"_Rig", ad)
    bpy.context.scene.collection.objects.link(ao); bpy.context.view_layer.objects.active = ao
    bpy.ops.object.mode_set(mode='EDIT')
    for n,h,t,p in bones:
        b = ad.edit_bones.new(n); b.head=h; b.tail=t; b.roll=0
        if p: b.parent = ad.edit_bones[p]
    bpy.ops.object.mode_set(mode='OBJECT')
    for pb in ao.pose.bones: pb.rotation_mode='XYZ'
    return ao
def skin(ob, arm):
    ob.parent = arm; md = ob.modifiers.new("Armature",'ARMATURE'); md.object = arm
def action(arm, name, frames, keys, loop=True):
    """keys: {frame: {bone: (rx,ry,rz) deg or ('loc',(x,y,z))}}"""
    act = bpy.data.actions.new(name); arm.animation_data_create(); arm.animation_data.action = act
    for pb in arm.pose.bones: pb.rotation_euler=(0,0,0); pb.location=(0,0,0)
    for f in sorted(keys):
        for pb in arm.pose.bones:  # key every bone each keyframe so clips never inherit stale pose
            v = keys[f].get(pb.name, (0,0,0))
            if not isinstance(v,dict): v = {"r":v}
            v = {"r":v.get("r",(0,0,0)),"l":v.get("l",(0,0,0))}
            if isinstance(v,dict):
                if "r" in v: pb.rotation_euler=[math.radians(a) for a in v["r"]]; pb.keyframe_insert("rotation_euler", frame=f)
                if "l" in v: pb.location=v["l"]; pb.keyframe_insert("location", frame=f)
            else:
                pb.rotation_euler=[math.radians(a) for a in v]; pb.keyframe_insert("rotation_euler", frame=f)
    act.use_fake_user = True
    tr = arm.animation_data.nla_tracks.new(); tr.name=name; st = tr.strips.new(name, int(frames[0]), act); tr.mute=True
    arm.animation_data.action = None
    for pb in arm.pose.bones: pb.rotation_euler=(0,0,0); pb.location=(0,0,0)
    return act
def export(name, objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=f"{OUT}/{name}.fbx", use_selection=True, apply_scale_options='FBX_SCALE_UNITS',
        axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False,
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0, object_types={'MESH','ARMATURE'})
    bpy.ops.wm.save_as_mainfile(filepath=f"{OUT}/{name}.blend")
STATS = {}
def stat(name, *builders): STATS[name] = sum(b.tris() for b in builders)

# ---------------- EXPLORER (faces -Y in Blender = +Z in Unity) ----------------
def explorer():
    reset()
    B = [("Hips",(0,0,0.95),(0,0,1.1),None),("Spine",(0,0,1.1),(0,0,1.35),"Hips"),("Chest",(0,0,1.35),(0,0,1.5),"Spine"),
         ("Head",(0,0,1.5),(0,0,1.8),"Chest"),
         ("UpperArm.L",(0.24,0,1.42),(0.28,0,1.15),"Chest"),("LowerArm.L",(0.28,0,1.15),(0.30,0,0.9),"UpperArm.L"),
         ("UpperArm.R",(-0.24,0,1.42),(-0.28,0,1.15),"Chest"),("LowerArm.R",(-0.28,0,1.15),(-0.30,0,0.9),"UpperArm.R"),
         ("Thigh.L",(0.11,0,0.92),(0.11,0,0.5),"Hips"),("Shin.L",(0.11,0,0.5),(0.11,0,0.08),"Thigh.L"),
         ("Thigh.R",(-0.11,0,0.92),(-0.11,0,0.5),"Hips"),("Shin.R",(-0.11,0,0.5),(-0.11,0,0.08),"Thigh.R")]
    arm = armature("Explorer", B)
    b = Builder("Explorer")
    # torso: teal vest over cream shirt, ochre belt
    b.add("box","Shirt",(0,0,1.32),(0.40,0.24,0.34),bone="Chest")
    b.add("box","Teal",(0,-0.005,1.30),(0.42,0.25,0.26),bone="Chest")
    b.add("box","Shirt",(0,0,1.08),(0.34,0.21,0.20),bone="Spine")
    b.add("box","Ochre",(0,0,0.97),(0.36,0.23,0.07),bone="Hips")
    b.add("box","Brass",(0,-0.12,0.97),(0.07,0.02,0.05),bone="Hips")
    b.add("box","Shorts",(0,0,0.88),(0.34,0.22,0.14),bone="Hips")
    # long teal scarf with a trailing tail (signature silhouette)
    b.add("cyl","Teal",(0,0,1.50),(0.24,0.22,0.08),seg=8,bone="Chest")
    b.add("box","Teal",(0.06,0.17,1.36),(0.08,0.03,0.30),rot=(-25,0,8),bone="Chest")
    b.add("box","Teal",(0.10,0.27,1.18),(0.07,0.03,0.22),rot=(-45,0,14),bone="Chest")
    # satchel on a cross strap
    b.add("box","Satchel",(-0.24,-0.02,0.98),(0.08,0.24,0.20),bone="Hips")
    b.add("box","Brass",(-0.285,-0.02,1.00),(0.01,0.05,0.04),bone="Hips")
    b.add("box","Satchel",(0,-0.13,1.25),(0.05,0.02,0.52),rot=(0,40,0),bone="Chest")
    # head, hair bun, pith helmet (dome + brim + teal band)
    b.add("box","Skin",(0,0,1.62),(0.22,0.22,0.24),bone="Head")
    b.add("box","Skin",(0,-0.12,1.60),(0.05,0.04,0.05),bone="Head")  # nose
    b.add("box","Hair",(0,0.10,1.58),(0.22,0.06,0.20),bone="Head")
    b.add("sphere","Hair",(0,0.15,1.66),(0.12,0.10,0.12),bone="Head")
    b.add("box","Void",(0.055,-0.112,1.645),(0.03,0.01,0.03),bone="Head")
    b.add("box","Void",(-0.055,-0.112,1.645),(0.03,0.01,0.03),bone="Head")
    b.add("cyl","Helmet",(0,0,1.755),(0.46,0.50,0.03),seg=12,bone="Head")
    b.add("sphere","Helmet",(0,0,1.79),(0.30,0.32,0.22),seg=8,bone="Head")
    b.add("cyl","HelmetBand",(0,0,1.77),(0.305,0.325,0.04),seg=12,bone="Head")
    b.add("sphere","Brass",(0,0,1.90),(0.04,0.04,0.03),bone="Head")
    for s,side in ((1,"L"),(-1,"R")):
        # rolled shirt sleeves + bare forearms + hands
        b.add("box","Shirt",(s*0.26,0,1.34),(0.12,0.13,0.18),bone=f"UpperArm.{side}")
        b.add("box","Shirt",(s*0.27,0,1.21),(0.13,0.14,0.06),bone=f"UpperArm.{side}")  # roll
        b.add("box","Skin",(s*0.29,0,1.04),(0.09,0.09,0.24),bone=f"LowerArm.{side}")
        b.add("box","Skin",(s*0.30,0,0.88),(0.08,0.10,0.09),bone=f"LowerArm.{side}")
        # legs: shorts, skin knees, knee socks, chunky boots
        b.add("box","Shorts",(s*0.11,0,0.78),(0.15,0.17,0.20),bone=f"Thigh.{side}")
        b.add("box","Skin",(s*0.11,0,0.58),(0.11,0.12,0.22),bone=f"Thigh.{side}")
        b.add("box","Sock",(s*0.11,0,0.33),(0.115,0.125,0.28),bone=f"Shin.{side}")
        b.add("box","Ochre",(s*0.11,0,0.47),(0.12,0.13,0.03),bone=f"Shin.{side}")
        b.add("box","Boot",(s*0.11,-0.03,0.09),(0.14,0.24,0.18),bone=f"Shin.{side}")
    ob = b.build(); skin(ob, arm); stat("Explorer", b)
    L,R="Thigh.L","Thigh.R"
    # Run (loop, 20f): legs/arms opposite swing, slight bob and lean
    def run_pose(p):
        return {"Thigh.L":(-40*p,0,0),"Shin.L":(max(0,40*p)+25,0,0),"Thigh.R":(40*p,0,0),"Shin.R":(max(0,-40*p)+25,0,0),
                "UpperArm.L":(45*p,0,0),"LowerArm.L":(-50,0,0),"UpperArm.R":(-45*p,0,0),"LowerArm.R":(-50,0,0),
                "Spine":(-12,0,0),"Hips":{"r":(0,0,6*p),"l":(0,0.04*abs(p),0)},"Head":(8,0,0)}
    action(arm,"Run",(1,21),{1:run_pose(1),6:run_pose(0),11:run_pose(-1),16:run_pose(0),21:run_pose(1)})
    action(arm,"Idle",(1,41),{1:{"Spine":(0,0,0),"UpperArm.L":(0,0,0),"UpperArm.R":(0,0,0)},
        21:{"Spine":(-4,0,0),"UpperArm.L":(5,0,-4),"UpperArm.R":(5,0,4)},41:{"Spine":(0,0,0),"UpperArm.L":(0,0,0),"UpperArm.R":(0,0,0)}})
    tuck={"Thigh.L":(-70,0,0),"Shin.L":(100,0,0),"Thigh.R":(-50,0,0),"Shin.R":(80,0,0),"UpperArm.L":(-150,0,-20),"UpperArm.R":(-150,0,20),"LowerArm.L":(-20,0,0),"LowerArm.R":(-20,0,0),"Spine":(-10,0,0)}
    action(arm,"Jump",(1,21),{1:{"Thigh.L":(-20,0,0),"Shin.L":(40,0,0),"Thigh.R":(20,0,0),"Shin.R":(20,0,0),"UpperArm.L":(0,0,0),"UpperArm.R":(0,0,0),"LowerArm.L":(0,0,0),"LowerArm.R":(0,0,0),"Spine":(-5,0,0)},
        8:tuck,14:tuck,21:{"Thigh.L":(-30,0,0),"Shin.L":(30,0,0),"Thigh.R":(10,0,0),"Shin.R":(20,0,0),"UpperArm.L":(-40,0,-10),"UpperArm.R":(-40,0,10),"LowerArm.L":(-30,0,0),"LowerArm.R":(-30,0,0),"Spine":(-5,0,0)}},loop=False)
    slide={"Hips":{"r":(65,0,0),"l":(0,-0.55,0)},"Spine":(-10,0,0),"Head":(-35,0,0),"Thigh.L":(-95,0,0),"Shin.L":(10,0,0),"Thigh.R":(-60,0,0),"Shin.R":(80,0,0),"UpperArm.L":(40,0,-30),"UpperArm.R":(-120,0,20),"LowerArm.L":(0,0,0),"LowerArm.R":(-20,0,0)}
    rest={k:((0,0,0) if not isinstance(v,dict) else {"r":(0,0,0),"l":(0,0,0)}) for k,v in slide.items()}
    action(arm,"Slide",(1,25),{1:rest,5:slide,20:slide,25:rest},loop=False)
    action(arm,"Stumble",(1,19),{1:rest,5:{"Spine":(35,0,8),"Head":(20,0,0),"Hips":{"r":(15,0,0),"l":(0,-0.08,0)},"UpperArm.L":(-80,0,-60),"UpperArm.R":(-60,0,70),"LowerArm.L":(0,0,0),"LowerArm.R":(0,0,0),"Thigh.L":(-30,0,0),"Shin.L":(60,0,0),"Thigh.R":(25,0,0),"Shin.R":(10,0,0),"Slide":None} if False else
        {"Spine":(35,0,8),"Head":(20,0,0),"Hips":{"r":(15,0,0),"l":(0,-0.08,0)},"UpperArm.L":(-80,0,-60),"UpperArm.R":(-60,0,70),"Thigh.L":(-30,0,0),"Shin.L":(60,0,0),"Thigh.R":(25,0,0),"Shin.R":(10,0,0)},
        19:{"Spine":(0,0,0),"Head":(0,0,0),"Hips":{"r":(0,0,0),"l":(0,0,0)},"UpperArm.L":(0,0,0),"UpperArm.R":(0,0,0),"Thigh.L":(0,0,0),"Shin.L":(0,0,0),"Thigh.R":(0,0,0),"Shin.R":(0,0,0)}},loop=False)
    # Caught: turns to look back, arms up in panic, then flattened forward
    action(arm,"Caught",(1,31),{1:{"Hips":{"r":(0,0,0),"l":(0,0,0)},"Spine":(0,0,0),"Head":(0,0,0),"UpperArm.L":(0,0,0),"UpperArm.R":(0,0,0),"Thigh.L":(0,0,0),"Thigh.R":(0,0,0),"Shin.L":(0,0,0),"Shin.R":(0,0,0)},
        10:{"Hips":{"r":(0,0,0),"l":(0,0,0)},"Spine":(0,0,40),"Head":(-15,0,50),"UpperArm.L":(-170,0,-30),"UpperArm.R":(-170,0,30),"Thigh.L":(-10,0,0),"Thigh.R":(10,0,0),"Shin.L":(10,0,0),"Shin.R":(10,0,0)},
        20:{"Hips":{"r":(-80,0,0),"l":(0,-0.75,0)},"Spine":(0,0,0),"Head":(-20,0,0),"UpperArm.L":(-170,0,-60),"UpperArm.R":(-170,0,60),"Thigh.L":(0,0,0),"Thigh.R":(0,0,20),"Shin.L":(0,0,0),"Shin.R":(0,0,0)},
        31:{"Hips":{"r":(-88,0,0),"l":(0,-0.82,0)},"Spine":(0,0,0),"Head":(-10,0,0),"UpperArm.L":(-175,0,-70),"UpperArm.R":(-175,0,70),"Thigh.L":(0,0,-10),"Thigh.R":(0,0,10),"Shin.L":(0,0,0),"Shin.R":(0,0,0)}},loop=False)
    export("Explorer",[arm,ob])

def boulder():
    reset(); b = Builder("Boulder")
    b.add("sphere","Stone",(0,0,0),(6,6,6),seg=16,jitter=0.0)
    me_b = b
    # carved band + glyph studs + moss patches
    b.add("cyl","StoneDark",(0,0,0),(6.06,6.06,0.7),rot=(0,90,0),seg=16)
    for i in range(8):
        a = i*math.pi/4; b.add("box","Stone",(0,math.cos(a)*3.05,math.sin(a)*3.05),(0.9,0.5,0.5),rot=(math.degrees(a),0,0))
    for i in range(6):
        d = Vector((random.uniform(-1,1),random.uniform(-1,1),random.uniform(-1,1))).normalized()
        b.add("sphere","Moss",tuple(d*2.85),(1.4,1.4,0.6),rot=tuple(math.degrees(x) for x in d.to_track_quat('Z','Y').to_euler()))
    ob=b.build()
    # roughen the main sphere a bit
    for v in ob.data.vertices:
        if v.co.length>2.9: v.co *= 1.0+random.uniform(-0.04,0.04)
    stat("Boulder",b); export("Boulder",[ob])

def idol():
    reset(); b = Builder("GoldenIdol")
    # original design: squat seated frog-owl spirit with crescent crest and gem eyes
    b.add("cyl","Gold",(0,0,0.03),(0.30,0.30,0.06),seg=8)
    b.add("cyl","Gold",(0,0,0.14),(0.24,0.22,0.18),seg=8,taper=0.85)
    b.add("sphere","Gold",(0,0,0.30),(0.26,0.22,0.22))
    b.add("box","Gold",(0,0,0.42),(0.10,0.08,0.10),rot=(0,0,45))
    b.add("cyl","Gold",(0.0,0,0.49),(0.34,0.06,0.08),rot=(90,0,0),seg=8)  # crescent crest
    b.add("cone","Gold",(0.17,0,0.53),(0.06,0.06,0.12),rot=(0,25,0))
    b.add("cone","Gold",(-0.17,0,0.53),(0.06,0.06,0.12),rot=(0,-25,0))
    b.add("sphere","Gem",(0.065,-0.10,0.34),(0.06,0.04,0.06))
    b.add("sphere","Gem",(-0.065,-0.10,0.34),(0.06,0.04,0.06))
    b.add("box","Gold",(0.14,-0.06,0.20),(0.06,0.10,0.05)); b.add("box","Gold",(-0.14,-0.06,0.20),(0.06,0.10,0.05))
    ob=b.build(); stat("GoldenIdol",b); export("GoldenIdol",[ob])

def tiles():
    W, L = 8.5, 20.0  # matches GameConfig lane width 2.5 x3 + 1, TrackManager tileLength 20
    reset(); b = Builder("Track_StonePath")
    for zi in range(10):
        for xi in range(4):
            off = 1.06 if zi%2 else 0
            x0 = -W/2 + xi*2.125 + (off if xi<3 else 0) * 0
            h = random.uniform(-0.03,0.03)
            b.add("box", random.choice(["Stone","Stone","StoneDark"]), (-W/2+1.0625+xi*2.125+(0.3 if zi%2 else -0.3)*(xi not in (0,3)), zi*2+1, -0.25+h),(2.05,1.95,0.5))
    b.add("box","StoneDark",(0,L/2,-0.6),(W,L,0.3))
    ob=b.build()
    # Unity tile origin = start of tile at path centre, extends +Z (Blender -Y): mirror Y
    for v in ob.data.vertices: v.co.y = -v.co.y
    ob.data.flip_normals()
    stat("Track_StonePath",b); export("Track_StonePath",[ob])
    reset(); b = Builder("Track_Wall")
    for zi in range(5):
        for row in range(2):
            b.add("box",random.choice(["Stone","StoneDark","Stone"]),(0, zi*4+2+(1 if row%2 else 0)*0, row*1.0+0.5),(0.8,3.95 if row!=1 else 3.9,0.97))
    b.add("box","StoneDark",(0,L/2,2.12),(1.1,L,0.25))
    for zi in range(10):  # dart holes facing the track (+X side faces track when placed on the left)
        b.add("cyl","Void",(0.40,zi*2+1,1.3),(0.12,0.12,0.06),rot=(0,90,0),seg=6)
        b.add("cyl","Void",(-0.40,zi*2+1,1.3),(0.12,0.12,0.06),rot=(0,90,0),seg=6)
    for i in range(6): b.add("sphere","Moss",(random.choice((-0.4,0.4)),random.uniform(1,19),random.uniform(0.3,1.9)),(0.3,1.2,0.5))
    ob=b.build()
    for v in ob.data.vertices: v.co.y=-v.co.y
    ob.data.flip_normals(); stat("Track_Wall",b); export("Track_Wall",[ob])
    reset(); b = Builder("Foliage_Bush")
    for i in range(5):
        b.add("sphere",random.choice(["Leaf","LeafLight"]),(random.uniform(-0.6,0.6),random.uniform(-0.6,0.6),0.5+random.uniform(0,0.4)),(1.1,1.1,0.9),jitter=0.06)
    ob=b.build(); stat("Foliage_Bush",b); export("Foliage_Bush",[ob])
    reset(); b = Builder("Foliage_Palm")
    for i in range(5): b.add("cyl","Shorts",(0.06*i,0,0.6+i*1.1),(0.35-0.03*i,0.35-0.03*i,1.15),rot=(0,4*i,0),seg=6)
    for i in range(7):
        a=i*360/7; b.add("box",random.choice(["Leaf","LeafLight"]),(0.3+1.2*math.cos(math.radians(a)),1.2*math.sin(math.radians(a)),5.6),(2.6,0.6,0.08),rot=(0,22,a))
    ob=b.build(); stat("Foliage_Palm",b); export("Foliage_Palm",[ob])
    reset(); b = Builder("Foliage_Fern")
    for i in range(8):
        a=i*45; b.add("box",random.choice(["Leaf","LeafLight"]),(0.5*math.cos(math.radians(a)),0.5*math.sin(math.radians(a)),0.35),(1.2,0.25,0.04),rot=(0,-35,a))
    ob=b.build(); stat("Foliage_Fern",b); export("Foliage_Fern",[ob])

def hazards():
    reset(); b = Builder("Pit")  # one lane wide (2.5m) x 3m long, origin at lane centre, ground level
    b.add("box","Void",(0,0,-0.9),(2.3,2.9,1.6))
    for s in (-1,1):
        b.add("box","StoneDark",(s*1.2,0,-0.15),(0.18,3.1,0.32))
        b.add("box","StoneDark",(0,s*1.5,-0.15),(2.5,0.18,0.32))
    for i in range(6): b.add("box","Stone",(random.uniform(-1.1,1.1),random.choice((-1.55,1.55)),0.02),(0.35,0.3,0.12),rot=(0,0,random.uniform(0,40)),jitter=0.03)
    ob=b.build(); stat("Pit",b); export("Pit",[ob])
    reset()
    arm = armature("Vine",[("V0",(0,0,4.5),(0,0,3.5),None),("V1",(0,0,3.5),(0,0,2.5),"V0"),("V2",(0,0,2.5),(0,0,1.4),"V1")])
    b = Builder("Vine")
    for i,(z,bn) in enumerate([(4.0,"V0"),(3.0,"V1"),(1.95,"V2")]):
        b.add("cyl","Vine",(0,0,z),(0.09,0.09,1.1),seg=6,bone=bn)
        b.add("box","LeafLight",(0.12,0,z+0.2),(0.25,0.04,0.12),rot=(0,30,0),bone=bn)
        b.add("box","Leaf",(-0.12,0,z-0.2),(0.25,0.04,0.12),rot=(0,-30,0),bone=bn)
    b.add("sphere","Vine",(0,0,1.4),(0.2,0.2,0.2),bone="V2")
    b.add("box","Moss",(0,0,4.6),(3,0.4,0.25))
    ob=b.build(); skin(ob,arm); stat("Vine",b)
    action(arm,"Sway",(1,61),{1:{"V0":(0,8,0),"V1":(0,5,0),"V2":(0,4,0)},31:{"V0":(0,-8,0),"V1":(0,-5,0),"V2":(0,-4,0)},61:{"V0":(0,8,0),"V1":(0,5,0),"V2":(0,4,0)}})
    export("Vine",[arm,ob])
    reset()  # Alligator faces -Y (Unity +Z); place it facing the player by rotating 180 in Unity
    arm = armature("Alligator",[("Body",(0,0.3,0.3),(0,-0.5,0.3),None),("Head",(0,-0.5,0.32),(0,-1.4,0.32),"Body"),("Jaw",(0,-0.55,0.22),(0,-1.4,0.22),"Body"),
        ("Tail1",(0,0.3,0.28),(0,1.2,0.25),"Body"),("Tail2",(0,1.2,0.25),(0,2.2,0.2),"Tail1")])
    b = Builder("Alligator")
    b.add("box","Gator",(0,-0.1,0.32),(0.8,1.5,0.4),bone="Body"); b.add("box","GatorBelly",(0,-0.1,0.13),(0.72,1.4,0.04),bone="Body")
    for i in range(5): b.add("cone","Gator",(0,-0.7+i*0.3,0.56),(0.12,0.12,0.12),seg=4,bone="Body")
    b.add("box","Gator",(0,-0.95,0.36),(0.5,0.9,0.16),bone="Head"); b.add("box","Mouth",(0,-0.95,0.27),(0.44,0.84,0.02),bone="Head")
    b.add("box","Eye",(0.15,-0.62,0.48),(0.1,0.1,0.08),bone="Head"); b.add("box","Eye",(-0.15,-0.62,0.48),(0.1,0.1,0.08),bone="Head")
    for i in range(5):
        for s in (-1,1): b.add("cone","Tooth",(s*0.22,-1.3+i*0.15,0.24),(0.04,0.04,0.07),rot=(180,0,0),seg=4,bone="Head")
    b.add("box","Gator",(0,-0.95,0.18),(0.46,0.86,0.1),bone="Jaw"); b.add("box","Mouth",(0,-0.95,0.235),(0.40,0.80,0.01),bone="Jaw")
    b.add("box","Gator",(0,0.75,0.28),(0.5,0.95,0.3),bone="Tail1",); b.add("box","Gator",(0,1.7,0.22),(0.28,1.0,0.18),bone="Tail2")
    for sx in (-1,1):
        for sy in (-0.6,0.35): b.add("box","Gator",(sx*0.45,sy,0.12),(0.18,0.22,0.24),rot=(0,sx*20,0),bone="Body")
    ob=b.build(); skin(ob,arm); stat("Alligator",b)
    action(arm,"Idle",(1,41),{1:{"Tail1":(0,0,6),"Tail2":(0,0,8),"Jaw":(0,0,0)},21:{"Tail1":(0,0,-6),"Tail2":(0,0,-8),"Jaw":(-4,0,0)},41:{"Tail1":(0,0,6),"Tail2":(0,0,8),"Jaw":(0,0,0)}})
    action(arm,"Lunge",(1,25),{1:{"Body":{"r":(0,0,0),"l":(0,0,0)},"Head":(0,0,0),"Jaw":(0,0,0),"Tail1":(0,0,0)},
        7:{"Body":{"r":(-8,0,0),"l":(0,0.1,0)},"Head":(-35,0,0),"Jaw":(30,0,0),"Tail1":(0,0,20)},
        12:{"Body":{"r":(-4,0,0),"l":(0,0.9,0.1)},"Head":(-30,0,0),"Jaw":(28,0,0),"Tail1":(0,0,-25)},
        15:{"Body":{"r":(0,0,0),"l":(0,1.0,0)},"Head":(0,0,0),"Jaw":(0,0,0),"Tail1":(0,0,-10)},
        25:{"Body":{"r":(0,0,0),"l":(0,0,0)},"Head":(0,0,0),"Jaw":(0,0,0),"Tail1":(0,0,0)}},loop=False)
    export("Alligator",[arm,ob])

def darts():
    """Dart (tip points -Y = Unity +Z), dart-hole emitter mask (faces -Y = Unity +Z, hole at origin), dust puff (Puff loop)."""
    reset(); b = Builder("Dart")
    b.add("cyl","Wood",(0,0,0),(0.035,0.035,0.30),rot=(90,0,0),seg=5)
    b.add("cone","Brass",(0,-0.19,0),(0.05,0.05,0.09),rot=(90,0,0),seg=5)
    for a in (0,90): b.add("box","Fletch",(0,0.13,0),(0.11,0.07,0.008),rot=(0,a,0))
    ob=b.build(); stat("Dart",b); export("Dart",[ob])
    reset(); b = Builder("DartEmitter")  # carved temple-mask plate, 0.7 m, origin = hole centre on the wall face
    b.add("box","StoneDark",(0,0.03,0),(0.72,0.06,0.72))
    b.add("box","Stone",(0,-0.02,0.24),(0.56,0.05,0.14))           # brow
    b.add("box","Moss",(0.2,-0.03,0.30),(0.18,0.03,0.06))
    for sx in (-1,1):
        b.add("cyl","Gem",(sx*0.16,-0.03,0.14),(0.08,0.08,0.03),rot=(90,0,0),seg=6)   # eyes
        b.add("box","Stone",(sx*0.22,-0.02,-0.04),(0.12,0.05,0.30))  # cheeks
        b.add("cone","Stone",(sx*0.26,-0.02,0.38),(0.10,0.10,0.14),seg=4)  # ears
    b.add("cyl","Stone",(0,-0.03,0),(0.22,0.22,0.06),rot=(90,0,0),seg=8)  # lips ring
    b.add("cyl","Void",(0,-0.065,0),(0.12,0.12,0.02),rot=(90,0,0),seg=8)   # hole
    b.add("box","Stone",(0,-0.02,-0.26),(0.40,0.05,0.10))            # chin
    ob=b.build(); stat("DartEmitter",b); export("DartEmitter",[ob])
    reset()
    arm = armature("DustPuff",[("P0",(0,0,0),(0,0,0.2),None)]+[(f"P{i}",(0,0,0),(0,0,0.2),"P0") for i in range(1,6)])
    b = Builder("DustPuff")
    offs=[(0,0,0),(0.12,-0.05,0.10),(-0.12,-0.04,0.08),(0.08,-0.06,-0.10),(-0.10,-0.05,-0.08),(0,-0.10,0.02)]
    for i,o in enumerate(offs): b.add("sphere","Dust",o,(0.22,0.22,0.22),bone=f"P{i}",jitter=0.01)
    ob=b.build(); skin(ob,arm); stat("DustPuff",b)
    # loop: puff lobes drift outward (-Y = into the track) while the cluster spins; root scale is pulsed by DartTrap
    k={}
    for f,t in ((1,0.0),(9,0.5),(17,1.0)):
        k[f]={f"P{i}":{"r":(0,0,0),"l":(o[0]*t*2, -o[2]*t*2, (o[1]-0.1)*t*-1.5) if i else (0,0,0)} for i,o in enumerate(offs)}
        k[f]["P0"]={"r":(0,120*t,0),"l":(0,0,0)}
    action(arm,"Puff",(1,17),k)
    export("DustPuff",[arm,ob])

explorer(); boulder(); idol(); tiles(); hazards(); darts()
import json; json.dump(STATS, open(f"{OUT}/tri_counts.json","w"), indent=1); print("STATS", STATS)
