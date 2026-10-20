using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace EvilFallForKipfel.MenuRestore
{
    // Operates only on caller-owned temporary prefab contents. It never saves assets.
    public static class OriginalEffectsRestorer
    {
        public static readonly Dictionary<Component, Component> Components = new Dictionary<Component, Component>();

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("OriginalEffectsRestorer: " + message);
        }

        private static string Path(Transform root, Transform child)
        {
            if (child == root) return "";
            Require(child != null && child.IsChildOf(root), "Object outside expected root");
            var names = new List<string>();
            for (var p = child; p != root; p = p.parent) names.Add(p.name);
            names.Reverse();
            return string.Join("/", names);
        }

        private static int Depth(Transform t)
        {
            int depth = 0;
            for (; t != null; t = t.parent) depth++;
            return depth;
        }

        private static Transform Child(Transform parent, string name)
        {
            var matches = parent.Cast<Transform>().Where(t => t.name == name).ToArray();
            Require(matches.Length <= 1, "Ambiguous child " + parent.name + "/" + name);
            return matches.SingleOrDefault();
        }

        private static string Alias(string name)
        {
            if (name == "Foot.L") return "foot.L";
            if (name == "Foot.R") return "foot.R";
            if (name == "MiddleProximal.L") return "MiddleProxima.L";
            if (name == "MiddleProximal.R") return "MiddleProxima.R";
            if (name == "LegToyBone") return "LegToy";
            return name;
        }

        private static bool IsPhysBone(Component c) { return c.GetType().Name == "VRCPhysBone"; }
        private static bool IsPBBlocker(Component c) { return c.GetType().Name == "ModularAvatarPBBlocker"; }

        public static Dictionary<Transform, Transform> Apply(GameObject source, GameObject target, JObject report)
        {
            Require(source != null && target != null && source != target, "Distinct source and target are required");
            Components.Clear();
            var before = target.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => t.localToWorldMatrix);
            var sourceAll = source.GetComponentsInChildren<Transform>(true);
            var targetOriginal = target.GetComponentsInChildren<Transform>(true);
            var originalRenderers = target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var beforeMeshes = originalRenderers.ToDictionary(r => r, r => r.sharedMesh);
            Require(originalRenderers.Length == 38, "Expected 38 fitted renderers");

            var map = new Dictionary<Transform, Transform>();
            var created = new JArray();
            var reparented = new JArray();
            var copiedComponents = new List<Component>();
            var mergeArmatureType = target.GetComponentsInChildren<Component>(true)
                .Where(c => c != null && c.GetType().Name == "ModularAvatarMergeArmature").Select(c => c.GetType()).FirstOrDefault();
            Require(mergeArmatureType != null, "Existing MA Merge Armature type missing");
            int restoredTubeMergeRoots = 0;
            var sourceArmature = Child(source.transform, "Armature");
            var fit = Child(target.transform, "armature");
            var sourceRig = Child(target.transform, "EvilFall_SourceRig");
            Require(sourceArmature != null && fit != null && sourceRig != null, "Required original/public/source armatures missing");
            var chest = fit.Find("Hips/Spine/Chest");
            Require(chest != null, "Public Chest missing");
            Action<Transform, Transform> bind = (a, b) =>
            {
                Require(a != null && b != null, "Null transform mapping");
                if (map.ContainsKey(a)) Require(map[a] == b, "Conflicting mapping for " + Path(source.transform, a));
                map[a] = b;
                Components[a] = b;
            };
            bind(source.transform, target.transform);
            bind(sourceArmature, fit);

            // Imported fitting separates the weapon and plug armatures, and flattens mesh objects.
            var sourceWeapon = Child(source.transform, "EvilFallBlade");
            var fittedWeapon = fit.GetComponentsInChildren<Transform>(true).Where(t => t.name == "EvilFallBlade").ToArray();
            Require(sourceWeapon != null && fittedWeapon.Length == 1, "Unique fitted weapon root missing");
            bind(sourceWeapon, fittedWeapon[0]);
            var originalPlugGroup = Child(source.transform, "BreastPlugMain");
            var fittedPlugGroup = Child(fit, "BreastPlugMain");
            Require(originalPlugGroup != null && fittedPlugGroup != null, "BreastPlugMain group missing");
            bind(originalPlugGroup, fittedPlugGroup);
            var originalPlugBase = Child(originalPlugGroup, "Base");
            var fittedPlugBase = Child(chest, "Base");
            Require(originalPlugBase != null && fittedPlugBase != null, "Fitted plug Base missing");
            bind(originalPlugBase, fittedPlugBase);

            foreach (var smr in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string name = Path(source.transform, smr.transform) == "EvilFallMaskV2/FaceMask" ? "FaceMaskV2" : smr.name;
                var matches = originalRenderers.Where(r => r.name == name).ToArray();
                Require(matches.Length == 1, "Expected unique fitted renderer " + name);
                Require(smr.sharedMesh != null && matches[0].sharedMesh != null, "Renderer mesh missing " + name);
                bind(smr.transform, matches[0].transform);
                Components[smr] = matches[0];
            }
            Require(source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 38, "Expected 38 original renderers");

            // The eight chest bones were present only in the internal source rig. Create the
            // public counterparts at the fitted world transforms, permitting MA's normal zip.
            foreach (string side in new[] { "L", "R" })
            {
                var rel = "Hips/Spine/Chest/Breast_" + side + "_Root";
                var originalBreast = sourceArmature.Find(rel);
                var fittedBreastSource = sourceRig.Find(rel);
                Require(originalBreast != null && fittedBreastSource != null, "Breast source chain missing " + side);
                // Use explicit names rather than child-order assumptions for actual mapping.
                var boneNames = new[] { "Breast_" + side + "_Root", "Breast_" + side + ".001", "Breast_" + side + ".002", "Breast_" + side + ".002_end" };
                Transform parent = chest;
                foreach (string boneName in boneNames)
                {
                    var a = originalBreast.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == boneName);
                    var current = fittedBreastSource.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == boneName);
                    Require(a != null && current != null, "Exact breast bone missing " + boneName);
                    var dest = Child(parent, boneName);
                    if (dest == null)
                    {
                        dest = new GameObject(boneName).transform;
                        dest.SetParent(current.parent, false);
                        dest.localPosition = current.localPosition;
                        dest.localRotation = current.localRotation;
                        dest.localScale = current.localScale;
                        dest.SetParent(parent, true);
                        dest.gameObject.SetActive(a.gameObject.activeSelf);
                        created.Add(Path(target.transform, dest));
                    }
                    bind(a, dest);
                    if (!boneName.EndsWith("_end", StringComparison.Ordinal)) parent = dest;
                }
            }

            Func<Transform, Transform> ensure = null;
            ensure = s =>
            {
                Transform found;
                if (map.TryGetValue(s, out found)) return found;
                var parent = ensure(s.parent);
                var name = Alias(s.name);
                found = Child(parent, name);
                if (found == null && s.IsChildOf(sourceArmature))
                {
                    // Known duplicated plug names distinguish the public chest control bones
                    // from the independent Base mesh bones mapped explicitly above.
                    if (s.name == "PlugBase.L.001" || s.name == "PlugBase.R.001") found = Child(chest, s.name);
                    if (s.name == "PlugBase.L.002" || s.name == "PlugBase.R.002")
                    {
                        string first = s.name.Substring(0, s.name.Length - 3) + "001";
                        var p = Child(chest, first);
                        if (p != null) found = Child(p, s.name);
                    }
                    if (found == null)
                    {
                        var candidates = targetOriginal
                            .Where(t => t.IsChildOf(fit) && t.name == name && t.GetComponent<SkinnedMeshRenderer>() == null).ToArray();
                        if (candidates.Length == 1) found = candidates[0];
                        else Require(candidates.Length == 0 || name == "PlugBase.PB" || name == "PlugBase.PBEnd", "Ambiguous fitted bone " + Path(source.transform, s));
                    }
                }
                if (found == null)
                {
                    found = new GameObject(s.name).transform;
                    found.SetParent(parent, false);
                    found.localPosition = s.localPosition;
                    found.localRotation = s.localRotation;
                    found.localScale = s.localScale;
                    found.gameObject.layer = s.gameObject.layer;
                    found.gameObject.SetActive(s.gameObject.activeSelf);
                    created.Add(Path(target.transform, found));
                }
                bind(s, found);
                return found;
            };

            // Instantiate complete particle subtrees, retaining renderer material and module
            // serialization. Cross-subtree references are remapped after all targets exist.
            var liquids = sourceAll.Where(t => t.name == "Liquid" && t.GetComponentsInChildren<ParticleSystem>(true).Length == 5).ToArray();
            Require(liquids.Length == 4, "Expected four five-system Liquid groups");
            foreach (var liquid in liquids)
            {
                var parent = ensure(liquid.parent);
                Require(Child(parent, liquid.name) == null, "Liquid already exists; refusing duplicate effects");
                var clone = UnityEngine.Object.Instantiate(liquid.gameObject);
                clone.name = liquid.name;
                clone.transform.SetParent(parent, false);
                clone.transform.localPosition = liquid.localPosition;
                clone.transform.localRotation = liquid.localRotation;
                clone.transform.localScale = liquid.localScale;
                foreach (var a in liquid.GetComponentsInChildren<Transform>(true))
                {
                    string relative = Path(liquid, a);
                    var dest = relative.Length == 0 ? clone.transform : clone.transform.Find(relative);
                    bind(a, dest);
                    var ac = a.GetComponents<Component>();
                    var bc = dest.GetComponents<Component>();
                    Require(ac.Length == bc.Length, "Particle clone component count mismatch");
                    for (int i = 0; i < ac.Length; i++)
                    {
                        Require(ac[i] != null && bc[i] != null && ac[i].GetType() == bc[i].GetType(), "Missing/mismatched particle component");
                        Components[ac[i]] = bc[i];
                        if (!(bc[i] is Transform)) copiedComponents.Add(bc[i]);
                    }
                }
                created.Add(Path(target.transform, clone.transform));
            }

            foreach (var s in sourceAll.OrderBy(Depth)) ensure(s);

            // Restore the active-object group semantics lost when the FBX mesh nodes flattened.
            foreach (var smr in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var dest = map[smr.transform];
                var expectedParent = map[smr.transform.parent];
                if (dest.parent == expectedParent) continue;
                dest.SetParent(expectedParent, true);
                reparented.Add(Path(target.transform, dest));
            }
            foreach (string side in new[] { "L", "R" })
            {
                var sourcePlug = sourceAll.Single(t => t.name == "PlugBase." + side + ".001" && t.parent.name == "BreastToy." + side);
                var dest = map[sourcePlug];
                var expectedParent = map[sourcePlug.parent];
                if (dest.parent != expectedParent)
                {
                    dest.SetParent(expectedParent, true);
                    reparented.Add(Path(target.transform, dest));
                }
                // The original tube bones begin below the PB helper chain. Existing fitted
                // bones are kept in place in world space while restoring that dependency.
                var tube = sourceAll.Single(t => t.name == "EvilFallChestTube." + side + ".002");
                var fittedTube = map[tube];
                var tubeParent = map[tube.parent];
                if (fittedTube.parent != tubeParent)
                {
                    fittedTube.SetParent(tubeParent, true);
                    reparented.Add(Path(target.transform, fittedTube));
                }
                // The source rig's tube chain has its previous hierarchy. Retain its exact
                // merge authority after the corresponding public bone moves under the PB.
                var oldTube = sourceRig.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == tube.name);
                Require(oldTube != null, "Internal tube counterpart missing " + tube.name);
                var merge = oldTube.GetComponent(mergeArmatureType);
                if (merge == null)
                {
                    merge = oldTube.gameObject.AddComponent(mergeArmatureType);
                    var mergeSO = new SerializedObject(merge);
                    var mergeRef = mergeSO.FindProperty("mergeTarget");
                    mergeRef.FindPropertyRelative("targetObject").objectReferenceValue = fittedTube.gameObject;
                    mergeRef.FindPropertyRelative("referencePath").stringValue = Path(target.transform, fittedTube);
                    mergeSO.FindProperty("LockMode").intValue = 1;
                    mergeSO.FindProperty("mangleNames").boolValue = false;
                    mergeSO.FindProperty("prefix").stringValue = "";
                    mergeSO.FindProperty("suffix").stringValue = "";
                    mergeSO.ApplyModifiedPropertiesWithoutUndo();
                    restoredTubeMergeRoots++;
                }
                else
                {
                    var mergeSO = new SerializedObject(merge);
                    Require(mergeSO.FindProperty("mergeTarget").FindPropertyRelative("targetObject").objectReferenceValue == fittedTube.gameObject,
                        "Existing tube merge authority differs " + tube.name);
                }
            }

            var originalPBs = source.GetComponentsInChildren<Component>(true).Where(IsPhysBone).ToArray();
            var existingPBs = target.GetComponentsInChildren<Component>(true).Where(IsPhysBone).ToArray();
            Require(originalPBs.Length == 19 && existingPBs.Length == 16, "Expected original 19 / existing fitted 16 PhysBones");
            int addedPB = 0, addedBlocker = 0;
            foreach (var src in originalPBs)
            {
                var candidates = existingPBs.Where(c => c.name == src.name).ToArray();
                Require(candidates.Length <= 1, "Ambiguous existing PhysBone owner " + src.name);
                if (candidates.Length == 1) Components[src] = candidates[0];
                else
                {
                    var dst = map[src.transform].gameObject.AddComponent(src.GetType());
                    EditorUtility.CopySerialized(src, dst);
                    Components[src] = dst;
                    copiedComponents.Add(dst);
                    addedPB++;
                }
            }
            foreach (var src in source.GetComponentsInChildren<Component>(true).Where(IsPBBlocker))
            {
                var dest = map[src.transform].gameObject;
                Require(dest.GetComponent(src.GetType()) == null, "PBBlocker already exists");
                var dst = dest.AddComponent(src.GetType());
                EditorUtility.CopySerialized(src, dst);
                Components[src] = dst;
                copiedComponents.Add(dst);
                addedBlocker++;
            }
            Require(addedPB == 3 && addedBlocker == 2, "Expected three restored PhysBones and two PBBlockers");

            foreach (var component in copiedComponents)
            {
                var so = new SerializedObject(component);
                var iterator = so.GetIterator();
                while (iterator.Next(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var obj = iterator.objectReferenceValue;
                    var go = obj as GameObject;
                    var sc = obj as Component;
                    if (go == null && sc != null) go = sc.gameObject;
                    if (go == null || (go != source && !go.transform.IsChildOf(source.transform))) continue;
                    if (obj is GameObject) iterator.objectReferenceValue = map[go.transform].gameObject;
                    else
                    {
                        Component mapped;
                        Require(sc != null && Components.TryGetValue(sc, out mapped), "Unmapped source component reference " + component.name + "." + iterator.propertyPath);
                        iterator.objectReferenceValue = Components[sc];
                    }
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            int refreshedMergePaths = 0;
            foreach (var component in target.GetComponentsInChildren<Component>(true).Where(c => c != null && c.GetType().Name == "ModularAvatarMergeArmature"))
            {
                var so = new SerializedObject(component);
                var targetRef = so.FindProperty("mergeTarget");
                var direct = targetRef.FindPropertyRelative("targetObject").objectReferenceValue as GameObject;
                if (direct == null) continue;
                Require(direct == target || direct.transform.IsChildOf(target.transform), "MA direct target outside fitted prefab");
                string path = Path(target.transform, direct.transform);
                Require(path.Length > 0, "Internal MA root target is ambiguous");
                targetRef.FindPropertyRelative("referencePath").stringValue = path;
                so.ApplyModifiedPropertiesWithoutUndo();
                refreshedMergePaths++;
            }

            float matrixError = 0;
            foreach (var pair in before)
            {
                Require(pair.Key != null, "Existing transform removed");
                var now = pair.Key.localToWorldMatrix;
                for (int i = 0; i < 16; i++) matrixError = Mathf.Max(matrixError, Mathf.Abs(pair.Value[i] - now[i]));
            }
            Require(matrixError <= 0.000002f, "Existing transform world matrix drift " + matrixError);
            Require(beforeMeshes.All(p => p.Key.sharedMesh == p.Value), "Existing fitted mesh replaced");
            Require(target.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 38, "Renderer count changed");
            Require(target.GetComponentsInChildren<ParticleSystem>(true).Length == 20, "Expected 20 restored particle systems");
            Require(map.Count == sourceAll.Length, "Incomplete original Transform map");

            // Every copied component must be fully independent of the disposable source instance.
            foreach (var component in copiedComponents)
            {
                var so = new SerializedObject(component);
                var iterator = so.GetIterator();
                while (iterator.Next(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var obj = iterator.objectReferenceValue;
                    var go = obj as GameObject;
                    if (go == null && obj is Component) go = ((Component)obj).gameObject;
                    Require(go == null || (go != source && !go.transform.IsChildOf(source.transform)), "Source instance reference survived remap");
                }
            }
            report["originalEffects"] = new JObject
            {
                ["mappedTransforms"] = map.Count,
                ["mappedComponents"] = Components.Count,
                ["restoredParticleSystems"] = 20,
                ["restoredPhysBones"] = addedPB,
                ["preservedPhysBones"] = 16,
                ["restoredPBBlockers"] = addedBlocker,
                ["worldMatrixMaxError"] = matrixError,
                ["refreshedMergeTargetPaths"] = refreshedMergePaths,
                ["restoredTubeMergeRoots"] = restoredTubeMergeRoots,
                ["createdNodes"] = created,
                ["keepWorldReparents"] = reparented
            };
            return map;
        }
    }
}
