using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using Triturbo.FaceTrackingFramework.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

[assembly: ExportsPlugin(typeof(KipfelFaceEmoTrackingCompatibility))]

// Build-only integration: source controllers, FaceEmo projects and scenes stay editable.
public sealed class KipfelFaceEmoTrackingCompatibility : Plugin<KipfelFaceEmoTrackingCompatibility>
{
    internal const string SwitchParameter = "FacialExpressionsDisabled";
    internal const string BridgeLayer = "Kipfel FaceEmo / Face Tracking";
    public override string QualifiedName => "local.kipfel.face-emo-tracking-compatibility";
    public override string DisplayName => "Kipfel FaceEmo / Face Tracking";

    protected override void Configure()
    {
        InPhase(BuildPhase.Generating).BeforePlugin("triturbo.facetrackingframework")
            .Run("Separate expression and face tracking channels", context =>
        {
            var modifier = context.AvatarRootObject.GetComponentInChildren<EmoteLayersModifierComponent>();
            // Apply-on-Play runs from the avatar root's Awake, before child
            // MonoBehaviours finish activation. isActiveAndEnabled can still be
            // false there even for an enabled modifier on an active object.
            if (modifier == null || !modifier.enabled || modifier.m_Parameter != SwitchParameter) return;
            Debug.Log($"[Kipfel FaceEmo/FT] Preparing {context.AvatarRootObject.name}; modifier enabled={modifier.enabled}, activeInHierarchy={modifier.gameObject.activeInHierarchy}, isActiveAndEnabled={modifier.isActiveAndEnabled}.");
            var merges = context.AvatarRootObject.GetComponentsInChildren<ModularAvatarMergeAnimator>();
            var faceEmo = merges.FirstOrDefault(m => m.layerType == VRCAvatarDescriptor.AnimLayerType.FX &&
                m.animator is AnimatorController controller &&
                controller.parameters.Any(p => p.name == "CN_FORCE_BYPASS_ENABLE"));
            if (faceEmo == null) return;
            var tracking = merges.Where(m => m.layerType == VRCAvatarDescriptor.AnimLayerType.FX &&
                m.transform.IsChildOf(modifier.transform.parent)).ToArray();
            if (tracking.Length == 0) return;
            var body = context.AvatarRootTransform.Find(modifier.m_MeshPath)?.GetComponent<SkinnedMeshRenderer>();
            if (body == null || body.sharedMesh == null) return;
            // Kipfel's patched mesh appends the device channels after this marker.
            // Native eye_joy / eye_close etc. remain expression channels.
            int marker = body.sharedMesh.GetBlendShapeIndex("-----FT-----");
            if (marker < 0) throw new InvalidOperationException("The Kipfel face tracking mesh has no FT channel marker.");
            var installation = context.GetState<Installation>();
            installation.MeshPath = modifier.m_MeshPath;
            installation.TrackingProperties = Enumerable.Range(marker + 1, body.sharedMesh.blendShapeCount - marker - 1)
                .Select(i => "blendShape." + body.sharedMesh.GetBlendShapeName(i)).ToHashSet();
            // FT's eye reset clips also animate native expression channels, even
            // when EyeTrackingEnable is off. Expressions must run after those clips.
            // The later pass removes device-channel resets from FaceEmo's default
            // face, while preserving explicitly authored expression curves.
            faceEmo.layerPriority = Math.Max(faceEmo.layerPriority, tracking.Max(m => m.layerPriority) + 1);
            installation.LayerNames = new HashSet<string>(
                ((AnimatorController)faceEmo.animator).layers.Select(l => l.name));
            var repairedMesh = RepairNativeToothMorph(body.sharedMesh);
            if (repairedMesh != body.sharedMesh)
            {
                body.sharedMesh = repairedMesh;
                context.AssetSaver.SaveAsset(repairedMesh);
            }
        });

        InPhase(BuildPhase.Transforming).AfterPlugin("nadena.dev.modular-avatar")
            .Run("Connect the Disable Expressions switch to FaceEmo", context =>
            {
                var fx = context.AvatarDescriptor.baseAnimationLayers
                    .FirstOrDefault(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX)
                    .animatorController as AnimatorController;
                var installation = context.GetState<Installation>();
                if (fx != null && installation.LayerNames != null) Apply(fx, installation);
            });
    }

