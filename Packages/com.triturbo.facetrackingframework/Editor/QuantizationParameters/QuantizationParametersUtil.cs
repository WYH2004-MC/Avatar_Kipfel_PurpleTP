
using System;

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;


#if ENABLE_MA
using nadena.dev.modular_avatar.core;
using Triturbo.FaceTrackingFramework.Util;
#endif

#if ENABLE_NDMF
using nadena.dev.ndmf;
using nadena.dev.ndmf.util;
#endif
using System.Diagnostics;
using Triturbo.FaceTrackingFramework.QuantizationParameters.Runtime;

namespace Triturbo.FaceTrackingFramework.QuantizationParameters
{
    public static class QuantizationParametersUtil
    {
        internal const string ALWAYS_ONE = "__QuantizationParameters/One";
        internal const string GENERATED_PREFIX = "Generated/";
        
#if ENABLE_MA
        public static void AddParameters(this Runtime.QuantizationParametersCreator component, VRCExpressionParameters vrcParameters)
        {

            AddParameters(vrcParameters, component.CreateMAParameterConfigs());
        }

        public static void AddParameters(VRCExpressionParameters vrcParameters, IEnumerable<ParameterConfig> parameterConfigs)
        {
            List<VRCExpressionParameters.Parameter> newParametrers = new List<VRCExpressionParameters.Parameter>();
            foreach (var config in parameterConfigs)
            {
                if (config.syncType == ParameterSyncType.NotSynced) continue;

                var existingParam = Array.Find(vrcParameters.parameters, p => p.name == config.nameOrPrefix);

                if (existingParam != null)
                {
                    if (config.hasExplicitDefaultValue)
                    {
                        existingParam.defaultValue = config.defaultValue;
                    }

                    existingParam.networkSynced = !config.localOnly;
                    existingParam.saved = config.saved;
                }
                else
                {
                    newParametrers.Add(new VRCExpressionParameters.Parameter()
                    {
                        name = config.nameOrPrefix,
                        defaultValue = config.defaultValue,
                        saved = config.saved,
                        networkSynced = !config.localOnly,
                        valueType = GetType(config.syncType)
                    });
                }
            }
            vrcParameters.parameters = vrcParameters.parameters.Concat(newParametrers).ToArray();

        }

        private static VRCExpressionParameters.ValueType GetType(ParameterSyncType syncType)
        {
            if (syncType == ParameterSyncType.Bool) return VRCExpressionParameters.ValueType.Bool;

            if (syncType == ParameterSyncType.Float) return VRCExpressionParameters.ValueType.Float;

            if (syncType == ParameterSyncType.Int) return VRCExpressionParameters.ValueType.Int;

            return VRCExpressionParameters.ValueType.Bool;
        }

        public static List<ParameterConfig> CreateMAParameterConfigs(this Runtime.QuantizationParametersCreator component)
        {
            List<ParameterConfig> maParameters = new List<ParameterConfig>();

            var parameters = component.GetParameterSettingsList();

            foreach (var parameter in parameters)
            {
                int bits = parameter.resolution + (parameter.symmetric ? 1 : 0);


                bool binarySynced = parameter.resolution > 0 && bits < 8;
                bool synced = parameter.resolution > 0;
                bool floatSynced = parameter.resolution > 0 && !binarySynced;

                if (parameter.combinedLocal && parameter.combinedRemote)
                {
                    maParameters.Add(new ParameterConfig()
                    {
                        nameOrPrefix = parameter.parameterName,
                        syncType = ParameterSyncType.Float,
                        defaultValue = parameter.defaultValue,
                        localOnly = !floatSynced
                    });


                    if (binarySynced)
                    {
                        float[] values = parameter.BinaryValues;
                        int counter = 0;
                        foreach (string b in parameter.BinaryParameters)
                        {
                            maParameters.Add(new ParameterConfig()
                            {
                                nameOrPrefix = b,
                                syncType = ParameterSyncType.Bool,
                                localOnly = false,
                                defaultValue = values[counter++]
                            });
                        }
                    }

                }
                else if (parameter.combinedLocal)
                {

                    maParameters.Add(new ParameterConfig()
                    {
                        nameOrPrefix = parameter.parameterName,
                        syncType = ParameterSyncType.Float,
                        defaultValue = parameter.defaultValue,
                        localOnly = true
                    });

                    if (binarySynced)
                    {
                        float[] values = parameter.BinaryValues;

                        foreach (string p in parameter.GetParameters())
                        {
                            int counter = 0;
                            foreach (string b in GetBinaryParameters(p, parameter.resolution, parameter.symmetric))
                            {
                                maParameters.Add(new ParameterConfig()
                                {
                                    nameOrPrefix = b,
                                    syncType = ParameterSyncType.Bool,
                                    defaultValue = values[counter++],
                                    localOnly = false
                                });
                            }

                        }
                    }
                    else if (floatSynced)
                    {
                        foreach (string p in parameter.GetParameters())
                        {
                            maParameters.Add(new ParameterConfig()
                            {
                                nameOrPrefix = p,
                                syncType = ParameterSyncType.Float,
                                defaultValue = parameter.defaultValue,
                                localOnly = false
                            });
                        }
                    }
                }
                else if (parameter.combinedRemote)
                {

                    foreach (string p in parameter.GetParameters())
                    {
                        maParameters.Add(new ParameterConfig()
                        {
                            nameOrPrefix = p,
                            syncType = ParameterSyncType.Float,
                            defaultValue = parameter.defaultValue,
                            localOnly = true
                        });
                    }

                    if (binarySynced)
                    {
                        float[] values = parameter.BinaryValues;
                        int counter = 0;
                        foreach (string b in parameter.BinaryParameters)
                        {
                            maParameters.Add(new ParameterConfig()
                            {
                                nameOrPrefix = b,
                                syncType = ParameterSyncType.Bool,
                                localOnly = false,
                                defaultValue = values[counter++]
                            });
                        }
                    }
                    else if (floatSynced)
                    {
                        maParameters.Add(new ParameterConfig()
                        {
                            nameOrPrefix = parameter.parameterName,
                            syncType = ParameterSyncType.Float,
                            defaultValue = parameter.defaultValue,
                            localOnly = false
                        });
                    }
                }
                else
                {
                    foreach (string p in parameter.GetParameters())
                    {
                        maParameters.Add(new ParameterConfig()
                        {
                            nameOrPrefix = p,
                            syncType = ParameterSyncType.Float,
                            defaultValue = parameter.defaultValue,
                            localOnly = !floatSynced
                        });
                    }

                    if (binarySynced)
                    {
                        float[] values = parameter.BinaryValues;

                        foreach (string p in parameter.GetParameters())
                        {
                            int counter = 0;
                            foreach (string b in GetBinaryParameters(p, parameter.resolution, parameter.symmetric))
                            {
                                maParameters.Add(new ParameterConfig()
                                {
                                    nameOrPrefix = b,
                                    syncType = ParameterSyncType.Bool,
                                    defaultValue = values[counter++],
                                    localOnly = false
                                });
                            }

                        }
                    }
                }

            }

            return maParameters;
        }

