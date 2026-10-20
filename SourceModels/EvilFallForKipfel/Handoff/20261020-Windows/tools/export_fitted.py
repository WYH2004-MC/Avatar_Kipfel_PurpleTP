import bpy, json, hashlib, numpy as np
from pathlib import Path
W=Path('/Users/wyh2004/Projects/VRChat/Avatar_Kipfel_BlueTP/SourceModels/EvilFallForKipfel/Exports/20261020-Unity')
source=Path(bpy.data.filepath)
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
assert sha(source)=='bf43363dd6c81e47ea5b6aaf4523d6be0f5a383f082264d92dab390f9fea20ef'
names=json.loads((W/'export49_object_names.json').read_text())
if isinstance(names,dict):names=names.get('objects',names.get('names'))
assert len(names)==49
selected=[bpy.data.objects[n] for n in names]
assert sum(o.type=='MESH' for o in selected)==38
assert sum(o.type=='ARMATURE' for o in selected)==9
assert sum(len(o.data.bones) for o in selected if o.type=='ARMATURE')==431
manifest={'sourceBlend':str(source),'sourceSHA256':sha(source),'meshMaterials':{},'defaultActive':{},'blendShapeValues':{},'meshCounts':{},'rigs':{},'materials':{},'objectParents':{}}
arr={};bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get()
for o in selected:
 manifest['objectParents'][o.name]={'parent':o.parent.name if o.parent else None,'type':o.parent_type,'bone':o.parent_bone}
 if o.type=='ARMATURE':manifest['rigs'][o.name]={'bones':len(o.data.bones)}
 if o.type!='MESH':continue
 manifest['meshMaterials'][o.name]=[m.name if m else None for m in o.data.materials]
 manifest['defaultActive'][o.name]=bool(o.visible_get())
 manifest['blendShapeValues'][o.name]={k.name:k.value*100 for k in o.data.shape_keys.key_blocks[1:]} if o.data.shape_keys else {}
 manifest['meshCounts'][o.name]={'vertices':len(o.data.vertices),'polygons':len(o.data.polygons),'shapeKeys':list(manifest['blendShapeValues'][o.name])}
 e=o.evaluated_get(dg);em=e.to_mesh()
 co=np.empty(len(em.vertices)*3,np.float32);em.vertices.foreach_get('co',co);co=co.reshape(-1,3).astype(float);M=np.array(e.matrix_world);arr[o.name+'__default_world']=co@M[:3,:3].T+M[:3,3];e.to_mesh_clear()
 for m in o.data.materials:
  if not m or m.name in manifest['materials']:continue
  textures=[]
  if m.use_nodes:
   textures=[n.image.filepath for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
  manifest['materials'][m.name]={'textures':textures}
np.savez_compressed(W/'export_source_geometry.npz',**arr)
(W/'blender_export_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
for c in bpy.data.collections:c.hide_viewport=False;c.hide_render=False
for layer in bpy.context.view_layer.layer_collection.children:
 layer.exclude=False;layer.hide_viewport=False
for o in bpy.context.view_layer.objects:o.select_set(False)
for o in selected:o.hide_set(False);o.hide_viewport=False;o.select_set(True)
bpy.context.view_layer.objects.active=bpy.data.objects['Armature']
path=W/'EvilFall_Kipfel_Fitted.fbx';assert not path.exists()
rna=bpy.ops.export_scene.fbx.get_rna_type().properties
for prop,value in [('apply_scale_options','FBX_SCALE_UNITS'),('mesh_smooth_type','OFF'),('axis_forward','-Z'),('axis_up','Y')]:assert value in [v.identifier for v in rna[prop].enum_items]
result=bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_space_transform=True,bake_space_transform=False,use_mesh_modifiers=False,mesh_smooth_type='OFF',use_mesh_edges=False,use_tspace=False,use_custom_props=True,add_leaf_bones=False,use_armature_deform_only=False,bake_anim=False,path_mode='AUTO',embed_textures=False,axis_forward='-Z',axis_up='Y')
assert result=={'FINISHED'} and path.stat().st_size>1000000
assert sha(source)==manifest['sourceSHA256']
report={'status':'EXPORTED_FULL_FITTED_49_OBJECTS_SOURCE_UNCHANGED','fbx':str(path),'sha256':sha(path),'bytes':path.stat().st_size,'meshes':38,'armatures':9,'bones':431,'reference_body_hair_excluded':True,'all_mesh_shape_key_names_preserved_for_import_validation':True,'no_animation_baked':True,'Blender_constraints_not_converted_to_Unity_runtime_logic':True,'source':str(source),'source_sha256':sha(source)}
(W/'export_report.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report),flush=True)
