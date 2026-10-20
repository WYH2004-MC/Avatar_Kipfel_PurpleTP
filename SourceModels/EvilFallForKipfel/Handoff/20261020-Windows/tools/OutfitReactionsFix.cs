#if UNITY_EDITOR
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using nadena.dev.modular_avatar.core;

namespace EvilFallForKipfel.Export20261020
{
    public static class OutfitReactionsFix
    {
        static GameObject Find(GameObject root, string name)
        {
            var found=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(x=>x.name==name).ToArray();
            if(found.Length!=1) throw new InvalidOperationException("Expected one renderer: "+name);
            return found[0].gameObject;
        }
        public static void Apply(GameObject root, JObject report)
        {
            var shoe=Find(root,"MainLegHeel");
            var shape=shoe.GetComponents<ModularAvatarShapeChanger>().FirstOrDefault(x=>!x.Inverted);
            if(shape==null) shape=shoe.AddComponent<ModularAvatarShapeChanger>();
            shape.Inverted=false;
            foreach(string key in new[]{"Lower_leg_01_OFF","Lower_leg_02_OFF"})
            {
                var entry=shape.Shapes.FirstOrDefault(x=>x.Object!=null && x.Object.referencePath=="Body_Base" && x.ShapeName==key);
                if(entry==null)
                {
                    entry=new ChangedShape{Object=new AvatarObjectReference{referencePath="Body_Base"},ShapeName=key};
                    shape.Shapes.Add(entry);
                }
                entry.ChangeType=ShapeChangeType.Set;entry.Value=100;
            }
            EditorUtility.SetDirty(shape);
            HideNative(Find(root,"HeadACC"),"Cat_Ear");
            HideNative(Find(root,"EvilFallTail"),"Cat_Tail");
            report["requestedReactions"]=new JObject{
                ["MainLegHeel"] = new JArray(shape.Shapes.Select(x=>new JObject{
                    ["target"]=x.Object.referencePath,["shape"]=x.ShapeName,["mode"]=x.ChangeType.ToString(),["value"]=x.Value})),
                ["HeadACC_on_hides"]="Cat_Ear",["EvilFallTail_on_hides"]="Cat_Tail",
                ["externalTargetsUseAvatarRelativePaths"]=true,["sceneObjectReferencesStored"]=false
            };
        }
        static void HideNative(GameObject owner,string target)
        {
            var c=owner.GetComponents<ModularAvatarObjectToggle>().FirstOrDefault(x=>!x.Inverted && x.Objects.Any(o=>o.Object!=null && o.Object.referencePath==target));
            if(c==null) c=owner.AddComponent<ModularAvatarObjectToggle>();
            c.Inverted=false;
            var list=c.Objects;
            int i=list.FindIndex(x=>x.Object!=null && x.Object.referencePath==target);
            var value=new ToggledObject{Object=new AvatarObjectReference{referencePath=target},Active=false};
            if(i<0) list.Add(value);else list[i]=value;
            EditorUtility.SetDirty(c);
        }
    }
}
#endif
