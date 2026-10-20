// Offline-authored one-shot Editor utility. Root copies this file to Assets/Editor.
// It runs only for a new RigBindingFix.request.json in the non-Assets export folder.
// Request: {"requestId":"rig-fix-1","allowOverwrite":true,"openPrefab":true}
// Does not modify the FBX, mesh assets, .mat assets, animations, or user scenes.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EvilFallForKipfel.Export20261020
{
    [InitializeOnLoad]
    public static class RigBindingFix
    {
        const string Folder = "SourceModels/EvilFallForKipfel/Exports/20261020-Unity";
        const string PrefabPath = "Assets/YaokiSubWork/EvilFallFullSet/ForKipfel/EvilFall_Kipfel.prefab";
        const string FbxPath = "Assets/YaokiSubWork/EvilFallFullSet/ForKipfel/EvilFall_Kipfel.fbx";
        const string MATypeName = "nadena.dev.modular_avatar.core.ModularAvatarMergeArmature";
        static string Project => Directory.GetParent(Application.dataPath).FullName;
        static string Full(string p) => Path.IsPathRooted(p) ? p : Path.Combine(Project, p);
        static string RequestPath => Full(Folder + "/RigBindingFix.request.json");
        static string ReportPath => Full(Folder + "/RigBindingFix.report.json");
        static double nextCheck;
        static bool busy;
        static Type maType;
        static RigBindingFix() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if (busy || EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 1;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath)) return;
            string text = File.ReadAllText(RequestPath);
            string key = "EvilFall20261020.RigBindingFix." + Hash(Encoding.UTF8.GetBytes(text));
            if (SessionState.GetBool(key, false)) return;
            SessionState.SetBool(key, true);
            busy = true;
            try { Run(JObject.Parse(text)); }
            catch (Exception e)
            {
                Write(new JObject { ["status"] = "FAILED_BEFORE_RUN", ["exception"] = e.ToString() });
                Debug.LogException(e);
            }
            finally { busy = false; }
        }
        static string Hash(byte[] b) { using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(b)).Replace("-", "").ToLowerInvariant(); }
        static string FileHash(string p) => Hash(File.ReadAllBytes(Full(p)));
        static string Str(JObject o, string k, string v) => (string)o[k] ?? v;
        static bool Flag(JObject o, string k, bool v) => o[k] == null ? v : (bool)o[k];
        static void Require(bool v, string reason) { if (!v) throw new InvalidOperationException(reason); }
        static void Write(JObject r) { Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)); File.WriteAllText(ReportPath, r.ToString()); }
        static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name, false)).FirstOrDefault(t => t != null);
        static string PathOf(Transform t, Transform root) => t == root ? "" : AnimationUtility.CalculateTransformPath(t, root);
        static int Depth(Transform t) { int d = 0; while (t.parent != null) { d++; t = t.parent; } return d; }
        static Transform[] All(Transform t) => t.GetComponentsInChildren<Transform>(true);
        static Transform Unique(Transform root, string name) { var a = All(root).Where(t => t.name == name).ToArray(); Require(a.Length == 1, "Expected one transform " + name + ", found " + a.Length); return a[0]; }
        static Transform Direct(Transform root, params string[] names)
        {
            var a = root.Cast<Transform>().Where(t => names.Contains(t.name)).ToArray();
            Require(a.Length == 1, "Expected direct rig root " + string.Join(" / ", names) + ", found " + a.Length);
            return a[0];
        }
        static Dictionary<string, Transform> BoneMap(Transform rig, IEnumerable<string> names, HashSet<Transform> rigs)
        {
            var result = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (string n in names)
            {
                var a = All(rig).Where(t => t != rig && t.name == n && !rigs.Any(r => r != rig && r.IsChildOf(rig) && (t == r || t.IsChildOf(r)))).ToArray();
                Require(a.Length == 1, "Missing or ambiguous bone " + rig.name + "/" + n + ": " + a.Length);
                result.Add(n, a[0]);
            }
            return result;
        }
        sealed class BoneRoute
        {
            public Transform source, target;
            public string sourceRig, bone, targetRig, subtarget;
        }
        static Component ConfigureMA(Transform source, Transform target, string externalPath = null)
        {
            var c = source.GetComponent(maType) ?? source.gameObject.AddComponent(maType);
            var so = new SerializedObject(c);
            so.Update();
            var r = so.FindProperty("mergeTarget");
            Require(r != null, "Installed MA has no mergeTarget field");
            Transform outfitRoot = source; while (outfitRoot.parent != null) outfitRoot = outfitRoot.parent;
            r.FindPropertyRelative("referencePath").stringValue = externalPath ?? (outfitRoot.name + "/" + PathOf(target, outfitRoot));
            r.FindPropertyRelative("targetObject").objectReferenceValue = target == null ? null : target.gameObject;
            so.FindProperty("prefix").stringValue = "";
            so.FindProperty("suffix").stringValue = "";
            so.FindProperty("LockMode").intValue = 1; // NotLocked: do not move authored rest transforms.
            so.FindProperty("legacyLocked").boolValue = false;
            so.FindProperty("mangleNames").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(c);
            return c;
        }
        // MA's matching is exact-child-name zip, with nested MA subtrees skipped.
        // The nearest explicit source ancestor therefore completely determines a bone's target.
        static Transform ZipTarget(Transform source, Dictionary<Transform, Transform> plan)
        {
            Transform a = source;
            var chain = new List<string>();
            while (a != null && !plan.ContainsKey(a)) { chain.Add(a.name); a = a.parent; }
            if (a == null) return null;
            Transform target = plan[a];
            for (int i = chain.Count - 1; i >= 0 && target != null; i--) target = target.Find(chain[i]);
            return target;
        }
        static List<BoneRoute> Routes(JObject authority, Dictionary<string, Dictionary<string, Transform>> bones)
        {
            var routes = new List<BoneRoute>();
            foreach (string rig in new[] { "Armature", "EvilFallBlade", "BreastPlugMain", "LegToy" })
                foreach (JObject record in (JArray)authority["armatures"][rig]["pose_constraints"])
                {
                    if ((string)record["type"] == "CHILD_OF" && rig == "BreastPlugMain" && (string)record["bone"] == "Base") continue;
                    Require((string)record["type"] == "COPY_TRANSFORMS" && Math.Abs((double)record["influence"] - 1) < 1e-8,
                        "Unsupported authoritative constraint in " + rig);
                    string name = (string)record["bone"], tr = (string)record["target"], tn = (string)record["subtarget"];
                    Require(bones.ContainsKey(tr) && bones[tr].ContainsKey(tn), "Authoritative target missing " + tr + "/" + tn);
                    routes.Add(new BoneRoute { source = bones[rig][name], target = bones[tr][tn], sourceRig = rig, bone = name, targetRig = tr, subtarget = tn });
                }
            Require(routes.Count(r => r.sourceRig == "Armature") == 158 && routes.Count(r => r.sourceRig == "EvilFallBlade") == 17 && routes.Count(r => r.sourceRig == "BreastPlugMain") == 4 && routes.Count(r => r.sourceRig == "LegToy") == 2 && routes.Count == 181,
                "Unexpected authoritative CopyTransforms counts (158+17+4+2=181; Plug Base ChildOf is the 182nd pose constraint)");
            return routes;
        }
        static JObject RouteJSON(IEnumerable<BoneRoute> routes, Transform root, Dictionary<Transform, Transform> plan)
        {
            var records = new JArray();
            foreach (var r in routes)
            {
                Transform found = ZipTarget(r.source, plan);
                Require(found == r.target, "MA zip would map the wrong bone: " + r.sourceRig + "/" + r.bone);
                records.Add(new JObject { ["source"] = PathOf(r.source, root), ["target"] = PathOf(r.target, root),
                    ["authorityTargetRig"] = r.targetRig, ["authoritySubtarget"] = r.subtarget,
                    ["explicitNestedMerge"] = plan.ContainsKey(r.source) });
            }
            return new JObject { ["count"] = records.Count, ["allExact"] = true, ["records"] = records };
        }
        sealed class RendererState
        {
            public SkinnedMeshRenderer r;
            public string path;
            public Mesh mesh;
            public Transform rootBone;
            public Transform[] bones;
            public float[] shapes;
            public bool active;
            public Vector3[] world;
        }
        static RendererState[] Capture(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Require(renderers.Length == 38, "Expected all 38 skinned mesh renderers");
            return renderers.Select(r =>
            {
                Require(r.sharedMesh != null && r.bones.Length == 431 && r.bones.All(b => b != null), "Invalid mesh/bones on " + r.name);
                Require(r.sharedMaterials.Length == r.sharedMesh.subMeshCount && r.sharedMaterials.All(m => m != null), "Invalid materials on " + r.name);
                var baked = new Mesh(); Vector3[] world;
                try { r.BakeMesh(baked); world = baked.vertices.Select(v => r.transform.TransformPoint(v)).ToArray(); }
                finally { Object.DestroyImmediate(baked); }
                return new RendererState { r = r, path = PathOf(r.transform, root.transform), mesh = r.sharedMesh,
                    rootBone = r.rootBone, bones = r.bones.ToArray(), active = r.gameObject.activeSelf, world = world,
                    shapes = Enumerable.Range(0, r.sharedMesh.blendShapeCount).Select(r.GetBlendShapeWeight).ToArray() };
            }).ToArray();
        }
        static JObject CheckRenderers(RendererState[] before, GameObject root, bool reloaded)
        {
            float max = 0; var perMesh = new JArray();
            Require(root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 38, "Renderer count changed");
            foreach (var s in before)
            {
                Transform t = root.transform.Find(s.path); Require(t != null, "Renderer path changed " + s.path);
                var r = t.GetComponent<SkinnedMeshRenderer>();
                Require(r != null && r.sharedMesh == s.mesh && r.bones.Length == s.bones.Length, "Shared mesh/bone count changed " + s.path);
                Require(r.gameObject.activeSelf == s.active && r.rootBone != null, "Active/rootBone changed " + s.path);
                if (!reloaded) Require(r.rootBone == s.rootBone && r.bones.SequenceEqual(s.bones), "Bone references changed " + s.path);
                Require(Enumerable.Range(0, s.shapes.Length).All(i => r.GetBlendShapeWeight(i) == s.shapes[i]), "Shape default changed " + s.path);
                var b = new Mesh(); float d = 0;
                try
                {
                    r.BakeMesh(b); var v = b.vertices; Require(v.Length == s.world.Length, "Bake vertex count changed");
                    for (int i = 0; i < v.Length; i++) d = Mathf.Max(d, Vector3.Distance(r.transform.TransformPoint(v[i]), s.world[i]));
                }
                finally { Object.DestroyImmediate(b); }
                Require(d <= 0.000002f, "Neutral skin moved more than 2um: " + s.path + " " + d);
                max = Mathf.Max(max, d);
                perMesh.Add(new JObject { ["path"] = s.path, ["vertices"] = s.world.Length, ["bones"] = r.bones.Length,
                    ["shapes"] = s.shapes.Length, ["neutralWorldMaxErrorMetres"] = d });
            }
            return new JObject { ["meshCount"] = 38, ["allBoneArrays"] = 431, ["maxNeutralWorldErrorMetres"] = max, ["renderers"] = perMesh };
        }
        static string RefKey(Object o, Transform root)
        {
            if (o == null) return "null";
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string guid, out long id) && !string.IsNullOrEmpty(guid)) return guid + ":" + id;
            if (o is GameObject go && (go.transform == root || go.transform.IsChildOf(root))) return "go:" + PathOf(go.transform, root);
            if (o is Component c && (c.transform == root || c.transform.IsChildOf(root)))
                return "component:" + PathOf(c.transform, root) + ":" + c.GetType().FullName + ":" + Array.IndexOf(c.GetComponents(c.GetType()), c);
            return "external:" + o.GetType().FullName + ":" + o.name;
        }
        static JObject SerializedState(GameObject root)
        {
            var result = new JObject();
            foreach (var t in All(root.transform))
                foreach (var c in t.GetComponents<Component>())
                {
                    Require(c != null, "Missing script on " + PathOf(t, root.transform));
                    var data = new JObject(); var so = new SerializedObject(c); var p = so.GetIterator();
                    bool enterChildren = true;
                    while (p.Next(enterChildren))
                    {
                        // Object references are already encoded by stable asset or hierarchy identity.
                        // Their native m_FileID children are transient instance IDs, not asset state.
                        enterChildren = p.propertyType != SerializedPropertyType.ObjectReference;
                        switch (p.propertyType)
                        {
                            case SerializedPropertyType.ObjectReference: data[p.propertyPath] = RefKey(p.objectReferenceValue, root.transform); break;
                            case SerializedPropertyType.Integer: case SerializedPropertyType.Enum: case SerializedPropertyType.LayerMask: case SerializedPropertyType.Character: data[p.propertyPath] = p.longValue; break;
                            case SerializedPropertyType.Boolean: data[p.propertyPath] = p.boolValue; break;
                            case SerializedPropertyType.Float: data[p.propertyPath] = p.doubleValue; break;
                            case SerializedPropertyType.String: data[p.propertyPath] = p.stringValue; break;
                            case SerializedPropertyType.Color: data[p.propertyPath] = p.colorValue.ToString("R"); break;
                            case SerializedPropertyType.Vector2: data[p.propertyPath] = p.vector2Value.ToString("R"); break;
                            case SerializedPropertyType.Vector3: data[p.propertyPath] = p.vector3Value.ToString("R"); break;
                            case SerializedPropertyType.Vector4: data[p.propertyPath] = p.vector4Value.ToString("R"); break;
                            case SerializedPropertyType.Quaternion: data[p.propertyPath] = p.quaternionValue.ToString("R"); break;
                            case SerializedPropertyType.Rect: data[p.propertyPath] = p.rectValue.ToString("R"); break;
                            case SerializedPropertyType.Bounds: data[p.propertyPath] = p.boundsValue.ToString("R"); break;
                            case SerializedPropertyType.ArraySize: data[p.propertyPath] = p.intValue; break;
                        }
                    }
                    result[PathOf(t, root.transform) + "|" + c.GetType().FullName + "|" + Array.IndexOf(t.GetComponents(c.GetType()), c)] = data;
                }
            return result;
        }
        static Dictionary<Component, string> FixedComponentJSON(GameObject root)
        {
            var d = new Dictionary<Component, string>();
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                Require(c != null, "Missing script");
                if (c is Transform || maType.IsInstanceOfType(c)) continue;
                var j = JObject.Parse(EditorJsonUtility.ToJson(c));
                if (c is Renderer) ((JObject)j[c.GetType().Name]).Remove("m_Materials");
                d.Add(c, j.ToString(Newtonsoft.Json.Formatting.None));
            }
            return d;
        }
        static JObject FixMaterials(GameObject root, string reviewPath)
        {
            var slot = JObject.Parse(File.ReadAllText(Full(reviewPath)));
            Require((string)slot["sha256"] == FileHash(FbxPath), "Material slot audit is for a different FBX");
            var generated = JObject.Parse(File.ReadAllText(Full(Folder + "/BuildFittedPrefab.report.json")));
            var old = ((JArray)generated["reloadedMeshBindings"]).OfType<JObject>().ToDictionary(x => (string)x["path"]);
            var indexed = AssetDatabase.FindAssets("t:Material", new[] { "Assets/YaokiSubWork/EvilFallFullSet/Material" })
                .Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).Where(m => m != null).GroupBy(m => m.name).ToDictionary(g => g.Key, g => g.ToArray());
            var result = new JArray();
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string path = PathOf(r.transform, root.transform);
                string[] current = r.sharedMaterials.Select(AssetDatabase.GetAssetPath).ToArray();
                string n = r.sharedMesh.name;
                var names = slot["meshMaterialsInFBXOrder"]?[n] as JArray;
                Require(names != null, "No FBX slot audit for mesh " + n);
                Require(names.Count == r.sharedMesh.subMeshCount, "FBX slot count differs on " + n);
                var desired = names.Select(x =>
                {
                    string name = ((string)x).Replace(" (Blender preview)", "");
                    if (name == "MainTrans") name = "EvillFallTrans FaceMask V2";
                    if (name == "Namelight") name = "EvilFall FaceMask V2 Name";
                    if (name == "MainUVTrains") name = "EvillFallTrans Blade";
                    Require(indexed.ContainsKey(name) && indexed[name].Length == 1, "Material missing/ambiguous " + name);
                    return indexed[name][0];
                }).ToArray();
                string status;
                if (current.SequenceEqual(desired.Select(AssetDatabase.GetAssetPath))) status = "ALREADY_CORRECT";
                else if (old.ContainsKey(path) && current.SequenceEqual(((JArray)old[path]["materials"]).Select(x => (string)x)))
                { r.sharedMaterials = desired; status = "FIXED_GENERATED_ARRAY_ONLY"; }
                else status = "PRESERVED_USER_OVERRIDE";
                result.Add(new JObject { ["path"] = path, ["mesh"] = n, ["status"] = status,
                    ["before"] = new JArray(current), ["after"] = new JArray(r.sharedMaterials.Select(AssetDatabase.GetAssetPath)) });
            }
            return new JObject { ["auditSHA256"] = FileHash(reviewPath), ["fbxSHA256"] = (string)slot["sha256"],
                ["fixedCount"] = result.OfType<JObject>().Count(j => (string)j["status"] == "FIXED_GENERATED_ARRAY_ONLY"), ["renderers"] = result };
        }
        static JObject ValidateWithPreviewAvatar(GameObject outfit, Dictionary<Transform, Transform> plan, Transform fit, Scene preview, List<BoneRoute> routes)
        {
            Type descriptor = FindType("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor");
            Require(descriptor != null, "VRC Avatar descriptor type unavailable for local reference proof");
            var stub = new GameObject("PreviewAvatarStub_ValidationOnly"); SceneManager.MoveGameObjectToScene(stub, preview);
            stub.AddComponent(descriptor);
            Transform external = CloneTransforms(fit, stub.transform); external.name = "armature";
            outfit.transform.SetParent(stub.transform, true);
            var found = new Dictionary<Transform, Transform>();
            var mergeList = new JArray();
            try
            {
                foreach (Component c in outfit.GetComponentsInChildren(maType, true))
                {
                    GameObject target = (GameObject)maType.GetProperty("mergeTargetObject").GetValue(c, null);
                    Transform expected = c.transform == fit ? external : plan[c.transform];
                    Require(target != null && target.transform == expected, "MA target did not resolve in avatar stub: " + c.transform.name);
                    found[c.transform] = expected;
                    var mapping = (IEnumerable)maType.GetMethod("GetBonesMapping").Invoke(c, null);
                    int count = 0;
                    Require(mapping != null, "MA GetBonesMapping returned null");
                    foreach (object pair in mapping)
                    {
                        var type = pair.GetType(); var b = (Transform)type.GetField("Item1").GetValue(pair); var s = (Transform)type.GetField("Item2").GetValue(pair);
                        Require(!found.ContainsKey(s) || found[s] == b, "Conflicting MA mapping " + s.name);
                        found[s] = b; count++;
                    }
                    mergeList.Add(new JObject { ["source"] = PathOf(c.transform, outfit.transform),
                        ["target"] = c.transform == fit ? "<Avatar>/armature" : PathOf(expected, outfit.transform), ["actualGetBonesMappingCount"] = count });
                }
                foreach (var p in plan) Require(found.ContainsKey(p.Key) && found[p.Key] == p.Value, "Explicit mapping missing");
                foreach (var r in routes) Require(found.ContainsKey(r.source) && found[r.source] == r.target, "Actual MA.GetBonesMapping missed " + r.sourceRig + "/" + r.bone);
                return new JObject { ["status"] = "PASS", ["externalAvatarArmatureDistinctFromOutfit"] = external != fit,
                    ["merges"] = mergeList, ["mappedSourceTransforms"] = found.Count,
                    ["note"] = "Local reference and exact-child zip proof only; no NDMF/avatar build or Play test was run." };
            }
            finally { outfit.transform.SetParent(null, true); Object.DestroyImmediate(stub); }
        }
        static Transform CloneTransforms(Transform from, Transform parent)
        {
            var go = new GameObject(from.name); var t = go.transform; t.SetParent(parent, false);
            t.localPosition = from.localPosition; t.localRotation = from.localRotation; t.localScale = from.localScale;
            foreach (Transform child in from) CloneTransforms(child, t);
            return t;
        }
        static JObject AnimationPaths(GameObject root)
        {
            var paths = new HashSet<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/YaokiSubWork/EvilFallFullSet/ForKipfel" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (!p.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
                foreach (var b in AnimationUtility.GetCurveBindings(clip).Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip)))
                    if (b.path.StartsWith("armature/", StringComparison.Ordinal)) paths.Add(b.path);
            }
            var missing = paths.Where(p => root.transform.Find(p) == null).ToArray();
            Require(missing.Length == 0, "Old armature animation paths missing: " + string.Join(", ", missing));
            return new JObject { ["bindingPathCount"] = paths.Count, ["allResolve"] = true, ["paths"] = new JArray(paths.OrderBy(p => p)) };
        }
        static JObject StableReferenceSnapshot(JObject state)
        {
            foreach (var row in state.Properties().Select(p => p.Value).OfType<JObject>())
                foreach (var p in row.Properties().Where(p => p.Name.EndsWith(".m_FileID", StringComparison.Ordinal) || p.Name.EndsWith(".m_PathID", StringComparison.Ordinal)).ToArray())
                {
                    string parent = p.Name.Substring(0, p.Name.Length - (p.Name.EndsWith(".m_FileID", StringComparison.Ordinal) ? ".m_FileID".Length : ".m_PathID".Length));
                    Require(row[parent]?.Type == Newtonsoft.Json.Linq.JTokenType.String,
                        "Session ID has no stable object reference identity: " + p.Name);
                    p.Remove();
                }
            return state;
        }
        static void VerifySavedCandidate(JObject req)
        {
            Require(FileHash(PrefabPath) == (string)req["expectedPrefabSHA256"], "Saved prefab changed since the diagnostic capture");
            var report = JObject.Parse(File.ReadAllText(Full(Folder + "/RigBindingFix/failed_session_ids_report.json")));
            Require((string)report["status"] == "FAILED_AFTER_SAVE", "Unexpected diagnostic status");
            var expected = StableReferenceSnapshot(JObject.Parse(File.ReadAllText(Full(Folder + "/RigBindingFix/reload_expected.json"))));
            var recorded = StableReferenceSnapshot(JObject.Parse(File.ReadAllText(Full(Folder + "/RigBindingFix/reload_actual.json"))));
            Require(JToken.DeepEquals(expected, recorded), "Persistent fields differ in recorded saved reload");
            var dirty = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToDictionary(s => s.handle, s => s.isDirty);
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(PrefabPath);
                var actual = SerializedState(root);
                File.WriteAllText(Full(Folder + "/RigBindingFix/final_expected_stable.json"), expected.ToString());
                File.WriteAllText(Full(Folder + "/RigBindingFix/final_actual_stable.json"), actual.ToString());
                Require(JToken.DeepEquals(expected, actual), "Current native saved reload differs from intended persistent state");
                var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Require(renderers.Length == 38 && renderers.All(r => r.sharedMesh != null && r.bones.Length == 431 && r.bones.All(b => b != null)), "Saved mesh/bone binding invalid");
                Require(FileHash(FbxPath) == (string)report["fbxSHA256Before"], "FBX changed");
                Require(FileHash(PrefabPath + ".meta") == (string)req["expectedPrefabMetaSHA256"], "Prefab meta changed");
                Require(AssetDatabase.AssetPathToGUID(PrefabPath) == (string)report["prefabGUIDBefore"], "Prefab GUID changed");
                report["diagnosticGuardCorrection"] = "Ignore only native ObjectReference m_FileID/m_PathID children already encoded by stable reference identity; stable reference identities and every persistent field compare exactly. No prefab changes were made by final verification.";
                report["serializedReloadExact"] = true;
                report["nativeFinalReadOnlyVerification"] = new JObject { ["status"] = "PASS", ["componentCount"] = actual.Count,
                    ["stableFieldCount"] = actual.Properties().Sum(p => ((JObject)p.Value).Count), ["meshCount"] = 38, ["allBoneArrays"] = 431,
                    ["currentAssetExactlyMatchesBeforeSaveIntent"] = true, ["assetWritten"] = false };
                report["fbxUnchanged"] = true; report["boneIdentities"] = 431;
                report["prefabGUIDAfter"] = AssetDatabase.AssetPathToGUID(PrefabPath); report["prefabSHA256After"] = FileHash(PrefabPath);
                report["poseConstraintCoverage"] = new JObject { ["copyTransforms"] = 181, ["keepWorldChildOf"] = 1, ["total"] = 182, ["unsupported"] = new JArray() };
                report["validationLimit"] = "Static exact routing/reference/serialization and pre-save neutral BakeMesh invariance. The saved asset matches that validated persistent state exactly. No agent Play test or full NDMF build was run.";
                report.Remove("exception"); report["status"] = "PASS";
            }
            finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
            Require(Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).All(s => !dirty.ContainsKey(s.handle) || dirty[s.handle] == s.isDirty), "User scene dirty state changed");
            report["userSceneDirtyStatesUnchanged"] = true;
            report["finalVerificationRequest"] = req.DeepClone();
            Write(report);
            Debug.Log("EvilFall rig binding and outfit reactions saved verification PASS");
            if (Flag(req, "openPrefab", true)) PrefabStageUtility.OpenPrefab(PrefabPath);
        }
        static void Run(JObject req)
        {
            if (Flag(req, "verifySavedCandidate", false)) { VerifySavedCandidate(req); return; }
            string path = Str(req, "prefabPath", PrefabPath);
            Require(path == PrefabPath, "This scoped repair only supports the original authorized prefab");
            Require(Flag(req, "allowOverwrite", false), "allowOverwrite:true is required for the authorized prefab repair");
            maType = FindType(MATypeName); Require(maType != null, "Installed Modular Avatar runtime type not found");
            var report = new JObject { ["status"] = "RUNNING", ["request"] = req.DeepClone(), ["prefab"] = path,
                ["scriptVersion"] = "export-nine-rigs-7-stable-references", ["scope"] = "Rename two rig roots; reproduce 181 CopyTransforms and one Plug Base ChildOf plus nine object follow routes; conditionally correct generated material slot arrays",
                ["playTestRun"] = false, ["avatarBuildRun"] = false };
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null) { Require(!stage.scene.isDirty, "Save or close the dirty PrefabStage first; this utility never discards it"); StageUtility.GoToMainStage(); }
            var dirty = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToDictionary(s => s.handle, s => s.isDirty);
            string guid = AssetDatabase.AssetPathToGUID(path), fbxHash = FileHash(FbxPath);
            string prefabBefore = FileHash(path), metaBefore = FileHash(path + ".meta");
            string backup = Full(Folder + "/BeforeRigBindingFix/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")); Directory.CreateDirectory(backup);
            File.Copy(Full(path), Path.Combine(backup, Path.GetFileName(path)));
            File.Copy(Full(path + ".meta"), Path.Combine(backup, Path.GetFileName(path) + ".meta"));
            report["backup"] = backup; report["prefabSHA256Before"] = prefabBefore; report["prefabGUIDBefore"] = guid;
            report["fbxSHA256Before"] = fbxHash;
            GameObject root = null, reload = null; bool saved = false;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                var before = Capture(root);
                var world = All(root.transform).ToDictionary(t => t, t => t.localToWorldMatrix);
                var activeBefore = All(root.transform).ToDictionary(t => t, t => t.gameObject.activeSelf);
                var fixedJSON = FixedComponentJSON(root);
                var authority = JObject.Parse(File.ReadAllText(Full(Folder + "/saved_hierarchy_rigs_controls_readonly.json")));
                report["authoritySHA256"] = FileHash(Folder + "/saved_hierarchy_rigs_controls_readonly.json");
                var rigRoots = new Dictionary<string, Transform>(StringComparer.Ordinal);
                rigRoots["Armature"] = Direct(root.transform, "armature", "EvilFall_SourceRig");
                rigRoots["Kipfel_Fit_Rig"] = Direct(root.transform, "Kipfel_Fit_Rig");
                foreach (var p in ((JObject)authority["armatures"]).Properties().Where(p => ((JArray)authority["export_candidate_from_source_and_support"]).Values<string>().Contains(p.Name)))
                    if (!rigRoots.ContainsKey(p.Name)) rigRoots[p.Name] = p.Name == "EvilFall_FaceBranch_Rig" ? Unique(root.transform, p.Name) : Direct(root.transform, p.Name);
                Require(rigRoots.Count == 9, "Expected exactly nine independent rig objects");
                var roots = new HashSet<Transform>(rigRoots.Values);
                var bones = rigRoots.ToDictionary(p => p.Key, p => BoneMap(p.Value,
                    ((JArray)authority["armatures"][p.Key]["bone_names"]).Select(x => (string)x), roots));
                Require(bones.Sum(p => p.Value.Count) == 431, "Exact bone identities do not total 431");
                var source = rigRoots["Armature"]; var fit = rigRoots["Kipfel_Fit_Rig"]; var blade = rigRoots["EvilFallBlade"];
                source.name = "EvilFall_SourceRig"; fit.name = "armature";
                // Preserve-rest ChildOf translates directly into keep-world hierarchy. No runtime constraint is added.
                var reparented = new JArray();
                Action<Transform, Transform, string> keepWorld = (child, parent, reason) =>
                {
                    string oldPath = PathOf(child, root.transform); child.SetParent(parent, true);
                    reparented.Add(new JObject { ["before"] = oldPath, ["after"] = PathOf(child, root.transform), ["reason"] = reason });
                };
                var hips = bones["Kipfel_Fit_Rig"]["Hips"];
                foreach (string n in new[] { "AnalToyMain", "CyberDickSet", "PussyToyMain", "LegToy" })
                {
                    var c = (JObject)((JArray)authority["object_inventory"][n]["constraints"]).Single();
                    Require((string)c["type"] == "CHILD_OF" && (string)c["target"] == "Kipfel_Fit_Rig" && (string)c["subtarget"] == "Hips", "Unexpected object ChildOf " + n);
                    keepWorld(rigRoots[n], hips, "Blender object ChildOf Hips (preserve rest)");
                }
                keepWorld(rigRoots["BreastPlugMain"], fit, "Blender object CopyTransforms Fit root (preserve current identity offset)");
                keepWorld(bones["BreastPlugMain"]["Base"], bones["Kipfel_Fit_Rig"]["Chest"], "Blender Base bone ChildOf Chest (preserve rest)");
                foreach (string side in new[] { "L", "R" })
                {
                    string name = "LegAssembly_" + side + "_UpperLegBind";
                    Transform helper = Direct(root.transform, name);
                    keepWorld(helper, bones["Kipfel_Fit_Rig"]["UpperLeg." + side], "Existing upper-leg delta helper (preserve authored offset)");
                    bones[name] = new Dictionary<string, Transform> { [""] = helper };
                }
                report["keepWorldHierarchyFollows"] = reparented;
                report["objectFollowCoverage"] = new JObject { ["count"] = 9, ["records"] = new JArray(
                    "Armature root CopyTransforms -> MA Source to Fit", "EvilFallBlade object/root CopyTransforms -> MA weapon bone to Fit",
                    "BreastPlugMain object CopyTransforms -> parent Fit", "AnalToyMain ChildOf Hips -> parent Hips",
                    "CyberDickSet ChildOf Hips -> parent Hips", "PussyToyMain ChildOf Hips -> parent Hips", "LegToy ChildOf Hips -> parent Hips",
                    "LegAssembly_L_UpperLegBind delta -> parent UpperLeg.L", "LegAssembly_R_UpperLegBind delta -> parent UpperLeg.R") };
                Require(rigRoots["EvilFall_FaceBranch_Rig"].IsChildOf(bones["Kipfel_Fit_Rig"]["EvilFallFaceMask"]), "FaceBranch no longer follows fitted head face branch");
                var plan = new Dictionary<Transform, Transform> { [source] = fit, [blade] = bones["Kipfel_Fit_Rig"]["EvilFallBlade"] };
                var routes = Routes(authority, bones);
                foreach (var r in routes.OrderBy(r => Depth(r.source)))
                    if (ZipTarget(r.source, plan) != r.target) plan[r.source] = r.target;
                foreach (var p in plan) ConfigureMA(p.Key, p.Value);
                ConfigureMA(fit, null, Str(req, "avatarArmaturePath", "armature"));
                report["exactCopyTransformRoutes"] = RouteJSON(routes, root.transform, plan);
                report["explicitInternalMergeCount"] = plan.Count;
                report["fitMerge"] = new JObject { ["source"] = "armature", ["target"] = "<Avatar>/" + Str(req, "avatarArmaturePath", "armature"), ["lockMode"] = "NotLocked", ["mangleNames"] = false };
                report["materials"] = FixMaterials(root, Str(req, "materialSlotReviewPath", "/tmp/evilfall_export_scope_readonly/fbx_v2_material_slot_order_review.json"));
                report["animationPaths"] = AnimationPaths(root);
                report["previewAvatarMapping"] = ValidateWithPreviewAvatar(root, plan, fit, root.scene, routes);
                // Verify actual package GetBonesMapping, not only our independent zip model.
                foreach (var entry in fixedJSON)
                {
                    var j = JObject.Parse(EditorJsonUtility.ToJson(entry.Key)); if (entry.Key is Renderer) ((JObject)j[entry.Key.GetType().Name]).Remove("m_Materials");
                    if (j.ToString(Newtonsoft.Json.Formatting.None) != entry.Value)
                    {
                        report["fixedComponentMismatch"] = new JObject { ["object"] = entry.Key.name,
                            ["type"] = entry.Key.GetType().FullName, ["before"] = JObject.Parse(entry.Value), ["after"] = j.DeepClone() };
                        throw new InvalidOperationException("An existing non-MA component changed: " + entry.Key.name + " / " + entry.Key.GetType().Name);
                    }
                }
                // Root may opt in to a separate authorized reactive-configuration hook. No hook runs by default.
                string hookName = Str(req, "beforeSaveHookType", "");
                if (!string.IsNullOrEmpty(hookName))
                {
                    Type hookType = FindType(hookName); Require(hookType != null, "Requested before-save hook type not found");
                    MethodInfo hook = hookType.GetMethod(Str(req, "beforeSaveHookMethod", "Apply"), BindingFlags.Public | BindingFlags.Static,
                        null, new[] { typeof(GameObject), typeof(JObject) }, null);
                    Require(hook != null, "Hook must be public static void Apply(GameObject,JObject)");
                    hook.Invoke(null, new object[] { root, report }); report["authorizedBeforeSaveHook"] = hookName;
                }
                foreach (var p in activeBefore) Require(p.Key.gameObject.activeSelf == p.Value, "Existing object active state changed " + p.Key.name);
                float matrixMax = 0;
                foreach (var p in world) for (int i = 0; i < 16; i++) matrixMax = Mathf.Max(matrixMax, Mathf.Abs(p.Key.localToWorldMatrix[i] - p.Value[i]));
                Require(matrixMax <= 0.000002f, "Existing rest/world transform changed unexpectedly");
                report["allExistingTransformWorldMaxComponentError"] = matrixMax;
                report["neutralBeforeSave"] = CheckRenderers(before, root, false);
                var expected = SerializedState(root);
                bool success; PrefabUtility.SaveAsPrefabAsset(root, path, out success); Require(success, "Prefab save failed"); saved = true;
                PrefabUtility.UnloadPrefabContents(root); root = null;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                reload = PrefabUtility.LoadPrefabContents(path);
                var actualReload = SerializedState(reload);
                if (!JToken.DeepEquals(expected, actualReload))
                {
                    File.WriteAllText(Full(Folder + "/RigBindingFix/reload_expected.json"), expected.ToString());
                    File.WriteAllText(Full(Folder + "/RigBindingFix/reload_actual.json"), actualReload.ToString());
                    throw new InvalidOperationException("Serialized prefab reload state differs; see reload_expected.json/reload_actual.json");
                }
                report["neutralAfterReload"] = CheckRenderers(before, reload, true);
                report["serializedReloadExact"] = true;
                Require(AssetDatabase.AssetPathToGUID(path) == guid && FileHash(path + ".meta") == metaBefore, "Prefab GUID/meta changed");
                Require(FileHash(FbxPath) == fbxHash, "FBX was changed");
                report["prefabGUIDAfter"] = AssetDatabase.AssetPathToGUID(path); report["prefabSHA256After"] = FileHash(path);
                report["fbxUnchanged"] = true; report["boneIdentities"] = 431;
                report["poseConstraintCoverage"] = new JObject { ["copyTransforms"] = 181, ["keepWorldChildOf"] = 1, ["total"] = 182, ["unsupported"] = new JArray() };
                report["validationLimit"] = "Static exact routing/reference/serialization and neutral BakeMesh invariance. Root/user must separately test the avatar in Play; no full NDMF build was run.";
                report["status"] = "PASS";
            }
            catch (Exception e)
            {
                report["status"] = saved ? "FAILED_AFTER_SAVE" : "FAILED_NOT_SAVED"; report["exception"] = e.ToString(); Debug.LogException(e);
            }
            finally
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
                if (reload != null) PrefabUtility.UnloadPrefabContents(reload);
                var changedScenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => dirty.ContainsKey(s.handle) && dirty[s.handle] != s.isDirty).Select(s => s.path).ToArray();
                report["userSceneDirtyStatesUnchanged"] = changedScenes.Length == 0;
                if (changedScenes.Length != 0) { report["changedUserScenes"] = new JArray(changedScenes); report["status"] = "FAILED_USER_SCENE_STATE_CHANGED"; }
                Write(report);
            }
            if ((string)report["status"] == "PASS")
            {
                Debug.Log("EvilFall rig binding static repair PASS; see " + ReportPath);
                if (Flag(req, "openPrefab", true)) PrefabStageUtility.OpenPrefab(path);
            }
        }
    }
}
#endif
