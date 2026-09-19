using System;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace Triturbo.FaceTrackingFramework.Installer
{
    public class MeshAssetMetadata: ScriptableObject
    {
        public string m_Version;
        public string m_Sha256;
        [FormerlySerializedAs("m_Fbx")] public Object m_MeshProvider;
        
        public FbxVersionInfo.FbxVersioning m_OriginalFbxVersion;

        public override bool Equals(object obj)
        {
            return obj is MeshAssetMetadata other &&
                   m_Version == other.m_Version &&
                   m_Sha256 == other.m_Sha256 && 
                   m_MeshProvider == other.m_MeshProvider;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(m_Version, m_Sha256, m_MeshProvider);
        }
    }
}