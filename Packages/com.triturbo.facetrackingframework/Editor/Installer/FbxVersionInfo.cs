using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;
#if ENABLE_BS
using Triturbo.BlendShapeShare.BlendShapeData;
#endif

namespace Triturbo.FaceTrackingFramework.Installer
{
    [CreateAssetMenu(fileName = "FbxVersionInfo", menuName = "TriturboFT/Addon/FbxVersionInfo")]
    public class FbxVersionInfo : ScriptableObject
    {
        public FbxObjectInfo[] m_FbxObjects;
        
        public bool IsAnyFbxImported
        {
            get
            {
                foreach (var fbxObjectInfo in m_FbxObjects)
                {
                    if(fbxObjectInfo.m_Fbx != null) return true;
                }
                return false;
            }
        }
        
        public FbxObjectInfo GetFbxObjectInfo(GameObject fbx)
        {
            if (fbx == null) return null;
            return m_FbxObjects?.FirstOrDefault(objInfo => objInfo.m_Fbx == fbx);
        }
        
        
        public FbxObjectInfo GetFbxObjectInfo(FbxVersioning fbxVersion)
        {
            if (fbxVersion == null) return null;
            return m_FbxObjects?.FirstOrDefault(objInfo => objInfo.m_FbxVersionings.Contains(fbxVersion));
        }
        
        
        public FbxVersioning GetFbxVersion(GameObject fbx)
        { 
            if (fbx == null) return null;
            var fbxObjectInfo = GetFbxObjectInfo(fbx);
            //if the object guid is different still find via sha256
            if (fbxObjectInfo == null)
            {
                string hash = CalculateHash(fbx);
                foreach (var current in m_FbxObjects)
                {
                    FbxVersioning version = current.m_FbxVersionings?.FirstOrDefault(objInfo => objInfo.m_Sha256 == hash);
                    if (version != null)
                    {
                        return version?.Clone();
                    }
                }
            }
            return GetFbxVersion(fbxObjectInfo);
        }
        public FbxVersioning[] GetFbxVersions()
        {
            return m_FbxObjects.SelectMany(objInfo => objInfo.m_FbxVersionings).ToArray();
        }

        
        private FbxVersioning GetFbxVersion(FbxObjectInfo fbxObjectInfo)
        {
            if(fbxObjectInfo == null) return null;
            if(fbxObjectInfo.m_Fbx == null) return null;
            var match = fbxObjectInfo.m_FbxVersionings?.FirstOrDefault(objInfo => objInfo.m_Sha256 == CalculateHash(fbxObjectInfo.m_Fbx));
            
            return match?.Clone();
        }
        
        
        [Serializable]
        public class FbxObjectInfo
        {
            public string m_Tag;
            public GameObject m_Fbx;
            public FbxVersioning[] m_FbxVersionings;
        }
        
        [Serializable]
        public class FbxVersioning
        {
            public string m_Version;
            public string m_Sha256;
            
            [SerializeField]
            private bool m_Unsupported;
#if ENABLE_BS
            public BlendShapeDataSO m_BlendShapeData;
#else
            public ScriptableObject m_BlendShapeData;
#endif
            public bool Unsupported
            {
                get { return m_Unsupported || m_BlendShapeData == null; }
            }
            
            public FbxVersioning Clone()
            {
                return (FbxVersioning)MemberwiseClone(); // shallow copy
            }
            
            public override bool Equals(object obj)
            {
                if (obj is FbxVersioning other)
                    return m_Sha256 == other.m_Sha256 && m_Version == other.m_Version;
                return false;
            }
            
            public override int GetHashCode() => HashCode.Combine(m_Version, m_Sha256, m_BlendShapeData);

        }


        
  
        public static string CalculateHash(UnityObject obj)
        {
            if(obj == null)
            {
                return "";
            }

            string filePath = AssetDatabase.GetAssetPath(obj);
            
            if(string.IsNullOrEmpty(filePath)) return "";

            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hashBytes).ToLower().Replace("-", "");
                }
            }
        }
       
        

    }

}


