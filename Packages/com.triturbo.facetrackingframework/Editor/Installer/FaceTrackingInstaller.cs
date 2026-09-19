
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using UnityObject = UnityEngine.Object;

#if ENABLE_BS
using Triturbo.BlendShapeShare.BlendShapeData;
#endif

namespace Triturbo.FaceTrackingFramework.Installer
{
    public  class FaceTrackingInstaller : EditorWindow
    {
        [SerializeField]
        public FaceTrackingAddonConfig addonConfig;
        
        
        private MeshPatcher _meshPatcher;
        
        #region Avatar Information
        private VRCAvatarDescriptor _avatarDescriptor;
        private bool _isAvatarAssigned = false;
        private int _targetAvatarParameterMemoryUsed;
        private bool _isTargetAvatarPrefab = false;
        #endregion
        
        private int _eyeSelection = 1;
        private int _mouthSelection = 1;
        
        
        private string _errorMessage = "";
        private bool _isAnyFbxImported = false;
        private bool _isAddonInstalled = false;

        public delegate void TargetAvatarHandler(VRCAvatarDescriptor targetAvatar, FbxVersionInfo.FbxObjectInfo currentFbxInfo, FbxVersionInfo.FbxVersioning currentFbxVersion);
        public event TargetAvatarHandler OnTargetAvatarChanged;
        

        
        private void OnEnable()
        {

            Debug.Assert(addonConfig !=null, "addonConfig is null");
            Debug.Assert(addonConfig.m_FbxVersionInfo !=null, "addonConfig.m_FbxVersionInfo is null");
            
            if (addonConfig == null)
            {
                _errorMessage += "[Error] Addon configuration is missing. Please ensure the addon is properly set up.";
            }
            else if (addonConfig.m_FbxVersionInfo == null)
            {
                _errorMessage += "[Error] FBX version information is missing. The addon can not function correctly.";
            }

            _meshPatcher = new MeshPatcher(addonConfig);
            
            _isAnyFbxImported = addonConfig.m_FbxVersionInfo?.IsAnyFbxImported is true;

            
            EditorApplication.hierarchyChanged += OnAvatarChanged;
        }
        
        void OnDisable()
        {
            EditorApplication.hierarchyChanged -= OnAvatarChanged;
        }
        