        public static void AddMAMergeAnimator(this Runtime.QuantizationParametersCreator component, RuntimeAnimatorController animatorController, VRCAvatarDescriptor.AnimLayerType layer = VRCAvatarDescriptor.AnimLayerType.FX)
        {
            var ma = component.gameObject.AddComponent<ModularAvatarMergeAnimator>();

            ma.animator = animatorController;
            ma.matchAvatarWriteDefaults = false;
            ma.pathMode = MergeAnimatorPathMode.Absolute;
            ma.layerType = layer;
        }

        public static void AddToAnimator(this Runtime.QuantizationParametersCreator component, AnimatorController animator, UnityEngine.Object assetsContainer)
        {
            animator.AddOrOverwriteParameter("IsLocal", AnimatorControllerParameterType.Bool);
            animator.AddOrOverwriteParameter(ALWAYS_ONE, AnimatorControllerParameterType.Float, 1);

            bool writeDefault = AnimationUtil.IsAllStatesWriteDefaultOn(animator);

            string prefix = component.m_QuantizationParameters.m_Prefix;
            if (string.IsNullOrWhiteSpace(prefix))
            {
                prefix = "__QuantizationParameters__";
            }
            var paramsSettings = component.GetParameterSettingsList();

            animator.AddLayer(CreateQuantizationParameterLayer(assetsContainer, animator, paramsSettings,
                component.m_LocalBlendTrees.Cast<BlendTree>().ToArray(), component.m_RemoteBlendTrees.Cast<BlendTree>().ToArray(), prefix));

            foreach (var driver in component.m_QuantizationParameterDrivers)
            {
                var target = paramsSettings.FirstOrDefault(s => s.parameterName == driver.m_Target);
                if (target == null)
                {
                    continue;
                }

                animator.AddLayer(CreateParameterDriverLayer(assetsContainer, animator, target, driver.m_Controller, driver.m_Toggle, writeDefault));
            }
        }

#endif
       
        public static string[] GetBinaryParameters(string parameter, int resolution, bool symmetric)
        {

            string[] binary = new string[resolution + (symmetric ? 1 : 0)];
            for (int i = 0; i < resolution; i++)
            {
                binary[i] = parameter + Mathf.Pow(2, i).ToString();
            }

            if (symmetric)
            {
                binary[resolution] = parameter + "Negative";

            }

            return binary;


        }
       

        public static AnimatorController CreateAnimator(this Runtime.QuantizationParametersCreator component, UnityEngine.Object assetsContainer, bool writeDefault)
        {
            AnimatorController animator = new AnimatorController();
            AssetDatabase.AddObjectToAsset(animator, assetsContainer);
            
            animator.AddParameter("IsLocal", AnimatorControllerParameterType.Bool);
            animator.AddParameter(
                new AnimatorControllerParameter()
                {
                    name = ALWAYS_ONE,
                    defaultFloat = 1f,
                    type = AnimatorControllerParameterType.Float
                });
            string prefix = component.m_QuantizationParameters.m_Prefix;
            if(string.IsNullOrWhiteSpace(prefix))
            {
                prefix = "__QuantizationParameters__";
            }
            

      
            var paramsSettings = component.GetParameterSettingsList();
            animator.AddLayer(CreateQuantizationParameterLayer(assetsContainer, animator, paramsSettings, 
                component.m_LocalBlendTrees.OfType<BlendTree>().ToArray(), component.m_LocalBlendTrees.OfType<BlendTree>().ToArray(), prefix));

            
            foreach (var driver in component.m_QuantizationParameterDrivers)
            {
                var target = paramsSettings.FirstOrDefault(s => s.parameterName == driver.m_Target);
                if(target == null)
                {
                    continue;
                }

                animator.AddLayer(CreateParameterDriverLayer(assetsContainer, animator, target, driver.m_Controller, driver.m_Toggle, writeDefault));
            }
                
            
            
            
           
            //Use Parameter Driver to sync Quantization Params

            //animator.AddOrOverwriteParameter("Sync", AnimatorControllerParameterType.Bool, 1);
            //foreach (var param in paramsSettings)
            //{
            //    if(param.resolution <= 0 || param.resolution + (param.symmetric?1:0) >= 8)
            //    {
            //        continue;
            //    }
            //    foreach (string input in param.GetInputOutputPairs(false).Keys)
            //    {
            //        animator.AddLayer(CreateParameterDriverLayer(assetsContainer, animator, input, input, "Sync", 
            //            param.resolution, param.symmetric, param.BinaryValues, writeDefault));
            //    }
            //}



            return animator;
        }
        
        public static AnimatorController CreateAnimator(this Runtime.QuantizationParametersCreator component, UnityEngine.Object assetsContainer, IEnumerable<QuantizationParameterSettings> parameterSettings)
        {
            AnimatorController animator = new AnimatorController();
            AssetDatabase.AddObjectToAsset(animator, assetsContainer);
            
            animator.AddParameter("IsLocal", AnimatorControllerParameterType.Bool);
            animator.AddParameter(
                new AnimatorControllerParameter()
                {
                    name = ALWAYS_ONE,
                    defaultFloat = 1f,
                    type = AnimatorControllerParameterType.Float
                });
            string prefix = component.m_QuantizationParameters.m_Prefix;
            if(string.IsNullOrWhiteSpace(prefix))
            {
                prefix = "__QuantizationParameters__";
            }
            animator.AddLayer(CreateQuantizationParameterLayer(assetsContainer, animator, parameterSettings, 
                null, null, prefix));
            
            return animator;
        }
        
        
        
        public static bool[] IntToBinary(int number, int bitCount)
        {
            bool[] binary = new bool[bitCount];

            for (int i = 0; i < bitCount; i++)
            {
                binary[i] = (number & (1 << i)) != 0;
            }

            return binary;
        }
        public static AnimatorControllerLayer CreateParameterDriverLayer(UnityEngine.Object assetsContainer, AnimatorController _animatorController, 
            Runtime.QuantizationParameterSettings target, string controller, string enabler, bool writeDefault = true)
        {
            AnimatorControllerLayer animLayer = new AnimatorControllerLayer()
            {
                name = $"{target.parameterName} Control",
                stateMachine = new AnimatorStateMachine() { name = $"{target.parameterName} Control" },
                defaultWeight = 0
            };

            if (AssetDatabase.IsMainAsset(_animatorController))
            {
                animLayer.stateMachine.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(animLayer.stateMachine, _animatorController);
            }
            else
            {
                AssetDatabase.AddObjectToAsset(animLayer.stateMachine, assetsContainer);
            }


            AnimatorState idle = animLayer.stateMachine.AddState("idle", new Vector3(30, 170, 0));
            idle.writeDefaultValues = writeDefault;
            var anyToIdle = animLayer.stateMachine.AddAnyStateTransition(idle);
            anyToIdle.duration = 0;
            anyToIdle.canTransitionToSelf = false;
            anyToIdle.AddCondition(AnimatorConditionMode.IfNot, 1, enabler);


            //Float-sync
            if (target.resolution + (target.symmetric ? 1 : 0) >= 8)
            {
                AnimatorState sync = animLayer.stateMachine.AddState("sync", new Vector3(250, 170, 0));
                sync.writeDefaultValues = writeDefault;

                var anyToSync = animLayer.stateMachine.AddAnyStateTransition(sync);
                anyToSync.duration = 0;
                anyToSync.canTransitionToSelf = true;
                anyToSync.AddCondition(AnimatorConditionMode.If, 1, enabler);

                var driver = sync.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                driver.localOnly = true;

                foreach(var io in target.GetInputOutputPairs(false))
                {
                    string input = io.Key;

                    driver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter()
                    {
                        type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Copy,
                        source = controller,
                        destParam = input,
                        name = input,
                    });
                }

                return animLayer;

            }

