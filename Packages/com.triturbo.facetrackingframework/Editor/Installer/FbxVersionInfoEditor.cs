using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using UnityEditorInternal;

namespace Triturbo.FaceTrackingFramework.Installer
{
    [CustomEditor(typeof(FbxVersionInfo))]
    public class FbxVersionInfoEditor : Editor
    {
        private string sha256 = "";
        private UnityEngine.Object selectedFbx = null;
        private FbxVersionInfo fbxVersionInfo;

        private void OnEnable()
        {
            fbxVersionInfo = (FbxVersionInfo)target;
        }

        public override void OnInspectorGUI()
        {
            

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Versioning Tool", EditorStyles.boldLabel);

            // FBX Selection
            selectedFbx = EditorGUILayout.ObjectField("FBX", selectedFbx, typeof(GameObject), false);

            // Calculate SHA256
            if (GUILayout.Button("Calculate Hash") && selectedFbx != null)
            {
                sha256 = FbxVersionInfo.CalculateHash(selectedFbx);
            }

            // Display Calculated SHA256
            EditorGUILayout.TextField("SHA256", sha256);


            DrawDefaultInspector();
            
            
            // Show Existing Versioning Data
            DisplayVersioningList();
            
            
        }
        
        private void DisplayVersioningList()
        {
            if (fbxVersionInfo == null || fbxVersionInfo.m_FbxObjects == null) return;

            foreach (var fbxObj in fbxVersionInfo.m_FbxObjects)
            {
                if (fbxObj == null) continue;
                EditorGUILayout.Space();

                EditorGUILayout.LabelField(fbxObj.m_Fbx != null ? $"FBX: {fbxObj.m_Fbx.name}" : $"FBX: Not Imported",
                    EditorStyles.boldLabel);

                foreach (var version in fbxObj.m_FbxVersionings)
                {
                    EditorGUILayout.LabelField($"- Version: {version.m_Version}");
                    EditorGUILayout.LabelField($"  SHA256: {version.m_Sha256}");
                }
            }
        }
    }


    
}
