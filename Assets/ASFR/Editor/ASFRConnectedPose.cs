using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.Constraint.Components;

namespace ASFR
{
    // Freeze local-space samples in a stationary bank; skin joints always follow those samples.
    static class ASFRConnectedPose
    {
        public static Dictionary<Transform, Transform> Build(ASFRRigBinding marker, BuildContext ctx, Dictionary<Transform, Transform> captures)
        {
            var avatarRoot = ctx.AvatarRootTransform;
            var container = new GameObject("Connected Frozen Skeleton").transform;
            container.SetParent(marker.transform, false);
            ConfigureSampleBanks(captures, avatarRoot);
            var copies = new Dictionary<Transform, Transform>();
            var animator = avatarRoot.GetComponent<Animator>();
            var hips = animator && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            Transform Copy(Transform source)
            {
                if (source == avatarRoot)
                    return container;
                if (!source || !source.IsChildOf(avatarRoot))
                    throw new InvalidOperationException("ASFR: connected skeleton source is outside the avatar.");
                if (copies.TryGetValue(source, out var existing))
                    return existing;
                var parent = Copy(source.parent);
                var target = new GameObject("Joint_" + copies.Count.ToString("D4") + "_" + source.name).transform;
                target.SetParent(parent, false);
                target.localPosition = source.localPosition;
                target.localRotation = source.localRotation;
                target.localScale = source.localScale;
                copies.Add(source, target);
                return target;
            }

            foreach (var pair in captures)
                Copy(pair.Key);
            var result = new Dictionary<Transform, Transform>();
            foreach (var pair in captures)
            {
                var source = pair.Key;
                var output = copies[source];
                // Only hips and the highest captured joint in an independent rig can translate.
                // All descendants inherit their parent's position, retaining their local joint offsets.
                bool hasCapturedAncestor = false;
                for (var p = source.parent; p && p != avatarRoot; p = p.parent)
                    if (captures.ContainsKey(p))
                    {
                        hasCapturedAncestor = true;
                        break;
                    }

                if (source == hips || !hasCapturedAncestor)
                {
                    var follow = output.gameObject.AddComponent<VRCParentConstraint>();
                    follow.Sources.Add(new VRCConstraintSource(pair.Value, 1));
                    follow.SolveInLocalSpace = true;
                    follow.ZeroConstraint();
                    follow.FreezeToWorld = false;
                    follow.ApplyConfigurationChanges();
                }
                else
                {
                    var follow = output.gameObject.AddComponent<VRCRotationConstraint>();
                    follow.Sources.Add(new VRCConstraintSource(pair.Value, 1));
                    follow.SolveInLocalSpace = true;
                    follow.ZeroConstraint();
                    follow.FreezeToWorld = false;
                    follow.ApplyConfigurationChanges();
                }

                result.Add(source, output);
            }

            // Detect the displayed joint position, which can differ from the live tracked rig now.
            if (marker.freezeAnchors != null)
                foreach (var anchor in marker.freezeAnchors)
                    if (anchor && result.TryGetValue(anchor, out var joint))
                        foreach (var receiver in anchor.GetComponentsInChildren<VRCContactReceiver>(true))
                            if (receiver.transform.parent == anchor && receiver.gameObject.name == "ASFR Joint Detector")
                            {
                                receiver.rootTransform = joint;
                                receiver.ApplyConfigurationChanges();
                            }

            if (marker.statueGrabAnchor && marker.statueOrigin)
            {
                WorldLock(ctx, marker.statueGrabAnchor);
                var carry = container.gameObject.AddComponent<VRCParentConstraint>();
                carry.Sources.Add(new VRCConstraintSource(marker.statueOrigin, 1));
                carry.ZeroConstraint();
                carry.ApplyConfigurationChanges();
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (marker.statueCameraMount && head && result.TryGetValue(head, out var displayedHead))
                {
                    var cameraFollow = marker.statueCameraMount.GetComponent<VRCParentConstraint>();
                    cameraFollow.Sources.Clear();
                    cameraFollow.Sources.Add(new VRCConstraintSource(displayedHead, 1));
                    cameraFollow.ZeroConstraint();
                    cameraFollow.ApplyConfigurationChanges();
                }
            }
            else
                WorldLock(ctx, container);
            Debug.Log("ASFR: created " + result.Count + " sample-driven skin joints; stationary local-pose samples preserve progressive freezing during rigid carrying.");
            return result;
        }

