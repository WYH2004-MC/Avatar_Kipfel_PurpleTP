// Editor-only generator. No custom MonoBehaviour is uploaded to VRChat.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.Dynamics;
using nadena.dev.modular_avatar.core;

namespace ASFR
{
    public partial class ASFRInstaller : EditorWindow
    {
        const string Enabled = "ASFR/Enabled", Frozen = "ASFR/Frozen", Style = "ASFR/Style";
        [SerializeField]
        VRCAvatarDescriptor avatar;
        [SerializeField]
        Transform mainArmature;
        [SerializeField]
        SkinnedMeshRenderer mainBody;
        [SerializeField]
        List<Material> overlayMaterials = new List<Material>{null};
        [SerializeField]
        float shellOffset = 0.0005f;
        [SerializeField]
        List<Material> preservedEffectMaterials = new List<Material>();
        [SerializeField]
        bool effectMaterialsInitialized;
        [SerializeField]
        List<SkinnedMeshRenderer> innerCoating = new List<SkinnedMeshRenderer>();
        [SerializeField]
        float seconds = 8;
        [SerializeField]
        bool addVisionOverlay;
        [SerializeField]
        bool addStatueCamera = true;
        [SerializeField]
        float statueEyeSeparation = .064f;
        [SerializeField]
        Texture2D visionTexture;
        [SerializeField]
        float visionStrength = .45f;
        Vector2 scroll;
        readonly List<SkinnedMeshRenderer> renderers = new List<SkinnedMeshRenderer>();
        [SerializeField]
        List<SkinnedMeshRenderer> clothing = new List<SkinnedMeshRenderer>();
        [SerializeField]
        List<SkinnedMeshRenderer> selected = new List<SkinnedMeshRenderer>();
        [MenuItem("Tools/ASFR/Install on avatar copy", false, 0)]
        static void Open()
        {
            GetWindow<ASFRInstaller>("ASFR Installer");
        }