    private sealed class Installation
    {
        public Installation() { }
        public HashSet<string> LayerNames;
        public string MeshPath;
        public HashSet<string> TrackingProperties;
    }

    internal static Mesh RepairNativeToothMorph(Mesh mesh)
    {
        int toothIndex = mesh.GetBlendShapeIndex("mouth_tooth");
        if (toothIndex < 0 || mesh.GetBlendShapeIndex("-----FT-----") < 0) return mesh;
        var sourcePath = AssetDatabase.GUIDToAssetPath("7078d86f15dd32d4bb4540ac2ff9e0ac");
        var original = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<Mesh>()
            .FirstOrDefault(m => m.name == "Body");
        if (original == null || original.vertexCount != mesh.vertexCount ||
            !original.bindposes.SequenceEqual(mesh.bindposes) || !original.triangles.SequenceEqual(mesh.triangles))
        {
            Debug.LogWarning("[Kipfel FaceEmo/FT] Cannot match the original Body mesh; skipped the native tooth correction.");
            return mesh;
        }
        int originalTooth = original.GetBlendShapeIndex("mouth_tooth");
        if (originalTooth < 0 || original.GetBlendShapeFrameCount(originalTooth) != 1 ||
            mesh.GetBlendShapeFrameCount(toothIndex) != 1 ||
            original.GetBlendShapeFrameWeight(originalTooth, 0) != 100 ||
            mesh.GetBlendShapeFrameWeight(toothIndex, 0) != 100) return mesh;

        var nativeDelta = new Vector3[mesh.vertexCount];
        var currentDelta = new Vector3[mesh.vertexCount];
        original.GetBlendShapeFrameVertices(originalTooth, 0, nativeDelta, null, null);
        mesh.GetBlendShapeFrameVertices(toothIndex, 0, currentDelta, null, null);
        // Only repair the unchanged original morph. A corrected or customized
        // tooth morph must not receive the compensation a second time.
        if (!nativeDelta.SequenceEqual(currentDelta)) return mesh;
        var nativeVertices = original.vertices;
        var ftVertices = mesh.vertices;
        var shiftedTeeth = Enumerable.Range(0, mesh.vertexCount).Where(i =>
            nativeDelta[i].sqrMagnitude > 1e-12f &&
            (nativeVertices[i] - ftVertices[i]).sqrMagnitude > 1e-12f).ToArray();
        if (shiftedTeeth.Length == 0) return mesh;

        // FT bakes a different rest position into these teeth. The stock reveal
        // morph still contains the original displacement, which moves them below
        // the chin. Compensate that displacement only while mouth_tooth is used;
        // the neutral mesh and every device-driven FT morph remain unchanged.
        var result = UnityEngine.Object.Instantiate(mesh);
        result.name = mesh.name + " (native teeth compatible)";
        result.ClearBlendShapes();
        var deltaVertices = new Vector3[mesh.vertexCount];
        var deltaNormals = new Vector3[mesh.vertexCount];
        var deltaTangents = new Vector3[mesh.vertexCount];
        for (int shape = 0; shape < mesh.blendShapeCount; shape++)
        for (int frame = 0; frame < mesh.GetBlendShapeFrameCount(shape); frame++)
        {
            mesh.GetBlendShapeFrameVertices(shape, frame, deltaVertices, deltaNormals, deltaTangents);
            if (shape == toothIndex)
                foreach (int vertex in shiftedTeeth)
                    deltaVertices[vertex] += nativeVertices[vertex] - ftVertices[vertex];
            result.AddBlendShapeFrame(mesh.GetBlendShapeName(shape), mesh.GetBlendShapeFrameWeight(shape, frame),
                deltaVertices, deltaNormals, deltaTangents);
        }
        Debug.Log($"[Kipfel FaceEmo/FT] Corrected {shiftedTeeth.Length} vertices in the native tooth expression.");
        return result;
    }

