#if ENABLE_NDMF && ENABLE_MA

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using nadena.dev.ndmf;
using Triturbo.FaceTrackingFramework.QuantizationParameters;

using Triturbo.FaceTrackingFramework.QuantizationParameters.Runtime;

[assembly: ExportsPlugin(typeof(NDMFPlugin))]

namespace Triturbo.FaceTrackingFramework.QuantizationParameters
{
    public class NDMFPlugin : Plugin<NDMFPlugin>
    {
        public override string QualifiedName => "triturbo.facetrackingframework.quantizationprameters";
        public override string DisplayName => "Triturbo FT - Quantization Prameters";
        protected override void Configure()
        {
            InPhase(BuildPhase.Generating)
                .Run(QuantizationParametersPass.Instance).Then
                .Run("Purge Quantization Parameters Creator components", ctx =>
                {
                    foreach (var component in ctx.AvatarRootTransform.GetComponentsInChildren<QuantizationParametersCreator>(true))
                    {
                        Object.DestroyImmediate(component);
                    }
                });
        }
    }
}

#endif