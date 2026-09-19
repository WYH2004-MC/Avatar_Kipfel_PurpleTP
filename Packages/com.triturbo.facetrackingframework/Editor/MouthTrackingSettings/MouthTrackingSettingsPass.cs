#if ENABLE_NDMF && ENABLE_MA
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;
using UnityEngine;
using Triturbo.FaceTrackingFramework.Runtime;
using UnityEditor;

namespace Triturbo.FaceTrackingFramework
{
    public class MouthTrackingSettingsPass: Pass<MouthTrackingSettingsPass>
    {
        protected override void Execute(BuildContext context)
        {
            var mouthSettings = context.AvatarRootObject.GetComponentInChildren<MouthTrackingSettingsComponent>();
            if (mouthSettings != null)
            {
                mouthSettings.ExistPreview();
                mouthSettings.ApplyParameters();
            }
        }
        
        
    }
}

#endif