        public void OnGUI()
        {
            if (addonConfig.m_MainBanner != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(addonConfig.m_MainBanner, GUILayout.Width(196), GUILayout.Height(49));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            Localization.DrawLanguageSelection();
            
            //Show addon version
            EditorGUILayout.LabelField(Localization.GF("addon_version", addonConfig.m_Version));

            
#if !ENABLE_BS
            EditorGUILayout.HelpBox(Localization.S("blend_share_error"), MessageType.Error);
            GUI.enabled = false;
#endif
#if !ENABLE_MA
            EditorGUILayout.HelpBox("Modular Avatar is not installed", MessageType.Error);
            GUI.enabled = false;
#endif
            if (_errorMessage != "")
            {
                EditorGUILayout.HelpBox(_errorMessage, MessageType.Error, true);
                return;
            }
            
            if (!_isAnyFbxImported)
            {
                EditorGUILayout.HelpBox(Localization.S("fbx_not_found"), MessageType.Error);
                GUI.enabled = false;
            }
            
            //Show target avatar field
            EditorGUI.BeginChangeCheck();
            _avatarDescriptor = EditorGUILayout.ObjectField(Localization.G(("avatar_field")), _avatarDescriptor, typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;
            if (EditorGUI.EndChangeCheck())
            {
                _isAddonInstalled = false;
                OnAvatarChanged();
            }
            if (!_isAvatarAssigned)
            {
                EditorGUILayout.HelpBox(Localization.S("avatar_field.tooltip"), MessageType.Info);
            }
            else
            {     
                EditorGUILayout.BeginVertical(GUI.skin.box);

                GUI.enabled = _meshPatcher.DrawFbxInfo();
                //Show memory used
                EditorGUILayout.LabelField(Localization.GF("memory_cost", _targetAvatarParameterMemoryUsed));
                EditorGUILayout.EndHorizontal();

            }
            
            
            if (_isAddonInstalled)
            {
                EditorGUILayout.HelpBox(Localization.S("ft_installed"), MessageType.Info, true);
                return;
            }
            
            
            #region Prefab Selection Popup
            EditorGUI.BeginDisabledGroup(!_isAvatarAssigned);
            if (_eyeSelection > addonConfig.m_EyeTrackingPrefabs.Length)
            {
                _eyeSelection = addonConfig.m_EyeTrackingPrefabs.Length - 1;
            }
            if (_mouthSelection > addonConfig.m_MouthTrackingPrefabs.Length)
            {
                _eyeSelection = addonConfig.m_MouthTrackingPrefabs.Length - 1;
            }
            int eye = addonConfig.m_EyeTrackingPrefabs[_eyeSelection].m_Cost;
            int mouth = addonConfig.m_MouthTrackingPrefabs[_mouthSelection].m_Cost;
            GUILayout.BeginHorizontal();
            _eyeSelection = EditorGUILayout.Popup(Localization.G("eye_tracking"), _eyeSelection, 
                addonConfig.m_EyeTrackingPrefabs.Select(i => i.m_Label).ToArray());
            EditorGUILayout.PrefixLabel(Localization.GF("memory_cost_title", eye));
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            _mouthSelection = EditorGUILayout.Popup(Localization.G("lip_tracking"), _mouthSelection, 
                addonConfig.m_MouthTrackingPrefabs.Select(i => i.m_Label).ToArray());
            EditorGUILayout.PrefixLabel(Localization.GF("memory_cost_title", mouth));
            GUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();
            #endregion
            
            #region Parameter Memory Display
            int totalCost = _targetAvatarParameterMemoryUsed + eye + mouth + 1;
            var current = GUI.backgroundColor;
            if (_isAvatarAssigned)
            {
                if (totalCost > 256)
                {
                    GUI.backgroundColor = Color.red;
                }
                
                EditorGUILayout.Separator();
                GUILayout.BeginHorizontal(GUI.skin.box);
                EditorGUILayout.LabelField(Localization.GF("ft_cost", eye + mouth + 1));
                EditorGUILayout.LabelField(Localization.GF("total", totalCost));
                GUILayout.EndHorizontal();
                if(totalCost > 256)
                {
                    EditorGUILayout.HelpBox(Localization.S("memory_not_enough") + "\n" + Localization.SF("synced_limit", totalCost), MessageType.Warning);
                }
            }
            
            #endregion
            
            #region Button
            EditorGUILayout.Separator();
            EditorGUI.BeginDisabledGroup(!_isAvatarAssigned || _eyeSelection==0 && _mouthSelection ==0);
            GUILayout.BeginHorizontal();
            bool apply = false;
            using (new EditorGUI.DisabledGroupScope(_isTargetAvatarPrefab))
            {
                apply = GUILayout.Button(Localization.G("ft_make"));
            }
            
            bool applyAsNew = GUILayout.Button(Localization.G("ft_make_new"));
            GUI.backgroundColor = current;
            GUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();
            
            if (apply || applyAsNew)
            {
                string suffix = $"[{addonConfig.m_EyeTrackingPrefabs[_eyeSelection].m_Label}+{addonConfig.m_MouthTrackingPrefabs[_mouthSelection].m_Label}]";

                var result = MakeFaceTrackingAvatar(
                    addonConfig.m_EyeTrackingPrefabs[_eyeSelection].m_Prefab,
                    addonConfig.m_MouthTrackingPrefabs[_mouthSelection].m_Prefab,
                    suffix, applyAsNew);
                OnAvatarChanged();
                EditorGUIUtility.PingObject(result);
            }
            if (apply)
            {
                EditorUtility.DisplayDialog(Localization.S("done"), Localization.S("ft_installed"), "OK");
                _isAddonInstalled = true;
            }
            
            #endregion
            
            #region Blueprint Warning

            if (_avatarDescriptor != null && _avatarDescriptor.TryGetComponent(out VRC.Core.PipelineManager pipelineManager) &&
                pipelineManager.blueprintId != "")
            {
                EditorGUILayout.Separator();
                EditorGUILayout.HelpBox(Localization.S("blueprint_warning"), MessageType.Warning);
            }

            #endregion
            
            #region Installation Guide

            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Localization.G("installation_guide"), GUILayout.Width(200)))
            {
                Application.OpenURL("https://tr1turbo.github.io/Docs/face-tracking-addon/installation-guide.html");
            }
            GUILayout.EndHorizontal();
            #endregion
        }

