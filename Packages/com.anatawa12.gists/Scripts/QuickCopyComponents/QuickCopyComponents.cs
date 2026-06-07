/*
 * Quick Copy Components
 * https://gist.github.com/anatawa12/c984c98b8676d1893d7d3f79f50c18aa
 *
 * Copy Components from one GameObject to another quickly & bulkily.
 *
 * The window allows you to copy components from source GameObjects to another GameObjects.
 * This does not copy recursively, only components attached to the specified GameObjects are copied.
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

#if UNITY_EDITOR && (!ANATAWA12_GISTS_VPM_PACKAGE || GIST_c984c98b8676d1893d7d3f79f50c18aa)

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using Object = System.Object;

namespace anatawa12.gists
{
    internal class QuickCopyComponents : EditorWindow
    {
        [MenuItem("Tools/anatawa12 gists/Quick Copy Components")]
        public static void Open() => GetWindow<QuickCopyComponents>("Quick Copy Components");

        [SerializeField]
        CopyConfig[] copies = Array.Empty<CopyConfig>();

        [NonSerialized]
        Vector2 scrollPos;

        private void OnGUI()
        {
            Undo.RecordObject(this, "Quick Copy Components UI Change");
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            for (var i = 0; i < copies.Length; i++)
            {
                ref var copy = ref copies[i];
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.BeginHorizontal();
                copy.source = (GameObject)EditorGUILayout.ObjectField(copy.source, typeof(GameObject), true);
                copy.destination = (GameObject)EditorGUILayout.ObjectField(copy.destination, typeof(GameObject), true);
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    ArrayUtility.Remove(ref copies, copy);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
                if (copy.source != null && copy.destination != null)
                {
                    EditorGUI.indentLevel++;

                    copy.generalCopies ??= Array.Empty<GeneralCopyConfig>();

                    // show transform copy options
                    EditorGUILayout.ObjectField(copy.source.transform, typeof(Transform), true);
                    {
                        EditorGUI.indentLevel++;
                        DrawTransformCopyFlag("Position", ref copy.transformCopy.copyPosition,
                            ref copy.transformCopy.copyPositionLocally);
                        DrawTransformCopyFlag("Rotation", ref copy.transformCopy.copyRotation,
                            ref copy.transformCopy.copyRotationLocally);
                        DrawTransformCopyFlag("Scale", ref copy.transformCopy.copyScale,
                            ref copy.transformCopy.copyScaleLocally);

                        void DrawTransformCopyFlag(string field, ref bool copy, ref bool copyLocally)
                        {
                            copy = EditorGUILayout.ToggleLeft($"Copy {field}", copy);
                            EditorGUI.indentLevel++;
                            copyLocally = EditorGUILayout.ToggleLeft("Locally", copyLocally);
                            EditorGUI.indentLevel--;
                        }

                        EditorGUI.indentLevel--;
                    }

                    // validate copy settings
                    for (var index = 0; index < copy.generalCopies.Length; index++)
                    {
                        ref var copyConfig = ref copy.generalCopies[index];

                        if (copyConfig.source != null && copyConfig.source.gameObject != copy.source)
                            copy.source.TryGetComponent(copyConfig.source.GetType(), out copyConfig.source);
                        if (copyConfig.destination != null && copyConfig.destination.gameObject != copy.destination)
                            copy.destination.TryGetComponent(copyConfig.destination.GetType(), out copyConfig.destination);

                        if (copyConfig.source == null || copyConfig.destination == null ||
                            copyConfig.source.GetType() != copyConfig.destination.GetType())
                        {
                            ArrayUtility.RemoveAt(ref copy.generalCopies, index);
                            index--;
                        }
                    }

                    // add missing components
                    foreach (var component in copy.source.GetComponents<Component>().Distinct<Component>(TypeComparator.Instance))
                    {
                        if (component is Transform) continue; // Skip Transform component
                        if (copy.generalCopies.Any(c => c.source != null && c.source.GetType() == component.GetType()))
                            continue; // already registered

                        var destinationComponent = copy.destination.GetComponent(component.GetType());
                        if (destinationComponent == null) continue; // destination does not have this component

                        ArrayUtility.Add(ref copy.generalCopies, new GeneralCopyConfig
                        {
                            source = component,
                            destination = destinationComponent,
                            doCopying = false,
                        });
                    }

                    // show copy options
                    for (var index = 0; index < copy.generalCopies.Length; index++)
                    {
                        ref var copyConfig = ref copy.generalCopies[index];
                        EditorGUILayout.ObjectField(copyConfig.source, typeof(Component), true);
                        {
                            EditorGUI.indentLevel++;
                            copyConfig.doCopying = EditorGUILayout.ToggleLeft("Do Copying", copyConfig.doCopying);
                            EditorGUI.indentLevel--;
                        }
                    }

                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Add Copy"))
            {
                ArrayUtility.Add(ref copies, new CopyConfig());
            }

            if (GUILayout.Button("Copy Components"))
            {
                Undo.IncrementCurrentGroup();
                var undoGroup = Undo.GetCurrentGroup();
                foreach (var copy in copies)
                {
                    if (copy.source == null || copy.destination == null) continue;

                    Undo.RecordObject(copy.destination.transform, "Quick Copy Components");

                    // copy transform
                    if (copy.transformCopy.copyPosition)
                    {
                        if (copy.transformCopy.copyPositionLocally)
                            copy.destination.transform.localPosition = copy.source.transform.localPosition;
                        else
                            copy.destination.transform.position = copy.source.transform.position;
                    }

                    if (copy.transformCopy.copyRotation)
                    {
                        if (copy.transformCopy.copyRotationLocally)
                            copy.destination.transform.localRotation = copy.source.transform.localRotation;
                        else
                            copy.destination.transform.rotation = copy.source.transform.rotation;
                    }

                    if (copy.transformCopy.copyScale)
                    {
                        if (copy.transformCopy.copyScaleLocally)
                            copy.destination.transform.localScale = copy.source.transform.localScale;
                        else
                            copy.destination.transform.localScale = copy.source.transform.lossyScale;
                    }
                    PrefabUtility.RecordPrefabInstancePropertyModifications(copy.destination.transform);

                    // copy other components
                    foreach (var generalCopy in copy.generalCopies ?? Array.Empty<GeneralCopyConfig>())
                    {
                        if (!generalCopy.doCopying) continue;
                        if (generalCopy.source == null || generalCopy.destination == null) continue;
                        if (generalCopy.source.GetType() != generalCopy.destination.GetType()) continue;

                        Undo.RecordObject(generalCopy.destination, "Quick Copy Components");
                        EditorUtility.CopySerialized(generalCopy.source, generalCopy.destination);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(generalCopy.destination);
                    }
                }
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        [Serializable]
        struct CopyConfig
        {
            [CanBeNull] public GameObject source;
            [CanBeNull] public GameObject destination;
            public TransformCopyConfig transformCopy;
            [CanBeNull] public GeneralCopyConfig[] generalCopies;
        }

        [Serializable]
        struct TransformCopyConfig
        {
            public bool copyPosition;
            public bool copyPositionLocally;
            public bool copyRotation;
            public bool copyRotationLocally;
            public bool copyScale;
            public bool copyScaleLocally;
        }

        [Serializable]
        struct GeneralCopyConfig
        {
            [CanBeNull] public Component source;
            [CanBeNull] public Component destination;
            public bool doCopying;
        }

        class TypeComparator : IEqualityComparer<object>
        {
            public static TypeComparator Instance = new TypeComparator();

            public bool Equals(object x, object y) => x?.GetType() == y?.GetType();
            public int GetHashCode(object obj) => obj?.GetType().GetHashCode() ?? 0;
        }
    }
}

#endif
