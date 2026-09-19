using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.Components;


#if ENABLE_NDMF && ENABLE_MA
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;
using Triturbo.FaceTrackingFramework.QuantizationParameters.Runtime;
using Triturbo.FaceTrackingFramework.Util;
using UnityEditor.Animations;

namespace Triturbo.FaceTrackingFramework.QuantizationParameters
{
    public class QuantizationParametersPass : Pass<QuantizationParametersPass> 
    {
        public override string DisplayName => "QuantizationParameters";

        protected override void Execute(BuildContext context)
        {
            foreach (var component in context.AvatarRootObject.GetComponentsInChildren<Runtime.QuantizationParametersCreator>(true))
            {
                var map = component.GetComponent<ModularAvatarParameters>();

                if (map == null)
                {
                    map = component.gameObject.AddComponent<ModularAvatarParameters>();
                }

                map.parameters.AddRange(component.CreateMAParameterConfigs());
                
                AnimatorController fx = context.AvatarDescriptor.baseAnimationLayers[4].animatorController as AnimatorController;
                component.AddMAMergeAnimator(component.CreateAnimator(context.AssetContainer, AnimationUtil.IsAllStatesWriteDefaultOn(fx)));


                if (component.m_EnableLayerSelection)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var paramsSettings = component.GetParameterSettingsList(AnimLayerMask.GetMask(i));
                        if (paramsSettings.Count == 0)
                        {
                            continue;
                        }

                        VRCAvatarDescriptor.AnimLayerType layerType = i switch
                        {
                            0 => VRCAvatarDescriptor.AnimLayerType.Base,
                            1 => VRCAvatarDescriptor.AnimLayerType.Additive,
                            2 => VRCAvatarDescriptor.AnimLayerType.Gesture,
                            3 => VRCAvatarDescriptor.AnimLayerType.Action,
                            _ => VRCAvatarDescriptor.AnimLayerType.FX
                        };
                        component.AddMAMergeAnimator(component.CreateAnimator(context.AssetContainer, paramsSettings),
                            layerType);
                    }
                }
               
            }
        }

    }

}
#endif