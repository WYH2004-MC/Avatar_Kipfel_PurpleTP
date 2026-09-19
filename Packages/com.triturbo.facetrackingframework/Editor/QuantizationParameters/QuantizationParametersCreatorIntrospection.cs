

#if ENABLE_NDMF && ENABLE_MA
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;
using Triturbo.FaceTrackingFramework.QuantizationParameters.Runtime;
using nadena.dev.modular_avatar.core;

namespace Triturbo.FaceTrackingFramework.QuantizationParameters
{

    [ParameterProviderFor(typeof(QuantizationParametersCreator))]

    public class QuantizationParametersCreatorIntrospection : IParameterProvider
    {
        private readonly QuantizationParametersCreator _component;

        public QuantizationParametersCreatorIntrospection(QuantizationParametersCreator component)
        {
            _component = component;
        }

        public IEnumerable<ProvidedParameter> GetSuppliedParameters(BuildContext context = null)
        {
            _component.GetParameterSettingsList();


            IEnumerable<ProvidedParameter> parameters = new List<ProvidedParameter>();

            return _component.CreateMAParameterConfigs().Select(p =>
            {
                AnimatorControllerParameterType paramType;
                bool animatorOnly = false;

                switch (p.syncType)
                {
                    case ParameterSyncType.Bool:
                        paramType = AnimatorControllerParameterType.Bool;
                        break;
                    case ParameterSyncType.Float:
                        paramType = AnimatorControllerParameterType.Float;
                        break;
                    case ParameterSyncType.Int:
                        paramType = AnimatorControllerParameterType.Int;
                        break;
                    default:
                        paramType = AnimatorControllerParameterType.Float;
                        animatorOnly = true;
                        break;
                }

                return new ProvidedParameter(
                    p.nameOrPrefix,
                    p.isPrefix ? ParameterNamespace.PhysBonesPrefix : ParameterNamespace.Animator,
                    _component, NDMFPlugin.Instance, paramType)
                {
                    IsAnimatorOnly = animatorOnly,
                    WantSynced = !p.localOnly,
                    IsHidden = p.internalParameter,
                };
            });

        }

        public void RemapParameters(ref ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> nameMap, BuildContext context = null)
        {
            //
        }
    }
}


#endif
