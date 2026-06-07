/*
 * ObjectRecursiveDiff
 * Simple utility to show differences between two UnityEngine.Objects recursively.
 * https://gist.github.com/anatawa12/96f14582df903936986b786b9210b4dd
 *
 * Click `Tools/anatawa12 gists/ObjectRecursiveDiff` to open this window.
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

#if UNITY_EDITOR && (!ANATAWA12_GISTS_VPM_PACKAGE || GIST_96f14582df903936986b786b9210b4dd)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace anatawa12.gists
{
    internal class ObjectRecursiveDiff : EditorWindow
    {
        private const string GIST_NAME = "ObjectRecursiveDiff";

        [MenuItem("Tools/anatawa12 gists/" + GIST_NAME)]
        static void Create() => GetWindow<ObjectRecursiveDiff>(GIST_NAME);

        public Object expectedValue;
        public Object actualValue;
        [NonSerialized] [CanBeNull] private DiffInfo _diffInfo = null;

        public Vector2 scrollPosition;

        private void OnGUI()
        {
            expectedValue = EditorGUILayout.ObjectField("Expected Value", expectedValue, typeof(Object), true);
            actualValue = EditorGUILayout.ObjectField("Actual Value", actualValue, typeof(Object), true);
            // TODO: allow specify ObjectInstanceEqualityTypes

            if ((_diffInfo?.Expected != expectedValue || _diffInfo?.Actual != actualValue))
            {
                if (expectedValue != null && actualValue != null)
                {
                    var ctx = CheckEqualityContext.NewContext();
                    //ctx.EqualityIgnoredTypes.Add(typeof(AnimationClip));
                    ctx.CheckEquality(expectedValue, actualValue);
                    _diffInfo = new DiffInfo()
                    {
                        Expected = expectedValue,
                        Actual = actualValue,
                        ObjectDifferences = ctx.Differences.ToArray(),
                    };
                }
                else
                {
                    _diffInfo = null;
                }
            }


            if (_diffInfo != null)
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                var prevLabelWidth = EditorGUIUtility.labelWidth;
                try
                {
                    EditorGUIUtility.labelWidth = Mathf.Min(position.width - 150f, position.width / 3 * 2);
                    foreach (var difference in _diffInfo.ObjectDifferences)
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                        for (var i = 0; i < difference.ObjectPath.Length; i++)
                        {
                            var (_, actual, propertyPath) = difference.ObjectPath[i];
                            EditorGUILayout.ObjectField(i == 0 ? "" : $"> {propertyPath}", actual, typeof(Object),
                                true);
                        }

                        EditorGUILayout.LabelField($"> {difference.PropertyPath}", $"{difference.DifferenceType}",
                            EditorStyles.boldLabel);

                        EditorGUILayout.LabelField("Expected", $"{difference.ExpectedValue}");
                        EditorGUILayout.LabelField("Actual", $"{difference.ActualValue}");

                        EditorGUILayout.EndVertical();
                    }
                }
                finally
                {
                    EditorGUIUtility.labelWidth = prevLabelWidth;
                }
                EditorGUILayout.EndScrollView();

                if (GUILayout.Button("Copy Differences"))
                {
                    var diffText = new StringBuilder();
                    foreach (var difference in _diffInfo.ObjectDifferences)
                    {
                        for (var i = 0; i < difference.ObjectPath.Length; i++)
                        {
                            var (_, actual, propertyPath) = difference.ObjectPath[i];
                            if (i == 0) diffText.AppendLine($": {actual}");
                            else diffText.AppendLine($" > {propertyPath}: {actual}");
                        }
                        diffText.AppendLine($" > {difference.PropertyPath}");
                        diffText.AppendLine("Type: " + difference.DifferenceType);
                        diffText.AppendLine("Expected: " + difference.ExpectedValue);
                        diffText.AppendLine("Actual: " + difference.ActualValue);
                        diffText.AppendLine();
                    }
                    EditorGUIUtility.systemCopyBuffer = diffText.ToString();
                }
            }
        }

        class DiffInfo
        {
            public Object Expected;
            public Object Actual;
            public ObjectDifference[] ObjectDifferences;
        }

        // diff algorithm is originally based on test code in Avatar Optimizer
        // Differences:
        // - Original implementation throws exception on find difference, this does push to list of errors.
        // - Original implementation hard coded types for object equality check, this doesn't
        // ref: https://github.com/anatawa12/AvatarOptimizer/blob/b429c4c3b5056bc299102f13312d5b14ecc823e1/Test~/AnimatorOptimizer/AnimatorOptimizerTestBase.cs#L12

        public struct ObjectDifference
        {
            public (Object expected, Object actual, string propertyPathFromParent)[] ObjectPath;
            public string PropertyPath;
            public DifferenceType DifferenceType;
            // basically the type of the expected / actual type are same.
            public object ExpectedValue;
            public object ActualValue;
        }

        public enum DifferenceType
        {
            PropertyPath,
            PropertyType,
            PropertyValue,
            UnsupportedType,
        }

        private struct CheckEqualityContext
        {
            public void CheckEquality(Object except, Object actual) => CheckEqualityImpl(except, actual, "");

            public static CheckEqualityContext NewContext() => new()
            {
                ObjectInstanceEqualityTypes = new HashSet<Type>()
                {
                    typeof(MonoScript),
                },
                EqualityIgnoredTypes = new HashSet<Type>(),
                _mapping = new Dictionary<Object, Object>(),
                _objectPath = new List<(Object expected, Object actual, string path)>(),
                Differences = new List<ObjectDifference>(),
            };

            // configurations
            public HashSet<Type> ObjectInstanceEqualityTypes { get; private set; }
            public HashSet<Type> EqualityIgnoredTypes { get; private set; }
            public List<ObjectDifference> Differences;

            private Dictionary<Object, Object> _mapping;
            private List<(Object expected, Object actual, string path)> _objectPath;

            private void CheckEqualityImpl(Object expect, Object actual, string propertyPath)
            {
                if (_mapping.TryGetValue(expect, out var mapped))
                {
                    Check(propertyPath, DifferenceType.PropertyValue, mapped, actual);
                    return;
                }
                _mapping.Add(expect, actual);

                if (EqualityIgnoredTypes.Contains(expect.GetType())) return;

                _objectPath.Add((expect, actual, propertyPath));
                try
                {
                    using var exceptSerialized = new SerializedObject(expect);
                    using var actualSerialized = new SerializedObject(actual);

                    var expectIterator = exceptSerialized.GetIterator();
                    var actualIterator = actualSerialized.GetIterator();
                    CheckEqualityImpl(expect, expectIterator, actualIterator, isRoot: true);
                }
                finally
                {
                    _objectPath.RemoveAt(_objectPath.Count - 1);
                }
            }

            private void CheckEqualityImpl(Object expect, SerializedProperty expectIterator, SerializedProperty actualIterator, bool isRoot = false)
            {
                expectIterator = expectIterator.Copy();
                actualIterator = actualIterator.Copy();
                var expectEnd = isRoot ? null : expectIterator.GetEndProperty(includeInvisible: true);
                var actualEnd = isRoot ? null : actualIterator.GetEndProperty(includeInvisible: true);

                var enterChildren = true;

                for (;;)
                {
                    var expectHasValue = expectIterator.Next(enterChildren) && !SerializedProperty.EqualContents(expectIterator, expectEnd);
                    var actualHasValue = actualIterator.Next(enterChildren) && !SerializedProperty.EqualContents(actualIterator, actualEnd);
                    if (!expectHasValue || !actualHasValue) break;

                    // break the loop when property value changes
                    if (!Check(expectIterator.propertyPath, DifferenceType.PropertyPath,
                            expectIterator.propertyPath, actualIterator.propertyPath))
                        break;

                    if (Check(expectIterator.propertyPath, DifferenceType.PropertyType,
                            expectIterator.propertyType, actualIterator.propertyType))
                    {
                        if (ShouldCheckProperty(expect, expectIterator.propertyPath))
                            PropertyEquality(expectIterator, actualIterator);

                        if (expectIterator.propertyType == SerializedPropertyType.Generic)
                        {
                            CheckEqualityImpl(expect, expectIterator, actualIterator);
                        }
                    }
                    enterChildren = false;
                }
                
                // check
                Check(expectIterator.propertyPath, DifferenceType.PropertyPath, 
                    expectIterator.propertyPath, actualIterator.propertyPath);
            }

            private static readonly Regex ControllerInParameterRegex =
                new Regex(@"m_AnimatorParameters\.Array\.data\[\d+\]\.m_Controller");
            static bool ShouldCheckProperty(Object except, string propertyPath)
            {
                if (propertyPath == "m_ObjectHideFlags") return false;
                if (except is AnimatorController && ControllerInParameterRegex.IsMatch(propertyPath))
                    return false;
                return true;
            }

            bool Check(
                string propertyPath,
                DifferenceType message,
                object expectedValue,
                object actualValue)
            {
                if (Equals(expectedValue, actualValue)) return true;
                Differences.Add(new ObjectDifference()
                {
                    ObjectPath = _objectPath.ToArray(),
                    PropertyPath = propertyPath,
                    DifferenceType = message,
                    ExpectedValue = expectedValue,
                    ActualValue = actualValue,
                });
                return false;
            }

            void PropertyEquality(SerializedProperty expectIterator, SerializedProperty actualIterator)
            {
                switch (expectIterator.propertyType)
                {
                    case SerializedPropertyType.Generic:
                        // reqursively
                        break;
                    case SerializedPropertyType.ObjectReference:
                        var expectObject = expectIterator.objectReferenceValue;
                        var actualObject = actualIterator.objectReferenceValue;
                        if (expectObject == null && actualObject == null) break;
                        if (expectObject == null || actualObject == null)
                        {
                            Check(expectIterator.propertyPath, DifferenceType.PropertyValue,
                                expectObject, actualObject);
                            break;
                        }
                        Check(expectIterator.propertyPath, DifferenceType.PropertyValue,
                            expectObject.GetType(), actualObject.GetType());

                        if (ObjectInstanceEqualityTypes.Contains(expectObject.GetType())
                            || ObjectInstanceEqualityTypes.Any(x => x.IsInstanceOfType(expectObject)))
                        {
                            ObjectInstanceEqualityTypes.Add(expectObject.GetType());
                            
                            Check(expectIterator.propertyPath, DifferenceType.PropertyValue,
                                expectObject, actualObject);
                        }
                        else
                        {
                            CheckEqualityImpl(expectObject, actualObject, expectIterator.propertyPath);
                        }

                        break;
                    case SerializedPropertyType.ArraySize:
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.Boolean:
                    case SerializedPropertyType.Float:
                    case SerializedPropertyType.String:
                    case SerializedPropertyType.Color:
                    case SerializedPropertyType.LayerMask:
                    case SerializedPropertyType.Enum:
                    case SerializedPropertyType.Vector2:
                    case SerializedPropertyType.Vector3:
                    case SerializedPropertyType.Vector4:
                    case SerializedPropertyType.Rect:
                    case SerializedPropertyType.Character:
                    case SerializedPropertyType.AnimationCurve:
                    case SerializedPropertyType.Bounds:
                    case SerializedPropertyType.Quaternion:
                    case SerializedPropertyType.FixedBufferSize:
                    case SerializedPropertyType.Vector2Int:
                    case SerializedPropertyType.Vector3Int:
                    case SerializedPropertyType.RectInt:
                    case SerializedPropertyType.BoundsInt:
                        Check(expectIterator.propertyPath, DifferenceType.PropertyValue,
                            expectIterator.boxedValue, actualIterator.boxedValue);
                        break;
                    case SerializedPropertyType.ExposedReference:
                    case SerializedPropertyType.Gradient:
                    case SerializedPropertyType.ManagedReference:
                    default:
                        Differences.Add(new ObjectDifference()
                        {
                            ObjectPath = _objectPath.ToArray(),
                            PropertyPath = expectIterator.propertyPath,
                            DifferenceType = DifferenceType.UnsupportedType,
                            ExpectedValue = expectIterator.boxedValue,
                            ActualValue = expectIterator.boxedValue,
                        });
                        break;
                }
            }
        }
    }
}

#endif
