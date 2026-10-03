import bpy, math, pathlib, argparse, sys
from mathutils import Vector, Matrix, Quaternion

# Run with Blender's --python option; arguments after -- belong to this tool.
parser = argparse.ArgumentParser(description='Generate the prototype WakeUp animation (overwrites keyframes).')
parser.add_argument('--project-root', type=pathlib.Path, default=pathlib.Path(__file__).resolve().parents[2])
parser.add_argument('--source-dir', type=pathlib.Path, help='External authoring directory; defaults to sibling 美术资产/制作/Tutorial.')
parser.add_argument('--overwrite', action='store_true', help='Explicitly allow replacing an existing authored blend or exported FBX.')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
root = args.project_root.resolve()
source_dir = (args.source_dir or root.parent/'美术资产'/'制作'/'Tutorial').resolve()
blend_path = source_dir/'TutorialWakeUp.blend'
fbx_path = root/'Assets'/'Tutorial'/'Animations'/'WakeUp.fbx'
if not (root/'ProjectSettings'/'ProjectVersion.txt').is_file():
 raise RuntimeError('Not a Unity project: '+str(root))
if source_dir == root or root in source_dir.parents:
 raise RuntimeError('Keep Blender authoring files outside the Unity project.')
if not args.overwrite and (blend_path.exists() or fbx_path.exists()):
 raise RuntimeError('Existing animation preserved. This generator replaces keyframes; use --overwrite only when regeneration is intended.')
source_dir.mkdir(parents=True, exist_ok=True)
fbx_path.parent.mkdir(parents=True, exist_ok=True)
arm = bpy.data.objects['Skeleton']
scene = bpy.context.scene
scene.render.fps = 30
scene.frame_start = 1
scene.frame_end = 169
arm.animation_data_create()
action = bpy.data.actions.get('WakeUp') or bpy.data.actions.new('WakeUp')
arm.animation_data.action = action
action.use_fake_user = True
bones = arm.pose.bones
rest_world = {b.name: arm.matrix_world @ b.matrix_local for b in arm.data.bones}
rest_head = {n:m.translation.copy() for n,m in rest_world.items()}
inv_arm = arm.matrix_world.inverted()
def smooth(x): return x*x*(3-2*x)
def lerp(a,b,x): return a+(b-a)*x
# seconds, pelvis height, body recline, turn to bed edge, foot height, forward foot reach, lean
stages = [(0,.70,-90,0,.69,.82,0),(.7,.705,-86,0,.69,.82,0),(1.8,.71,-8,0,.69,.72,-4),(2.8,.70,0,-90,.38,.53,-3),(3.5,.70,0,-90,.102,.56,-8),(4.0,.73,0,-90,.102,.43,-25),(4.85,.97,0,-90,.102,-.045,-3),(5.25,.981099,0,-90,.101896,-.059862,0),(5.6,.981099,0,-90,.101896,-.059862,0)]
def params(t):
 for a,b in zip(stages, stages[1:]):
  if t<=b[0]:
   w=smooth(max(0,min(1,(t-a[0])/(b[0]-a[0]))))
   return [lerp(a[i],b[i],w) for i in range(1,7)]
 return list(stages[-1][1:])
def world_rotate(name,delta):
 p=bones[name]
 m=arm.matrix_world@p.matrix
 m = Matrix.Translation(m.translation) @ delta.to_matrix().to_4x4() @ Matrix.Translation(-m.translation) @ m
 p.matrix=inv_arm@m
 bpy.context.view_layer.update()
def point_bone(name,child,target):
 p=bones[name]
 m=arm.matrix_world@p.matrix
 start=m.translation
 actual=(arm.matrix_world@bones[child].matrix).translation-start
 delta=actual.rotation_difference(Vector(target)-start)
 world_rotate(name,delta)
def solve_leg(side,foot,pole):
 upper=side+'_UpperLeg'; lower=side+'_LowerLeg'; ankle=side+'_Foot'
 h=(arm.matrix_world@bones[upper].matrix).translation
 l1=(rest_head[lower]-rest_head[upper]).length
 l2=(rest_head[ankle]-rest_head[lower]).length
 dvec=Vector(foot)-h; dist=min(dvec.length,l1+l2-.0001); direction=dvec.normalized()
 a=(l1*l1-l2*l2+dist*dist)/(2*dist)
 bend=Vector(pole)-h; bend=(bend-direction*bend.dot(direction)).normalized()
 knee=h+direction*a+bend*math.sqrt(max(0,l1*l1-a*a))
 point_bone(upper,lower,knee); point_bone(lower,ankle,foot)
 # Preserve a stable sole orientation when feet reach the floor.
 p=bones[ankle]; m=arm.matrix_world@p.matrix; q=Quaternion((0,0,1),math.radians(yaw))
 desired=(q.to_matrix().to_4x4() @ rest_world[ankle]);desired.translation=m.translation
 if t>=3.5: p.matrix=inv_arm@desired; bpy.context.view_layer.update()
