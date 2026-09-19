using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

#if ENABLE_BS
using Triturbo.BlendShapeShare.BlendShapeData;
#endif

namespace Triturbo.FaceTrackingFramework.Installer
{
    [System.Serializable]
    public class MeshPatcher
    {
        [SerializeField]
        private Texture2D blendShareIcon;
        
        private FbxVersionInfo allFbxInfo;
        private VRCAvatarDescriptor targetAvatar;
        
        private GameObject originalFbx;
        
#if ENABLE_BS
        private GeneratedMeshAssetSO meshContianer;
#endif
        
        private FbxVersionInfo.FbxObjectInfo currentFbxInfo;
        private FbxVersionInfo.FbxVersioning currentFbxVersion;
        
        private int userSelectedFbxVersionIndex = -1;
        private int userSelectedFbxObjectIndex = -1;
        
        private bool _isMeshPatched = false;
        private bool isGeneratedMeshOld = false;

        private FaceTrackingAddonConfig _addonConfig;
        private MeshAssetMetadata prevMeshAssetMetadata;
        
        public MeshPatcher(FaceTrackingAddonConfig addonConfig)
        {
            this._addonConfig = addonConfig;
            this.allFbxInfo = addonConfig.m_FbxVersionInfo;
            blendShareIcon = LoadTextureByGUID("1edbd77f50be9ce4ab39356a8420652a");
        }
        
        
        private Texture2D LoadTextureByGUID(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"No asset found for GUID: {guid}");
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        private bool _isAllowModifiedFbx = false;
        
        
        private IEnumerable<FbxVersionInfo.FbxVersioning> AllAvailableFbxVersions
        {
            get
            {
                return currentFbxInfo != null
                    ? currentFbxInfo.m_FbxVersionings.Where(v => !v.Unsupported)
                    : allFbxInfo.GetFbxVersions().Where(v => !v.Unsupported);
            }
        }
        
