#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.modular_avatar.core;
using Object = UnityEngine.Object;

namespace EvilFallForKipfel.MenuRestore
{
    [InitializeOnLoad]
    public static class OriginalMenuRestorer
    {
        const string Work = "SourceModels/EvilFallForKipfel/Exports/20261020-Unity/MenuRestore";
        const string Outfit = "Assets/YaokiSubWork/EvilFallFullSet/ForKipfel";
        const string TargetPrefab = Outfit + "/EvilFall_Kipfel.prefab";
        const string SourcePrefab = "Assets/YaokiSubWork/EvilFallFullSet/ForLime&Chiffin/prefabe/Lime_EvilFall S breast.prefab";
        const string SourceController = "Assets/YaokiSubWork/EvilFallFullSet/FX&Animation/EvilFallFullSetAnImation Lime.controller";
        static string Base => Directory.GetParent(Application.dataPath).FullName;
        static string Full(string path) => Path.IsPathRooted(path) ? path : Path.Combine(Base, path);
        static bool busy;
        static double next;
        static OriginalMenuRestorer() { EditorApplication.update += Tick; }
        static void Require(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); }
        static string Hash(byte[] bytes) { using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        static string FileHash(string p) => Hash(File.ReadAllBytes(Full(p)));
        static string PathOf(Transform t, Transform root) => t == root ? "" : AnimationUtility.CalculateTransformPath(t, root);
        static void Write(JObject report) => File.WriteAllText(Full(Work + "/restore.report.json"), report.ToString());
        static void Step(JObject report, string name) { report["step"] = name; Write(report); }
        static string Rename(string name) => name != null && name.StartsWith("EvilFall/", StringComparison.Ordinal) ? "EvilFallKipfel/" + name.Substring("EvilFall/".Length) : name;

        static void Tick()
        {
            if (busy || EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 1;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string requestPath = Full(Work + "/restore.request.json");
            if (!File.Exists(requestPath)) return;
            string request = File.ReadAllText(requestPath);
            string key = "EvilFall.OriginalMenuRestore." + Hash(Encoding.UTF8.GetBytes(request));
            if (SessionState.GetBool(key, false)) return;
            SessionState.SetBool(key, true); busy = true;
            try { Run(JObject.Parse(request)); }
            catch (Exception e) { Write(new JObject { ["status"] = "FAILED_BEFORE_RUN", ["exception"] = e.ToString() }); Debug.LogException(e); }
            finally { busy = false; }
        }

        sealed class MeshBefore
        {
            public SkinnedMeshRenderer renderer;
            public Mesh mesh;
            public Transform[] bones;
            public float[] shapes;
            public Vector3[] vertices;
        }
        static List<MeshBefore> CaptureMeshes(GameObject root)
        {
            return root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r =>
            {
                var baked = new Mesh(); r.BakeMesh(baked);
                var data = new MeshBefore { renderer = r, mesh = r.sharedMesh, bones = r.bones,
                    shapes = Enumerable.Range(0, r.sharedMesh.blendShapeCount).Select(r.GetBlendShapeWeight).ToArray(),
                    vertices = baked.vertices.Select(r.transform.TransformPoint).ToArray() };
                Object.DestroyImmediate(baked); return data;
            }).ToList();
        }
        static JObject CheckMeshes(List<MeshBefore> before)
        {
            float max = 0;
            foreach (var b in before)
            {
                var r = b.renderer;
                Require(r.sharedMesh == b.mesh && r.bones.SequenceEqual(b.bones), "Mesh/bone identity changed on " + r.name);
                Require(b.shapes.SequenceEqual(Enumerable.Range(0, b.mesh.blendShapeCount).Select(r.GetBlendShapeWeight)), "Blendshape defaults changed on " + r.name);
                var mesh = new Mesh(); r.BakeMesh(mesh); var vertices = mesh.vertices;
                Require(vertices.Length == b.vertices.Length, "Vertex count changed");
                for (int i = 0; i < vertices.Length; i++) max = Mathf.Max(max, Vector3.Distance(b.vertices[i], r.transform.TransformPoint(vertices[i])));
                Object.DestroyImmediate(mesh);
            }
            Require(max < 0.000003f, "Accepted neutral geometry changed: " + max);
            return new JObject { ["meshes"] = before.Count, ["boneReferencesPreserved"] = true, ["blendshapeDefaultsPreserved"] = true, ["neutralMaxWorldErrorMetres"] = max };
        }

        static string Ref(Object o, Transform root)
        {
            if (o == null) return "null";
            if (o is GameObject go && (go.transform == root || go.transform.IsChildOf(root))) return "go:" + PathOf(go.transform, root);
            if (o is Component c && (c.transform == root || c.transform.IsChildOf(root))) return "component:" + PathOf(c.transform, root) + ":" + c.GetType().FullName + ":" + Array.IndexOf(c.GetComponents(c.GetType()), c);
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string guid, out long id) && !string.IsNullOrEmpty(guid)) return guid + ":" + id;
            return "external:" + o.GetType().FullName + ":" + o.name;
        }
        static JObject Snapshot(GameObject root)
        {
            var result = new JObject();
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                Require(c != null, "Missing component script");
                var row = new JObject(); var so = new SerializedObject(c); var p = so.GetIterator(); bool enter = true;
                while (p.Next(enter))
                {
                    enter = p.propertyType != SerializedPropertyType.ObjectReference;
                    switch (p.propertyType)
                    {
                        case SerializedPropertyType.ObjectReference: row[p.propertyPath] = Ref(p.objectReferenceValue, root.transform); break;
                        case SerializedPropertyType.Integer: case SerializedPropertyType.Enum: case SerializedPropertyType.LayerMask: case SerializedPropertyType.Character: row[p.propertyPath] = p.longValue; break;
                        case SerializedPropertyType.ArraySize: row[p.propertyPath] = p.intValue; break;
                        case SerializedPropertyType.Boolean: row[p.propertyPath] = p.boolValue; break;
                        case SerializedPropertyType.Float: row[p.propertyPath] = p.doubleValue; break;
                        case SerializedPropertyType.String: row[p.propertyPath] = p.stringValue; break;
                        case SerializedPropertyType.Vector2: row[p.propertyPath] = p.vector2Value.ToString("R"); break;
                        case SerializedPropertyType.Vector3: row[p.propertyPath] = p.vector3Value.ToString("R"); break;
                        case SerializedPropertyType.Vector4: row[p.propertyPath] = p.vector4Value.ToString("R"); break;
                        case SerializedPropertyType.Quaternion: row[p.propertyPath] = p.quaternionValue.ToString("R"); break;
                        case SerializedPropertyType.Color: row[p.propertyPath] = p.colorValue.ToString("R"); break;
                        case SerializedPropertyType.Rect: row[p.propertyPath] = p.rectValue.ToString("R"); break;
                        case SerializedPropertyType.Bounds: row[p.propertyPath] = p.boundsValue.ToString("R"); break;
                        case SerializedPropertyType.AnimationCurve:
                            var curve = p.animationCurveValue;
                            row[p.propertyPath] = new JObject { ["preWrap"] = (int)curve.preWrapMode, ["postWrap"] = (int)curve.postWrapMode,
                                ["keys"] = new JArray(curve.keys.Select(k => new JObject { ["time"] = k.time, ["value"] = k.value,
                                    ["inTangent"] = k.inTangent, ["outTangent"] = k.outTangent, ["inWeight"] = k.inWeight,
                                    ["outWeight"] = k.outWeight, ["weightedMode"] = (int)k.weightedMode })) };
                            break;
                    }
                }
                result[PathOf(c.transform, root.transform) + "|" + c.GetType().FullName + "|" + Array.IndexOf(c.GetComponents(c.GetType()), c)] = row;
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                result[PathOf(t, root.transform) + "|GameObject"] = new JObject { ["activeSelf"] = t.gameObject.activeSelf, ["layer"] = t.gameObject.layer, ["tag"] = t.tag };
            return result;
        }

        static void RestoreMenus(JObject spec, JObject report)
        {
            var menus = new Dictionary<string, VRCExpressionsMenu>();
            foreach (JObject item in (JArray)spec["menus"])
            {
                string sourcePath = (string)item["source_asset"], targetPath = (string)item["target_asset"];
                var src = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(sourcePath); Require(src != null, "Source menu missing " + sourcePath);
                var dst = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(targetPath);
                if (dst == null) { dst = Object.Instantiate(src); AssetDatabase.CreateAsset(dst, targetPath); }
                else EditorUtility.CopySerialized(src, dst);
                dst.name = Path.GetFileNameWithoutExtension(targetPath); menus[sourcePath] = dst;
            }
            int leaves = 0;
            foreach (JObject item in (JArray)spec["menus"])
            {
                var dst = menus[(string)item["source_asset"]];
                foreach (var control in dst.controls)
                {
                    if (control.parameter != null) control.parameter.name = Rename(control.parameter.name);
                    if (control.subParameters != null) foreach (var p in control.subParameters) p.name = Rename(p.name);
                    if (control.subMenu != null)
                    {
                        string old = AssetDatabase.GetAssetPath(control.subMenu);
                        if (menus.ContainsKey(old)) control.subMenu = menus[old];
                        else Require(menus.Values.Contains(control.subMenu), "Unknown source submenu " + old);
                    }
                    if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                        Require(control.subMenu != null, "Submenu control has no target");
                    else { Require(control.icon != null, "Original menu leaf icon was lost"); leaves++; }
                }
                Require(dst.controls.Count <= 8, "VRChat menu exceeds eight controls"); EditorUtility.SetDirty(dst);
            }
            Require(leaves == 32 && menus.Count == 7, "Original full menu coverage mismatch");
            report["menus"] = new JObject { ["assets"] = 7, ["leafControls"] = leaves, ["originalOrderNamesTypesIconsRestored"] = true,
                ["paths"] = new JArray(menus.Values.Select(AssetDatabase.GetAssetPath)) };
        }

        static void CheckReferences(GameObject root, GameObject source)
        {
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                Require(c != null, "Missing script after effects restoration");
                var so = new SerializedObject(c); var p = so.GetIterator(); bool enter = true;
                while (p.Next(enter))
                {
                    enter = p.propertyType != SerializedPropertyType.ObjectReference;
                    if (p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue == null) continue;
                    var obj = p.objectReferenceValue;
                    Transform t = obj is GameObject go ? go.transform : obj is Component cp ? cp.transform : null;
                    Require(t == null || !(t == source.transform || t.IsChildOf(source.transform)), "Reference leaked to original loaded prefab " + c.name + "/" + p.propertyPath);
                }
            }
        }

        static IEnumerable<AnimatorState> States(AnimatorStateMachine sm)
        {
            foreach (var s in sm.states) yield return s.state;
            foreach (var child in sm.stateMachines) foreach (var s in States(child.stateMachine)) yield return s;
        }
        static JObject ValidateMenuAndController(GameObject root, AnimatorController controller)
        {
            Require(controller.layers.Length == 26, "Full original controller must have 26 layers");
            Require(controller.parameters.Length == 32 && controller.parameters.All(p => p.type == AnimatorControllerParameterType.Bool && p.name.StartsWith("EvilFallKipfel/", StringComparison.Ordinal)), "Original 32 boolean parameters not restored");
            var parameters = root.GetComponent<ModularAvatarParameters>().parameters;
            Require(parameters.Count == 32 && parameters.All(p => p.syncType == ParameterSyncType.Bool), "MA parameter coverage differs");
            Require(parameters.Select(p => p.nameOrPrefix).OrderBy(x => x).SequenceEqual(controller.parameters.Select(p => p.name).OrderBy(x => x)), "Menu parameter namespace mismatch");
            var states = controller.layers.SelectMany(l => States(l.stateMachine)).ToArray();
            int drivers = states.SelectMany(s => s.behaviours).Count(b => b != null && b.GetType().Name.Contains("ParameterDriver"));
            Require(drivers == 12, "Original mutual-exclusion parameter drivers not restored");
            return new JObject { ["parameters"] = 32, ["allBoolean"] = true, ["layers"] = 26, ["states"] = states.Length, ["mutualExclusionDrivers"] = drivers };
        }

        static void Run(JObject request)
        {
            Require((bool?)request["apply"] == true, "Explicit apply request required");
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null) { Require(!stage.scene.isDirty, "Current prefab stage has unsaved user edits"); StageUtility.GoToMainStage(); }
            var report = new JObject { ["status"] = "RUNNING", ["request"] = request.DeepClone(), ["version"] = "original-menu-restore-3",
                ["agentPlayTestRun"] = false, ["agentFullAvatarBuildRun"] = false };
            var dirty = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToDictionary(s => s.handle, s => s.isDirty);
            var assets = JObject.Parse(File.ReadAllText(Full(Work + "/before_assets.json")));
            string prefabGUID = AssetDatabase.AssetPathToGUID(TargetPrefab), prefabMeta = FileHash(TargetPrefab + ".meta");
            GameObject source = null, target = null, reload = null; bool saved = false;
            try
            {
                Step(report, "load-original-and-fitted-prefabs");
                source = PrefabUtility.LoadPrefabContents(SourcePrefab); target = PrefabUtility.LoadPrefabContents(TargetPrefab);
                var beforePaths = target.GetComponentsInChildren<Transform>(true).ToDictionary(t => PathOf(t, target.transform), t => t);
                var beforeWorld = beforePaths.Values.ToDictionary(t => t, t => t.localToWorldMatrix);
                var beforeMeshes = CaptureMeshes(target); Require(beforeMeshes.Count == 38, "Unexpected fitted mesh count");
                Step(report, "restore-functional-hierarchy-and-effects");
                var map = OriginalEffectsRestorer.Apply(source, target, report);
                Step(report, "restore-original-animation-controller");
                File.Copy(Full(SourceController), Full(Outfit + "/Accessories.controller"), true);
                AssetDatabase.ImportAsset(Outfit + "/Accessories.controller", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var controller = OriginalAnimationsRestorer.Apply(source, target, map, OriginalEffectsRestorer.Components, beforePaths, report);
                Step(report, "restore-menu-assets-and-parameters");
                var spec = JObject.Parse(File.ReadAllText(Full(Work + "/menus_spec.json"))); RestoreMenus(spec, report);
                var sourceParams = source.GetComponent<ModularAvatarParameters>(); Require(sourceParams != null && sourceParams.parameters.Count == 32, "Original MA defaults missing");
                var targetParams = target.GetComponent<ModularAvatarParameters>() ?? target.AddComponent<ModularAvatarParameters>();
                targetParams.parameters = sourceParams.parameters.Select(p => { p.nameOrPrefix = Rename(p.nameOrPrefix); p.remapTo = Rename(p.remapTo); return p; }).ToList();
                var mergers = target.GetComponents<ModularAvatarMergeAnimator>();
                var merger = mergers.FirstOrDefault(m => AssetDatabase.GetAssetPath(m.animator) == Outfit + "/Accessories.controller") ?? target.AddComponent<ModularAvatarMergeAnimator>();
                foreach (var other in mergers.Where(m => m != merger)) Object.DestroyImmediate(other);
                merger.animator = controller; merger.layerType = VRCAvatarDescriptor.AnimLayerType.FX; merger.pathMode = MergeAnimatorPathMode.Relative;
                merger.relativePathRoot = new AvatarObjectReference(); merger.deleteAttachedAnimator = true; merger.matchAvatarWriteDefaults = true;
                merger.mergeAnimatorMode = MergeAnimatorMode.Append; merger.layerPriority = 0;
                var installer = target.GetComponent<ModularAvatarMenuInstaller>() ?? target.AddComponent<ModularAvatarMenuInstaller>();
                installer.menuToAppend = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(Outfit + "/InstallMenu.asset");
                report["controller"] = ValidateMenuAndController(target, controller);
                CheckReferences(target, source);
                float matrixMax = 0;
                foreach (var p in beforeWorld) for (int i = 0; i < 16; i++) matrixMax = Mathf.Max(matrixMax, Mathf.Abs(p.Key.localToWorldMatrix[i] - p.Value[i]));
                Require(matrixMax < 0.000003f, "Previously accepted transform moved: " + matrixMax);
                report["existingTransformMaxWorldComponentError"] = matrixMax;
                report["geometry"] = CheckMeshes(beforeMeshes);
                Step(report, "save-and-reload-fitted-prefab");
                EditorUtility.SetDirty(targetParams); EditorUtility.SetDirty(merger); EditorUtility.SetDirty(installer);
                foreach (JObject menu in (JArray)spec["menus"])
                    AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>((string)menu["target_asset"]));
                AssetDatabase.SaveAssetIfDirty(controller);
                var expected = Snapshot(target); bool success;
                PrefabUtility.SaveAsPrefabAsset(target, TargetPrefab, out success); Require(success, "Prefab save failed"); saved = true;
                PrefabUtility.UnloadPrefabContents(target); target = null;
                AssetDatabase.ImportAsset(TargetPrefab, ImportAssetOptions.ForceSynchronousImport);
                reload = PrefabUtility.LoadPrefabContents(TargetPrefab);
                var actual = Snapshot(reload);
                if (!JToken.DeepEquals(expected, actual))
                {
                    File.WriteAllText(Full(Work + "/expected_saved_snapshot.json"), expected.ToString());
                    File.WriteAllText(Full(Work + "/actual_saved_snapshot.json"), actual.ToString());
                    throw new InvalidOperationException("Saved reload differs; diagnostic snapshots recorded");
                }
                Require(AssetDatabase.AssetPathToGUID(TargetPrefab) == prefabGUID && FileHash(TargetPrefab + ".meta") == prefabMeta, "Prefab GUID/meta changed");
                foreach (JObject asset in (JArray)assets["targets"])
                    if ((bool)asset["exists"] && ((string)asset["path"]).EndsWith(".meta", StringComparison.Ordinal))
                        Require(FileHash((string)asset["path"]) == (string)asset["sha256"], "Existing target meta changed " + (string)asset["path"]);
                foreach (var p in ((JObject)assets["protected"]).Properties()) Require(FileHash(p.Name) == (string)p.Value, "Protected original/FBX changed " + p.Name);
                report["savedPrefab"] = TargetPrefab; report["savedPrefabGUID"] = prefabGUID; report["savedPrefabSHA256"] = FileHash(TargetPrefab);
                report["serializedReloadExact"] = true; report["protectedSourceAssetsAndFBXUnchanged"] = true;
                report["status"] = "PASS";
            }
            catch (Exception e) { report["status"] = saved ? "FAILED_AFTER_PREFAB_SAVE" : "FAILED_BEFORE_PREFAB_SAVE"; report["exception"] = e.ToString(); Debug.LogException(e); }
            finally
            {
                if (source != null) PrefabUtility.UnloadPrefabContents(source);
                if (target != null) PrefabUtility.UnloadPrefabContents(target);
                if (reload != null) PrefabUtility.UnloadPrefabContents(reload);
                bool unchanged = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).All(s => !dirty.ContainsKey(s.handle) || dirty[s.handle] == s.isDirty);
                report["userSceneDirtyStatesUnchanged"] = unchanged;
                if (!unchanged) report["status"] = "FAILED_USER_SCENE_STATE_CHANGED";
                Write(report);
            }
            if ((string)report["status"] == "PASS")
            {
                Debug.Log("EvilFall original menu/features restored and saved; see " + Full(Work + "/restore.report.json"));
                PrefabStageUtility.OpenPrefab(TargetPrefab);
            }
        }
    }
}
#endif
