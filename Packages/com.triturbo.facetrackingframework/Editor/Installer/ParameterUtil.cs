using System;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

#if ENABLE_NDMF
using nadena.dev.ndmf;
#endif

namespace Triturbo.FaceTrackingFramework.Installer
{
    public class ParameterUtil
    {
        public static int GetAvatarParameterMemoryUsed(VRCAvatarDescriptor avatarDescriptor)
        {
            int parameterCount = avatarDescriptor.GetExpressionParameterCount();
            int totalCost = 0;
            
            //VRC
            for (int i = 0; i < parameterCount; i++)
            {
                VRCExpressionParameters.Parameter parameter = avatarDescriptor.GetExpressionParameter(i);

                if (parameter.networkSynced == true)
                {
                    VRCExpressionParameters.ValueType valueType = parameter.valueType;
                    totalCost += VRCExpressionParameters.TypeCost(valueType);
                }
            }
#if ENABLE_NDMF
            try
            {
                totalCost = ParameterInfo.ForUI.GetParametersForObject(avatarDescriptor.gameObject).Sum(p => p.BitUsage);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
#endif
            return totalCost;
        }
        public static int GetMAParameterMemoryUsed(GameObject maObject)
        {
            if (maObject == null) return 0;
#if ENABLE_NDMF
            return ParameterInfo.ForUI.GetParametersForObject(maObject).Sum(p => p.BitUsage);;
#else
            return 0;
#endif
        }


    }
    
}