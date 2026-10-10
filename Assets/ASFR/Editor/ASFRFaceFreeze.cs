using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;

namespace ASFR
{
    static class ASFRFaceFreeze
    {
        public const string Frozen = "ASFR/HeadFrozen";
        public static void HeadState(AnimatorState state, bool frozen)
        {
            var driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            driver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter{name = Frozen, value = frozen ? 1 : 0});
            var tracking = state.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
            tracking.trackingEyes = frozen ? VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Animation : VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Tracking;
            tracking.trackingMouth = tracking.trackingEyes;
        }

        static HashSet<string> FacialBindings(Transform root)
        {
            var result = new HashSet<string>();
            var animator = root.GetComponent<Animator>();
            var head = animator && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (!head)
                return result;
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = r.sharedMesh;
                if (!mesh || mesh.blendShapeCount == 0)
                    continue;
                var bones = r.bones;
                var facial = new bool[mesh.vertexCount];
                using (var counts = mesh.GetBonesPerVertex())
                using (var weights = mesh.GetAllBoneWeights())
                {
                    int offset = 0;
                    for (int v = 0; v < counts.Length; v++)
                    {
                        float sum = 0;
                        for (int b = 0; b < counts[v]; b++)
                        {
                            var w = weights[offset++];
                            if (w.boneIndex < bones.Length && bones[w.boneIndex] && bones[w.boneIndex].IsChildOf(head))
                                sum += w.weight;
                        }

                        facial[v] = sum > .5f;
                    }
                }

                string path = AnimationUtility.CalculateTransformPath(r.transform, root);
                var delta = new Vector3[mesh.vertexCount];
                for (int shape = 0; shape < mesh.blendShapeCount; shape++)
                {
                    int frames = mesh.GetBlendShapeFrameCount(shape);
                    if (frames == 0)
                        continue;
                    mesh.GetBlendShapeFrameVertices(shape, frames - 1, delta, null, null);
                    int changed = 0, face = 0;
                    for (int v = 0; v < delta.Length; v++)
                        if (delta[v].sqrMagnitude > 1e-12f)
                        {
                            changed++;
                            if (facial[v])
                                face++;
                        }

                    if (changed > 0 && face >= changed * .8f)
                        result.Add(path + "|blendShape." + mesh.GetBlendShapeName(shape));
                }
            }

