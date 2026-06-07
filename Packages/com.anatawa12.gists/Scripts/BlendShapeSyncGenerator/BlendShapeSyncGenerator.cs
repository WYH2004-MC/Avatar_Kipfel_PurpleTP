/*
 * BlendShapeSyncGenerator
 * Simple window to generate Modular Avatar's Blendshape Sync components.
 * https://gist.github.com/anatawa12/b577547d96eea6975279dee2a7e59578
 *
 * 1. Click `Tools/anatawa12 gists/BlendShapeSyncGenerator` to open this window.
 * 2. Set `Find Root Object` to the root GameObject of your avatar, or outfit you want to process.
 * 3. Set `Sync Sources` to SkinnedMeshRenderers which has blendshapes you want to sync from. This is tipically mesh like Body_base.
 * 4. Click `Find Candidates` button to find SkinnedMeshRenderers which can sync blendshapes from `Sync Sources`.
 * 5. Enable / disable blendshape sync you want to generate.
 * 6. Click `Generate BlendShape Sync` button to generate MA Blendshape Sync components.
 *
 * MIT License
 * 
 * Copyright (c) 2023 anatawa12
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

#if UNITY_EDITOR && (!ANATAWA12_GISTS_VPM_PACKAGE || GIST_b577547d96eea6975279dee2a7e59578)

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace anatawa12.gists
{
    internal class BlendShapeSyncGenerator : EditorWindow
    {
        private const string GIST_NAME = "BlendShapeSyncGenerator";

        [MenuItem("Tools/anatawa12 gists/" + GIST_NAME)]
        static void Create() => GetWindow<BlendShapeSyncGenerator>(GIST_NAME);

        [SerializeField] private GameObject findRootObject;
        [SerializeField] private SkinnedMeshRenderer[] syncSources = Array.Empty<SkinnedMeshRenderer>();
        [SerializeField] private SyncBlendShapeRendererInfo[] syncDestinations = Array.Empty<SyncBlendShapeRendererInfo>();
        [SerializeField] private Vector2 _scrollPosition;

        private SerializedObject _serializedObject;
        private SerializedProperty _findRootObjectProperty;
        private SerializedProperty _syncSourcesProperty;

        private void OnEnable()
        {
            _serializedObject = new SerializedObject(this);
            _findRootObjectProperty = _serializedObject.FindProperty(nameof(findRootObject));
            _syncSourcesProperty = _serializedObject.FindProperty(nameof(syncSources));
        }

        [Serializable]
        struct SyncBlendShapeRendererInfo
        {
            public SkinnedMeshRenderer destRenderer;
            [CanBeNull] public SyncBlendshapeInfo[] blendShapes;
        }

        [Serializable]
        struct SyncBlendshapeInfo
        {
            public string blendShapeName;
            public SkinnedMeshRenderer sourceRenderer;
            public bool enabled;
        }

        private void OnGUI()
        {
            var prevFindRoot = findRootObject;
            _syncSourcesProperty.isExpanded = true;
            _serializedObject.Update();
            EditorGUILayout.PropertyField(_findRootObjectProperty);
            
            EditorGUILayout.PropertyField(_syncSourcesProperty, true);
            _serializedObject.ApplyModifiedProperties();
            if (prevFindRoot == null && findRootObject != null && syncSources.Length == 0)
            {
                syncSources = findRootObject.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(r => r.sharedMesh)
                    .Where(r => r.sharedMesh.blendShapeCount > 0)
                    .Where(r => r.name.ToLowerInvariant().Contains("body"))
                    .ToArray();
                var bodyIndex = Array.FindIndex(syncSources, r => r.name.ToLowerInvariant().Contains("body"));
                if (bodyIndex != -1 && syncSources.Length >= 2)
                {
                    ArrayUtility.RemoveAt(ref syncSources, bodyIndex);
                }
            }

            // syncSources validation

            if (GUILayout.Button("Find Candidates"))
            {
                syncDestinations = GenerateSyncInfo(findRootObject, syncSources).ToArray();
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            foreach (var shapeRendererInfo in syncDestinations)
            {
                EditorGUILayout.ObjectField(shapeRendererInfo.destRenderer, typeof(SkinnedMeshRenderer), false);
                EditorGUI.indentLevel++;
                if (shapeRendererInfo.blendShapes != null)
                {
                    foreach (ref var shapeInfo in shapeRendererInfo.blendShapes.AsSpan())
                    {
                        EditorGUILayout.BeginHorizontal();
                        shapeInfo.enabled = EditorGUILayout.ToggleLeft($"{shapeInfo.blendShapeName}", shapeInfo.enabled);
                        var prevIndentLevel = EditorGUI.indentLevel;
                        EditorGUI.indentLevel = 0;
                        EditorGUILayout.ObjectField(shapeInfo.sourceRenderer, typeof(SkinnedMeshRenderer), false);
                        EditorGUI.indentLevel = prevIndentLevel; 
                        EditorGUILayout.EndHorizontal();
                    }
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Generate BlendShape Sync"))
            {
                GenerateBlendShapeSync(syncDestinations);
            }
        }

        private static List<SyncBlendShapeRendererInfo> GenerateSyncInfo(GameObject root, SkinnedMeshRenderer[] syncSources)
        {
            var sourceByShapeNames = new Dictionary<string, SkinnedMeshRenderer>();

            foreach (var skinnedMeshRenderer in syncSources)
            {
                var mesh = skinnedMeshRenderer.sharedMesh;
                if (mesh == null) continue;
                for (var i = 0; i < mesh.blendShapeCount; i++)
                {
                    var shapeName = mesh.GetBlendShapeName(i);
                    sourceByShapeNames.TryAdd(shapeName, skinnedMeshRenderer);
                }
            }

            var result = new List<SyncBlendShapeRendererInfo>();

            foreach (var skinnedMeshRenderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = skinnedMeshRenderer.sharedMesh;
                if (mesh == null) continue;
                ICollection<(SkinnedMeshRenderer, string)> existing = Array.Empty<(SkinnedMeshRenderer, string)>();
                if (skinnedMeshRenderer.gameObject.TryGetComponent<ModularAvatarBlendshapeSync>(out var syncComponent)) 
                    existing = FindSyncBindings(syncComponent);

                var shapes = new List<SyncBlendshapeInfo>();
                for (var i = 0; i < mesh.blendShapeCount; i++)
                {
                    var shapeName = mesh.GetBlendShapeName(i);
                    if (!sourceByShapeNames.TryGetValue(shapeName, out var sourceRenderer)) continue;
                    if (sourceRenderer == skinnedMeshRenderer) continue;
                    if (existing.Contains((sourceRenderer, shapeName))) continue;
                    
                    shapes.Add(new SyncBlendshapeInfo
                    {
                        blendShapeName = shapeName,
                        sourceRenderer = sourceRenderer,
                        enabled = true,
                    });
                }

                if (shapes.Count > 0)
                {
                    result.Add(new SyncBlendShapeRendererInfo
                    {
                        destRenderer = skinnedMeshRenderer,
                        blendShapes = shapes.ToArray(),
                    });
                }
            }

            return result;
        }

        private static void GenerateBlendShapeSync(SyncBlendShapeRendererInfo[] syncBlendShapeRendererInfos)
        {
            var undoGroup = Undo.GetCurrentGroup();
            foreach (var syncBlendShapeRendererInfo in syncBlendShapeRendererInfos)
            {
                if (syncBlendShapeRendererInfo.blendShapes.All(r => !r.enabled)) continue;

                var destRenderer = syncBlendShapeRendererInfo.destRenderer;
                if (!destRenderer.gameObject.TryGetComponent(out ModularAvatarBlendshapeSync syncComponent))
                    syncComponent = Undo.AddComponent<ModularAvatarBlendshapeSync>(destRenderer.gameObject);

                Undo.RecordObject(syncComponent, "Add BlendShape Sync Binding");
                var existing = FindSyncBindings(syncComponent);
                foreach (var blendShape in syncBlendShapeRendererInfo.blendShapes)
                {
                    if (!blendShape.enabled) continue;
                    var key = (blendShape.sourceRenderer, blendShape.blendShapeName);
                    if (existing.Contains(key)) continue;
                    var newBinding = new BlendshapeBinding
                    {
                        ReferenceMesh = new AvatarObjectReference(),
                        Blendshape = blendShape.blendShapeName,
                        LocalBlendshape = "",
                    };
                    newBinding.ReferenceMesh.Set(blendShape.sourceRenderer.gameObject);
                    syncComponent.Bindings.Add(newBinding);
                }

                PrefabUtility.RecordPrefabInstancePropertyModifications(syncComponent);
            }
            Undo.CollapseUndoOperations(undoGroup);
            Undo.SetCurrentGroupName("Generate BlendShape Sync Components");
        }

        private static HashSet<(SkinnedMeshRenderer, string)> FindSyncBindings(
            ModularAvatarBlendshapeSync syncComponent)
        {
            var result = new HashSet<(SkinnedMeshRenderer, string)>();
            foreach (var binding in syncComponent.Bindings)
            {
                var meshObj = binding.ReferenceMesh.Get(syncComponent);
                if (meshObj == null) continue;
                var skinnedMeshRenderer = meshObj.GetComponent<SkinnedMeshRenderer>();
                if (skinnedMeshRenderer == null) continue;
                result.Add((skinnedMeshRenderer, binding.Blendshape));
            }

            return result;
        }
    }
}

#endif