        void Refresh()
        {
            renderers.Clear();
            selected.Clear();
            clothing.Clear();
            innerCoating.Clear();
            preservedEffectMaterials.Clear();
            effectMaterialsInitialized = false;
            mainBody = null;
            if (!avatar)
                return;
            renderers.AddRange(avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh));
            foreach (var r in renderers)
                selected.Add(r);
            mainArmature = avatar.transform.Find("Armature");
            var animator = avatar.GetComponent<Animator>();
            if (!mainArmature && animator && animator.isHuman)
            {
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips)
                    mainArmature = hips.parent == avatar.transform ? hips : hips.parent;
            }
        }

        void InitializeEffectMaterials()
        {
            if (effectMaterialsInitialized || !avatar)
                return;
            preservedEffectMaterials.Clear();
            foreach (var material in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m).Distinct())
            {
                string name = material.name.ToLowerInvariant().Replace("_", "").Replace(" ", "").Replace("-", "");
                if (name.Contains("blush") || name.Contains("faceeffect") || name.Contains("fakeshadow"))
                    preservedEffectMaterials.Add(material);
            }

            effectMaterialsInitialized = true;
        }

        static void Save(UnityEngine.Object o, string path)
        {
            AssetDatabase.CreateAsset(o, path);
        }

        static string Path(Transform t, Transform root)
        {
            return AnimationUtility.CalculateTransformPath(t, root);
        }

        static void Curve(AnimationClip clip, string path, Type type, string property, float from, float to, float length)
        {
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), AnimationCurve.Linear(0, from, length, to));
        }

        static void MaterialCurve(AnimationClip clip, string path, int slot, Material material)
        {
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(path, typeof(SkinnedMeshRenderer), "m_Materials.Array.data[" + slot + "]"), new[]{new ObjectReferenceKeyframe{time = 0, value = material}});
        }

        static AnimatorState State(AnimatorStateMachine sm, string name, AnimationClip clip)
        {
            var s = sm.AddState(name);
            s.motion = clip;
            s.writeDefaultValues = false;
            return s;
        }

        static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, string parameter, AnimatorConditionMode mode, float value = 0)
        {
            var t = from.AddTransition(to);
            t.duration = 0;
            t.hasExitTime = false;
            t.AddCondition(mode, value, parameter);
            return t;
        }

        static void Locomotion(AnimatorState state, bool freeze)
        {
            state.AddStateMachineBehaviour<VRCAnimatorLocomotionControl>().disableLocomotion = freeze;
        }

        static IEnumerable<string> ValidateSkin(SkinnedMeshRenderer renderer, Transform avatarRoot)
        {
            string label = Path(renderer.transform, avatarRoot);
            var bones = renderer.bones;
            if (bones.Length == 0)
            {
                yield return label + ": no skin bones are assigned.";
                yield break;
            }

            var used = new HashSet<int>();
            // Use all influences, including meshes with more than four per vertex.
            using (var weights = renderer.sharedMesh.GetAllBoneWeights())
                foreach (var weight in weights)
                    if (weight.weight > 0)
                        used.Add(weight.boneIndex);
            foreach (int index in used.OrderBy(i => i))
                if (index < 0 || index >= bones.Length || !bones[index])
                    yield return label + ": weighted bone slot " + index + " is missing. Repair this reference or deselect this mesh.";
            for (int index = 0; index < bones.Length; index++)
                if (bones[index] && !bones[index].IsChildOf(avatarRoot))
                    yield return label + ": bone slot " + index + " (" + bones[index].name + ") points outside this avatar. Repair the reference or deselect this mesh.";
            if (renderer.rootBone && !renderer.rootBone.IsChildOf(avatarRoot))
                yield return label + ": root bone points outside this avatar.";
        }

        static Transform[] CaptureBones(Transform avatarRoot, Transform armature)
        {
            return avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.bones.Concat(r.rootBone ? new[]{r.rootBone} : Array.Empty<Transform>())).Where(b => b && b.IsChildOf(armature)).Distinct().ToArray();
        }

        void Generate()
        {
            var errors = CheckSetup();
            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join("\n", errors));
            var originals = selected.Where(r => r && r.sharedMesh).Distinct().OrderBy(r => Path(r.transform, avatar.transform)).ToArray();
            if (!AssetDatabase.IsValidFolder("Assets/ASFRGenerated"))
                AssetDatabase.CreateFolder("Assets", "ASFRGenerated");
            string folder = "Assets/ASFRGenerated/v3_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            string folderGuid = AssetDatabase.CreateFolder("Assets/ASFRGenerated", folder.Substring(folder.LastIndexOf('/') + 1));
            if (string.IsNullOrEmpty(folderGuid))
                throw new InvalidOperationException("Could not create the generated asset folder.");
            bool originalWasActive = avatar.gameObject.activeSelf;
            GameObject copy = null;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            try
            {
                copy = Instantiate(avatar.gameObject, avatar.transform.parent);
                copy.name = avatar.name + " ASFR v3";
                Undo.RegisterCreatedObjectUndo(copy, "ASFR v3 copy");
                if (PrefabUtility.IsPartOfPrefabInstance(copy))
                    PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var root = copy.transform;
                var main = root.Find(Path(mainArmature, avatar.transform));
                // Explicit side mapping supports any mixture of single and double coatings.
                // 0 = original material culling, 1 = outer/front, 2 = inner/back.
                var shellPlan = originals.SelectMany(r => innerCoating.Contains(r) ? new[]{(source: r, side: 1), (source: r, side: 2)} : new[]{(source: r, side: 0)}).ToArray();
                var targets = shellPlan.Select(p => root.Find(Path(p.source.transform, avatar.transform)).GetComponent<SkinnedMeshRenderer>()).ToArray();
                var sources = CaptureBones(root, main);
                if (sources.Length == 0)
                    throw new InvalidOperationException("No skin bones found in the selected main armature.");
                var system = new GameObject("ASFR System");
                system.transform.SetParent(root, false);
                var proxyRoot = new GameObject("Pose Capture").transform;
                proxyRoot.SetParent(system.transform, false);
                var captures = new List<VRCParentConstraint>();
                var bindings = new List<EditorCurveBinding>();
                foreach (var source in sources)
                {
                    var proxy = new GameObject("MainBone_" + captures.Count.ToString("D4")).transform;
                    proxy.SetParent(proxyRoot, false);
                    proxy.SetPositionAndRotation(source.position, source.rotation);
                    var s = source.lossyScale;
                    var p = proxyRoot.lossyScale;
                    proxy.localScale = new Vector3(s.x / p.x, s.y / p.y, s.z / p.z);
                    var constraint = proxy.gameObject.AddComponent<VRCParentConstraint>();
                    constraint.Sources.Add(new VRCConstraintSource(source, 1));
                    constraint.ZeroConstraint();
                    constraint.FreezeToWorld = false;
                    constraint.RebakeOffsetsWhenUnfrozen = false;
                    constraint.ApplyConfigurationChanges();
                    captures.Add(constraint);
                    var candidates = AnimationUtility.GetAnimatableBindings(proxy.gameObject, copy).Where(b => b.type == typeof(VRCParentConstraint) && b.propertyName.IndexOf("FreezeToWorld", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                    if (candidates.Length != 1)
                        throw new InvalidOperationException("SDK FreezeToWorld binding unavailable.");
                    bindings.Add(candidates[0]);
                }

                var marker = system.AddComponent<ASFRRigBinding>();
                if (!marker)
                    throw new InvalidOperationException("Unity could not attach ASFRRigBinding. Replace the complete Assets/ASFR folder with the current package, including its Editor/ASFRRigBinding.cs compatibility placeholder, and wait for compilation.");
                marker.captures = captures.ToArray();
                marker.mainArmature = main;
                marker.overlaySources = targets;
                var overlays = new List<SkinnedMeshRenderer>();
                var visibility = new Dictionary<Transform, Transform>();
                int meshIndex = 0;
                foreach (var original in targets)
                {
                    bool inner = shellPlan[meshIndex].side == 2;
                    var go = new GameObject(inner ? "ASFR Inner Overlay" : "ASFR Overlay");
                    go.transform.SetParent(ASFRPlanarMesh.Gate(original.transform, root, system.transform, visibility), false);
                    var overlay = go.AddComponent<SkinnedMeshRenderer>();
                    EditorUtility.CopySerialized(original, overlay);
                    var mesh = Instantiate(original.sharedMesh);
                    mesh.name = original.sharedMesh.name + (inner ? " ASFR Inner Shell" : " ASFR Shell");
                    var vertices = mesh.vertices;
                    var normals = mesh.normals;
                    if (normals.Length != vertices.Length)
                    {
                        mesh.RecalculateNormals();
                        normals = mesh.normals;
                    }

                    var scale = original.transform.lossyScale;
                    float averageScale = (Mathf.Abs(scale.x) + Mathf.Abs(scale.y) + Mathf.Abs(scale.z)) / 3;
                    float expansion = shellOffset / Mathf.Max(.0001f, averageScale) * (inner ? -1 : 1);
                    for (int v = 0; v < vertices.Length; v++)
                        vertices[v] += normals[v] * expansion;
                    // Alpha-masked facial cards must not become solid coating geometry.
                    // Keep indices stable for all animation/material bindings and blendshapes.
                    var originalMaterials = original.sharedMaterials;
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        var sourceMaterial = originalMaterials.Length == 0 ? null : originalMaterials[Mathf.Min(sub, originalMaterials.Length - 1)];
                        if (sourceMaterial && preservedEffectMaterials.Contains(sourceMaterial))
                            mesh.SetIndices(Array.Empty<int>(), mesh.GetTopology(sub), sub, false);
                    }

                    mesh.vertices = vertices;
                    mesh.RecalculateBounds();
                    ASFRPlanarMesh.Rebase(mesh, root.worldToLocalMatrix * original.transform.localToWorldMatrix);
                    Save(mesh, folder + "/OverlayMesh_" + (meshIndex++) + ".asset");
                    overlay.sharedMesh = mesh;
                    overlay.rootBone = root;
                    overlay.updateWhenOffscreen = true;
                    overlay.sortingOrder = original.sortingOrder + 1;
                    overlay.localBounds = mesh.bounds;
                    overlays.Add(overlay);
                    go.SetActive(false);
                }

                marker.overlays = overlays.ToArray();
                marker.visibilitySources = visibility.Keys.ToArray();
                marker.visibilityGates = visibility.Values.ToArray();
                // Do NOT rebind renderers in the scene: MA must finish merging outfit rigs first.
                var clothingSources = clothing.Where(r => r).Distinct().Select(r => root.Find(Path(r.transform, avatar.transform)).GetComponent<SkinnedMeshRenderer>()).ToArray();
                float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
                foreach (var r in targets.Concat(clothingSources).Distinct())
                {
                    // Culling bounds are often intentionally huge on VRChat clothing.
                    // Calibrate against the actual posed surface, never renderer bounds.
                    var baked = new Mesh();
                    try
                    {
                        r.BakeMesh(baked, false);
                        foreach (var point in baked.vertices)
                        {
                            float y = root.InverseTransformPoint(r.transform.TransformPoint(point)).y;
                            if (float.IsNaN(y) || float.IsInfinity(y))
                                continue;
                            bottom = Mathf.Min(bottom, y);
                            top = Mathf.Max(top, y);
                        }
                    }
                    finally
                    {
                        DestroyImmediate(baked);
                    }
                }

                if (!float.IsInfinity(bottom) && top - bottom > .01f)
                {
                    // Include raised hands in the sweep, even when installed in a T-pose.
                    var humanoid = copy.GetComponent<Animator>();
                    if (humanoid && humanoid.isHuman)
                        foreach (var side in new[]{new[]{HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand}, new[]{HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand}})
                        {
                            var upper = humanoid.GetBoneTransform(side[0]);
                            var lower = humanoid.GetBoneTransform(side[1]);
                            var hand = humanoid.GetBoneTransform(side[2]);
                            if (upper && lower && hand)
                            {
                                var a = root.InverseTransformPoint(upper.position);
                                var b = root.InverseTransformPoint(lower.position);
                                var c = root.InverseTransformPoint(hand.position);
                                top = Mathf.Max(top, a.y + Vector3.Distance(a, b) + Vector3.Distance(b, c) + (top - bottom) * .12f);
                            }
                        }

                    float margin = (top - bottom) * .005f;
                    bottom -= margin;
                    top += margin;
                    Debug.Log("ASFR reveal calibrated from posed vertices: " + bottom.ToString("F3") + " to " + top.ToString("F3") + " avatar-local units over " + seconds + " seconds.", copy);
                }
                else
                    throw new InvalidOperationException("Could not determine avatar height from selected meshes.");
                var removalCopies = ASFRClothingDissolve.Create(root, system.transform, clothingSources, visibility, folder, bottom, top);
                marker.overlaySources = targets.Concat(clothingSources).ToArray();
                marker.overlays = overlays.Concat(removalCopies).ToArray();
                marker.clothingRemovalCopies = removalCopies;
                marker.visibilitySources = visibility.Keys.ToArray();
                marker.visibilityGates = visibility.Values.ToArray();
                var controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/ASFR.controller");
                controller.AddParameter(Enabled, AnimatorControllerParameterType.Bool);
                controller.AddParameter(Frozen, AnimatorControllerParameterType.Bool);
                controller.AddParameter(Style, AnimatorControllerParameterType.Int);
                ASFRDuration.AddParameters(controller, seconds);
                ASFRProgress.AddParameters(controller);
                var sm = controller.layers[0].stateMachine;
                var off = new AnimationClip{name = "Normal"};
                foreach (var binding in bindings)
                    AnimationUtility.SetEditorCurve(off, binding, AnimationCurve.Constant(0, 1f / 30, 0));
                foreach (var overlay in overlays)
                    Curve(off, Path(overlay.transform, root), typeof(GameObject), "m_IsActive", 0, 0, 1f / 30);
                ASFRClothingDissolve.Animate(off, root, removalCopies, bottom, top, 1f / 30, false, false);
                Save(off, folder + "/Normal.anim");
                var idle = State(sm, "Normal", off);
                sm.defaultState = idle;
                Locomotion(idle, false);
                for (int style = 0; style < overlayMaterials.Count; style++)
                {
                    string label = "Material " + (style + 1);
                    var rise = new AnimationClip{name = label + " Reveal"};
                    var full = new AnimationClip{name = label + " Frozen"};
                    foreach (var binding in bindings)
                    {
                        AnimationUtility.SetEditorCurve(rise, binding, AnimationCurve.Constant(0, ASFRDuration.ClipSeconds, 0));
                        AnimationUtility.SetEditorCurve(full, binding, AnimationCurve.Constant(0, 1f / 30, 1));
                    }

                    for (int index = 0; index < overlays.Count; index++)
                    {
                        var overlay = overlays[index];
                        var original = targets[index];
                        string path = Path(overlay.transform, root);
                        int queue = original.sharedMaterials.Where(m => m).Select(m => m.renderQueue).DefaultIfEmpty(2000).Max();
                        var material = ASFROverlayMaterial.Create(overlayMaterials[style], overlay.transform, root, bottom, top, queue);
                        if (shellPlan[index].side != 0)
                        {
                            // Outward/front and inward/back shells each draw one side only.
                            // This avoids doubling the opacity of a two-sided transparent material.
                            bool inner = shellPlan[index].side == 2;
                            if (material.HasProperty("_Cull"))
                                material.SetFloat("_Cull", inner ? 1 : 2);
                            if (inner)
                            {
                                if (material.HasProperty("_EnableOutlines"))
                                    material.SetFloat("_EnableOutlines", 0);
                                if (material.HasProperty("_OutlineWidth"))
                                    material.SetFloat("_OutlineWidth", 0);
                            }
                        }

                        Save(material, folder + "/Overlay_" + style + "_" + index + ".mat");
                        int slots = Mathf.Max(1, Mathf.Max(original.sharedMaterials.Length, overlay.sharedMesh.subMeshCount));
                        if (style == 0)
                            overlay.sharedMaterials = Enumerable.Repeat(material, slots).ToArray();
                        foreach (var clip in new[]{rise, full})
                        {
                            Curve(clip, path, typeof(GameObject), "m_IsActive", 1, 1, clip == rise ? ASFRDuration.ClipSeconds : 1f / 30);
                            for (int slot = 0; slot < slots; slot++)
                                MaterialCurve(clip, path, slot, material);
                        }

                        ASFROverlayMaterial.Reveal(material, overlay.transform, root, bottom, top, out string revealProperty, out float revealStart, out float revealEnd);
                        if (revealProperty == "material._DissolveParams.z")
                        {
                            ASFROverlayMaterial.AnimateLil(rise, overlay.transform, root, revealStart, revealEnd, ASFRDuration.ClipSeconds, false);
                            foreach (var clip in new[]{full})
                                ASFROverlayMaterial.AnimateLil(clip, overlay.transform, root, revealEnd, revealEnd, 1f / 30, false, true);
                        }
                        else
                        {
                            Curve(rise, path, typeof(SkinnedMeshRenderer), revealProperty, revealStart, revealEnd, ASFRDuration.ClipSeconds);
                            foreach (var clip in new[]{full})
                                Curve(clip, path, typeof(SkinnedMeshRenderer), revealProperty, revealEnd, revealEnd, 1f / 30);
                        }
                    }

                    ASFRClothingDissolve.Animate(rise, root, removalCopies, bottom, top, ASFRDuration.ClipSeconds, false, true);
                    ASFRClothingDissolve.Animate(full, root, removalCopies, bottom, top, 1f / 30, true, true);
                    foreach (var clip in new[]{rise, full})
                        Save(clip, folder + "/" + clip.name + ".anim");
                    var changing = State(sm, label + " Transforming", rise);
                    changing.timeParameter = ASFRProgress.Value;
                    changing.timeParameterActive = true;
                    Locomotion(changing, false);
                    var frozen = State(sm, label + " Frozen", full);
                    Locomotion(frozen, true);
                    foreach (var state in new[]{changing, frozen})
                    {
                        Transition(state, idle, ASFRProgress.Active, AnimatorConditionMode.IfNot);
                        Transition(state, idle, Style, AnimatorConditionMode.NotEqual, style);
                    }

                    var start = Transition(idle, changing, ASFRProgress.Active, AnimatorConditionMode.If);
                    start.AddCondition(AnimatorConditionMode.Equals, style, Style);
                    start.AddCondition(AnimatorConditionMode.IfNot, 0, Frozen);
                    var late = Transition(idle, frozen, ASFRProgress.Active, AnimatorConditionMode.If);
                    late.AddCondition(AnimatorConditionMode.Equals, style, Style);
                    late.AddCondition(AnimatorConditionMode.If, 0, Frozen);
                    Transition(changing, frozen, Frozen, AnimatorConditionMode.If);
                    Transition(frozen, changing, Frozen, AnimatorConditionMode.IfNot);
                }

                ASFRProgressiveFreeze.Build(controller, marker, root, bottom, top, folder);
                var clothingOverlays = overlays.Where((r, i) => clothingSources.Contains(targets[i])).ToArray();
                var clothingMenu = ASFRClothing.Build(controller, root, clothingSources, clothingOverlays, removalCopies, folder);
                if (mainBody)
                {
                    var body = root.Find(Path(mainBody.transform, avatar.transform)).GetComponent<SkinnedMeshRenderer>();
                    var bodyOverlays = overlays.Where((r, i) => targets[i] == body).ToArray();
                    ASFRClothing.BuildBodyOverride(controller, root, body, bodyOverlays, folder);
                }

                var merge = system.AddComponent<ModularAvatarMergeAnimator>();
                merge.animator = controller;
                merge.pathMode = MergeAnimatorPathMode.Absolute;
                merge.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
                merge.matchAvatarWriteDefaults = true;
                var parameters = system.AddComponent<ModularAvatarParameters>();
                parameters.parameters.Add(new ParameterConfig{nameOrPrefix = Enabled, syncType = ParameterSyncType.Bool, saved = false, hasExplicitDefaultValue = true});
                parameters.parameters.Add(new ParameterConfig{nameOrPrefix = Frozen, syncType = ParameterSyncType.Bool, saved = false, hasExplicitDefaultValue = true});
                parameters.parameters.Add(new ParameterConfig{nameOrPrefix = Style, syncType = ParameterSyncType.Int, saved = true, hasExplicitDefaultValue = true});
                parameters.parameters.Add(new ParameterConfig{nameOrPrefix = ASFRClothing.Parameter, syncType = ParameterSyncType.Int, saved = true, hasExplicitDefaultValue = true});
                ASFRContactTrigger.Build(controller, root, system.transform, parameters, folder);
                var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                menu.controls.Add(new VRCExpressionsMenu.Control{name = "Transform / Reset", type = VRCExpressionsMenu.Control.ControlType.Toggle, parameter = new VRCExpressionsMenu.Control.Parameter{name = Enabled}, value = 1});
                for (int style = 0; style < overlayMaterials.Count; style++)
                    menu.controls.Add(new VRCExpressionsMenu.Control{name = overlayMaterials[style].name, icon = ASFRMenuIcons.MaterialPreview(overlayMaterials[style], folder, style), type = VRCExpressionsMenu.Control.ControlType.Toggle, parameter = new VRCExpressionsMenu.Control.Parameter{name = Style}, value = style});
                var options = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                var interactions = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                ASFRContactTrigger.AddMenu(interactions);
                var transformationMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                ASFRDuration.Build(controller, parameters, transformationMenu, seconds, folder);
                options.controls.Add(new VRCExpressionsMenu.Control{name = "Transformation", type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = transformationMenu});
                options.controls.Add(new VRCExpressionsMenu.Control{name = "Clothing", type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = clothingMenu});
                if (addVisionOverlay)
                {
                    var visionMenu = ASFRVision.Build(controller, root, system.transform, parameters, visionTexture, visionStrength, folder);
                    options.controls.Add(new VRCExpressionsMenu.Control{name = "Vision", type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = visionMenu});
                }

                if (addStatueCamera)
                    ASFRStatueView.Build(controller, marker, root, parameters, interactions, folder, statueEyeSeparation);
                Save(interactions, folder + "/ExtraInteractions Menu.asset");
                options.controls.Add(new VRCExpressionsMenu.Control{name = "ExtraInteractions", type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = interactions});
                ASFRProgress.Build(controller, parameters, transformationMenu, folder);
                Save(transformationMenu, folder + "/Transformation Menu.asset");
                Save(options, folder + "/Options Menu.asset");
                menu.controls.Add(new VRCExpressionsMenu.Control{name = "Options", type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = options});
                Save(menu, folder + "/ASFR Menu.asset");
                var wrapper = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                wrapper.controls.Add(new VRCExpressionsMenu.Control{name = "ASFR", type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = menu});
                Save(wrapper, folder + "/Menu Installer.asset");
                ASFRMenuIcons.Decorate(wrapper, folder);
                ASFRLanguage.TranslateMenu(wrapper);
                system.AddComponent<ModularAvatarMenuInstaller>().menuToAppend = wrapper;
                ValidateMenu(wrapper, new HashSet<VRCExpressionsMenu>());
                AssetDatabase.SaveAssets();
                Undo.RecordObject(avatar.gameObject, "Disable original avatar");
                avatar.gameObject.SetActive(false);
                copy.SetActive(true);
                Selection.activeGameObject = copy;
                Undo.CollapseUndoOperations(group);
                EditorUtility.DisplayDialog(ASFRLanguage.T("ASFR overlay created"), ASFRLanguage.T("Created ") + copy.name + ASFRLanguage.T(".\n\nAssets: ") + folder + ASFRLanguage.T("\n\nChoose a TransformationMat under ASFR before transforming. Contacts, clothing and installed vision features are in Options. Run NDMF Play Mode or Build & Test before uploading."), "OK");
            }
            catch
            {
                if (copy)
                    DestroyImmediate(copy);
                if (avatar)
                    avatar.gameObject.SetActive(originalWasActive);
                // This exact, uniquely created folder belongs only to the failed attempt.
                if (AssetDatabase.GUIDToAssetPath(folderGuid) == folder)
                    AssetDatabase.DeleteAsset(folder);
                throw;
            }
        }
    }
}
