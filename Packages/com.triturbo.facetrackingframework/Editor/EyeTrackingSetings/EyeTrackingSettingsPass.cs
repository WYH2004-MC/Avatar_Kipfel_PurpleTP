#if ENABLE_NDMF && ENABLE_MA
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;
using UnityEngine;
using Triturbo.FaceTrackingFramework.Runtime;
using UnityEditor;

namespace Triturbo.FaceTrackingFramework
{
    public class EyeTrackingSettingsPass: Pass<EyeTrackingSettingsPass>
    {
        protected override void Execute(BuildContext context)
        {
            var eyeSettings = context.AvatarRootObject.GetComponentInChildren<Runtime.EyeTrackingSettingsComponent>();
            if (eyeSettings != null && eyeSettings.GetBody() != null)
            {
                Animator animator = context.AvatarRootObject.GetComponent<Animator>();
                RuntimeAnimatorController controller = null;

                if (animator != null)
                {
                    controller = animator.runtimeAnimatorController;
                    animator.runtimeAnimatorController = null;
                }
                eyeSettings.m_ExpressionBlendShapes.SplitExpressionWeight(eyeSettings.Body);
                eyeSettings.ExistPreview();
                eyeSettings.ApplyParameters();
                if (animator != null)
                {
                    animator.runtimeAnimatorController = controller;
                }
            }
        }
        
        
     
    }
}

#endif