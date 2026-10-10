using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.Constraint.Components;

namespace ASFR
{
    static class ASFRProgressiveFreeze
    {
        static readonly HumanBodyBones[] Joints = {HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes, HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand};
        public static int GroupFor(Transform source, Transform[] anchors)
        {
            for (var p = source; p; p = p.parent)
            {
                int i = Array.IndexOf(anchors, p);
                if (i >= 0)
                    return i;
            }

            int best = 0;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < anchors.Length; i++)
                if (anchors[i])
                {
                    float d = (source.position - anchors[i].position).sqrMagnitude;
                    if (d < distance)
                    {
                        distance = d;
                        best = i;
                    }
                }

            return best;
        }

        static void FreezeCurve(AnimationClip clip, VRCParentConstraint capture, Transform root, float value)
        {
            var candidates = AnimationUtility.GetAnimatableBindings(capture.gameObject, root.gameObject).Where(b => b.type == typeof(VRCParentConstraint) && b.propertyName.IndexOf("FreezeToWorld", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            if (candidates.Length != 1)
                throw new InvalidOperationException("Cannot animate progressive freeze for " + capture.name);
            AnimationUtility.SetEditorCurve(clip, candidates[0], AnimationCurve.Constant(0, 1f / 30, value));
        }

        public static void Build(AnimatorController controller, ASFRRigBinding marker, Transform root, float bottom, float top, string folder)
        {
            var animator = root.GetComponent<Animator>();
            if (!animator || !animator.isHuman)
                throw new InvalidOperationException("Progressive joint freezing currently requires a humanoid avatar.");
            controller.AddParameter(ASFRFaceFreeze.Frozen, AnimatorControllerParameterType.Bool);
            var anchors = Joints.Select(j => animator.GetBoneTransform(j)).Where(t => t).Distinct().ToArray();
            if (anchors.Length == 0)
                throw new InvalidOperationException("No humanoid joints found for progressive freezing.");
            int existing = root.GetComponentsInChildren<VRCContactSender>(true).Length + root.GetComponentsInChildren<VRCContactReceiver>(true).Length;
            if (existing + anchors.Length + 1 > 256)
                throw new InvalidOperationException("ASFR progressive freeze needs " + (anchors.Length + 1) + " contacts, but this avatar already has " + existing + ". Total must stay at or below 256.");
            var scale = root.lossyScale;
            float sx = Mathf.Abs(scale.x), sy = Mathf.Abs(scale.y), sz = Mathf.Abs(scale.z);
            float height = top - bottom + .2f;
            if (sx < .0001f || sy < .0001f || sz < .0001f || height * sy > 5.9f)
                throw new InvalidOperationException("Avatar scale exceeds the progressive contact volume range.");
            string tag = "ASFRPlane" + Guid.NewGuid().ToString("N");
            var plane = new GameObject("Progressive Freeze Volume").transform;
            plane.SetParent(marker.transform, false);
            float half = height * .5f;
            plane.localPosition = new Vector3(0, bottom - half, 0);
            var sender = plane.gameObject.AddComponent<VRCContactSender>();
            sender.shapeType = ContactBase.ShapeType.Box;
            sender.size = new Vector3(5.9f / sx, height, 5.9f / sz);
            sender.collisionTags = new List<string>{tag};
            plane.gameObject.SetActive(false);
            string planePath = AnimationUtility.CalculateTransformPath(plane, root);
            // Drive the upper box face with the very same timeline as the shader plane.
            foreach (var clip in controller.animationClips.Distinct())
            {
                bool normal = clip.name == "Normal";
                bool rise = clip.name.EndsWith(" Reveal", StringComparison.Ordinal);
                float length = Mathf.Max(1f / 30, clip.length);
                float from = normal || rise ? bottom : top;
                float to = normal ? bottom : top;
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(planePath, typeof(Transform), "m_LocalPosition.y"), AnimationCurve.Linear(0, from - half, length, to - half));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(planePath, typeof(GameObject), "m_IsActive"), AnimationCurve.Constant(0, length, normal ? 0 : 1));
            }

            marker.freezeAnchors = anchors;
            marker.freezeRepresentatives = new VRCParentConstraint[anchors.Length];
            var groups = new List<VRCParentConstraint>[anchors.Length];
            for (int i = 0; i < groups.Length; i++)
                groups[i] = new List<VRCParentConstraint>();
            foreach (var capture in marker.captures)
                if (capture && capture.Sources.Count > 0 && capture.Sources[0].SourceTransform)
                    groups[GroupFor(capture.Sources[0].SourceTransform, anchors)].Add(capture);
            for (int i = 0; i < anchors.Length; i++)
            {
                // Keep a curve template even for a joint only used by an outfit merged later.
                if (groups[i].Count == 0)
                {
                    var proxy = new GameObject("JointTemplate_" + i).transform;
                    proxy.SetParent(marker.captures[0].transform.parent, false);
                    proxy.SetPositionAndRotation(anchors[i].position, anchors[i].rotation);
                    var ss = anchors[i].lossyScale;
                    var ps = proxy.parent.lossyScale;
                    proxy.localScale = new Vector3(ss.x / ps.x, ss.y / ps.y, ss.z / ps.z);
                    var c = proxy.gameObject.AddComponent<VRCParentConstraint>();
                    c.Sources.Add(new VRCConstraintSource(anchors[i], 1));
                    c.ZeroConstraint();
                    c.FreezeToWorld = false;
                    c.ApplyConfigurationChanges();
                    groups[i].Add(c);
                    marker.captures = marker.captures.Concat(new[]{c}).ToArray();
                }

                marker.freezeRepresentatives[i] = groups[i][0];
                var detector = new GameObject("ASFR Joint Detector").transform;
                detector.SetParent(anchors[i], false);
                var receiver = detector.gameObject.AddComponent<VRCContactReceiver>();
                receiver.shapeType = ContactBase.ShapeType.Sphere;
                receiver.radius = .0005f / Mathf.Max(.0001f, Mathf.Abs(detector.lossyScale.x));
                receiver.allowSelf = true;
                receiver.allowOthers = false;
                receiver.localOnly = false;
                receiver.receiverType = ContactReceiver.ReceiverType.Constant;
                receiver.collisionTags = new List<string>{tag};
                string parameter = "ASFR/JointReached/" + i;
                receiver.parameter = parameter;
                controller.AddParameter(parameter, AnimatorControllerParameterType.Bool);
                controller.AddLayer("ASFR Freeze " + anchors[i].name);
                var layers = controller.layers;
                int layer = layers.Length - 1;
                layers[layer].defaultWeight = 1;
                controller.layers = layers;
                var machine = controller.layers[layer].stateMachine;
                var live = new AnimationClip{name = "Joint " + i + " Tracking"};
                var held = new AnimationClip{name = "Joint " + i + " Frozen"};
                foreach (var capture in groups[i])
                {
                    FreezeCurve(live, capture, root, 0);
                    FreezeCurve(held, capture, root, 1);
                }

                AssetDatabase.CreateAsset(live, folder + "/Joint_" + i + "_Tracking.anim");
                AssetDatabase.CreateAsset(held, folder + "/Joint_" + i + "_Frozen.anim");
                var tracking = machine.AddState("Tracking");
                tracking.motion = live;
                tracking.writeDefaultValues = false;
                machine.defaultState = tracking;
                var frozen = machine.AddState("Frozen until reset");
                frozen.motion = held;
                frozen.writeDefaultValues = false;
                if (anchors[i] == animator.GetBoneTransform(HumanBodyBones.Head))
                {
                    ASFRFaceFreeze.HeadState(tracking, false);
                    ASFRFaceFreeze.HeadState(frozen, true);
                }

                var reached = tracking.AddTransition(frozen);
                reached.hasExitTime = false;
                reached.duration = 0;
                reached.AddCondition(AnimatorConditionMode.If, 0, "ASFR/Enabled");
                reached.AddCondition(AnimatorConditionMode.If, 0, parameter);
                var complete = tracking.AddTransition(frozen);
                complete.hasExitTime = false;
                complete.duration = 0;
                complete.AddCondition(AnimatorConditionMode.If, 0, "ASFR/Enabled");
                complete.AddCondition(AnimatorConditionMode.If, 0, "ASFR/Frozen");
                var reset = frozen.AddTransition(tracking);
                reset.hasExitTime = false;
                reset.duration = 0;
                reset.AddCondition(AnimatorConditionMode.IfNot, 0, "ASFR/Enabled");
                var thaw = frozen.AddTransition(tracking);
                thaw.hasExitTime = false;
                thaw.duration = 0;
                thaw.AddCondition(AnimatorConditionMode.If, 0, ASFRProgress.Reversing);
                thaw.AddCondition(AnimatorConditionMode.IfNot, 0, parameter);
                thaw.AddCondition(AnimatorConditionMode.IfNot, 0, "ASFR/Frozen");
            }
        }
    }
}