        public void UpdateAvatarInfo(VRCAvatarDescriptor avatar, FaceTrackingInstaller.TargetAvatarHandler onTargetAvatarChanged)
        {
            bool belongsToCurrentAvatar = false;
            originalFbx =  null;
            currentFbxInfo = null;
            currentFbxVersion = null;
            prevMeshAssetMetadata = null;
            userSelectedFbxVersionIndex = -1;
            
#if ENABLE_BS      
            meshContianer = null;
            targetAvatar = avatar;
            if (targetAvatar == null) return;
       
            string meshAssetPath = GetAssetPath(targetAvatar.transform);
            
            
            if (meshAssetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                //fbx file
                originalFbx = AssetDatabase.LoadAssetAtPath<GameObject>(meshAssetPath);
                meshContianer = null;
            }
            else
            {
                GeneratedMeshAssetSO gma = AssetDatabase.LoadAssetAtPath<GeneratedMeshAssetSO>(meshAssetPath);
                if (gma != null)
                {
                    meshContianer = gma;

                    if (gma.m_OriginalFbxAsset != null)
                    {
                        originalFbx = gma.m_OriginalFbxAsset;
                    }
                }

                prevMeshAssetMetadata = AssetDatabase.LoadAssetAtPath<MeshAssetMetadata>(meshAssetPath);
                if (prevMeshAssetMetadata != null)
                {
                    if (allFbxInfo.GetFbxObjectInfo(prevMeshAssetMetadata.m_OriginalFbxVersion) != null)
                    {
                        belongsToCurrentAvatar = true;
                        _isMeshPatched = true;
                        isGeneratedMeshOld = _addonConfig.IsCurrentFbxVersionNewerThan(prevMeshAssetMetadata.m_Version);
                    }
                }
            }
#endif          
            currentFbxInfo = allFbxInfo.GetFbxObjectInfo(originalFbx);
            currentFbxVersion = allFbxInfo.GetFbxVersion(originalFbx);
            
            if (belongsToCurrentAvatar && prevMeshAssetMetadata != null)
            {
                var preVersion = prevMeshAssetMetadata.m_OriginalFbxVersion;
                if (preVersion is { Unsupported: false })
                {
                    //Update m_BlendShapeData, might be updated
                    if (currentFbxVersion != null && 
                        currentFbxVersion.m_Version == preVersion.m_Version &&
                        currentFbxVersion.m_Sha256 == preVersion.m_Sha256)
                    {
                        preVersion.m_BlendShapeData = currentFbxVersion.m_BlendShapeData;
                    }
                    
                    currentFbxVersion = preVersion;
                }
            }

            onTargetAvatarChanged?.Invoke(targetAvatar, currentFbxInfo, currentFbxVersion);
        }
        
        
        public bool DrawFbxInfo()
        {
            if (currentFbxVersion == null)
            {
                DrawFbxSelection();
                return true;
            }
             
#if ENABLE_BS            
            string version = currentFbxVersion.m_Version;
            if (meshContianer?.m_AppliedBlendShapes != null && meshContianer.m_AppliedBlendShapes.Length > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(Localization.GF("fbx_version", version));
                foreach (var data in meshContianer.m_AppliedBlendShapes)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(blendShareIcon, GUILayout.Width(18), GUILayout.Height(18)); // icon
                    // Render as hyperlink-like text
                    if (GUILayout.Button(data.m_DeformerID, EditorStyles.label))
                    {
                        EditorGUIUtility.PingObject(data);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField(Localization.GF("fbx_version", version));
            }

            
            if (currentFbxVersion is { Unsupported: true })
            { 
                EditorGUILayout.HelpBox(Localization.S("fbx_unsupported"), MessageType.Error); 
                return false;
            }
            
            if (_isMeshPatched && isGeneratedMeshOld)
            {
                EditorGUILayout.HelpBox(Localization.S("update_mesh.tooltip"), MessageType.Info);
                if (GUILayout.Button(Localization.G("update_mesh")))
                {
                    var meshAsset = GenerateMeshAsset(prevMeshAssetMetadata?.m_MeshProvider);
                    if (meshAsset != null)
                    {
                        ApplyMesh(meshAsset, targetAvatar.transform);
                        EditorUtility.DisplayDialog(Localization.S("done"), Localization.SF("dialog_mesh_updated", 
                            $"From {prevMeshAssetMetadata?.m_Version ?? "UNKNOWN"} to {_addonConfig.m_Version}"), "OK");
                        UpdateAvatarInfo(targetAvatar, null);
                    }
                }
                EditorGUILayout.Separator();
            }
            return  true;
#endif
            return  false;
        }
        
        private void DrawFbxSelection()
        {
            //we can not find the version by sha256
            //=> user do edit on the fbx
            
            //init: select the latest as default
            if (userSelectedFbxVersionIndex == -1)
            {
                userSelectedFbxVersionIndex = AllAvailableFbxVersions.Count() - 1;
            }

            if (originalFbx == null)
            {
                EditorGUILayout.HelpBox(Localization.S("fbx_missing"), MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(Localization.S("fbx_unknown"), MessageType.Warning);
            }
            
            EditorGUI.BeginChangeCheck();
            //ask user the source version
            userSelectedFbxVersionIndex = EditorGUILayout.Popup(Localization.G("source_fbx_ver"), userSelectedFbxVersionIndex, 
                AllAvailableFbxVersions.Select(i => i.m_Version).ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                if (userSelectedFbxVersionIndex != -1 && currentFbxVersion == null)
                {
                    currentFbxVersion = AllAvailableFbxVersions.ElementAtOrDefault(userSelectedFbxVersionIndex);
                }
            }
            
            //Ask user if allow use modified fbx
            if (originalFbx != null)
            {
                EditorGUILayout.Separator();
                EditorGUILayout.HelpBox(Localization.S("fbx_target.tooltip"), MessageType.Warning);
                int fbxSource = _isAllowModifiedFbx ? 1 : 0;
                fbxSource = EditorGUILayout.Popup(Localization.G("fbx_target"), fbxSource, 
                    new [] { Localization.S("original"), Localization.S("current_in_use") });
                _isAllowModifiedFbx = fbxSource == 1;
            }
        }
        
        private static string GetAssetPath(Transform target, string concernPath = "Body")
        {
            Transform obj = target.Find(concernPath);
            if (obj == null) return "";
            
            var renderer = obj.GetComponent<SkinnedMeshRenderer>();
            if (renderer == null) return "";
            
            return AssetDatabase.GetAssetPath(renderer.sharedMesh);
        }
 
        private Object GetMeshProvider()
        {
#if ENABLE_BS 
            if (meshContianer != null)
            {
                return meshContianer;
            }
#endif
            if (!_isAllowModifiedFbx )
            {
                var fbxInfo = allFbxInfo.GetFbxObjectInfo(GetDetectedOrSelectedFbxVersion());
                if (fbxInfo != null)
                {
                    return fbxInfo.m_Fbx;
                }
            }
            
            return originalFbx;
        }
        
        //Source ID for folder name
        private string GetSourceId(Object meshProvider)
        {
            var fbxVersion = GetDetectedOrSelectedFbxVersion();

            //Can not autodetect the FBX version
            if (currentFbxVersion == null)
            {
                if (meshProvider is GameObject fbx)
                    fbxVersion = allFbxInfo.GetFbxVersion(fbx) ?? fbxVersion;
                
                if(fbxVersion != null)
                    return _isAllowModifiedFbx ? fbxVersion.m_Version + "_fork" : fbxVersion.m_Version;
                
                return "UNKNOWN";
            }

#if ENABLE_BS            
            //FBX
            if (originalFbx != null && meshContianer == null)
            {
                return fbxVersion.m_Version;
            }
            
            //GeneratedMeshAssetSO
            if (meshContianer != null)
            {
                string thisId = fbxVersion?.m_BlendShapeData?.m_DeformerID ?? "";

                var version = fbxVersion.m_Version;
                
                foreach (var data in meshContianer.m_AppliedBlendShapes)
                {
                    if(data.m_DeformerID == thisId) continue;
                    version += data.m_DeformerID.Replace("BlendShare-", "");
                }
                return version;
            }
#endif
            return "UNKNOWN";
        }
        
        private string GetSafeFolderName(string baseId)
        {
            // Remove invalid path chars
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                baseId = baseId.Replace(c, '_');

            // Limit total length
            const int maxLength = 50;
            if (baseId.Length > maxLength)
            {
                // Keep first 20 chars + hash of the rest
                string prefix = baseId.Substring(0, 20);
                string hash = ComputeShortHash(baseId);
                baseId = $"{prefix}_{hash}";
            }

            return baseId;
        }

        private string ComputeShortHash(string input)
        {
            using (var sha = SHA1.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = sha.ComputeHash(bytes);
                // take first 4 bytes to make a short hex string
                return BitConverter.ToString(hashBytes, 0, 4).Replace("-", "");
            }
        }
        
        
        private string GetUniqueName(string folder, string fileName)
        {
            string path = System.IO.Path.Combine(folder, fileName);
            if(!System.IO.File.Exists(path))
            {
                return path;
            }

            string extension = System.IO.Path.GetExtension(fileName);
            string fileNameWithoutExtension = $"{System.IO.Path.GetFileNameWithoutExtension(fileName)}";
 
            path = System.IO.Path.Combine(folder, $"{fileNameWithoutExtension}{extension}");

            int counter = 1;
            while (System.IO.File.Exists(path))
            {
                path = System.IO.Path.Combine(folder, $"{fileNameWithoutExtension}_{counter}{extension}");
                counter++;
            }
            return path;
        }
        
        public static void ApplyMesh(Object meshContainer, Transform target)
        {
            if (meshContainer == null || target == null)
            {
                Debug.LogError("ApplyMesh: meshContainer or target is null.");
                return;
            }


#if BLENDSHARE_1_0_3_OR_NEWER 
            if (meshContainer is GeneratedMeshAssetSO gma)
            {
                try
                {
                    gma.ApplyMesh(target);
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to apply GeneratedMeshAssetSO '{gma.name}' to '{target.name}': {ex}");
                }
            }   
#endif
            
            string assetPath = AssetDatabase.GetAssetPath(meshContainer);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError($"ApplyMesh: Invalid asset path for {meshContainer.name}.");
                return;
            }

            Object[] subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);

            var meshRenderers = new Dictionary<string, List<SkinnedMeshRenderer>>();
            foreach (var renderer in target.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string key = renderer.sharedMesh != null ? renderer.sharedMesh.name : renderer.gameObject.name;
                if (!meshRenderers.TryGetValue(key, out var list))
                {
                    list = new List<SkinnedMeshRenderer>();
                    meshRenderers[key] = list;
                }
                list.Add(renderer);
            }
            
            // Apply meshes
            foreach (var asset in subAssets)
            {
                if (asset is Mesh mesh)
                {
                    if (meshRenderers.TryGetValue(mesh.name, out var renderers))
                    {
                        if (renderers.Count > 1)
                        {
                            Debug.LogWarning(
                                $"[Apply Mesh] Multiple renderers share the same mesh name '{mesh.name}' under '{target.name}'.",
                                target
                            );
                        }

                        foreach (var targetMeshRenderer in renderers)
                        {
                            targetMeshRenderer.sharedMesh = mesh;
                        }
                    }
                    else
                    {
                        Debug.LogError($"[Apply Mesh] Mesh '{mesh.name}' not found in target '{target.name}'.", target);
                    }
                }
            }
            
        }
        
        
        private FbxVersionInfo.FbxVersioning GetDetectedOrSelectedFbxVersion()
        {
            if (userSelectedFbxVersionIndex != -1 && currentFbxVersion == null)
            {
                var userSelectedFbxSourceVersion = AllAvailableFbxVersions.ElementAtOrDefault(userSelectedFbxVersionIndex);
                if(userSelectedFbxSourceVersion is { Unsupported: false })
                    return userSelectedFbxSourceVersion;
            }

            return currentFbxVersion;
        }
                
#if ENABLE_BS