            //Binary-sync
            float halfUnit = 0.5f / (Mathf.Pow(2, target.resolution) - 1);
            int stateCount =(int) Mathf.Pow(2, target.resolution);


            AnimatorState zero = animLayer.stateMachine.AddState("state 0", new Vector3(250, 170, 0));
            zero.writeDefaultValues = writeDefault;

            var anyToZero = animLayer.stateMachine.AddAnyStateTransition(zero);
            anyToZero.duration = 0;
            anyToZero.canTransitionToSelf = false;
            anyToZero.AddCondition(AnimatorConditionMode.If, 1, enabler);

            anyToZero.AddCondition(AnimatorConditionMode.Less,  halfUnit, controller);
            anyToZero.AddCondition(AnimatorConditionMode.Greater, -halfUnit, controller);

            var zeroDriver = zero.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            var idleDriver = idle.AddStateMachineBehaviour<VRCAvatarParameterDriver>();

            zeroDriver.localOnly = true;
            idleDriver.localOnly = true;

            List<string[]> binaryParametersList = new List<string[]>();

            float[] binaryValue = target.BinaryValues;

            foreach (var io in target.GetInputOutputPairs(false))
            {
                string[] binaryParameters = GetBinaryParameters(io.Key, target.resolution, target.symmetric);
                binaryParametersList.Add(binaryParameters);
                int counter = 0;
                foreach (string b in binaryParameters)
                {
                    zeroDriver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter()
                    {
                        type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set,
                        name = b,
                        value = 0
                    });


                    idleDriver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter()
                    {
                        type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set,
                        name = b,
                        value = counter < binaryValue.Length ? binaryValue[counter] : 0
                    });