for frame in range(1,170):
 scene.frame_set(frame); t=(frame-1)/30
 height,recline,yaw,foot_h,reach,lean=params(t)
 for p in bones: p.matrix_basis=Matrix.Identity(4);p.rotation_mode='QUATERNION'
 bpy.context.view_layer.update()
 turn=Quaternion((0,0,1),math.radians(yaw))
 tilt=Quaternion((1,0,0),math.radians(recline))
 hips=turn.to_matrix().to_4x4()@tilt.to_matrix().to_4x4()@rest_world['Hips']
 hips.translation=Vector((0,0,height))
 bones['Hips'].matrix=inv_arm@hips
 bpy.context.view_layer.update()
 # Small breathing/head movement, then a controlled forward weight shift.
 world_rotate('Spine',Quaternion(turn@Vector((1,0,0)),math.radians(-lean*.45)))
 world_rotate('Chest',Quaternion(turn@Vector((1,0,0)),math.radians(-lean*.55)))
 if t<1.8: world_rotate('Head',Quaternion((1,0,0),math.radians(math.sin(t*3)*2)))
 # Legs move across the mattress before lowering. Reach is along the current facing.
 forward=turn@Vector((0,-1,0)); lateral=turn@Vector((1,0,0))
 for side,sign in [('Left',1),('Right',-1)]:
  foot=forward*reach+lateral*(sign*.1154);foot.z=foot_h
  hip=(arm.matrix_world@bones[side+'_UpperLeg'].matrix).translation
  solve_leg(side,foot,hip+forward*.5+Vector((0,0,.1)))
 # Temporary arms kept near the sides; broad support gesture while sitting.
 for side,sign in [('Left',1),('Right',-1)]:
  shoulder=(arm.matrix_world@bones[side+'_UpperArm'].matrix).translation
  body_down=turn@tilt@Vector((0,0,-1))
  support=math.sin(min(1,max(0,(t-.7)/3.5))*math.pi)*.10
  elbow=shoulder+body_down*.265+lateral*(sign*.06)+forward*support
  point_bone(side+'_UpperArm',side+'_LowerArm',elbow)
  elbow_now=(arm.matrix_world@bones[side+'_LowerArm'].matrix).translation
  point_bone(side+'_LowerArm',side+'_Hand',elbow_now+body_down*.23+forward*.03)
 for p in bones:
  p.keyframe_insert(data_path='location',frame=frame,group=p.name)
  p.keyframe_insert(data_path='rotation_quaternion',frame=frame,group=p.name)
  p.keyframe_insert(data_path='scale',frame=frame,group=p.name)
# Phase markers are the editable timing guide for the Action.
for name,sec in [('Lie / breathing',0),('Wake / head',.7),('Sit up',1.8),('Turn to edge',2.8),('Feet planted',3.5),('Weight shift',4.0),('Stand',4.85),('Idle handoff',5.25)]:
 scene.timeline_markers.new(name,frame=round(sec*30)+1)
scene.frame_set(1)
# Workbench reference bed in the isolated authoring scene; never exported.
bed=bpy.data.objects.get('WakeUp_ReferenceMattress')
if bed is None:
 bpy.ops.mesh.primitive_cube_add(size=1,location=(0,0,.46))
 bed=bpy.context.object;bed.name='WakeUp_ReferenceMattress'
bed.dimensions=(.95,2.3616,.22)
# Save outside Assets so Unity does not automatically import the .blend.
model_root=bpy.data.objects.get('Armature') or bpy.data.objects['Mannequin(Clone)']
model_root.name='Mannequin(Clone)'
mesh=bpy.data.objects.get('Armature_Mesh') or bpy.data.objects['Mannequin_Mesh']
world=mesh.matrix_world.copy();mesh.parent=bpy.data.objects['Geometry'];mesh.matrix_world=world;mesh.name='Mannequin_Mesh'
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
bpy.ops.object.select_all(action='DESELECT')
for o in [arm,model_root,mesh,bpy.data.objects['Geometry']]:o.select_set(True)
bpy.context.view_layer.objects.active=arm
bpy.ops.export_scene.fbx(filepath=str(fbx_path),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y')
result={'action':action.name,'frames':169,'seconds':5.6,'blend':str(blend_path),'fbx':str(fbx_path)}
