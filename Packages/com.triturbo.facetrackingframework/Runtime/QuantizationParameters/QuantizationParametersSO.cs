using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Triturbo.FaceTrackingFramework.QuantizationParameters.Runtime
{
    [CreateAssetMenu(menuName = "TriturboFT/Quantization Parameters")]

    public class QuantizationParametersSO : ScriptableObject
    {
        public string m_Prefix;
        public QuantizationParameter[] m_Parameters;

        public string[] m_LocalSmoothers;
        public string[] m_RemoteSmoothers;
        [System.Serializable]
        public class QuantizationParameter
        {

            public string m_ParameterName;
            public bool m_Symmetric;

            public int m_Resolution;
            public float m_DefaultValue;
            //public string[] m_SubParameters;
 
            public SubParameter[] m_SubParameters;
            public LinkedParameter[] m_LinkedParameters;

            public int m_LocalSmootherIndex;
            public int m_RemoteSmootherIndex;

            public string[] SubParameters
            {
                get
                {
                    if(subParametersName == null)
                    {
                        subParametersName = m_SubParameters.Select(s => s.m_ParameterName).ToArray();
                    }
                    
                    return subParametersName;
                }
            }
            [System.NonSerialized]
            private string[] subParametersName;
        }
        [System.Serializable]
        public class SubParameter
        {
            public string m_ParameterName;
            public LinkedParameter[] m_LinkedParameters;
        }

        [System.Serializable]
        public class LinkedParameter
        {
            public string m_ParameterName;
            public float m_Weight = 1;
            public bool m_Absolute = false;
        }
    }
}


