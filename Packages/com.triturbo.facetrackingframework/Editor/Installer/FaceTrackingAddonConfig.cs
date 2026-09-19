using UnityEngine;
using System;

using UnityEditor;
using UnityObject = UnityEngine.Object;

#if ENABLE_BS
using Triturbo.BlendShapeShare.BlendShapeData;
#endif

namespace Triturbo.FaceTrackingFramework.Installer
{
    [CustomEditor(typeof(FaceTrackingAddonConfig))]
    public class FaceTrackingAddonConfigEditor : Editor
    {
        bool locked = true;
        public void OnEnable()
        {
            UpdateCost();
        }
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button(locked ? "Unlock" : "Lock")) locked = !locked;
            
            if (GUILayout.Button("Update Cost"))
            {
                UpdateCost();
            }
            // if (GUILayout.Button("Reload Languages"))
            // {
            //     Localization.LoadAvailableLanguages();
            // }
            GUI.enabled = !locked;
            DrawDefaultInspector();
        }

        private void UpdateCost()
        {
            var data = target as FaceTrackingAddonConfig;
            if(data == null) return;

            for (int i = 0; i < data.m_EyeTrackingPrefabs.Length; i++)
            {
                data.m_EyeTrackingPrefabs[i].m_Cost = ParameterUtil.GetMAParameterMemoryUsed(data.m_EyeTrackingPrefabs[i].m_Prefab);
            }
            
            for (int i = 0; i < data.m_MouthTrackingPrefabs.Length; i++)
            {
                data.m_MouthTrackingPrefabs[i].m_Cost = ParameterUtil.GetMAParameterMemoryUsed(data.m_MouthTrackingPrefabs[i].m_Prefab);
            }

            EditorUtility.SetDirty(target);
        }
    }
    
    [CreateAssetMenu(fileName = "FaceTrackingAddonConfig", menuName = "TriturboFT/Addon/FaceTrackingAddonConfig")]

    public class FaceTrackingAddonConfig: ScriptableObject
    {
        public string m_Version = "1.0.0";
        public Texture m_MainBanner;
        public UnityObject m_AddonRootFolder;
        public GameObject m_MenuInstallerPrefab;
        public AddonPrefab[] m_EyeTrackingPrefabs;
        public AddonPrefab[]  m_MouthTrackingPrefabs;
        public string m_GeneratedAssetsDirectory = "Generated";
        public FbxVersionInfo m_FbxVersionInfo;
        
        
        public bool IsCurrentFbxVersionNewerThan(string version)
        {
            string[] currentParts = m_Version.Split('.');
            string[] targetParts = version.Split('.');

            int majorCurent = currentParts.Length > 0 ? int.Parse(currentParts[0]) : 0;
            int majorTarget= currentParts.Length > 0 ? int.Parse(targetParts[0]): 0;
            
            int minorCurent = currentParts.Length > 1 ? int.Parse(currentParts[1]): 0;
            int minorTarget = currentParts.Length > 1 ? int.Parse(targetParts[1]): 0;
            
            if (majorCurent > majorTarget) return true;
            if (majorCurent == majorTarget && minorCurent > minorTarget) return true;

            return false;
        }
        

    }
    
    [Serializable]
    public struct AddonPrefab
    {
        public string m_Label;
        public GameObject m_Prefab;
        public int m_Cost;
    }
    
    
}