    private static void Apply(AnimatorController fx, Installation installation)
    {
        var faceEmoLayerNames = installation.LayerNames;
        var parameters = fx.parameters;
        if (fx.layers.Any(l => l.name == BridgeLayer) ||
            !parameters.Any(p => p.name == SwitchParameter) ||
            !parameters.Any(p => p.name == "CN_FORCE_BYPASS_ENABLE") ||
            !parameters.Any(p => p.name == "EyeTrackingEnable") ||
            !parameters.Any(p => p.name == "VisemesEnable")) return;

        var player = fx.layers.FirstOrDefault(l => l.name.EndsWith("[ USER EDIT ] FACE EMOTE PLAYER", StringComparison.Ordinal));
        if (player == null) return;
        var control = fx.layers.Single(l => l.stateMachine.states.Any(s =>
            s.state.name == "DISABLE" && s.state.transitions.Any(t =>
                t.conditions.Any(c => c.parameter == "CN_FORCE_BYPASS_ENABLE")))).stateMachine;
        var disabled = control.states.Select(s => s.state).Single(s => s.name == "DISABLE");
        var enabled = control.states.Select(s => s.state).Single(s => s.name == "ENABLE");

        // OR the existing public bypass input with Triturbo's switch. Other gimmicks
        // retain ownership of CN_FORCE_BYPASS_ENABLE; we never write that parameter.
        AddFlag(Transition(disabled, enabled), fx, SwitchParameter, true);
        foreach (var transition in enabled.transitions.Where(t => t.destinationState == disabled))
            AddFlag(transition, fx, SwitchParameter, false);

        // Triturbo neutralizes the original expression controller's tracking
        // controls before MA merges FaceEmo. Apply the same policy to FaceEmo:
        // tracking owns eyes/lip sync, including when gesture expressions change.
        // Only the DEFAULT FACE layer snapshots every blendshape on the mesh.
        // An authored expression can deliberately animate FT shapes (eye direction,
        // tongue, etc.); filtering those motions would erase the expression itself.
        var defaultClips = new HashSet<AnimationClip>();
        foreach (var layer in fx.layers.Where(l => faceEmoLayerNames.Contains(l.name) &&
                     l.name == "[ USER EDIT ] DEFAULT FACE"))
            CollectDefaults(layer.stateMachine);
        var rewrittenMotions = new Dictionary<Motion, Motion>();
        foreach (var layer in fx.layers.Where(l => faceEmoLayerNames.Contains(l.name)))
        {
            RelinquishTracking(layer.stateMachine);
            RemoveDefaultTrackingCurves(layer.stateMachine);
        }

        // A WD-off bypass can still contain reset curves for the face tracking
        // blendshapes. Mute only FaceEmo's rendering layers while tracking owns the
        // face, and restore their original weights when expressions are enabled.
        string[] renderedLayers = {
            "[ USER EDIT ] DEFAULT FACE", "[ USER EDIT ] FACE EMOTE PLAYER",
            "[ USER EDIT ] FACE EMOTE OVERRIDE", "BLINK", "MOUTH MORPH CANCELLER"
        };
        var controlledLayers = fx.layers.Select((layer, index) => new { layer, index })
            .Where(x => faceEmoLayerNames.Contains(x.layer.name) && renderedLayers.Contains(x.layer.name))
            .ToArray();
        if (controlledLayers.Any(x => x.index == 0))
            throw new InvalidOperationException("FaceEmo must not be the FX base layer for this face tracking integration.");

        var machine = new AnimatorStateMachine { name = BridgeLayer };
        var empty = new AnimationClip { name = "Face Tracking Bridge (no curves)" };
        var idle = machine.AddState("FaceEmo and face tracking");
        machine.defaultState = idle;
        idle.motion = empty;
        idle.writeDefaultValues = false;
        AddFlag(Any(machine, idle), fx, SwitchParameter, false);
        SetWeights(idle, true);

        var trackingOnly = machine.AddState("Face tracking only");
        trackingOnly.motion = empty;
        trackingOnly.writeDefaultValues = false;
        AddFlag(Any(machine, trackingOnly), fx, SwitchParameter, true);
        SetWeights(trackingOnly, false);

        fx.AddLayer(new AnimatorControllerLayer { name = BridgeLayer, stateMachine = machine, defaultWeight = 1 });
        Debug.Log($"[Kipfel FaceEmo/FT] Connected {SwitchParameter}; {controlledLayers.Length} FaceEmo animation layers switch with face tracking.");

        void SetWeights(AnimatorState state, bool restore)
        {
            foreach (var target in controlledLayers)
            {
                var controlBehaviour = state.AddStateMachineBehaviour<VRCAnimatorLayerControl>();
                controlBehaviour.playable = VRC_AnimatorLayerControl.BlendableLayer.FX;
                controlBehaviour.layer = target.index;
                controlBehaviour.goalWeight = restore ? target.layer.defaultWeight : 0;
                controlBehaviour.blendDuration = 0;
            }
        }

        void CollectDefaults(AnimatorStateMachine stateMachine)
        {
            foreach (var state in stateMachine.states) CollectDefaultMotion(state.state.motion);
            foreach (var child in stateMachine.stateMachines) CollectDefaults(child.stateMachine);
        }

        void CollectDefaultMotion(Motion motion)
        {
            if (motion is AnimationClip clip) defaultClips.Add(clip);
            else if (motion is BlendTree tree)
                foreach (var child in tree.children) CollectDefaultMotion(child.motion);
        }

        void RemoveDefaultTrackingCurves(AnimatorStateMachine stateMachine)
        {
            foreach (var state in stateMachine.states) state.state.motion = RewriteMotion(state.state.motion);
            foreach (var child in stateMachine.stateMachines) RemoveDefaultTrackingCurves(child.stateMachine);
        }

        Motion RewriteMotion(Motion motion)
        {
            if (motion == null) return null;
            if (rewrittenMotions.TryGetValue(motion, out var cached)) return cached;
            Motion result = motion;
            if (motion is AnimationClip clip && defaultClips.Contains(clip))
            {
                var bindings = AnimationUtility.GetCurveBindings(clip).Where(b =>
                    b.type == typeof(SkinnedMeshRenderer) && b.path == installation.MeshPath &&
                    installation.TrackingProperties.Contains(b.propertyName)).ToArray();
                if (bindings.Length > 0)
                {
                    // Clips may be shared with the original FX or tracking layers.
                    // Change only FaceEmo's copy, never the imported clip or project.
                    var copy = UnityEngine.Object.Instantiate(clip);
                    copy.name = clip.name + " (expression channels)";
                    foreach (var binding in bindings) AnimationUtility.SetEditorCurve(copy, binding, null);
                    result = copy;
                }
            }
            else if (motion is BlendTree tree)
            {
                var children = tree.children;
                bool changed = false;
                for (int i = 0; i < children.Length; i++)
                {
                    var replacement = RewriteMotion(children[i].motion);
                    changed |= replacement != children[i].motion;
                    children[i].motion = replacement;
                }
                if (changed)
                {
                    var copy = UnityEngine.Object.Instantiate(tree);
                    copy.children = children;
                    result = copy;
                }
            }
            rewrittenMotions[motion] = result;
            return result;
        }
    }

