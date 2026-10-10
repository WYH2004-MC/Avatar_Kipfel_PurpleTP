using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace ASFR
{
    static class ASFRFirstPerson
    {
        public static void Build(ASFRRigBinding marker, Transform root, Dictionary<Transform, Transform> connected)
        {
            var animator = root.GetComponent<Animator>();
            if (!animator || !animator.isHuman)
                return;
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (!head || !connected.TryGetValue(head, out var displayedHead))
                throw new InvalidOperationException("ASFR: generated head bone is missing; cannot configure first-person head hiding.");
            var hidden = new HashSet<Transform>{displayedHead};
            // Preserve explicit hiding of separately rigged hoods/face attachments as well.
            var existing = root.GetComponentsInChildren<VRCHeadChop>(true);
            foreach (var chop in existing)
                if (chop.enabled && chop.targetBones != null)
                    foreach (var bone in chop.targetBones)
                        if (bone.transform && bone.scaleFactor == 0 && bone.applyCondition == VRCHeadChop.HeadChopBone.ApplyCondition.AlwaysApply && connected.TryGetValue(bone.transform, out var target))
                            hidden.Add(target);
            // A parent entry also hides its descendants; avoid unnecessary duplicate targets.
            var targets = hidden.Where(t => !hidden.Any(p => p != t && t.IsChildOf(p))).ToArray();
            int batches = (targets.Length + 31) / 32;
            if (existing.Length + batches > VRCHeadChop.MaxComponentCount)
                throw new InvalidOperationException("ASFR needs " + batches + " additional Head Chop component(s) for its generated head, exceeding the SDK component limit.");
            for (int i = 0; i < targets.Length; i += 32)
            {
                var go = new GameObject("ASFR First Person Head Hiding");
                go.transform.SetParent(marker.transform, false);
                var chop = go.AddComponent<VRCHeadChop>();
                chop.targetBones = targets.Skip(i).Take(32).Select(t => new VRCHeadChop.HeadChopBone{transform = t, scaleFactor = 0, applyCondition = VRCHeadChop.HeadChopBone.ApplyCondition.AlwaysApply}).ToArray();
            }
        }
    }
}
