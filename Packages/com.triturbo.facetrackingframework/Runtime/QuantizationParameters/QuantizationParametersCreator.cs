using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using VRC.SDKBase;

namespace Triturbo.FaceTrackingFramework.QuantizationParameters.Runtime
{
    [HelpURL("https://tr1turbo.github.io/Docs/face-tracking-addon/quantization-parameters-creator.html")]
    [AddComponentMenu("TriturboFT/Quantization Parameters Creator")]
    public class QuantizationParametersCreator : MonoBehaviour, IEditorOnly
    {
        public QuantizationParametersSO m_QuantizationParameters;
        public QuantizationParameterOverrideSettings[] m_OverrideSettings;
        public QuantizationParameterDriverConfig[] m_QuantizationParameterDrivers;
        
        public bool m_EnableLayerSelection = false;

        public UnityEngine.Object[] m_LocalBlendTrees;
        public UnityEngine.Object[] m_RemoteBlendTrees;

        public float[] m_RemoteSmoothnessList;
        public float[] m_LocalSmoothnessList;

        //public string prefix;

        public List<QuantizationParameterSettings> GetParameterSettingsList(int layer = AnimLayerMask.FX)
        {
            List<QuantizationParameterSettings> parameterSettings = new List<QuantizationParameterSettings>();
            if (m_QuantizationParameters == null)
            {
                return parameterSettings;
            }

            if (!m_EnableLayerSelection && layer != AnimLayerMask.FX)
            {
                return parameterSettings;
            }
            string outputPrefix = m_QuantizationParameters.m_Prefix;
            for (int i = 0; i < m_QuantizationParameters.m_Parameters.Length; i++)
            {
                
                var param = m_QuantizationParameters.m_Parameters[i];
                

                var overrideSettings = m_OverrideSettings.FirstOrDefault(p => p.m_ParameterName == param.m_ParameterName);
                
                if (m_EnableLayerSelection && !AnimLayerMask.IsEnabled(overrideSettings.m_InstallingLayers, layer))
                {
                    continue;
                }
                
                int resolution = overrideSettings != null ? overrideSettings.m_Resolution : param.m_Resolution;
                bool combinedLocal = overrideSettings != null ? overrideSettings.m_CombinedLocal : false;
                bool combinedRemote = overrideSettings != null ? overrideSettings.m_CombinedRemote : false;

                float scaleFactor = overrideSettings != null ? overrideSettings.m_ScaleFactor : 1;
                string[] linkedParameters = overrideSettings != null ? overrideSettings.m_LinkedParameters : new string[0];

                string localSmoother = GetValueSafe(param.m_LocalSmootherIndex, m_QuantizationParameters.m_LocalSmoothers, "LocalSmoother");
                float localSmoothness = GetValueSafe(param.m_LocalSmootherIndex, m_LocalSmoothnessList, 0.08f);

                string remoteSmoother = GetValueSafe(param.m_RemoteSmootherIndex, m_QuantizationParameters.m_RemoteSmoothers, "RemoteSmoother");
                float remoteSmoothness = GetValueSafe(param.m_RemoteSmootherIndex, m_RemoteSmoothnessList, 0.7f);



                parameterSettings.Add(new QuantizationParameterSettings(param) {
                    localSmoother = localSmoother,
                    remoteSmoother = remoteSmoother,

                    //Override settings
                    combinedLocal = combinedLocal,
                    combinedRemote = combinedRemote,
                    
                    remoteSmoothness = remoteSmoothness,
                    localSmoothness = localSmoothness,
                    scaleFactor = scaleFactor,
                    resolution = resolution,
                });
            }
            return parameterSettings;
        }


        private T GetValueSafe<T>(int index, T[] array, T defaultValue)
        {
            if (array == null) return defaultValue;

            return index >= 0 && index < array.Length ? array[index] : defaultValue;
        }


    }
    
    public static class AnimLayerMask
    {
        public const int Base = 1 << 0;      // 00001
        public const int Additive = 1 << 1;  // 00010
        public const int Gesture = 1 << 2;   // 00100
        public const int Action = 1 << 3;    // 01000
        public const int FX = 1 << 4;        // 10000

        public static int GetMask(int index)
        {
            return 1 << index;
        }
        public static bool IsEnabled(int mask, int targetFlag)
        {
            return (mask & targetFlag) != 0;
        }
        public static readonly string[] LayersName ={"Base", "Additive", "Gesture", "Action", "FX"};
    }



    [Serializable]
    public class QuantizationParameterDriverConfig
    {
        public string m_Target;
        public string m_Controller;
        public string m_Toggle;
    }

    [Serializable]
    public class QuantizationParameterOverrideSettings
    {
        public string m_ParameterName;
        public int m_Resolution;
        public float m_ScaleFactor = 1f;
        public bool m_CombinedLocal = false;
        public bool m_CombinedRemote = false;
        public string[] m_LinkedParameters;
        public int m_InstallingLayers = AnimLayerMask.FX;
    }

