using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Constraint.Components;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;

namespace ASFR
{
    static class ASFRCompleteCapture
    {
        public static Dictionary<Transform, Transform> Build(ASFRRigBinding marker, BuildContext ctx, out HashSet<SkinnedMeshRenderer> frozenRenderers)
        {
            var map = new Dictionary<Transform, Transform>();
            var template = marker.captures.FirstOrDefault(c => c && c.Sources.Count > 0 && c.Sources[0].SourceTransform);
            if (!template)
                throw new InvalidOperationException("ASFR: freeze capture template is missing. Regenerate from the original avatar.");
            if (!marker.mainArmature)
                throw new InvalidOperationException("ASFR: regenerate the avatar with the updated installer to validate the final merged skeleton.");
            foreach (var capture in marker.captures)
                if (capture && capture.Sources.Count > 0 && capture.Sources[0].SourceTransform)
                    map[capture.Sources[0].SourceTransform] = capture.transform;
            var selected = new HashSet<SkinnedMeshRenderer>((marker.overlaySources ?? Array.Empty<SkinnedMeshRenderer>()).Concat(marker.overlays ?? Array.Empty<SkinnedMeshRenderer>()).Where(r => r));
            frozenRenderers = new HashSet<SkinnedMeshRenderer>();
            var required = new HashSet<Transform>();
            foreach (var renderer in ctx.AvatarRootObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!renderer.sharedMesh)
                    continue;
                var bones = renderer.bones;
                var used = new HashSet<Transform>();
                bool missing = false;
                using (var weights = renderer.sharedMesh.GetAllBoneWeights())
                    foreach (var weight in weights)
                        if (weight.weight > 0)
                        {
                            if (weight.boneIndex < 0 || weight.boneIndex >= bones.Length || !bones[weight.boneIndex])
                            {
                                missing = true;
                                continue;
                            }

                            used.Add(bones[weight.boneIndex]);
                        }

                // Never partially rebind an excluded independent accessory.
                bool included = selected.Contains(renderer) || (!missing && used.Count > 0 && used.All(b => b.IsChildOf(marker.mainArmature)));
                if (!included)
                    continue;
                if (missing)
                    throw new InvalidOperationException("ASFR: missing weighted bone on " + renderer.name);
                if (used.Any(b => !b.IsChildOf(ctx.AvatarRootTransform)))
                    throw new InvalidOperationException("ASFR: " + renderer.name + " references bones outside this avatar.");
                frozenRenderers.Add(renderer);
                required.UnionWith(used);
                if (renderer.rootBone && renderer.rootBone != ctx.AvatarRootTransform)
                {
                    if (!renderer.rootBone.IsChildOf(ctx.AvatarRootTransform))
                        throw new InvalidOperationException("ASFR: root bone outside this avatar on " + renderer.name);
                    required.Add(renderer.rootBone);
                }
            }

            // Intermediate transforms also affect descendant joint positions. Capture their rotations
            // so live helper transforms cannot continue moving the connected hierarchy after freezing.
            foreach (var bone in required.ToArray())
                for (var parent = bone.parent; parent && parent != ctx.AvatarRootTransform; parent = parent.parent)
                    required.Add(parent);
            string templatePath = AnimationUtility.CalculateTransformPath(template.transform, ctx.AvatarRootTransform);
            var index = ctx.Extension<AnimatorServicesContext>().AnimationIndex;
            int added = 0;
            foreach (var source in required.Where(b => !map.ContainsKey(b)))
            {
                var curveTemplate = template;
                if (marker.freezeAnchors != null && marker.freezeAnchors.Length > 0 && marker.freezeRepresentatives != null)
                    curveTemplate = marker.freezeRepresentatives[ASFRProgressiveFreeze.GroupFor(source, marker.freezeAnchors)];
                string curvePath = AnimationUtility.CalculateTransformPath(curveTemplate.transform, ctx.AvatarRootTransform);
                var clips = index.GetClipsForObjectPath(curvePath).ToArray();
                var proxy = new GameObject("MergedBone_" + (added++).ToString("D4")).transform;
                proxy.SetParent(template.transform.parent, false);
                proxy.SetPositionAndRotation(source.position, source.rotation);
                var s = source.lossyScale;
                var p = proxy.parent.lossyScale;
                proxy.localScale = new Vector3(s.x / p.x, s.y / p.y, s.z / p.z);
                var capture = proxy.gameObject.AddComponent<VRCParentConstraint>();
                capture.Sources.Add(new VRCConstraintSource(source, 1));
                capture.ZeroConstraint();
                capture.FreezeToWorld = false;
                capture.RebakeOffsetsWhenUnfrozen = false;
                capture.ApplyConfigurationChanges();
                string path = AnimationUtility.CalculateTransformPath(proxy, ctx.AvatarRootTransform);
                int copied = 0;
                foreach (var clip in clips)
                    foreach (var binding in clip.GetFloatCurveBindings().ToArray())
                        if (binding.path == curvePath && binding.type == typeof(VRCParentConstraint) && binding.propertyName.IndexOf("FreezeToWorld", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var target = binding;
                            target.path = path;
                            clip.SetFloatCurve(target, clip.GetFloatCurve(binding));
                            copied++;
                        }

                if (copied == 0)
                    throw new InvalidOperationException("ASFR: could not synchronize the merged-bone freeze animation.");
                map.Add(source, proxy);
            }

            Debug.Log("ASFR: verified " + required.Count + " weighted bones; added " + added + " captures for merged or selected accessory bones.");
            return map;
        }
    }
}