        private static GeneratedMeshAssetSO FindMatchingAsset(string folderPath, string sha256, string version, Object meshProvider)
        {
            // Find all assets of type MeshAssetMetadata in the given folder
            string[] guids = AssetDatabase.FindAssets("t:MeshAssetMetadata", new[] { folderPath });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var metadata = AssetDatabase.LoadAssetAtPath<MeshAssetMetadata>(path);
                if (metadata == null)
                    continue;

                if (metadata.m_Version == version &&
                    metadata.m_Sha256 == sha256 &&
                    metadata.m_MeshProvider == meshProvider)
                {
                    return  AssetDatabase.LoadAssetAtPath<GeneratedMeshAssetSO>(path); // Found a matching one
                }
            }

            return null; // No match found
        }
        

        
        internal GeneratedMeshAssetSO GenerateMeshAsset(Object meshProvider = null)
        {
            var version = GetDetectedOrSelectedFbxVersion();
            BlendShapeDataSO blendShapeData = version.m_BlendShapeData;
            
            if(meshProvider == null)
                meshProvider = GetMeshProvider() ?? blendShapeData.m_Original;
            
            string subfolder = GetSafeFolderName(GetSourceId(meshProvider));
            
            string meshAssetsName = $"{blendShapeData.DefaultMeshAssetName}_mesh_{_addonConfig.m_Version}.asset";
            string fbxName = $"{blendShapeData.DefaultMeshAssetName}_{_addonConfig.m_Version}.fbx";
            
            string root = AssetDatabase.GetAssetPath(_addonConfig.m_AddonRootFolder);
            string generatedAssetsFolder = System.IO.Path.Combine(root, _addonConfig.m_GeneratedAssetsDirectory);
            
            if (!AssetDatabase.IsValidFolder(generatedAssetsFolder))
            {
                AssetDatabase.CreateFolder(root, _addonConfig.m_GeneratedAssetsDirectory);
            }
            if (!AssetDatabase.IsValidFolder(System.IO.Path.Combine(generatedAssetsFolder, subfolder)))
            {
                AssetDatabase.CreateFolder(generatedAssetsFolder, subfolder);
            }
            string folder = System.IO.Path.Combine(generatedAssetsFolder, subfolder);
            string sha256 = FbxVersionInfo.CalculateHash(meshProvider);
            
            var foundMeshAssets = FindMatchingAsset(folder, sha256, _addonConfig.m_Version, meshProvider);
            if (foundMeshAssets != null) return foundMeshAssets;
            
            string uniqueMeshAssetPath = GetUniqueName(folder, meshAssetsName);
            
            var meshAssets = BlendShapeAppender.CreateMeshAsset(meshProvider, new[] { blendShapeData }, uniqueMeshAssetPath);


            ProcessAssetForFT(uniqueMeshAssetPath);
            var meta = ScriptableObject.CreateInstance<MeshAssetMetadata>();
            meta.hideFlags = HideFlags.NotEditable;
            meta.name = "Metadata";
            meta.m_Version = _addonConfig.m_Version;
            meta.m_MeshProvider = meshProvider;
            meta.m_Sha256 = sha256;
            meta.m_OriginalFbxVersion = version;
            
            AssetDatabase.AddObjectToAsset(meta, meshAssets);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return meshAssets;
        }
#endif
        
        
        