        static void ConfigureSampleBanks(Dictionary<Transform, Transform> captures, Transform avatarRoot)
        {
            // The bank never moves in world space. FreezeToWorld on a sample therefore
            // retains its LOCAL coordinates as well. No skinned bone is world-pinned.
            foreach (var bank in captures.Values.Select(t => t.parent).Distinct())
            {
                var stationary = bank.gameObject.AddComponent<VRCParentConstraint>();
                stationary.Sources.Add(new VRCConstraintSource(avatarRoot, 1));
                stationary.ZeroConstraint();
                stationary.FreezeToWorld = true;
                stationary.RebakeOffsetsWhenUnfrozen = false;
                stationary.ApplyConfigurationChanges();
            }

            foreach (var pair in captures)
            {
                var sample = pair.Value;
                sample.localPosition = pair.Key.localPosition;
                sample.localRotation = pair.Key.localRotation;
                sample.localScale = Vector3.one;
                var constraint = sample.GetComponent<VRCParentConstraint>();
                constraint.SolveInLocalSpace = true;
                constraint.ZeroConstraint();
                constraint.FreezeToWorld = false;
                constraint.RebakeOffsetsWhenUnfrozen = false;
                constraint.ApplyConfigurationChanges();
            }
        }

        static void WorldLock(BuildContext ctx, Transform container)
        {
            var anchor = container.gameObject.AddComponent<VRCParentConstraint>();
            anchor.Sources.Add(new VRCConstraintSource(ctx.AvatarRootTransform, 1));
            anchor.ZeroConstraint();
            anchor.FreezeToWorld = false;
            anchor.ApplyConfigurationChanges();
            var binding = AnimationUtility.GetAnimatableBindings(container.gameObject, ctx.AvatarRootObject).Single(b => b.type == typeof(VRCParentConstraint) && b.propertyName.IndexOf("FreezeToWorld", StringComparison.OrdinalIgnoreCase) >= 0);
            var services = ctx.Extension<AnimatorServicesContext>();
            var controller = services.ControllerContext.GetAllControllers().FirstOrDefault(c => c.Parameters.ContainsKey("ASFR/Frozen") && c.Parameters.ContainsKey("ASFR/Enabled"));
            if (controller == null)
                throw new InvalidOperationException("ASFR: controller for shared world lock is missing.");
            var layer = controller.AddLayer(new LayerPriority(1000), "ASFR Completed Pose World Lock");
            layer.DefaultWeight = 1;
            var machine = VirtualStateMachine.Create(services.ControllerContext.CloneContext, "Completed pose anchor");
            layer.StateMachine = machine;
            var live = VirtualClip.Create("ASFR Root Following");
            live.SetFloatCurve(binding, AnimationCurve.Constant(0, 1f / 30, 0));
            var held = VirtualClip.Create("ASFR Root World Locked");
            held.SetFloatCurve(binding, AnimationCurve.Constant(0, 1f / 30, 1));
            var a = machine.AddState("Following", live);
            var b = machine.AddState("World locked", held);
            machine.DefaultState = a;
            a.WriteDefaultValues = false;
            b.WriteDefaultValues = false;
            var freeze = VirtualStateTransition.Create();
            freeze.SetDestination(b);
            freeze.Duration = 0;
            freeze.ExitTime = null;
            freeze.Conditions = ImmutableList.Create(new AnimatorCondition{parameter = "ASFR/Enabled", mode = AnimatorConditionMode.If}, new AnimatorCondition{parameter = "ASFR/Frozen", mode = AnimatorConditionMode.If});
            a.Transitions = ImmutableList.Create(freeze);
            var reset = VirtualStateTransition.Create();
            reset.SetDestination(a);
            reset.Duration = 0;
            reset.ExitTime = null;
            reset.Conditions = ImmutableList.Create(new AnimatorCondition{parameter = "ASFR/Frozen", mode = AnimatorConditionMode.IfNot});
            b.Transitions = ImmutableList.Create(reset);
        }
    }
}