    private static void RelinquishTracking(AnimatorStateMachine machine)
    {
        foreach (var behaviour in machine.behaviours.Concat(machine.states.SelectMany(s => s.state.behaviours)))
        {
            if (!(behaviour is VRC_AnimatorTrackingControl tracking)) continue;
            tracking.trackingEyes = VRC_AnimatorTrackingControl.TrackingType.NoChange;
            tracking.trackingMouth = VRC_AnimatorTrackingControl.TrackingType.NoChange;
        }
        foreach (var child in machine.stateMachines) RelinquishTracking(child.stateMachine);
    }

    private static AnimatorStateTransition Any(AnimatorStateMachine machine, AnimatorState destination)
    {
        var transition = machine.AddAnyStateTransition(destination);
        ConfigureTransition(transition);
        return transition;
    }

    private static AnimatorStateTransition Transition(AnimatorState source, AnimatorState destination)
    {
        var transition = source.AddTransition(destination);
        ConfigureTransition(transition);
        return transition;
    }

    private static void ConfigureTransition(AnimatorStateTransition transition)
    {
        transition.hasExitTime = false;
        transition.duration = 0;
        transition.canTransitionToSelf = false;
    }

    private static void AddFlag(AnimatorStateTransition transition, AnimatorController fx, string name, bool value)
    {
        var type = fx.parameters.Single(p => p.name == name).type;
        if (type == AnimatorControllerParameterType.Bool)
            transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, name);
        else if (type == AnimatorControllerParameterType.Float)
            transition.AddCondition(value ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, 0.5f, name);
        else throw new InvalidOperationException($"Unexpected parameter type for {name}: {type}");
    }
}