        private static void ProcessAssetForFT(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;

            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (subAssets == null || subAssets.Length == 0) return;

            bool changed = false;

            foreach (var obj in subAssets)
            {
                if (obj is Mesh mesh && mesh.name == "Body")
                {
                    BakeFTBlendshapeIntoBase(mesh);
                    EditorUtility.SetDirty(mesh);
                    changed = true;
                }
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }
        private static void BakeFTBlendshapeIntoBase(Mesh mesh)
        {
            if (mesh == null) return;

            int index = mesh.GetBlendShapeIndex("-----FT-----");
            if (index < 0) return; // nothing to bake

            int vertexCount = mesh.vertexCount;
            var baseVertices = mesh.vertices;
            var deltaVertices = new Vector3[vertexCount];
            // Get first frame of this blendshape
            mesh.GetBlendShapeFrameVertices(index, 0, deltaVertices, null, null);

            // Check if the FT blendshape is effectively empty
            bool allZero = true;
            for (int i = 0; i < vertexCount; i++)
            {
                if (deltaVertices[i] != Vector3.zero)
                {
                    allZero = false;
                    break;
                }
            }

            if (allZero) return; // skip empty shape

            // Add deltas to base
            for (int i = 0; i < vertexCount; i++)
            {
                baseVertices[i] += deltaVertices[i];
            }
            mesh.vertices = baseVertices;
            ClearBlendShape(mesh, index);
            mesh.UploadMeshData(false);
        }

        private static void ClearBlendShape(Mesh mesh, int index)
        {
            Mesh temp = Object.Instantiate(mesh);
            mesh.ClearBlendShapes();
            int count = temp.vertexCount;
            var zeroV = new Vector3[count];
            var zeroN = new Vector3[count];
            var zeroT = new Vector3[count];

            for (int i = 0; i < temp.blendShapeCount; i++)
            {
                string name = temp.GetBlendShapeName(i);
                int frames = temp.GetBlendShapeFrameCount(i);

                for (int f = 0; f < frames; f++)
                {
                    float weight = temp.GetBlendShapeFrameWeight(i, f);

                    if (i == index)
                    {
                        // FT blendshape becomes empty
                        mesh.AddBlendShapeFrame(name, weight, zeroV, zeroN, zeroT);
                    }
                    else
                    {
                        // Copy original data
                        var dv = new Vector3[count];
                        var dn = new Vector3[count];
                        var dt = new Vector3[count];

                        temp.GetBlendShapeFrameVertices(i, f, dv, dn, dt);
                        mesh.AddBlendShapeFrame(name, weight, dv, dn, dt);
                    }
                }
            }
            Object.DestroyImmediate(temp);
        }
    }
}