            return result;
        }

        static IEnumerable<VirtualClip> Clips(VirtualMotion motion)
        {
            if (motion is VirtualClip clip)
                yield return clip;
            if (motion is VirtualBlendTree tree)
                foreach (var child in tree.Children)
                    foreach (var c in Clips(child.Motion))
                        yield return c;
        }

        static IEnumerable<VirtualTransitionBase> Transitions(VirtualStateMachine machine)
        {
            foreach (var t in machine.AnyStateTransitions)
                yield return t;
            foreach (var state in machine.States)
                if (state.State != null)
                    foreach (var t in state.State.Transitions)
                        yield return t;
            foreach (var item in machine.StateMachineTransitions)
                foreach (var t in item.Value)
                    yield return t;
            foreach (var child in machine.StateMachines)
                foreach (var t in Transitions(child.StateMachine))
                    yield return t;
        }

        static VirtualMotion BufferTree(VirtualMotion motion, Func<string, string> buffer)
        {
            if (!(motion is VirtualBlendTree tree))
                return motion;
            var copy = VirtualBlendTree.Create(tree.Name + " ASFR buffered");
            copy.BlendType = tree.BlendType;
            copy.BlendParameter = buffer(tree.BlendParameter);
            copy.BlendParameterY = buffer(tree.BlendParameterY);
            copy.MinThreshold = tree.MinThreshold;
            copy.MaxThreshold = tree.MaxThreshold;
            copy.UseAutomaticThresholds = tree.UseAutomaticThresholds;
            copy.NormalizedBlendValues = tree.NormalizedBlendValues;
            copy.Children = tree.Children.Select(c => new VirtualBlendTree.VirtualChildMotion{Motion = BufferTree(c.Motion, buffer), CycleOffset = c.CycleOffset, DirectBlendParameter = buffer(c.DirectBlendParameter), Mirror = c.Mirror, Threshold = c.Threshold, Position = c.Position, TimeScale = c.TimeScale}).ToImmutableList();
            return copy;
        }

        static VirtualStateTransition Go(VirtualState destination, bool frozen)
        {
            var t = VirtualStateTransition.Create();
            t.SetDestination(destination);
            t.Duration = 0;
            t.ExitTime = null;
            t.Conditions = ImmutableList.Create(new AnimatorCondition{parameter = Frozen, mode = frozen ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot});
            return t;
        }

        public static void Build(BuildContext ctx)
        {
            var face = FacialBindings(ctx.AvatarRootTransform);
            var services = ctx.Extension<AnimatorServicesContext>();
            int modified = 0, controllerId = 0;
            foreach (var controller in services.ControllerContext.GetAllControllers().ToArray())
            {
                var layers = controller.Layers.Where(l => !l.Name.StartsWith("ASFR", StringComparison.Ordinal) && l.StateMachine != null && l.StateMachine.AllStates().Any(s => Clips(s.Motion).Any(c => c.GetFloatCurveBindings().Any(b => b.type == typeof(SkinnedMeshRenderer) && face.Contains(b.path + "|" + b.propertyName))))).ToArray();
                if (layers.Length == 0)
                    continue;
                controller.Parameters = controller.Parameters.SetItem(Frozen, new AnimatorControllerParameter{name = Frozen, type = AnimatorControllerParameterType.Bool});
                string prefix = "ASFR/FaceController" + (controllerId++) + "/";
                string rate = prefix + "PlaybackRate";
                controller.Parameters = controller.Parameters.SetItem(rate, new AnimatorControllerParameter{name = rate, type = AnimatorControllerParameterType.Float, defaultFloat = 1});
                var inputs = new Dictionary<string, string>();
                var speedInputs = new Dictionary<string, string>();
                string Buffer(string name)
                {
                    if (string.IsNullOrEmpty(name) || name.StartsWith("ASFR/", StringComparison.Ordinal) || !controller.Parameters.TryGetValue(name, out var source))
                        return name;
                    if (inputs.TryGetValue(name, out var cached))
                        return cached;
                    if (source.type == AnimatorControllerParameterType.Trigger)
                        throw new InvalidOperationException("ASFR face freeze cannot buffer Trigger parameter: " + name);
                    string alias = prefix + "Input/" + inputs.Count;
                    inputs.Add(name, alias);
                    controller.Parameters = controller.Parameters.SetItem(alias, new AnimatorControllerParameter{name = alias, type = source.type, defaultBool = source.defaultBool, defaultFloat = source.defaultFloat, defaultInt = source.defaultInt});
                    return alias;
                }

                foreach (var layer in layers)
                {
                    foreach (var state in layer.StateMachine.AllStates())
                    {
                        state.Motion = BufferTree(state.Motion, Buffer);
                        state.TimeParameter = Buffer(state.TimeParameter);
                        state.CycleOffsetParameter = Buffer(state.CycleOffsetParameter);
                        state.MirrorParameter = Buffer(state.MirrorParameter);
                        if (string.IsNullOrEmpty(state.SpeedParameter))
                            state.SpeedParameter = rate;
                        else
                        {
                            string original = state.SpeedParameter;
                            string alias = prefix + "Speed/" + speedInputs.Count;
                            if (!speedInputs.TryGetValue(original, out var existing))
                            {
                                speedInputs.Add(original, alias);
                                controller.Parameters = controller.Parameters.SetItem(alias, new AnimatorControllerParameter{name = alias, type = AnimatorControllerParameterType.Float, defaultFloat = controller.Parameters.TryGetValue(original, out var p) ? p.defaultFloat : 1});
                            }
                            else
                                alias = existing;
                            state.SpeedParameter = alias;
                        }
                    }

                    foreach (var transition in Transitions(layer.StateMachine))
                    {
                        transition.Conditions = transition.Conditions.Select(c => new AnimatorCondition{mode = c.mode, threshold = c.threshold, parameter = Buffer(c.parameter)}).Append(new AnimatorCondition{mode = AnimatorConditionMode.IfNot, parameter = Frozen}).ToImmutableList();
                    }

                    Debug.Log("ASFR face freeze: " + controller.Name + " / " + layer.Name);
                    modified++;
                }

                var layerBuffer = controller.AddLayer(new LayerPriority(-1000), "ASFR Face Input Hold");
                layerBuffer.DefaultWeight = 1;
                layerBuffer.StateMachine = VirtualStateMachine.Create(services.ControllerContext.CloneContext, "Face input latch");
                var machine = layerBuffer.StateMachine;
                var a = machine.AddState("Sample A");
                var b = machine.AddState("Sample B");
                var hold = machine.AddState("Hold expression");
                machine.DefaultState = a;
                foreach (var state in new[]{a, b, hold})
                    state.WriteDefaultValues = false;
                foreach (var state in new[]{a, b})
                {
                    var driver = ScriptableObject.CreateInstance<VRCAvatarParameterDriver>();
                    foreach (var input in inputs.Concat(speedInputs))
                        driver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter{name = input.Value, source = input.Key, type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Copy});
                    driver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter{name = rate, value = 1});
                    state.Behaviours = state.Behaviours.Add(driver);
                }

                var stopped = ScriptableObject.CreateInstance<VRCAvatarParameterDriver>();
                stopped.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter{name = rate, value = 0});
                foreach (var input in speedInputs)
                    stopped.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter{name = input.Value, value = 0});
                hold.Behaviours = hold.Behaviours.Add(stopped);
                a.Transitions = ImmutableList.Create(Go(hold, true), Go(b, false));
                b.Transitions = ImmutableList.Create(Go(hold, true), Go(a, false));
                hold.Transitions = ImmutableList.Create(Go(a, false));
            }

            if (modified == 0)
                Debug.LogWarning("ASFR: no animator layers driving head-weighted facial blendshapes were found. Only native eye/mouth tracking controls will stop at head freeze.");
            Debug.Log("ASFR: buffered facial inputs and playback for " + modified + " animator layers; native eye/mouth tracking is controlled by the head freeze state.");
        }
    }
}