                    counter++;
                }  
            }

            for (int i = 1; i < stateCount; i++)
            {
                AnimatorState currentPositive = animLayer.stateMachine.AddState("state" + i, new Vector3(250, 170 +60 * i, 0));
                currentPositive.writeDefaultValues = writeDefault;


                float value = i / (Mathf.Pow(2, target.resolution) - 1);

                var anyToCurrent = animLayer.stateMachine.AddAnyStateTransition(currentPositive);
                anyToCurrent.duration = 0;
                anyToCurrent.canTransitionToSelf = false;

                anyToCurrent.AddCondition(AnimatorConditionMode.If, 1, enabler);

                anyToCurrent.AddCondition(AnimatorConditionMode.Greater, value - halfUnit, controller);
                
                if (i + 1 < stateCount)
                {
                    anyToCurrent.AddCondition(AnimatorConditionMode.Less, value + halfUnit, controller);
                }

                var driver = currentPositive.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                driver.localOnly = true;

                var bits = IntToBinary(i, target.resolution);
                foreach(var binaryParameters in binaryParametersList)
                {
                    for (int j = 0; j < target.resolution; j++)
                    {
                        driver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter()
                        {
                            type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set,

                            name = binaryParameters[j],
                            value = bits[j] ? 1 : 0
                        });
                    }
                    if (target.symmetric)
                    {
                        driver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter()
                        {
                            type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set,
                            name = binaryParameters[target.resolution],
                            value = 0
                        });
                    }
                }
            }
            if(target.symmetric)
            {
                for (int i = 1; i < stateCount; i++)
                {
                    AnimatorState currentNegative = animLayer.stateMachine.AddState("state -" + i, new Vector3(250, 170 - 60 * i, 0));
                    currentNegative.writeDefaultValues = writeDefault;

                    float value =  -i / (Mathf.Pow(2, target.resolution) - 1);

                    var anyToCurrent = animLayer.stateMachine.AddAnyStateTransition(currentNegative);
                    anyToCurrent.duration = 0;
                    anyToCurrent.canTransitionToSelf = false;

                    anyToCurrent.AddCondition(AnimatorConditionMode.If, 1, enabler);

                    anyToCurrent.AddCondition(AnimatorConditionMode.Less, value + halfUnit, controller);

                    if (i + 1 < stateCount)
                    {
                        anyToCurrent.AddCondition(AnimatorConditionMode.Greater, value - halfUnit, controller);

                    }

                    var driver = currentNegative.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                    driver.localOnly = true;
                    
                    var bits = IntToBinary(i, target.resolution);
                    foreach (var binaryParameters in binaryParametersList)
                    {
                        for (int j = 0; j < target.resolution; j++)
                        {
                            driver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter()
                            {
                                type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set,
                                name = binaryParameters[j],
                                value = bits[j] ? 1 : 0
                            });
                        }

                        driver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter()
                        {
                            type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set,
                            name = binaryParameters[target.resolution],
                            value = 1
                        });
                    }
      

                }

            }

            return animLayer;
        }

        public static AnimatorControllerLayer CreateParameterDriverLayer(UnityEngine.Object assetsContainer, AnimatorController _animatorController,
            string target, string controller, string enabler, int resolution, bool symmetric, float[] binaryValue, bool writeDefault = true)
        {
            AnimatorControllerLayer animLayer = new AnimatorControllerLayer()
            {
                name = $"{target} Control",
                stateMachine = new AnimatorStateMachine() { name = $"{target} Control" },
                defaultWeight = 0
            };

            if (AssetDatabase.IsMainAsset(_animatorController))
            {
                animLayer.stateMachine.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(animLayer.stateMachine, _animatorController);
            }
            else
            {
                AssetDatabase.AddObjectToAsset(animLayer.stateMachine, assetsContainer);
            }

            AnimatorState idle = animLayer.stateMachine.AddState("idle", new Vector3(30, 170, 0));
            idle.writeDefaultValues = writeDefault;
            var anyToIdle = animLayer.stateMachine.AddAnyStateTransition(idle);
            anyToIdle.duration = 0;
            anyToIdle.canTransitionToSelf = false;
            anyToIdle.AddCondition(AnimatorConditionMode.IfNot, 1, enabler);


            //Binary-sync
            float halfUnit = 0.5f / (Mathf.Pow(2, resolution) - 1);
            int stateCount = (int)Mathf.Pow(2, resolution);


            AnimatorState zero = animLayer.stateMachine.AddState("state 0", new Vector3(250, 170, 0));
            zero.writeDefaultValues = writeDefault;

            var anyToZero = animLayer.stateMachine.AddAnyStateTransition(zero);
            anyToZero.duration = 0;
            anyToZero.canTransitionToSelf = false;
            anyToZero.AddCondition(AnimatorConditionMode.If, 1, enabler);

            anyToZero.AddCondition(AnimatorConditionMode.Less, halfUnit, controller);
            anyToZero.AddCondition(AnimatorConditionMode.Greater, -halfUnit, controller);

            var zeroDriver = zero.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            var idleDriver = idle.AddStateMachineBehaviour<VRCAvatarParameterDriver>();

            zeroDriver.localOnly = true;
            idleDriver.localOnly = true;


            string[] binaryParameters = GetBinaryParameters(target, resolution, symmetric);

            if(binaryValue.Length != binaryParameters.Length)
            {
                binaryValue = new float[binaryParameters.Length];
            }
            for (int i = 0; i < binaryParameters.Length; i++ )
            { 
                zeroDriver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter()
                {
                    type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set,
                    name = binaryParameters[i],
                    value = 0
                });

                idleDriver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter()
                {
                    type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set,
                    name = binaryParameters[i],
                    value = binaryValue[i]
                });
            }
            

            for (int i = 1; i < stateCount; i++)
            {
                AnimatorState currentPositive = animLayer.stateMachine.AddState("state" + i, new Vector3(250, 170 + 60 * i, 0));
                currentPositive.writeDefaultValues = writeDefault;


                float value = i / (Mathf.Pow(2, resolution) - 1);

                var anyToCurrent = animLayer.stateMachine.AddAnyStateTransition(currentPositive);
                anyToCurrent.duration = 0;
                anyToCurrent.canTransitionToSelf = false;

                anyToCurrent.AddCondition(AnimatorConditionMode.If, 1, enabler);

                anyToCurrent.AddCondition(AnimatorConditionMode.Greater, value - halfUnit, controller);

                if (i + 1 < stateCount)
                {
                    anyToCurrent.AddCondition(AnimatorConditionMode.Less, value + halfUnit, controller);
                }

                var driver = currentPositive.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                driver.localOnly = true;

                var bits = IntToBinary(i, resolution);
          
                for (int j = 0; j < resolution; j++)
                {
                    driver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter()
                    {
                        type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set,

                        name = binaryParameters[j],
                        value = bits[j] ? 1 : 0
                    });
                }
                if (symmetric)
                {
                    driver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter()
                    {
                        type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set,
                        name = binaryParameters[resolution],
                        value = 0
                    });
                }
                
            }
            if (symmetric)
            {
                for (int i = 1; i < stateCount; i++)
                {
                    AnimatorState currentNegative = animLayer.stateMachine.AddState("state -" + i, new Vector3(250, 170 - 60 * i, 0));
                    currentNegative.writeDefaultValues = writeDefault;

                    float value = -i / (Mathf.Pow(2, resolution) - 1);

                    var anyToCurrent = animLayer.stateMachine.AddAnyStateTransition(currentNegative);
                    anyToCurrent.duration = 0;
                    anyToCurrent.canTransitionToSelf = false;

                    anyToCurrent.AddCondition(AnimatorConditionMode.If, 1, enabler);

                    anyToCurrent.AddCondition(AnimatorConditionMode.Less, value + halfUnit, controller);

                    if (i + 1 < stateCount)
                    {
                        anyToCurrent.AddCondition(AnimatorConditionMode.Greater, value - halfUnit, controller);

                    }

                    var driver = currentNegative.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                    driver.localOnly = true;

                    var bits = IntToBinary(i, resolution);
               
                    for (int j = 0; j < resolution; j++)
                    {
                        driver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter()
                        {
                            type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set,
                            name = binaryParameters[j],
                            value = bits[j] ? 1 : 0
                        });
                    }

                    driver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter()
                    {
                        type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set,
                        name = binaryParameters[resolution],
                        value = 1
                    });
                }

            }

            return animLayer;
        }
        private static bool NeedMappingLocal(Runtime.QuantizationParameterSettings param)
        {
            return param.scaleFactor != 1 || param.combinedLocal;
        }

        public static AnimatorControllerLayer CreateQuantizationParameterLayer(UnityEngine.Object assetsContainer, AnimatorController _animatorController, 
            IEnumerable<Runtime.QuantizationParameterSettings> parameters, BlendTree[] localBlendTrees, BlendTree[] remoteBlendTrees, 
            string prefix = "QuantizationParameters", bool addPrefixAtOutput = true)
        {
            AnimatorControllerLayer animLayer = new AnimatorControllerLayer()
            {
                name = "Quantization Parameters",
                stateMachine = new AnimatorStateMachine() { name = "Quantization Parameters" },
                defaultWeight = 1
            };

            if(AssetDatabase.IsMainAsset(_animatorController))
            {
                animLayer.stateMachine.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(animLayer.stateMachine, _animatorController);
            }
            else
            {
                AssetDatabase.AddObjectToAsset(animLayer.stateMachine, assetsContainer);


            }

            AnimatorState localState = animLayer.stateMachine.AddState("Local", new Vector3(30, 170, 0));
            AnimatorState remoteState = animLayer.stateMachine.AddState("Remote", new Vector3(30, 170 + 60, 0));

            localState.writeDefaultValues = true;
            remoteState.writeDefaultValues = true;

            var toRemoteState = localState.AddTransition(remoteState);
            toRemoteState.duration = 0;

            var toLocalState = remoteState.AddTransition(localState);
            toLocalState.duration = 0;

            toRemoteState.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLocal");
            toLocalState.AddCondition(AnimatorConditionMode.If, 0, "IsLocal");



            // Creating BlendTree objects to better customize them in the AC Editor         
            var remoteRootBlendTree = new BlendTree()
            {
                blendType = BlendTreeType.Direct,
                hideFlags = HideFlags.HideInHierarchy,
                name = "Quantization_Parameters_Remote_Root",
                useAutomaticThresholds = false
            };

            var localRootBlendTree = new BlendTree()
            {
                blendType = BlendTreeType.Direct,
                hideFlags = HideFlags.HideInHierarchy,
                name = "Quantization_Parameters_Local_Root",
                useAutomaticThresholds = false

            };

            localState.motion = localRootBlendTree;
            remoteState.motion = remoteRootBlendTree;

            AssetDatabase.AddObjectToAsset(remoteRootBlendTree, assetsContainer);
            AssetDatabase.AddObjectToAsset(localRootBlendTree, assetsContainer);

            List<ChildMotion> remoteChildren = new List<ChildMotion>();
            List<ChildMotion> localChildren = new List<ChildMotion>();

            // Go through each parameter and create each child to eventually stuff into the Direct BlendTrees. 
            foreach (var param in parameters)
            {
                int bits = param.resolution + (param.symmetric ? 1 : 0);


                _animatorController.AddOrOverwriteParameter(param.parameterName, AnimatorControllerParameterType.Float, param.defaultValue);

                // Binary Gen
                if (param.resolution > 0 && bits < 8)
                {
                    foreach ( var io in param.GetInputOutputPairs(false, outputPrefix: GENERATED_PREFIX))
                    {
                        string input = addPrefixAtOutput ? io.Key : prefix + io.Key;
                        string[] outputs = io.Value;
                        remoteChildren.Add(new ChildMotion
                        {
                            directBlendParameter = ALWAYS_ONE,
                            motion = CreateBinaryBlendTree(assetsContainer, _animatorController, animLayer.stateMachine, 
                                input, outputs, param.resolution, param.symmetric, param.scaleFactor),
                            timeScale = 1
                        });
                    }
                }
                //Remote Mapping
                else
                {
                    foreach (var io in param.GetInputOutputPairs(false, outputPrefix: GENERATED_PREFIX))
                    {
                        string input = addPrefixAtOutput ? io.Key : prefix + io.Key;
                        string[] outputs = io.Value;

                        remoteChildren.Add(new ChildMotion
                        {
                            directBlendParameter = ALWAYS_ONE,
                            motion = CreateMappingBlendTree(assetsContainer, _animatorController, animLayer.stateMachine,
                                    input, outputs, param.scaleFactor),
                            timeScale = 1
                        });
                    }
                }

                //Mapping local
                if (NeedMappingLocal(param))
                {
                    foreach (var io in param.GetInputOutputPairs(true, outputPrefix: GENERATED_PREFIX))
                    {
                        string input = addPrefixAtOutput ? io.Key : prefix + io.Key;
                        string[] outputs = io.Value;
                        localChildren.Add(new ChildMotion
                        {
                            directBlendParameter = ALWAYS_ONE,
                            motion = CreateMappingBlendTree(assetsContainer, _animatorController, animLayer.stateMachine,
                                    input, outputs, param.scaleFactor),

                            timeScale = 1
                        });
                        
                    }

                }

                
                // Smooth
                foreach (string subParam in param.GetParameters())
                {
                    string localInput = NeedMappingLocal(param) ? GENERATED_PREFIX + subParam : subParam;

                    string output = addPrefixAtOutput ? prefix + subParam : subParam;


                    //localChildren.Add(new ChildMotion
                    //{
                    //    directBlendParameter = ALWAYS_ONE,
                    //    motion = CreateSmoothingBlendTree(assetsContainer, _animatorController, animLayer.stateMachine,
                    //    localInput, output, param.localSmoother, param.localSmoothness),
                    //    timeScale = 1
                    //});

                    var rootBlendTree = CreateLocalRemoteSmoothingBlendTree(assetsContainer, _animatorController, animLayer.stateMachine,
                        localInput, $"Generated/{subParam}", output, param.GetLinkedParameters(subParam), param.localSmoother, param.localSmoothness, param.remoteSmoother, param.remoteSmoothness);

                    localChildren.Add(new ChildMotion
                    {
                        directBlendParameter = ALWAYS_ONE,
                        motion = rootBlendTree.Item1,
                        //motion = CreateSmoothingBlendTree(assetsContainer, _animatorController, animLayer.stateMachine,
                        //localInput, output, param.GetLinkedParameters(subParam), param.localSmoother, param.localSmoothness),
                        timeScale = 1
                    });


                    if (param.resolution > 0)
                    {
                        remoteChildren.Add(new ChildMotion
                        {
                            directBlendParameter = ALWAYS_ONE,
                            motion = rootBlendTree.Item2,

                            //motion = CreateSmoothingBlendTree(assetsContainer, _animatorController, animLayer.stateMachine,
                            //$"Generated/{subParam}", output, param.GetLinkedParameters(subParam), param.remoteSmoother, param.remoteSmoothness),
                            timeScale = 1
                        });
                    }
                    else
                    {
                        _animatorController.AddOrOverwriteParameter(output, AnimatorControllerParameterType.Float, param.defaultValue);
                    }
                        

                }
            }


            HashSet<string> animatorParameters = new HashSet<string>();

            if (localBlendTrees != null)
            {
                foreach (var lacalTree in localBlendTrees)
                {
                    BlendTree local = CloneBlendTreeAndChangeBlendParameter(assetsContainer, lacalTree, parameters, true);
                    localChildren.Add(new ChildMotion
                    {
                        directBlendParameter = ALWAYS_ONE,
                        motion = local,
                        timeScale = 1
                    });
                    GetParametersInBlendTree(local, animatorParameters);
                }
            }

            if (remoteBlendTrees != null)
            {
                foreach (var remoteTree in remoteBlendTrees)
                {
                    BlendTree remote = CloneBlendTreeAndChangeBlendParameter(assetsContainer, remoteTree, parameters, false);

                    remoteChildren.Add(new ChildMotion
                    {
                        directBlendParameter = ALWAYS_ONE,
                        motion = remote,
                        timeScale = 1
                    });
                    GetParametersInBlendTree(remote, animatorParameters);
                }
            }

            foreach (string p in animatorParameters)
            {
                _animatorController.AddOrOverwriteParameter(p, AnimatorControllerParameterType.Float);
            }
            remoteRootBlendTree.children = remoteChildren.ToArray();
            localRootBlendTree.children = localChildren.ToArray();

            return animLayer;
        }
        public static BlendTree CloneBlendTreeAndChangeBlendParameter(UnityEngine.Object assetsContainer, BlendTree targetTree, IEnumerable<Runtime.QuantizationParameterSettings> parameters, bool isLocal)
        {
            //var clonedTree = UnityEngine.Object.Instantiate(targetTree);
            var clonedTree = new BlendTree() {
                blendParameter = targetTree.blendParameter,
                blendParameterY = targetTree.blendParameterY,
                blendType = targetTree.blendType,
                children = targetTree.children,
                maxThreshold = targetTree.maxThreshold,
                minThreshold = targetTree.minThreshold,
                name = targetTree.name,
                useAutomaticThresholds = targetTree.useAutomaticThresholds,
            };
           
            AssetDatabase.AddObjectToAsset(clonedTree, assetsContainer);

            // Change parameter
            if (!string.IsNullOrEmpty(clonedTree.blendParameter) && clonedTree.blendType != BlendTreeType.Direct)
            {
                string name = clonedTree.blendParameter;

                var qParam = parameters.FirstOrDefault(p => p.parameterName == name || p.subParameters.Contains(name));
                if(qParam != null)
                {
                    if (!isLocal)
                    {
                        clonedTree.blendParameter = GENERATED_PREFIX + name;
                    }
                    else if (NeedMappingLocal(qParam))
                    {
                        clonedTree.blendParameter = GENERATED_PREFIX + name;
                    }
                }
            }

            if (clonedTree.blendType != BlendTreeType.Direct && clonedTree.blendType != BlendTreeType.Simple1D)
            {
                string name = clonedTree.blendParameterY;

                if (!string.IsNullOrEmpty(name))
                {
                    var qParam = parameters.FirstOrDefault(p => p.parameterName == name || p.subParameters.Contains(name));
                    if (qParam != null)
                    {
                        if (!isLocal)
                        {
                            clonedTree.blendParameterY = GENERATED_PREFIX + name;
                        }
                        else if (NeedMappingLocal(qParam))
                        {
                            clonedTree.blendParameterY = GENERATED_PREFIX + name;
                        }
                    }
                }
            }

            if (clonedTree.blendType == BlendTreeType.Direct)
            {
                var children = clonedTree.children;

                for (int i = 0; i < children.Length; i++)
                {
                    string name = children[i].directBlendParameter;

                    if (!string.IsNullOrEmpty(name))
                    {
                        var qParam = parameters.FirstOrDefault(p => p.parameterName == name || p.subParameters.Contains(name));
                        if (qParam != null)
                        {
                            if (!isLocal)
                            {
                                children[i].directBlendParameter = GENERATED_PREFIX + name;
                            }
                            else if (NeedMappingLocal(qParam))
                            {
                                children[i].directBlendParameter = GENERATED_PREFIX + name;
                            }
                        }
                    }
                }

                clonedTree.children = children;
            }

            var clonedChildren = clonedTree.children;
            for (int i = 0; i < clonedChildren.Length; i++)
            {
                clonedChildren[i].threshold = targetTree.children[i].threshold;
                if (clonedChildren[i].motion is BlendTree childTree)
                {
                    clonedChildren[i].motion = CloneBlendTreeAndChangeBlendParameter(assetsContainer, childTree, parameters, isLocal);
                }
            }
            clonedTree.children = clonedChildren;
            return clonedTree;
        }

        public static void GetParametersInBlendTree(BlendTree blendTree, HashSet<string> parameterNames)
        {
           
            if (!string.IsNullOrEmpty(blendTree.blendParameter) && blendTree.blendType != BlendTreeType.Direct)
            {
                parameterNames.Add(blendTree.blendParameter);
            }

            if (blendTree.blendType != BlendTreeType.Direct && blendTree.blendType != BlendTreeType.Simple1D)
            {
                if (!string.IsNullOrEmpty(blendTree.blendParameterY))
                {
                    parameterNames.Add(blendTree.blendParameterY);
                }
            }

            if (blendTree.blendType == BlendTreeType.Direct)
            {
                foreach (var childMotion in blendTree.children)
                {
                    if (!string.IsNullOrEmpty(childMotion.directBlendParameter))
                    {
                        parameterNames.Add(childMotion.directBlendParameter);
                    }
                }
            }

            foreach (var childMotion in blendTree.children)
            {
                if(childMotion.motion is BlendTree childTree)
                {
                    GetParametersInBlendTree(childTree, parameterNames);
                }
            }
        }
        public static BlendTree CreateMappingBlendTree(UnityEngine.Object assetsContainer, AnimatorController animatorController, AnimatorStateMachine stateMachine,
            string input, string output, float scale = 1)
        {
            return CreateMappingBlendTree(assetsContainer, animatorController, stateMachine, input, new string[] { output }, scale);
        }
        public static BlendTree CreateMappingBlendTree(UnityEngine.Object assetsContainer, AnimatorController animatorController, AnimatorStateMachine stateMachine,
            string input, string[] outputs, float scale = 1)
        {
            animatorController.AddOrOverwriteParameter(input, AnimatorControllerParameterType.Float);

            foreach(string output in outputs)
            {
                animatorController.AddOrOverwriteParameter(output, AnimatorControllerParameterType.Float);
            }


            BlendTree blendtree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                name = $"Map_{input.Split("/").Last()}",
                blendParameter = input,
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(blendtree, assetsContainer);


            blendtree.AddChild(CreateParametarsAnimation(assetsContainer, animatorController, $"Map_{input}_{-scale}", outputs, -scale), -1);
            blendtree.AddChild(CreateParametarsAnimation(assetsContainer, animatorController, $"Map_{input}_{scale}", outputs, scale), 1);

            return blendtree;
        }

        public static BlendTree CreateBinaryBlendTree(UnityEngine.Object assetsContainer, AnimatorController animatorController, AnimatorStateMachine stateMachine, string input, string output, int resolution, bool symmetric, float scale = 1)
        {
            return CreateBinaryBlendTree(assetsContainer, animatorController, stateMachine, input, new string[] { output }, resolution, symmetric, scale);

        }
        public static BlendTree CreateBinaryBlendTree(UnityEngine.Object assetsContainer, AnimatorController animatorController, AnimatorStateMachine stateMachine, string input, string[] outputs, int resolution, bool symmetric, float scale = 1)
        {
            animatorController.AddOrOverwriteParameter(input, AnimatorControllerParameterType.Float);

            foreach (string output in outputs)
            {
                animatorController.AddOrOverwriteParameter(output, AnimatorControllerParameterType.Float);
            }

            BlendTree decodeBinaryPositiveTree = new BlendTree
            {
                blendType = BlendTreeType.Direct,
                hideFlags = HideFlags.HideInHierarchy,
                name = "Binary_" + input + "_Positive",
                useAutomaticThresholds = false
            };

            List<ChildMotion> childBinaryPositiveDecode = new List<ChildMotion>();

            for (int i = 0; i < resolution; i++)
            {

                string binaryParam = input + Mathf.Pow(2, i).ToString();

                animatorController.AddOrOverwriteParameter(binaryParam, AnimatorControllerParameterType.Float);
    

                float value = scale * Mathf.Pow(2, i) / (Mathf.Pow(2, resolution) - 1);
                childBinaryPositiveDecode.Add(new ChildMotion
                {
                    directBlendParameter = binaryParam,
                    motion = CreateParametarsAnimation(assetsContainer, animatorController, binaryParam, outputs, value),
                    timeScale = 1
                });

            }
            decodeBinaryPositiveTree.children = childBinaryPositiveDecode.ToArray();

            AssetDatabase.AddObjectToAsset(decodeBinaryPositiveTree, assetsContainer);

            if (symmetric)
            {
                animatorController.AddOrOverwriteParameter(input + "Negative", AnimatorControllerParameterType.Float);

                BlendTree decodeBinaryRoot = new BlendTree
                {
                    blendType = BlendTreeType.Simple1D,
                    hideFlags = HideFlags.HideInHierarchy,
                    blendParameter = input + "Negative",
                    name = "Binary_" + input + "_Root",
                    useAutomaticThresholds = false
                };

                BlendTree decodeBinaryNegativeTree = new BlendTree
                {
                    blendType = BlendTreeType.Direct,
                    hideFlags = HideFlags.HideInHierarchy,
                    name = "Binary_" + input + "_Negative",
                    useAutomaticThresholds = false
                };

                List<ChildMotion> childBinaryNegativeDecode = new List<ChildMotion>();
                for (int i = 0; i < resolution; i++)
                {
                    string binaryParam = input + Mathf.Pow(2, i).ToString();

                    float value = -scale * Mathf.Pow(2, i) / (Mathf.Pow(2, resolution) - 1);


                    childBinaryNegativeDecode.Add(new ChildMotion
                    {
                        directBlendParameter = binaryParam,
                        motion = CreateParametarsAnimation(assetsContainer, animatorController, $"{input}-{Mathf.Pow(2, i).ToString()}", outputs, value),
                        timeScale = 1
                    });

                }

                decodeBinaryNegativeTree.children = childBinaryNegativeDecode.ToArray();

                decodeBinaryRoot.AddChild(decodeBinaryPositiveTree, 0f);
                decodeBinaryRoot.AddChild(decodeBinaryNegativeTree, 1f);

                AssetDatabase.AddObjectToAsset(decodeBinaryNegativeTree, assetsContainer);
                AssetDatabase.AddObjectToAsset(decodeBinaryRoot, assetsContainer);

                return decodeBinaryRoot;
            }

            return decodeBinaryPositiveTree;
        }


        //Create AAP animation clip
        public static AnimationClip CreateParametarsAnimation(UnityEngine.Object assetsContainer, AnimatorController animatorController, string clipName, string[] parameters, float value = 1)
        {
            AnimationClip clip = new AnimationClip() { name = clipName.Replace("/", "_") };
            foreach (string param in parameters)
            {
                AnimationCurve curve = new AnimationCurve(new Keyframe(0.0f, value));

                clip.SetCurve("", typeof(Animator), param, curve);
            }
            clip.hideFlags = HideFlags.None;

            AssetDatabase.AddObjectToAsset(clip, assetsContainer);
            return clip;
        }
        public static AnimationClip CreateParametarsAnimation(UnityEngine.Object assetsContainer, AnimatorController animatorController, string clipName, string[] parameters, float[] values)
        {
            if(parameters.Length > values.Length)
            {
                int difference = parameters.Length - values.Length;

                Array.Resize(ref values, difference);

                for (int i = values.Length - difference; i < values.Length; i++)
                {
                    values[i] = 1;
                }
            }

            AnimationClip clip = new AnimationClip() { name = clipName.Replace("/", "_") };
            for(int i = 0; i < parameters.Length; i++)
            {
                AnimationCurve curve = new AnimationCurve(new Keyframe(0.0f, values[i]));

                clip.SetCurve("", typeof(Animator), parameters[i], curve);
            }
            clip.hideFlags = HideFlags.None;

            AssetDatabase.AddObjectToAsset(clip, assetsContainer);
            return clip;
        }

        public static AnimationClip CreateParametarAnimation(UnityEngine.Object assetsContainer, AnimatorController animatorController, string parameter, float value = 1)
        {
            AnimationClip clip = new AnimationClip() { name = $"{parameter.Replace("/", "_")}_{value}" };
            

            AnimationCurve curve = new AnimationCurve(new Keyframe(0.0f, value));

            clip.SetCurve("", typeof(Animator), parameter, curve);
            clip.hideFlags = HideFlags.None;

            AssetDatabase.AddObjectToAsset(clip, assetsContainer);

            return clip;
        }


        public static BlendTree CreateSmoothingBlendTree(UnityEngine.Object assetsContainer, AnimatorController animatorController, AnimatorStateMachine stateMachine, string input, string output, string smoother, float smoothness, float range = 1f)
        {
            animatorController.AddOrOverwriteParameter(smoother, AnimatorControllerParameterType.Float, smoothness);
            animatorController.AddOrOverwriteParameter(output, AnimatorControllerParameterType.Float);
            animatorController.AddOrOverwriteParameter(input, AnimatorControllerParameterType.Float);


            string paramName = input.Split("/").Last();
            // Creating 3 blend trees to create the feedback loop
            BlendTree rootTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = smoother,
                name = paramName + "_Root",
                useAutomaticThresholds = false
            };
            BlendTree inputTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = input,
                name = paramName + "_Input",
                useAutomaticThresholds = false
            };
            BlendTree outputTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = output,
                name = paramName + "_Output",
                useAutomaticThresholds = false
            };

        
            AnimationClip start = CreateParametarAnimation(assetsContainer, animatorController, output, -range);
            AnimationClip end = CreateParametarAnimation(assetsContainer, animatorController, output, range);

            rootTree.AddChild(inputTree, 0);
            rootTree.AddChild(outputTree, 1);

            inputTree.AddChild(start, -1f);
            inputTree.AddChild(end, 1f);

            outputTree.AddChild(start, -range);
            outputTree.AddChild(end, range);

            AssetDatabase.AddObjectToAsset(rootTree, assetsContainer);
            AssetDatabase.AddObjectToAsset(inputTree, assetsContainer);
            AssetDatabase.AddObjectToAsset(outputTree, assetsContainer);
            return rootTree;
        }
        public static BlendTree CreateSmoothingBlendTree(UnityEngine.Object assetsContainer, AnimatorController animatorController, AnimatorStateMachine stateMachine, string input, string output, (string, float)[] linkedParameters, string smoother, float smoothness, float range = 1f)
        {
            animatorController.AddOrOverwriteParameter(smoother, AnimatorControllerParameterType.Float, smoothness);
            animatorController.AddOrOverwriteParameter(output, AnimatorControllerParameterType.Float);
            animatorController.AddOrOverwriteParameter(input, AnimatorControllerParameterType.Float);

            string[] linkedParametersName = linkedParameters.Select(tuple => tuple.Item1).ToArray();

            foreach (string name in linkedParametersName)
            {
                animatorController.AddOrOverwriteParameter(name, AnimatorControllerParameterType.Float);
            }

            string paramName = input.Split("/").Last();
            // Creating 3 blend trees to create the feedback loop
            BlendTree rootTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = smoother,
                name = paramName + "_Root",
                useAutomaticThresholds = false
            };
            BlendTree inputTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = input,
                name = paramName + "_Input",
                useAutomaticThresholds = false
            };
            BlendTree outputTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = output,
                name = paramName + "_Output",
                useAutomaticThresholds = false
            };


            AnimationClip startClip, endClip;
            if (linkedParameters.Length > 0)
            {
                float[] parametersWieghtStart = linkedParameters.Select(tuple => tuple.Item2 * -range).Append(-range).ToArray();
                float[] parametersWieghtEnd = linkedParameters.Select(tuple => tuple.Item2 * range).Append(range).ToArray();
                string[] parameters = linkedParametersName.Append(output).ToArray();

                startClip = CreateParametarsAnimation(assetsContainer, animatorController, $"LinkedParam-{output}", parameters, parametersWieghtStart);
                endClip = CreateParametarsAnimation(assetsContainer, animatorController, $"LinkedParam-{output}", parameters, parametersWieghtEnd);
            }
            else
            {
                 startClip = CreateParametarAnimation(assetsContainer, animatorController, output, -range);
                 endClip = CreateParametarAnimation(assetsContainer, animatorController, output, range);
            }

            rootTree.AddChild(inputTree, 0);
            rootTree.AddChild(outputTree, 1);

            inputTree.AddChild(startClip, -1f);
            inputTree.AddChild(endClip, 1f);

            outputTree.AddChild(startClip, -range);
            outputTree.AddChild(endClip, range);

            AssetDatabase.AddObjectToAsset(rootTree, assetsContainer);
            AssetDatabase.AddObjectToAsset(inputTree, assetsContainer);
            AssetDatabase.AddObjectToAsset(outputTree, assetsContainer);
            return rootTree;
        }

        public static BlendTree CreateSmoothingBlendTree(UnityEngine.Object assetsContainer, AnimatorController animatorController, AnimatorStateMachine stateMachine, string input, string output, Runtime.QuantizationParametersSO.LinkedParameter[] linkedParameters, string smoother, float smoothness, float range = 1f)
        {
            animatorController.AddOrOverwriteParameter(smoother, AnimatorControllerParameterType.Float, smoothness);
            animatorController.AddOrOverwriteParameter(output, AnimatorControllerParameterType.Float);
            animatorController.AddOrOverwriteParameter(input, AnimatorControllerParameterType.Float);

            string[] linkedParametersName = linkedParameters.Select(l => l.m_ParameterName).ToArray();

            foreach (string name in linkedParametersName)
            {
                animatorController.AddOrOverwriteParameter(name, AnimatorControllerParameterType.Float);
            }

            string paramName = input.Split("/").Last();
            // Creating 3 blend trees to create the feedback loop
            BlendTree rootTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = smoother,
                name = paramName + "_Root",
                useAutomaticThresholds = false
            };
            BlendTree inputTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = input,
                name = paramName + "_Input",
                useAutomaticThresholds = false,
            };
            BlendTree outputTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = output,
                name = paramName + "_Output",
                useAutomaticThresholds = false,
            };


            AnimationClip startClip, endClip;
            string[] parameters = linkedParametersName.Append(output).ToArray();
            if (linkedParameters.Length > 0)
            {
                float[] parametersWieghtStart = linkedParameters.Select(l => l.m_Weight * (l.m_Absolute? 1:-1) * range).Append(-range).ToArray();
                float[] parametersWieghtEnd = linkedParameters.Select(l => l.m_Weight * range).Append(range).ToArray();

                startClip = CreateParametarsAnimation(assetsContainer, animatorController, $"Linked-{output}_-1", parameters, parametersWieghtStart);
                endClip = CreateParametarsAnimation(assetsContainer, animatorController, $"Linked-{output}_1", parameters, parametersWieghtEnd);
            }
            else
            {
                startClip = CreateParametarAnimation(assetsContainer, animatorController, output, -range);
                endClip = CreateParametarAnimation(assetsContainer, animatorController, output, range);
            }

            rootTree.AddChild(inputTree, 0);
            rootTree.AddChild(outputTree, 1);

            inputTree.AddChild(startClip, -1f);
            outputTree.AddChild(startClip, -range);

            if (linkedParameters.Any(l => l.m_Absolute))
            {
                AnimationClip zeroClip = CreateParametarsAnimation(assetsContainer, animatorController, $"Linked-{output}_0", parameters, 0);
                inputTree.AddChild(zeroClip, 0);
                outputTree.AddChild(zeroClip, 0);
            }

            inputTree.AddChild(endClip, 1f);
            outputTree.AddChild(endClip, range);

            AssetDatabase.AddObjectToAsset(rootTree, assetsContainer);
            AssetDatabase.AddObjectToAsset(inputTree, assetsContainer);
            AssetDatabase.AddObjectToAsset(outputTree, assetsContainer);
            return rootTree;
        }

        public static (BlendTree, BlendTree) CreateLocalRemoteSmoothingBlendTree(UnityEngine.Object assetsContainer, AnimatorController animatorController, AnimatorStateMachine stateMachine, 
            string input, string remoteInput, string output, Runtime.QuantizationParametersSO.LinkedParameter[] linkedParameters, 
            string smoother, float smoothness, string remoteSmoother, float remoteSmoothness, float range = 1f)
        {
            animatorController.AddOrOverwriteParameter(smoother, AnimatorControllerParameterType.Float, smoothness);
            animatorController.AddOrOverwriteParameter(remoteSmoother, AnimatorControllerParameterType.Float, remoteSmoothness);

            animatorController.AddOrOverwriteParameter(output, AnimatorControllerParameterType.Float);
            animatorController.AddOrOverwriteParameter(input, AnimatorControllerParameterType.Float);
            animatorController.AddOrOverwriteParameter(remoteInput, AnimatorControllerParameterType.Float);

            string[] linkedParametersName = linkedParameters.Select(l => l.m_ParameterName).ToArray();

            foreach (string name in linkedParametersName)
            {
                animatorController.AddOrOverwriteParameter(name, AnimatorControllerParameterType.Float);
            }

            string paramName = input.Split("/").Last();
            // Creating 3 blend trees to create the feedback loop
            BlendTree rootTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = smoother,
                name = paramName + "_Root",
                useAutomaticThresholds = false
            };
            BlendTree inputTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = input,
                name = paramName + "_Input",
                useAutomaticThresholds = false,
            };
            BlendTree outputTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = output,
                name = paramName + "_Output",
                useAutomaticThresholds = false,
            };

            //Remote
            BlendTree rootTreeRemote = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = remoteSmoother,
                name = paramName + "_Root",
                useAutomaticThresholds = false
            };
            BlendTree inputTreeRemote = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = remoteInput,
                name = paramName + "_Input",
                useAutomaticThresholds = false,
            };
            BlendTree outputTreeRemote = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = output,
                name = paramName + "_Output",
                useAutomaticThresholds = false,
            };
            //

            AnimationClip startClip, endClip;
            string[] parameters = linkedParametersName.Append(output).ToArray();
            if (linkedParameters.Length > 0)
            {
                float[] parametersWieghtStart = linkedParameters.Select(l => l.m_Weight * (l.m_Absolute ? 1 : -1) * range).Append(-range).ToArray();
                float[] parametersWieghtEnd = linkedParameters.Select(l => l.m_Weight * range).Append(range).ToArray();

                startClip = CreateParametarsAnimation(assetsContainer, animatorController, $"Linked-{output}_-1", parameters, parametersWieghtStart);
                endClip = CreateParametarsAnimation(assetsContainer, animatorController, $"Linked-{output}_1", parameters, parametersWieghtEnd);
            }
            else
            {
                startClip = CreateParametarAnimation(assetsContainer, animatorController, output, -range);
                endClip = CreateParametarAnimation(assetsContainer, animatorController, output, range);
            }

            rootTree.AddChild(inputTree, 0);
            rootTree.AddChild(outputTree, 1);

            inputTree.AddChild(startClip, -1f);
            outputTree.AddChild(startClip, -range);

            //           
            rootTreeRemote.AddChild(inputTreeRemote, 0);
            rootTreeRemote.AddChild(outputTreeRemote, 1);

            inputTreeRemote.AddChild(startClip, -1f);
            outputTreeRemote.AddChild(startClip, -range);
            //

            if (linkedParameters.Any(l => l.m_Absolute))
            {
                AnimationClip zeroClip = CreateParametarsAnimation(assetsContainer, animatorController, $"Linked-{output}_0", parameters, 0);
                inputTree.AddChild(zeroClip, 0);
                outputTree.AddChild(zeroClip, 0);

                inputTreeRemote.AddChild(zeroClip, 0);
                outputTreeRemote.AddChild(zeroClip, 0);
            }

            inputTree.AddChild(endClip, 1f);
            outputTree.AddChild(endClip, range);

            //
            inputTreeRemote.AddChild(endClip, 1f);
            outputTreeRemote.AddChild(endClip, range);

            AssetDatabase.AddObjectToAsset(rootTree, assetsContainer);
            AssetDatabase.AddObjectToAsset(inputTree, assetsContainer);
            AssetDatabase.AddObjectToAsset(outputTree, assetsContainer);

            AssetDatabase.AddObjectToAsset(rootTreeRemote, assetsContainer);
            AssetDatabase.AddObjectToAsset(inputTreeRemote, assetsContainer);
            AssetDatabase.AddObjectToAsset(outputTreeRemote, assetsContainer);

            return (rootTree, rootTreeRemote);
        }
        
        public static AnimatorControllerParameter AddOrOverwriteParameter(this AnimatorController animatorController, string paramName, AnimatorControllerParameterType type, float? defaultVal = null)
        {

            // Check if the parameter already exists
            var existingParam = Array.Find(animatorController.parameters, p => p.name == paramName);

            if (existingParam != null)
            {
                // Update the existing parameter's values if a default value is provided
                if (defaultVal.HasValue)
                {
                    existingParam.defaultFloat = defaultVal.Value;
                    existingParam.defaultInt = (int)defaultVal.Value;
                    existingParam.defaultBool = Convert.ToBoolean(defaultVal.Value);
                }
                return existingParam;
            }

            // Create and add a new parameter
            var newParameter = new AnimatorControllerParameter
            {
                name = paramName,
                type = type
            };

            if (defaultVal.HasValue)
            {
                newParameter.defaultFloat = defaultVal.Value;
                newParameter.defaultInt = (int)defaultVal.Value;
                newParameter.defaultBool = Convert.ToBoolean(defaultVal.Value);
            }

            animatorController.AddParameter(newParameter);
            return newParameter;
        }

    }
}