    public class QuantizationParameterSettings
    {
        public string[] GetParameters(string prefix = "")
        {
            if(subParameters.Length > 0)
            {
                return subParameters.Select(s => prefix + s).ToArray();
            }
            
            return new string[] { prefix + parameterName };
        }

        public Dictionary<string, string[]> GetInputOutputPairs(bool isLocal, string inputPrefix = "", string outputPrefix = "")
        {
            Dictionary<string, string[]> parameterDict = new Dictionary<string, string[]>();  
            if(isLocal && combinedLocal)
            {
                parameterDict.Add(inputPrefix + parameterName, GetParameters(outputPrefix));
                return parameterDict;
            }

            if (!isLocal && combinedRemote)
            {
                parameterDict.Add(inputPrefix + parameterName, GetParameters(outputPrefix));
                return parameterDict;
            }

            foreach(string p in GetParameters())
            {
                parameterDict.Add(inputPrefix + p, new string[] { outputPrefix + p });
            }

            return parameterDict;
        }

        public QuantizationParameterSettings(QuantizationParametersSO.QuantizationParameter param)
        {
            parameterName = param.m_ParameterName;
            subParameters = param.m_SubParameters.Select(s => s.m_ParameterName).ToArray();
            defaultValue = param.m_DefaultValue;
            symmetric = param.m_Symmetric;


            linkedParameters = new Dictionary<string, QuantizationParametersSO.LinkedParameter[]>();

            linkedParameters.Add(parameterName, 
                param.m_LinkedParameters.GroupBy(lp => lp.m_ParameterName)
                .Select(g => g.First()).ToArray());

            foreach (var sub in param.m_SubParameters)
            {
                if (string.IsNullOrWhiteSpace(sub.m_ParameterName))
                {
                    Debug.LogWarning($"Skipped entry with empty sub parameter for {param.m_ParameterName}");
                    continue;
                }

                //var linkedParams = param.m_LinkedParameters
                //    .Select(lp => (lp.m_ParameterName, lp.m_Weight / param.m_SubParameters.Length))
                //    .Concat(sub.m_LinkedParameters.Select(lp => (lp.m_ParameterName, lp.m_Weight)))
                //    .GroupBy(lp => lp.m_ParameterName) // Group by m_ParameterName
                //    .Select(g => g.First()) // Select unique names and their weights
                //    .ToArray();

                var linkedParams = param.m_LinkedParameters
                    .Select(lp => new QuantizationParametersSO.LinkedParameter
                    {
                        m_ParameterName = lp.m_ParameterName,
                        m_Weight = lp.m_Weight / param.m_SubParameters.Length,
                        m_Absolute = lp.m_Absolute
                    })
                    .Concat(sub.m_LinkedParameters)
                    .GroupBy(lp => lp.m_ParameterName) // Group by m_ParameterName
                    .Select(g => g.First()) // Select unique names and their weights
                    .ToArray();

                if (!linkedParameters.TryAdd(sub.m_ParameterName, linkedParams))
                {
                    // Skip if the sub parameter is the same as the main parameter
                    if (sub.m_ParameterName != param.m_ParameterName)
                    {
                        Debug.LogWarning(
                            $"Duplicate sub-parameter '{sub.m_ParameterName}' detected in main parameter '{param.m_ParameterName}'."
                        );
                    }
                }
            }

        }

        //public (string, float)[] GetLinkedParameters(string param)
        //{
        //    return linkedParameters.GetValueOrDefault(param, Array.Empty<(string, float)>());
        //}

        public QuantizationParametersSO.LinkedParameter[] GetLinkedParameters(string param)
        {
            return linkedParameters.GetValueOrDefault(param, Array.Empty<QuantizationParametersSO.LinkedParameter>());
        }

        public string parameterName;
        public string[] subParameters;
        private Dictionary<string, QuantizationParametersSO.LinkedParameter[]> linkedParameters;

        public bool combinedLocal;
        public bool combinedRemote;


        public float defaultValue;
        public float scaleFactor;

        public bool symmetric;
        public int resolution;




        public string localSmoother;
        public string remoteSmoother;

        public float remoteSmoothness;
        public float localSmoothness;

        public string[] BinaryParameters
        {
            get
            {
                if(resolution < 1)
                {
                    return new string[0];
                }
                string[] binary = new string[resolution + (symmetric ? 1 : 0)];
                for (int i = 0; i < resolution; i++)
                {
                    binary[i] = parameterName + Mathf.Pow(2, i).ToString();
                }

                if (symmetric)
                {
                    binary[resolution] = parameterName + "Negative";

                }

                return binary;
            }

        }

        public float[] BinaryValues
        {
            get
            {
                float[] binary = new float[resolution + (symmetric ? 1 : 0)];

                if (symmetric)
                {
                    binary[resolution] = defaultValue < 0 ? 1 : 0;
                }


                float f = Math.Abs(defaultValue);


                // Multiply the float by 2 and extract the integer part
                for (int i = 0; i < resolution; i++)
                {
                    f *= 2;
                    binary[resolution - i - 1] = (float)Math.Floor(f);
                    f -= (float)Math.Floor(f);
                }

                return binary;

            }
        }


    }




}