        private void OnAvatarChanged()
        {
            _meshPatcher.UpdateAvatarInfo(_avatarDescriptor, OnTargetAvatarChanged);
            _isAvatarAssigned = _avatarDescriptor != null;
            _targetAvatarParameterMemoryUsed = _avatarDescriptor != null ? 
                ParameterUtil.GetAvatarParameterMemoryUsed(_avatarDescriptor) : 0;
            
            if(_avatarDescriptor == null) return;

            _isTargetAvatarPrefab = AssetDatabase.Contains(_avatarDescriptor.transform);
        }

        
        private Transform MakeFaceTrackingAvatar(GameObject eyeTrackingPrefab, GameObject lipTrackingPrefab,
            string suffix, bool duplicate = true)
        {
            Transform newAvatar = null;
#if ENABLE_BS
            if (_avatarDescriptor == null)
            {
                return null;
            }
            
            GeneratedMeshAssetSO meshAsset = _meshPatcher.GenerateMeshAsset();
            
            if (meshAsset ==null)
            {
                return null;
            }

            if (duplicate)
            {
                newAvatar = DuplicateGameObject(_avatarDescriptor.gameObject).transform;
                newAvatar.gameObject.SetActive(true);
                Undo.RegisterCreatedObjectUndo(newAvatar.gameObject, "Create face tracking avatar");
            }
            else
            {
                newAvatar = _avatarDescriptor.transform;
                Undo.RecordObject(newAvatar.gameObject, "Add face tracking addon to avatar");
            }
            
            MeshPatcher.ApplyMesh(meshAsset, newAvatar);
 
            
            if (!newAvatar.name.Contains("-FaceTracking"))
                newAvatar.name = _avatarDescriptor.gameObject.name + "-FaceTracking" + suffix;
            
            GameObject ftRootObject = new GameObject("Face Tracking");
            ftRootObject.transform.parent = newAvatar;

            PrefabUtility.InstantiatePrefab(addonConfig.m_MenuInstallerPrefab, ftRootObject.transform);

            if (eyeTrackingPrefab != null)
                PrefabUtility.InstantiatePrefab(eyeTrackingPrefab, ftRootObject.transform);

            if (lipTrackingPrefab != null)
                PrefabUtility.InstantiatePrefab(lipTrackingPrefab, ftRootObject.transform);

            Undo.RegisterCreatedObjectUndo(ftRootObject, "Add face tracking addon to avatar");


            if (eyeTrackingPrefab != null)
            {
                VRCAvatarDescriptor avatarDescriptor = newAvatar.GetComponent<VRCAvatarDescriptor>();
                Undo.RecordObject(avatarDescriptor, "Change eyelidType to None");
                avatarDescriptor.customEyeLookSettings.eyelidType = VRCAvatarDescriptor.EyelidType.None;
                PrefabUtility.RecordPrefabInstancePropertyModifications(avatarDescriptor);

            }

#endif
            return newAvatar;
        }
        
        public static GameObject DuplicateGameObject(GameObject source)
        {
            if (AssetDatabase.Contains(source))
            {
                return PrefabUtility.InstantiatePrefab(source) as GameObject;
            }
            UnityObject[] lastSelection = Selection.objects;
            Selection.activeGameObject = source;
            
            Unsupported.DuplicateGameObjectsUsingPasteboard();
            Selection.objects = lastSelection;
            
            GameObject[] rootGameObjects = source.scene.GetRootGameObjects();
            int index = Array.IndexOf(rootGameObjects, source);
            if (index < 0)
            {
                index = rootGameObjects.Length - 1;
            }
            else
            {
                index += 1;
            }
            return rootGameObjects[index];
        }

    }
}

