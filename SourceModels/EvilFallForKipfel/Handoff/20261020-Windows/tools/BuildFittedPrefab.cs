// Copy this file to an Editor folder only when ready to build. It does nothing until
// SourceModels/EvilFallForKipfel/Exports/20261020-Unity/BuildFittedPrefab.request.json
// exists. No user-scene instance is created. Existing output requires explicit allowOverwrite:true.
// Request example: { "requestId":"fit-1", "mainArmatureName":"Armature",
//   "materialManifestPath":"SourceModels/EvilFallForKipfel/Exports/20261020-Unity/materials.json" }
// Optional manifest: { "meshMaterials":{"HeadACC":["Blender material name", ...]},
//   "materialAssets":{"Blender material name":"Assets/.../Existing.mat"},
//   "rigs":[{"name":"Armature","boneCount":166}, ...] }
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EvilFallForKipfel.Export20261020
{
    [InitializeOnLoad]
    public static class BuildFittedPrefab
    {
        const string Folder = "SourceModels/EvilFallForKipfel/Exports/20261020-Unity";
        const string Template = "Assets/YaokiSubWork/EvilFallFullSet/ForKipfel/EvilFall_Kipfel.prefab";
        const string Fbx = "Assets/YaokiSubWork/EvilFallFullSet/ForKipfel/EvilFall_Kipfel.fbx";
        const string Output = "Assets/YaokiSubWork/EvilFallFullSet/ForKipfel/EvilFall_Kipfel.prefab";
        static double nextCheck;
        static bool busy;
        static string Project => Directory.GetParent(Application.dataPath).FullName;
        static string RequestFile => Path.Combine(Project, Folder, "BuildFittedPrefab.request.json");
        static string ReportFile => Path.Combine(Project, Folder, "BuildFittedPrefab.report.json");
        static string Full(string p) => Path.IsPathRooted(p) ? p : Path.Combine(Project, p);

        static BuildFittedPrefab() { EditorApplication.update += Tick; }

        static void Tick()
        {
            if (busy || EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 1;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestFile)) return;
            string text = File.ReadAllText(RequestFile);
            string token;
            using (var h = SHA256.Create()) token = BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
            string key = "EvilFall20261020.Build." + token;
            if (SessionState.GetBool(key, false)) return;
            // New request contents/requestId are required to retry a failed build.
            SessionState.SetBool(key, true);
            busy = true;
            try { Build(JObject.Parse(text)); }
            catch (Exception ex)
            {
                WriteReport(new JObject { ["status"] = "FAILED_BEFORE_BUILD", ["exception"] = ex.ToString() });
                Debug.LogException(ex);
            }
            finally { busy = false; }
        }

        static string Str(JObject o, string k, string fallback) => (string)o[k] ?? fallback;
        static int Num(JObject o, string k, int fallback) => o[k] == null ? fallback : (int)o[k];
        static bool Bool(JObject o, string k, bool fallback) => o[k] == null ? fallback : (bool)o[k];
        static void CheckAssetPath(string p)
        {
            if (!p.StartsWith("Assets/", StringComparison.Ordinal) || p.Contains("..") || p.Contains('\\'))
                throw new ArgumentException("Expected a project-relative Assets path: " + p);
        }

        static void Build(JObject req)
        {
            string input = Str(req, "fbxPath", Fbx), output = Str(req, "prefabPath", Output);
            string templatePath = Str(req, "templatePath", Template);
            CheckAssetPath(input); CheckAssetPath(output); CheckAssetPath(templatePath);
            var warnings = new JArray(); var errors = new JArray(); var skipped = new JArray();
            var report = new JObject { ["status"] = "BUILDING", ["input"] = input, ["output"] = output,
                ["template"] = templatePath, ["request"] = req.DeepClone(), ["warnings"] = warnings,
                ["errors"] = errors, ["skippedComponents"] = skipped, ["builtInPreviewScene"] = true };
            bool overwrite = Bool(req, "allowOverwrite", false);
            if (!overwrite && (File.Exists(Full(output)) || AssetDatabase.LoadAssetAtPath<GameObject>(output) != null))
            {
                report["status"] = "SKIPPED_OUTPUT_ALREADY_EXISTS_NO_OVERWRITE"; WriteReport(report); return;
            }
            if (!File.Exists(Full(input))) throw new FileNotFoundException("New fitted FBX is not present", input);
            var currentStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (currentStage != null)
            {
                if (currentStage.scene.isDirty) throw new InvalidOperationException("Current PrefabStage has unsaved changes; Root must save or close it before this request");
                StageUtility.GoToMainStage();
            }
            string originalOutputGUID = AssetDatabase.AssetPathToGUID(output);
            string originalInputGUID = AssetDatabase.AssetPathToGUID(input);
            report["allowOverwrite"] = overwrite;
            report["outputGUIDBefore"] = originalOutputGUID;
            report["inputGUIDBefore"] = originalInputGUID;
            SceneSetup[] userScenes = EditorSceneManager.GetSceneManagerSetup();
            var dirtyBefore = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
                .ToDictionary(s => s.handle, s => s.isDirty);
            Scene preview = default(Scene); GameObject source = null, result = null, reloaded = null;
            bool saved = false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Full(output)));
                AssetDatabase.ImportAsset(input, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(input) as ModelImporter;
                if (importer == null) throw new InvalidOperationException("New FBX has no ModelImporter");
                // For the authorized original-path overwrite, keep the old .meta/importer settings by default.
                if (Bool(req, "configureImporter", !overwrite))
                {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importBlendShapes = true; importer.importAnimation = false;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importBlendShapeNormals = ModelImporterNormals.Calculate;
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.optimizeGameObjects = false; importer.optimizeBones = false;
                importer.preserveHierarchy = true; importer.isReadable = true;
                importer.skinWeights = ModelImporterSkinWeights.Custom;
                importer.maxBonesPerVertex = 255; importer.minBoneWeight = 0;
                importer.SaveAndReimport();
                }
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(input);
                if (model == null) throw new InvalidOperationException("FBX did not import as a GameObject");
                JObject manifest = new JObject();
                string manifestPath = Str(req, "materialManifestPath", Folder + "/blender_export_manifest.json");
                if (!string.IsNullOrEmpty(manifestPath)) manifest = JObject.Parse(File.ReadAllText(Full(manifestPath)));
                // The exported manifest uses rigs:{name:{bones:N}}. Add the separately audited exact bone identities in memory.
                string identitiesPath = Str(req, "rigIdentityPath", Folder + "/saved_hierarchy_rigs_controls_readonly.json");
                JObject identities = File.Exists(Full(identitiesPath)) ? JObject.Parse(File.ReadAllText(Full(identitiesPath))) : new JObject();
                if (manifest["rigs"] is JObject rigObject)
                    manifest["rigs"] = new JArray(rigObject.Properties().Select(p => new JObject { ["name"] = p.Name,
                        ["boneCount"] = p.Value["bones"], ["boneNames"] = identities["armatures"]?[p.Name]?["bone_names"]?.DeepClone() }));
                preview = EditorSceneManager.NewPreviewScene();
                result = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
                if (result == null) throw new InvalidOperationException("Cannot instantiate model into preview scene");
                PrefabUtility.UnpackPrefabInstance(result, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                result.name = Path.GetFileNameWithoutExtension(output);
                Renderer[] allRenderers = MeshRenderers(result);
                report["importedMeshRendererCount"] = allRenderers.Length;
                // Capture rig nodes BEFORE renderer-path reparenting. A supplied rigs manifest is authoritative.
                JObject importedSkeleton = SkeletonSummary(result, manifest);
                var importedRigRoots = ((JArray)importedSkeleton["rigs"]).Select(r =>
                    result.transform.Find((string)r["path"])).ToArray();
                var importedBoneSets = ((JArray)importedSkeleton["rigs"]).Select(r =>
                    ((JArray)r["bonePaths"]).Select(x => result.transform.Find((string)x)).ToArray()).ToArray();
                if (importedRigRoots.Any(t => t == null)) errors.Add("One or more rig roots could not be resolved before construction");
                report["skeletonAtImport"] = importedSkeleton;
                RestoreManifestBoneParenting(result, manifest, warnings);
                source = PrefabUtility.LoadPrefabContents(templatePath);
                var map = new Dictionary<Object, Object> { [source] = result, [source.transform] = result.transform };
                var aliases = new Dictionary<string, string>(StringComparer.Ordinal) { ["EvilFallTail"] = "Tail" };
                if (req["rendererAliases"] is JObject ra)
                    foreach (var p in ra.Properties()) aliases[p.Name] = (string)p.Value;
                var originalNames = allRenderers.ToDictionary(r => r, r => r.name);
                var byOriginalName = allRenderers.GroupBy(r => r.name).ToDictionary(g => g.Key, g => g.ToArray());
                var originalTemplate = MeshRenderers(source);
                foreach (var old in originalTemplate.OrderBy(r => Relative(r.transform, source.transform).Count(c => c == '/')))
                {
                    string n = aliases.TryGetValue(old.name, out var a) ? a : old.name;
                    if (!byOriginalName.TryGetValue(n, out var matches) || matches.Length != 1)
                    { warnings.Add("No unique new renderer for template " + old.name + " (alias " + n + ")"); continue; }
                    Renderer nr = matches[0];
                    string path = Relative(old.transform, source.transform);
                    int slash = path.LastIndexOf('/');
                    Transform parent = slash < 0 ? result.transform : result.transform.Find(path.Substring(0, slash));
                    if (parent == null)
                    { warnings.Add("Renderer parent path unresolved, retaining imported placement: " + path); parent = nr.transform.parent; }
                    nr.transform.SetParent(parent, true); nr.name = old.name;
                    map[old] = nr; map[old.gameObject] = nr.gameObject; map[old.transform] = nr.transform;
                }
                string rigName = Str(req, "mainArmatureName", "Armature");
                var rigRoot = result.transform.Find(rigName);
                var rig = rigRoot == null ? new Transform[0] : new[] { rigRoot };
                if (rig.Length == 1) rig[0].name = "armature";
                else warnings.Add("Main armature rename unresolved: " + rigName + "; matches=" + rig.Length);
                MapTransforms(source.transform, result.transform, map, warnings);
                // Never merge multiple old MA/PhysBone/constraint instances into one component.
                // Transform and Renderer mappings are explicit; only an existing MeshFilter may be reused here.
                foreach (var oc in source.GetComponentsInChildren<Component>(true))
                {
                    if (!(oc is MeshFilter) || map.ContainsKey(oc) || !map.TryGetValue(oc.gameObject, out var ng)) continue;
                    var existing = ((GameObject)ng).GetComponents(oc.GetType());
                    if (existing.Length == 1) map[oc] = existing[0];
                }
                BindMaterials(allRenderers, originalNames, originalTemplate, map, manifest, errors);
                ApplyManifestDefaults(allRenderers, originalNames, manifest, errors);
                var trs = Descendants(result.transform).ToDictionary(t => t, t => new TRS(t));
                var copied = CopySafeComponents(source, result, map, skipped, warnings);
                var changedRest = trs.Where(p => !p.Value.Exact(p.Key)).ToArray();
                if (changedRest.Length != 0)
                {
                    // Optional copied authoring/constraint components must never override the new fitted rest pose.
                    foreach (var c in copied) if (c != null) Object.DestroyImmediate(c);
                    foreach (var p in trs) p.Value.Restore(p.Key);
                    skipped.Add(new JObject { ["reason"] = "Removed migrated optional components because they altered imported fitted rest transforms",
                        ["changedPaths"] = new JArray(changedRest.Select(p => Relative(p.Key, result.transform))) });
                }
                var rigSpecs = new JArray();
                for (int i = 0; i < importedRigRoots.Length; i++)
                    rigSpecs.Add(new JObject { ["path"] = importedRigRoots[i] == null ? "<missing>" : Relative(importedRigRoots[i], result.transform),
                        ["expected"] = importedSkeleton["rigs"][i]["expected"],
                        ["missingAtImport"] = importedSkeleton["rigs"][i]["missingNames"],
                        ["bonePaths"] = new JArray(importedBoneSets[i].Where(t => t != null).Select(t => Relative(t, result.transform))) });
                report["skeletonBeforeSave"] = MeasureRigPaths(result, rigSpecs);
                report["templateRendererCount"] = originalTemplate.Length;
                report["mappedTemplateRendererCount"] = originalTemplate.Count(map.ContainsKey);
                report["materialAndMeshBindings"] = DescribeMeshes(result);
                report["validationBeforeSave"] = Validate(result, Num(req, "expectedMeshCount", 38),
                    Num(req, "expectedBoneCount", 431), (JObject)report["skeletonBeforeSave"], errors);
                // A complete bare fitted model is useful even if optional old components cannot map safely.
                // Fatal validation issues are reported; never substitute old rest/meshes or delete new meshes to hide them.
                bool success;
                PrefabUtility.SaveAsPrefabAsset(result, output, out success);
                if (!success) throw new InvalidOperationException("SaveAsPrefabAsset failed");
                saved = true;
                reloaded = PrefabUtility.LoadPrefabContents(output);
                // Re-measure the actual saved hierarchy, rather than reusing an import-time integer.
                report["skeletonAfterReload"] = MeasureRigPaths(reloaded, rigSpecs);
                report["validationAfterReload"] = Validate(reloaded, Num(req, "expectedMeshCount", 38),
                    Num(req, "expectedBoneCount", 431), (JObject)report["skeletonAfterReload"], errors);
                if (!JToken.DeepEquals(report["skeletonBeforeSave"], report["skeletonAfterReload"])) errors.Add("Saved/reloaded actual rig hierarchy changed");
                report["reloadedMeshBindings"] = DescribeMeshes(reloaded);
                if (!JToken.DeepEquals(report["materialAndMeshBindings"], report["reloadedMeshBindings"])) errors.Add("Saved/reloaded mesh, material, shape defaults, or ordered bone bindings changed");
                ValidateLocalComponentReferences(reloaded, errors);
                report["outputGUIDAfter"] = AssetDatabase.AssetPathToGUID(output);
                report["inputGUIDAfter"] = AssetDatabase.AssetPathToGUID(input);
                if (!string.IsNullOrEmpty(originalOutputGUID) && originalOutputGUID != (string)report["outputGUIDAfter"]) errors.Add("Existing prefab GUID changed");
                if (!string.IsNullOrEmpty(originalInputGUID) && originalInputGUID != (string)report["inputGUIDAfter"]) errors.Add("Existing FBX GUID changed");
                report["bakedWorldGeometry"] = WriteBakedGeometry(reloaded, output, aliases);
                if (MeshRenderers(reloaded).Length != allRenderers.Length) errors.Add("Saved/reloaded mesh count changed");
                report["runtimeReady"] = false;
                report["blenderConstraintsConvertedToUnityRuntime"] = false;
                warnings.Add("All nine rigs are preserved; Blender CopyTransforms/ChildOf drivers are not FBX runtime logic. Verify the Source166, Fit212, FaceBranch and accessory-rig follow/merge routing in Unity before avatar use.");
                report["status"] = errors.Count == 0 ? "PASS_COMPLETE_MESH_PREFAB_SAVED_RELOADED_RUNTIME_RIG_CONFIGURATION_PENDING" :
                    "PREFAB_SAVED_VALIDATION_ERRORS_NEEDS_UNITY_FOLLOWUP";
            }
            catch (Exception ex)
            {
                report["status"] = saved ? "PREFAB_SAVED_BUT_FOLLOWUP_FAILED" : "BUILD_FAILED";
                report["exception"] = ex.ToString(); Debug.LogException(ex);
            }
            finally
            {
                if (reloaded != null) PrefabUtility.UnloadPrefabContents(reloaded);
                if (source != null) PrefabUtility.UnloadPrefabContents(source);
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                SceneSetup[] after = EditorSceneManager.GetSceneManagerSetup();
                bool setupExact = userScenes.Length == after.Length && userScenes.Zip(after, (a, b) =>
                    a.path == b.path && a.isLoaded == b.isLoaded && a.isActive == b.isActive).All(x => x);
                var dirtyAfter = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
                    .ToDictionary(s => s.handle, s => s.isDirty);
                bool dirtExact = dirtyBefore.All(p => dirtyAfter.ContainsKey(p.Key) && dirtyAfter[p.Key] == p.Value);
                report["userSceneSetupUnchangedBeforeOpeningResult"] = setupExact;
                report["userSceneDirtyFlagsUnchanged"] = dirtExact;
                if (!setupExact || !dirtExact) { errors.Add("User scene setup/dirty state changed unexpectedly"); report["status"] = "SCENE_STATE_CHECK_FAILED"; }
                report["oldTemplateNotSaved"] = !(templatePath == output && saved);
                report["templateOverwrittenOnlyAtExplicitRequestedOutput"] = overwrite && templatePath == output && saved;
                WriteReport(report);
            }
            if (saved && Bool(req, "openPrefab", true))
            {
                try { PrefabStageUtility.OpenPrefab(output); }
                catch (Exception ex) { report["openPrefabError"] = ex.ToString(); WriteReport(report); }
            }
        }

        struct TRS
        {
            readonly Vector3 p, s; readonly Quaternion q;
            public TRS(Transform t) { p = t.localPosition; q = t.localRotation; s = t.localScale; }
            public bool Exact(Transform t) => p.Equals(t.localPosition) && q.Equals(t.localRotation) && s.Equals(t.localScale);
            public void Restore(Transform t) { t.localPosition = p; t.localRotation = q; t.localScale = s; }
        }
        static Renderer[] MeshRenderers(GameObject root) => root.GetComponentsInChildren<Renderer>(true)
            .Where(r => r is SkinnedMeshRenderer || r.GetComponent<MeshFilter>() != null).ToArray();
        static Mesh GetMesh(Renderer r) => r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
        static IEnumerable<Transform> Descendants(Transform t) => t.GetComponentsInChildren<Transform>(true);
        static string Relative(Transform t, Transform root) => AnimationUtility.CalculateTransformPath(t, root);
        static void RestoreManifestBoneParenting(GameObject root, JObject manifest, JArray warnings)
        {
            if (!(manifest["objectParents"] is JObject parents)) return;
            foreach (var p in parents.Properties())
            {
                if ((string)p.Value["type"] != "BONE") continue;
                var children = Descendants(root.transform).Where(t => t.name == p.Name).ToArray();
                string rigName = (string)p.Value["parent"], boneName = (string)p.Value["bone"];
                var parentRig = root.transform.Find(rigName);
                var rigs = parentRig == null ? new Transform[0] : new[] { parentRig };
                var bones = rigs.Length == 1 ? Descendants(rigs[0]).Where(t => t.name == boneName && t.GetComponent<Renderer>() == null).ToArray() : new Transform[0];
                if (children.Length == 1 && bones.Length == 1 && children[0] != bones[0] && !bones[0].IsChildOf(children[0]))
                { children[0].SetParent(bones[0], true); warnings.Add("Restored explicit BONE parent, world pose preserved: " + p.Name + " -> " + rigName + "/" + boneName); }
                else warnings.Add("BONE parent unresolved, imported world placement preserved: " + p.Name);
            }
        }
        static void MapTransforms(Transform oldRoot, Transform newRoot, Dictionary<Object, Object> map, JArray warnings)
        {
            var names = Descendants(newRoot).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.ToArray());
            foreach (var ot in Descendants(oldRoot))
            {
                if (map.ContainsKey(ot)) continue;
                string path = Relative(ot, oldRoot);
                Transform nt = newRoot.Find(path);
                // Unique-name fallback is explicit and logged; ambiguous bones never select an arbitrary match.
                if (nt == null && names.TryGetValue(ot.name, out var found) && found.Length == 1)
                { nt = found[0]; warnings.Add("Unique-name transform fallback: " + path + " -> " + Relative(nt, newRoot)); }
                if (nt == null) continue;
                map[ot] = nt; map[ot.gameObject] = nt.gameObject;
            }
        }
        static bool Allowed(Component c)
        {
            Type t = c.GetType(); string n = t.FullName ?? "";
            return n.StartsWith("nadena.dev.modular_avatar.", StringComparison.Ordinal) ||
                n.StartsWith("VRC.SDK3.Dynamics.PhysBone.", StringComparison.Ordinal) ||
                typeof(IConstraint).IsAssignableFrom(t);
        }
        static IEnumerable<KeyValuePair<string, Object>> References(Component c)
        {
            var so = new SerializedObject(c); var p = so.GetIterator();
            while (p.Next(true)) if (p.propertyType == SerializedPropertyType.ObjectReference &&
                p.propertyPath != "m_GameObject" && p.propertyPath != "m_Script" && !p.propertyPath.StartsWith("m_Prefab"))
                yield return new KeyValuePair<string, Object>(p.propertyPath, p.objectReferenceValue);
        }
        static List<Component> CopySafeComponents(GameObject source, GameObject result, Dictionary<Object, Object> map, JArray skipped, JArray warnings)
        {
            var plans = new Dictionary<Component, GameObject>();
            foreach (var c in source.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform || c is Renderer || c is MeshFilter) continue;
                string path = Relative(c.transform, source.transform);
                if (!Allowed(c)) { skipped.Add(new JObject { ["owner"] = path, ["type"] = c.GetType().FullName, ["reason"] = "Not in explicit MA/PhysBone/IConstraint allowlist" }); continue; }
                if (!map.TryGetValue(c.gameObject, out var target))
                { skipped.Add(new JObject { ["owner"] = path, ["type"] = c.GetType().FullName, ["reason"] = "No unique destination owner" }); continue; }
                plans[c] = (GameObject)target;
            }
            // Fixed point: a component referring to an unmappable component must also be skipped.
            bool changed;
            do
            {
                changed = false;
                foreach (var c in plans.Keys.ToArray())
                {
                    var bad = References(c).Where(p => p.Value != null && !AssetDatabase.Contains(p.Value) &&
                        !map.ContainsKey(p.Value) && (!(p.Value is Component rc) || !plans.ContainsKey(rc))).ToArray();
                    if (bad.Length == 0) continue;
                    skipped.Add(new JObject { ["owner"] = Relative(c.transform, source.transform), ["type"] = c.GetType().FullName,
                        ["reason"] = "Unmapped temporary-prefab references; component not copied", ["properties"] = new JArray(bad.Select(p => p.Key + " -> " + p.Value.name)) });
                    plans.Remove(c); changed = true;
                }
            } while (changed);
            var added = new List<Component>();
            foreach (var p in plans)
            {
                var nc = p.Value.AddComponent(p.Key.GetType());
                if (nc == null) throw new InvalidOperationException("Cannot add mapped component: " + p.Key.GetType().FullName);
                map[p.Key] = nc;
                added.Add(nc);
            }
            foreach (var p in plans)
            {
                Component dst = (Component)map[p.Key]; var from = new SerializedObject(p.Key); var to = new SerializedObject(dst);
                var prop = from.GetIterator(); bool enter = true;
                while (prop.Next(enter))
                {
                    enter = false;
                    if (prop.propertyPath == "m_GameObject" || prop.propertyPath == "m_Script" ||
                        prop.propertyPath == "m_CorrespondingSourceObject" || prop.propertyPath.StartsWith("m_Prefab")) continue;
                    to.CopyFromSerializedProperty(prop);
                }
                // Remap the pending serialized references BEFORE the first Apply/OnValidate callback.
                prop = to.GetIterator();
                while (prop.Next(true))
                {
                    if (prop.propertyType != SerializedPropertyType.ObjectReference || prop.objectReferenceValue == null ||
                        prop.propertyPath == "m_GameObject" || prop.propertyPath == "m_Script") continue;
                    Object old = prop.objectReferenceValue;
                    if (map.TryGetValue(old, out var replacement)) prop.objectReferenceValue = replacement;
                    else if (!AssetDatabase.Contains(old)) throw new InvalidOperationException("Unexpected unmapped reference after preflight: " + prop.propertyPath);
                }
                to.ApplyModifiedPropertiesWithoutUndo();
            }
            warnings.Add("Copied " + plans.Count + " safely mapped MA/PhysBone/constraint components; skipped components are listed separately.");
            return added;
        }

        static void BindMaterials(Renderer[] renderers, Dictionary<Renderer, string> originalNames, Renderer[] old,
            Dictionary<Object, Object> map, JObject manifest, JArray errors)
        {
            var slotNames = manifest["meshMaterials"] as JObject; var assetNames = manifest["materialAssets"] as JObject;
            var indexed = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/YaokiSubWork/EvilFallFullSet/Material" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m != null && !indexed.ContainsKey(m.name)) indexed[m.name] = m;
            }
            foreach (var r in renderers)
            {
                Mesh mesh = GetMesh(r); int count = mesh == null ? r.sharedMaterials.Length : mesh.subMeshCount;
                Renderer previous = old.FirstOrDefault(o => map.TryGetValue(o, out var n) && n == r);
                string originalName = originalNames[r];
                JArray names = (slotNames?[originalName] ?? slotNames?[r.name]) as JArray;
                var assigned = new Material[count]; var imported = r.sharedMaterials;
                for (int i = 0; i < count; i++)
                {
                    string name = names != null && i < names.Count ? (string)names[i] : null;
                    string path = name == null ? null : (string)assetNames?[name];
                    if (!string.IsNullOrEmpty(path)) assigned[i] = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (assigned[i] == null && name != null) indexed.TryGetValue(name, out assigned[i]);
                    string canonicalName = name == null ? null : name.Replace(" (Blender preview)", "");
                    if (canonicalName == "MainTrans") canonicalName = "EvillFallTrans FaceMask V2";
                    if (canonicalName == "Namelight") canonicalName = "EvilFall FaceMask V2 Name";
                    if (canonicalName == "MainUVTrains") canonicalName = "EvillFallTrans Blade";
                    if (assigned[i] == null && canonicalName != null) indexed.TryGetValue(canonicalName, out assigned[i]);
                    if (assigned[i] == null && names == null && previous != null && i < previous.sharedMaterials.Length) assigned[i] = previous.sharedMaterials[i];
                    if (assigned[i] == null && i < imported.Length && imported[i] != null) indexed.TryGetValue(imported[i].name, out assigned[i]);
                    if (assigned[i] == null) errors.Add("Unresolved material: " + r.name + " slot " + i + " (" + name + ")");
                }
                r.sharedMaterials = assigned;
            }
        }
        static void ApplyManifestDefaults(Renderer[] renderers, Dictionary<Renderer, string> originalNames, JObject manifest, JArray errors)
        {
            foreach (var r in renderers)
            {
                string n = originalNames[r];
                if (manifest["defaultActive"]?[n] != null) r.gameObject.SetActive((bool)manifest["defaultActive"][n]);
                if (!(r is SkinnedMeshRenderer sm) || sm.sharedMesh == null) continue;
                if (manifest["meshCounts"]?[n]?["shapeKeys"] is JArray expected && expected.Count != sm.sharedMesh.blendShapeCount)
                    errors.Add("Shape count mismatch: " + n + " imported " + sm.sharedMesh.blendShapeCount + "; manifest " + expected.Count);
                if (!(manifest["blendShapeValues"]?[n] is JObject values)) continue;
                foreach (var v in values.Properties())
                {
                    int index = sm.sharedMesh.GetBlendShapeIndex(v.Name);
                    if (index < 0) errors.Add("Missing exported shape: " + n + "/" + v.Name);
                    else sm.SetBlendShapeWeight(index, (float)v.Value);
                }
            }
        }
        static JObject SkeletonSummary(GameObject root, JObject manifest)
        {
            var rigs = new JArray();
            if (manifest["rigs"] is JArray requested && requested.Count > 0)
            {
                // A bone may have the same name as another rig (EvilFallBlade/PussyToyMain).
                // Only direct children of the imported model are rig objects; descendant-name search is incorrect.
                var rigObjects = new HashSet<Transform>(requested.Select(r => root.transform.Find((string)r["name"])).Where(t => t != null));
                foreach (JObject r in requested)
                {
                    string n = (string)r["name"]; var direct = root.transform.Find(n);
                    var found = direct == null ? new Transform[0] : new[] { direct };
                    var paths = new JArray(); var missing = new JArray();
                    if (found.Length == 1 && r["boneNames"] is JArray names)
                    {
                        foreach (string name in names.Values<string>())
                        {
                            var match = Descendants(found[0]).Where(t => t != found[0] && t.name == name && t.GetComponent<Renderer>() == null &&
                                !HasOtherRigAncestor(t, found[0], rigObjects)).ToArray();
                            if (match.Length == 1) paths.Add(Relative(match[0], root.transform));
                            else missing.Add(name + ": matches=" + match.Length);
                        }
                    }
                    else missing.Add("Exact audited bone_names unavailable or rig root ambiguous; count not guessed from descendants");
                    rigs.Add(new JObject { ["name"] = n, ["matches"] = found.Length, ["path"] = found.Length == 1 ? Relative(found[0], root.transform) : "<unresolved>",
                        ["count"] = paths.Count, ["bonePaths"] = paths, ["missingNames"] = missing,
                        ["expected"] = r["boneCount"]?.DeepClone() });
                }
            }
            else
            {
                throw new InvalidOperationException("An explicit rigs manifest and audited bone_names are required; no guessed 431-bone assertion");
            }
            return new JObject { ["rigs"] = rigs, ["inferredSkeletonTransformCount"] = rigs.Sum(r => (int)r["count"]),
                ["uniqueSMRBones"] = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.bones).Where(t => t != null).Distinct().Count(),
                ["method"] = "Audited per-rig bone_names; meshes, rig objects and nested rigs excluded" };
        }
        static bool HasOtherRigAncestor(Transform t, Transform own, HashSet<Transform> rigRoots)
        {
            for (var p = t.parent; p != null && p != own; p = p.parent) if (rigRoots.Contains(p)) return true;
            return false;
        }
        static JObject MeasureRigPaths(GameObject root, JArray rigSpecs)
        {
            var rigs = new JArray();
            foreach (JObject spec in rigSpecs)
            {
                string path = (string)spec["path"];
                Transform t = root.transform.Find(path);
                var records = new JArray();
                foreach (string bonePath in ((JArray)spec["bonePaths"]).Values<string>().OrderBy(x => x, StringComparer.Ordinal))
                {
                    Transform b = root.transform.Find(bonePath);
                    records.Add(new JObject { ["path"] = bonePath, ["found"] = b != null,
                        ["parent"] = b == null || b.parent == null ? null : Relative(b.parent, root.transform),
                        ["position"] = b == null ? null : JArray.FromObject(new[] { b.localPosition.x, b.localPosition.y, b.localPosition.z }),
                        ["rotation"] = b == null ? null : JArray.FromObject(new[] { b.localRotation.x, b.localRotation.y, b.localRotation.z, b.localRotation.w }),
                        ["scale"] = b == null ? null : JArray.FromObject(new[] { b.localScale.x, b.localScale.y, b.localScale.z }) });
                }
                rigs.Add(new JObject { ["path"] = path, ["found"] = t != null,
                    ["count"] = records.Count(b => (bool)b["found"]), ["expected"] = spec["expected"],
                    ["missingAtImport"] = spec["missingAtImport"], ["bones"] = records });
            }
            return new JObject { ["rigs"] = rigs, ["inferredSkeletonTransformCount"] = rigs.Sum(r => (int)r["count"]),
                ["uniqueSMRBones"] = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.bones).Where(t => t != null).Distinct().Count() };
        }
        static JObject Validate(GameObject root, int expectedMeshes, int expectedBones, JObject skeleton, JArray errors)
        {
            Renderer[] rr = MeshRenderers(root); int nullBones = 0, nullMaterials = 0, missingScripts = 0;
            var allTransforms = new HashSet<Transform>(Descendants(root.transform));
            foreach (var r in rr)
            {
                if (GetMesh(r) == null) errors.Add("Missing mesh: " + r.name);
                nullMaterials += r.sharedMaterials.Count(m => m == null);
                if (r is SkinnedMeshRenderer sm)
                {
                    nullBones += sm.bones.Count(b => b == null);
                    if (sm.rootBone == null) errors.Add("Null rootBone: " + sm.name);
                    if (sm.bones.Any(b => b != null && !allTransforms.Contains(b))) errors.Add("External/non-prefab bone: " + sm.name);
                    if (sm.sharedMesh != null && sm.bones.Length != sm.sharedMesh.bindposes.Length) errors.Add("Bone/bindpose count mismatch: " + sm.name);
                }
            }
            foreach (var t in Descendants(root.transform)) missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            int bones = (int)skeleton["inferredSkeletonTransformCount"];
            int rigsFound = ((JArray)skeleton["rigs"]).Count(r => (bool)r["found"]);
            foreach (var rig in (JArray)skeleton["rigs"])
                if (!(bool)rig["found"] || rig["expected"] == null || (int)rig["count"] != (int)rig["expected"] || ((JArray)rig["missingAtImport"]).Count != 0)
                    errors.Add("Rig bone identities/count incomplete: " + (string)rig["path"]);
            if (rr.Length != expectedMeshes) errors.Add("Mesh count: " + rr.Length + "; expected " + expectedMeshes);
            if (rigsFound != 9 || bones != expectedBones) errors.Add("Actual rig/bone count: " + rigsFound + "/" + bones + "; expected 9/" + expectedBones);
            if (nullBones != 0 || nullMaterials != 0 || missingScripts != 0) errors.Add("Null bones/materials/missing scripts: " + nullBones + "/" + nullMaterials + "/" + missingScripts);
            return new JObject { ["meshCount"] = rr.Length, ["expectedMeshCount"] = expectedMeshes, ["skeletonBoneCount"] = bones,
                ["expectedBoneCount"] = expectedBones, ["rigCount"] = rigsFound, ["nullBones"] = nullBones, ["nullMaterials"] = nullMaterials, ["missingScripts"] = missingScripts };
        }
        static JArray DescribeMeshes(GameObject root)
        {
            return new JArray(MeshRenderers(root).Select(r =>
            {
                Mesh m = GetMesh(r); var shapes = new JArray(); string meshGuid = ""; long meshLocalID = 0;
                if (m != null) AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m, out meshGuid, out meshLocalID);
                if (m != null) for (int i = 0; i < m.blendShapeCount; i++) shapes.Add(new JObject { ["name"] = m.GetBlendShapeName(i),
                    ["frames"] = m.GetBlendShapeFrameCount(i), ["defaultWeight"] = r is SkinnedMeshRenderer s ? s.GetBlendShapeWeight(i) : 0 });
                return new JObject { ["path"] = Relative(r.transform, root.transform), ["meshName"] = m == null ? null : m.name,
                    ["meshGUID"] = meshGuid, ["meshLocalID"] = meshLocalID, ["activeSelf"] = r.gameObject.activeSelf,
                    ["meshAsset"] = m == null ? null : AssetDatabase.GetAssetPath(m), ["vertices"] = m == null ? 0 : m.vertexCount,
                    ["submeshes"] = m == null ? 0 : m.subMeshCount, ["materials"] = new JArray(r.sharedMaterials.Select(x => x == null ? null : AssetDatabase.GetAssetPath(x))),
                    ["boneCount"] = r is SkinnedMeshRenderer sm ? sm.bones.Length : 0,
                    ["rootBone"] = r is SkinnedMeshRenderer rs && rs.rootBone != null ? Relative(rs.rootBone, root.transform) : null,
                    ["orderedBones"] = r is SkinnedMeshRenderer bs ? new JArray(bs.bones.Select(b => b == null ? null : Relative(b, root.transform))) : new JArray(),
                    ["blendShapeCount"] = m == null ? 0 : m.blendShapeCount, ["blendShapes"] = shapes };
            }));
        }
        static void ValidateLocalComponentReferences(GameObject root, JArray errors)
        {
            var local = new HashSet<Object>(root.GetComponentsInChildren<Component>(true).Where(c => c != null).Cast<Object>());
            foreach (var t in Descendants(root.transform)) local.Add(t.gameObject);
            foreach (var c in root.GetComponentsInChildren<Component>(true).Where(c => c != null && Allowed(c)))
                foreach (var p in References(c))
                    if (p.Value != null && !AssetDatabase.Contains(p.Value) && !local.Contains(p.Value))
                        errors.Add("Saved component references external temporary scene: " + Relative(c.transform, root.transform) + "/" + p.Key);
        }
        static JObject WriteBakedGeometry(GameObject root, string prefabPath, Dictionary<string, string> aliases)
        {
            string path = Path.Combine(Project, Folder, "unity_baked_geometry.json");
            var summaries = new JArray();
            using (var stream = new StreamWriter(path, false, new UTF8Encoding(false)))
            using (var w = new Newtonsoft.Json.JsonTextWriter(stream))
            {
                w.Formatting = Newtonsoft.Json.Formatting.None;
                w.WriteStartObject();
                w.WritePropertyName("prefabPath"); w.WriteValue(prefabPath);
                w.WritePropertyName("coordinateSpace"); w.WriteValue("Unity world metres; BakeMesh(useScale:false) vertices transformed by renderer.localToWorldMatrix; no guessed Blender axis conversion");
                w.WritePropertyName("meshes"); w.WriteStartObject();
                foreach (Renderer r in MeshRenderers(root).OrderBy(x => x.name, StringComparer.Ordinal))
                {
                    Mesh temporary = null;
                    try
                    {
                        Mesh mesh = GetMesh(r);
                        if (r is SkinnedMeshRenderer sm)
                        {
                            temporary = new Mesh(); sm.BakeMesh(temporary, false); mesh = temporary;
                        }
                        if (mesh == null) throw new InvalidOperationException("Cannot bake missing mesh: " + r.name);
                        Vector3[] vertices = mesh.vertices; Matrix4x4 matrix = r.localToWorldMatrix;
                        Vector3 lo = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                        Vector3 hi = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                        string sourceName = aliases.TryGetValue(r.name, out var original) ? original : r.name;
                        w.WritePropertyName(sourceName); w.WriteStartObject();
                        w.WritePropertyName("rendererPath"); w.WriteValue(Relative(r.transform, root.transform));
                        w.WritePropertyName("vertexCount"); w.WriteValue(vertices.Length);
                        w.WritePropertyName("activeSelf"); w.WriteValue(r.gameObject.activeSelf);
                        w.WritePropertyName("localToWorld"); w.WriteStartArray();
                        for (int row = 0; row < 4; row++) { w.WriteStartArray(); for (int col = 0; col < 4; col++) w.WriteValue((double)matrix[row, col]); w.WriteEndArray(); }
                        w.WriteEndArray();
                        w.WritePropertyName("verticesWorld"); w.WriteStartArray();
                        foreach (Vector3 local in vertices)
                        {
                            Vector3 v = matrix.MultiplyPoint3x4(local); lo = Vector3.Min(lo, v); hi = Vector3.Max(hi, v);
                            w.WriteStartArray(); w.WriteValue((double)v.x); w.WriteValue((double)v.y); w.WriteValue((double)v.z); w.WriteEndArray();
                        }
                        w.WriteEndArray();
                        var summary = new JObject { ["sourceName"] = sourceName, ["vertexCount"] = vertices.Length,
                            ["minWorld"] = JArray.FromObject(new[] { lo.x, lo.y, lo.z }), ["maxWorld"] = JArray.FromObject(new[] { hi.x, hi.y, hi.z }) };
                        w.WritePropertyName("bbox"); summary.WriteTo(w); w.WriteEndObject(); summaries.Add(summary);
                    }
                    finally { if (temporary != null) Object.DestroyImmediate(temporary); }
                }
                w.WriteEndObject(); w.WriteEndObject();
            }
            string hash;
            using (var h = SHA256.Create()) using (var file = File.OpenRead(path)) hash = BitConverter.ToString(h.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
            return new JObject { ["path"] = path, ["SHA256"] = hash, ["meshCount"] = summaries.Count, ["bboxes"] = summaries };
        }
        static void WriteReport(JObject report)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportFile));
            report["utc"] = DateTime.UtcNow.ToString("O");
            File.WriteAllText(ReportFile, report.ToString(Newtonsoft.Json.Formatting.Indented), new UTF8Encoding(false));
        }
    }
}
#endif
