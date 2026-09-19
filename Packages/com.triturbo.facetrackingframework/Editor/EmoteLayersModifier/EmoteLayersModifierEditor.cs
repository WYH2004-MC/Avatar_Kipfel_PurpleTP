using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Linq;

using VRC.SDK3.Avatars.Components;


namespace Triturbo.FaceTrackingFramework
{
    [CustomEditor(typeof(Runtime.EmoteLayersModifierComponent))]
    public class EmoteLayersModifierEditor : Editor
    {
        public Transform avatarRoot;
        public AnimatorController controller;
        public int[] layers;

        public Texture banner;

        public SerializedProperty meshPathProp;
        public SerializedProperty autoAnimationProp;
        public SerializedProperty defaultAnimationProp;

        public SerializedProperty parameterProp;
        public SerializedProperty durationProp;
        public SerializedProperty excludeLayersProp;

        public SerializedProperty trackingEyesProp;
        public SerializedProperty trackingMouthProp;

        public SerializedProperty parametersProp;

        public SerializedProperty overwriteTrackingEyesProp;
        public SerializedProperty overwriteTrackingMouthProp;
        public SerializedProperty doOverwriteTrackingControlProp;

        private void OnHierarchyChanged()
        {
            var component = target as Runtime.EmoteLayersModifierComponent;
            avatarRoot = GetRootParent(component.transform);

            controller = avatarRoot?.GetComponent<VRCAvatarDescriptor>()?.baseAnimationLayers[4].animatorController as AnimatorController;
            layers = component.GetTargetLayers(controller);
        }
        public static Transform GetRootParent(Transform source)
        {
            Transform current = source;

            while (current.parent != null)
            {
                // Move up to the parent GameObject
                current = current.parent;
            }

            // Return the root parent GameObject
            return current;
        }



        void OnEnable()
        {
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            meshPathProp = serializedObject.FindProperty(nameof(Runtime.EmoteLayersModifierComponent.m_MeshPath));
            parameterProp = serializedObject.FindProperty(nameof(Runtime.EmoteLayersModifierComponent.m_Parameter));
            durationProp = serializedObject.FindProperty(nameof(Runtime.EmoteLayersModifierComponent.m_Duration));
            excludeLayersProp = serializedObject.FindProperty(nameof(Runtime.EmoteLayersModifierComponent.m_ExcludeLayers));
            defaultAnimationProp = serializedObject.FindProperty(nameof(Runtime.EmoteLayersModifierComponent.m_DefaultAnimation));
            autoAnimationProp = serializedObject.FindProperty(nameof(Runtime.EmoteLayersModifierComponent.m_AutoAnimation));

            trackingEyesProp = serializedObject.FindProperty(nameof(Runtime.EmoteLayersModifierComponent.m_TrackingEyes));
            trackingMouthProp = serializedObject.FindProperty(nameof(Runtime.EmoteLayersModifierComponent.m_TrackingMouth));
            parametersProp = serializedObject.FindProperty(nameof(Runtime.EmoteLayersModifierComponent.m_LayerParameters));

            doOverwriteTrackingControlProp = serializedObject.FindProperty(nameof(Runtime.EmoteLayersModifierComponent.m_DoOverwriteTrackingControl));
            overwriteTrackingEyesProp = serializedObject.FindProperty(nameof(Runtime.EmoteLayersModifierComponent.m_OverwriteTrackingEyes));
            overwriteTrackingMouthProp = serializedObject.FindProperty(nameof(Runtime.EmoteLayersModifierComponent.m_OverwriteTrackingMouth));

            var component = target as Runtime.EmoteLayersModifierComponent;
            avatarRoot = GetRootParent(component.transform);

            controller = avatarRoot?.GetComponent<VRCAvatarDescriptor>()?.baseAnimationLayers[4].animatorController as AnimatorController;
            layers = component.GetTargetLayers(controller);

        }
        private void OnDestroy()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }
        public override void OnInspectorGUI()
        {
            if (banner != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace(); // Pushes the label to the center
                GUILayout.Label(banner, GUILayout.Width(196), GUILayout.Height(49));
                GUILayout.FlexibleSpace(); // Pushes the label to the center
                GUILayout.EndHorizontal();
            }

            serializedObject.Update();


            //Controller field
            if (controller != null)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(controller, typeof(RuntimeAnimatorController), false);
                EditorGUI.EndDisabledGroup();
            }
            else
                EditorGUILayout.Separator();

            
            EditorGUILayout.PropertyField(meshPathProp);
            EditorGUILayout.PropertyField(parameterProp);
            EditorGUILayout.PropertyField(durationProp);
            
