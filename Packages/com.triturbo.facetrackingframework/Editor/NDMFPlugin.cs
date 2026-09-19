#if ENABLE_NDMF

using nadena.dev.ndmf;
using Triturbo.FaceTrackingFramework;
using Triturbo.FaceTrackingFramework.Runtime;
using UnityEngine;

[assembly: ExportsPlugin(typeof(NDMFPlugin))]
namespace Triturbo.FaceTrackingFramework
{
    public class NDMFPlugin : Plugin<NDMFPlugin>
    {
        public override string QualifiedName => "triturbo.facetrackingframework";
        public override string DisplayName => "Triturbo FT";
        protected override void Configure()
        {

            InPhase(BuildPhase.Resolving)
                .Run(InverseVisemeCreatorPass.Instance);
            
            InPhase(BuildPhase.Generating)
                .Run(EyeTrackingSettingsPass.Instance).Then
                .Run(MouthTrackingSettingsPass.Instance).Then
                .Run(EmoteLayersModifierPass.Instance).Then
                .Run("Purge Triturbo Face Tracking Framework components", ctx =>
                {
                    foreach (var component in ctx.AvatarRootTransform.GetComponentsInChildren<InverseVisemeCreatorComponent>(true))
                    {
                        Object.DestroyImmediate(component);
                    }
                    foreach (var component in ctx.AvatarRootTransform.GetComponentsInChildren<EyeTrackingSettingsComponent>(true))
                    {
                        Object.DestroyImmediate(component);
                    }
                    foreach (var component in ctx.AvatarRootTransform.GetComponentsInChildren<MouthTrackingSettingsComponent>(true))
                    {
                        Object.DestroyImmediate(component);
                    }
                    foreach (var component in ctx.AvatarRootTransform.GetComponentsInChildren<EmoteLayersModifierComponent>(true))
                    {
                        Object.DestroyImmediate(component);
                    }
                });;
        }
    }
}
#endif