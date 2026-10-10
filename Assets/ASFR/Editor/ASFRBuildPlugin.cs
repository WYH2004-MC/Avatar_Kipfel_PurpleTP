using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor;

[assembly: ExportsPlugin(typeof(ASFR.ASFRBuildPlugin))]
namespace ASFR
{
    public sealed class ASFRBuildPlugin : Plugin<ASFRBuildPlugin>
    {
        public override string QualifiedName => "local.asfr.main-armature";
        public override string DisplayName => "ASFR main-armature binding";
        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming).AfterPlugin("nadena.dev.modular-avatar").WithRequiredExtension(typeof(AnimatorServicesContext), seq => seq.Run("Bind merged outfits and overlay animation", ctx =>
            {
                foreach (var marker in ctx.AvatarRootObject.GetComponentsInChildren<ASFRRigBinding>(true))
                {
                    if (marker.overlays != null && marker.overlaySources != null)
                    {
                        var index = ctx.Extension<AnimatorServicesContext>().AnimationIndex;
                        for (int i = 0; i < marker.overlays.Length; i++)
                        {
                            var overlay = marker.overlays[i];
                            var source = marker.overlaySources[i];
                            if (!overlay || !source)
                                continue;
                            overlay.enabled = source.enabled;
                            var from = AnimationUtility.CalculateTransformPath(source.transform, ctx.AvatarRootTransform);
                            var to = AnimationUtility.CalculateTransformPath(overlay.transform, ctx.AvatarRootTransform);
                            foreach (var clip in index.GetClipsForObjectPath(from).ToArray())
                                foreach (var binding in clip.GetFloatCurveBindings().ToArray())
                                    if (binding.path == from && binding.type == typeof(SkinnedMeshRenderer) && (binding.propertyName == "m_Enabled" || binding.propertyName.StartsWith("blendShape.")))
                                    {
                                        // ASFR source hiding must never disable the replacement or coating renderer.
                                        if (binding.propertyName == "m_Enabled" && clip.Name == ASFRClothing.HideSourcesClip)
                                            continue;
                                        // Our removal layer hides the original while its dissolving replacement stays visible.
                                        bool removalCopy = marker.clothingRemovalCopies != null && marker.clothingRemovalCopies.Contains(overlay);
                                        if (removalCopy && binding.propertyName == "m_Enabled" && clip.GetFloatCurveBindings().Any(b => b.path == to && b.type == typeof(GameObject) && b.propertyName == "m_IsActive"))
                                            continue;
                                        var target = binding;
                                        target.path = to;
                                        clip.SetFloatCurve(target, clip.GetFloatCurve(binding));
                                    }
                        }
                    }

                    if (marker.visibilitySources != null && marker.visibilityGates != null)
                    {
                        var animations = ctx.Extension<AnimatorServicesContext>().AnimationIndex;
                        for (int i = 0; i < marker.visibilitySources.Length; i++)
                        {
                            var source = marker.visibilitySources[i];
                            var gate = marker.visibilityGates[i];
                            if (!source || !gate)
                                continue;
                            gate.gameObject.SetActive(source.gameObject.activeSelf);
                            string from = AnimationUtility.CalculateTransformPath(source, ctx.AvatarRootTransform);
                            string to = AnimationUtility.CalculateTransformPath(gate, ctx.AvatarRootTransform);
                            foreach (var clip in animations.GetClipsForObjectPath(from).ToArray())
                                foreach (var binding in clip.GetFloatCurveBindings().ToArray())
                                    if (binding.path == from && binding.type == typeof(GameObject) && binding.propertyName == "m_IsActive")
                                    {
                                        var target = binding;
                                        target.path = to;
                                        clip.SetFloatCurve(target, clip.GetFloatCurve(binding));
                                    }
                        }
                    }

                    ASFRFaceFreeze.Build(ctx);
                    var captures = ASFRCompleteCapture.Build(marker, ctx, out var frozenRenderers);
                    var map = ASFRConnectedPose.Build(marker, ctx, captures);
                    ASFRFirstPerson.Build(marker, ctx.AvatarRootTransform, map);
                    int changed = 0;
                    foreach (var renderer in frozenRenderers)
                    {
                        var bones = renderer.bones;
                        bool modified = false;
                        for (int i = 0; i < bones.Length; i++)
                            if (bones[i] && map.TryGetValue(bones[i], out var proxy))
                            {
                                bones[i] = proxy;
                                modified = true;
                            }

                        if (modified)
                        {
                            renderer.bones = bones;
                            renderer.updateWhenOffscreen = true;
                            changed++;
                        }

                        if (renderer.rootBone && map.TryGetValue(renderer.rootBone, out var root))
                            renderer.rootBone = root;
                    }

                    Debug.Log("ASFR: bound " + changed + " renderers after Modular Avatar merging; " + map.Count + " connected skin joints.");
                    Object.DestroyImmediate(marker);
                }
            }));
        }
    }
}