            EditorGUILayout.PropertyField(parametersProp);

            //Tracking Control
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Tracking Control");
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(trackingEyesProp, new GUIContent("Eyes & Eyelids"));
            EditorGUILayout.PropertyField(trackingMouthProp, new GUIContent("Mouth & Jaw"));


            EditorGUILayout.Separator();
            EditorGUILayout.PropertyField(doOverwriteTrackingControlProp, new GUIContent("Overwrite"));

            if (doOverwriteTrackingControlProp.boolValue)
            {
                EditorGUILayout.LabelField("Original Tracking Control Overwrite");

                EditorGUILayout.PropertyField(overwriteTrackingEyesProp, new GUIContent("Eyes & Eyelids"));
                EditorGUILayout.PropertyField(overwriteTrackingMouthProp, new GUIContent("Mouth & Jaw"));
            }

            EditorGUI.indentLevel--;
            GUILayout.EndVertical();

            EditorGUILayout.PropertyField(autoAnimationProp);
            if (!autoAnimationProp.boolValue)
            {
                EditorGUILayout.PropertyField(defaultAnimationProp);
            }

            serializedObject.ApplyModifiedProperties();


            if (controller == null || layers == null || layers.Length == 0)
            {
                EditorGUILayout.PropertyField(excludeLayersProp);
                serializedObject.ApplyModifiedProperties();

                EditorGUILayout.HelpBox("No Layer Match", MessageType.Warning);
                return;
            }



            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.BeginHorizontal();
            EditorGUIUtility.labelWidth = 50;

            EditorGUILayout.LabelField("Index");
            EditorGUILayout.LabelField("Name");

            GUIContent label = new GUIContent("Excluded");
            EditorGUILayout.LabelField(label, GUILayout.Width(GUI.skin.label.CalcSize(label).x));
            EditorGUILayout.EndHorizontal();

            bool toggleChanged = false;
            List<string> excludedLayers = new List<string>();




            foreach (var layer in layers)
            {
                if (layer >= controller.layers.Length) continue;

                EditorGUIUtility.labelWidth = 50;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(layer.ToString());

                string name = controller.layers[layer].name;
                EditorGUILayout.LabelField(name);


                EditorGUI.BeginChangeCheck();
                bool excluded = IsExcluded(name);
                excluded = EditorGUILayout.Toggle(excluded, GUILayout.Width(GUI.skin.label.CalcSize(label).x));

                if (excluded)
                {
                    excludedLayers.Add(name);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    toggleChanged = true;
                }

                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            if (toggleChanged && excludedLayers != null)
            {
                excludeLayersProp.ClearArray();
                for (int i = 0; i < excludedLayers.Count; i++)
                {
                    excludeLayersProp.InsertArrayElementAtIndex(i);
                    excludeLayersProp.GetArrayElementAtIndex(i).stringValue = excludedLayers[i];
                }
                serializedObject.ApplyModifiedProperties();
            }

        }

        private bool IsExcluded(string name)
        {
            for (int i = 0; i < excludeLayersProp.arraySize; i++)
            {
                if (excludeLayersProp.GetArrayElementAtIndex(i).stringValue == name)
                {
                    return true;
                }
            }
            return false;
        }


    }
}

