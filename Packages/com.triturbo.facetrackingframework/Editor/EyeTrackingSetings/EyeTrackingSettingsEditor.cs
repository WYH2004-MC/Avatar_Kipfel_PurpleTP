using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;

using Triturbo.FaceTrackingFramework.Runtime;
using VRC.SDK3.Avatars.ScriptableObjects;

#if ENABLE_MA
using nadena.dev.modular_avatar.core;
#endif

namespace Triturbo.FaceTrackingFramework
{
    [CustomEditor(typeof(EyeTrackingSettingsComponent))]
    public class EyeTrackingSettingsEditor : Editor
    {
        public Texture banner;
        EyeTrackingSettingsComponent component;
        public SerializedProperty eyeWeightProp;
        public SerializedProperty eyeWeightParameterProp;
        public SerializedProperty expressionBlendShapesProp;
        public SerializedProperty resettingGroupsProp;
        public SerializedProperty maParametersProp;

        public Transform root;

        private static List<SkinnedMeshRenderer> previewProxys = new List<SkinnedMeshRenderer>();
        private bool showHiddenSettings = false;

        public float previewEyeClosedStrength = 1;
        public EyeResetBlendShapesGroup[] resetBlendShapesGroups;


        public struct EyeResetBlendShapesGroup
        {
            private ResetBlendShapeGroup resetBlendShapes;
            private ResetBlendShapeGroup resetBlendShapesLeft;
            private ResetBlendShapeGroup resetBlendShapesRight;
            public EyeResetBlendShapesGroup(AnimationClip both, AnimationClip left, AnimationClip right)
            {
                resetBlendShapes = new ResetBlendShapeGroup(both);
                resetBlendShapesLeft = new ResetBlendShapeGroup(left);
                resetBlendShapesRight = new ResetBlendShapeGroup(right);
            }
            public void SetInitialWeights(SkinnedMeshRenderer skinnedMeshRenderer)
            {
                resetBlendShapes.SetInitialWeights(skinnedMeshRenderer);
                resetBlendShapesLeft.SetInitialWeights(skinnedMeshRenderer);
                resetBlendShapesRight.SetInitialWeights(skinnedMeshRenderer);
            }
            public void SetBlendShapesWeight(SkinnedMeshRenderer skinnedMeshRenderer, float weight, float left, float right)
            {
                resetBlendShapes.SetBlendShapesWeight(skinnedMeshRenderer, weight);
                resetBlendShapesLeft.SetBlendShapesWeight(skinnedMeshRenderer, left);
                resetBlendShapesRight.SetBlendShapesWeight(skinnedMeshRenderer, right);
            }
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
        
        void init(Runtime.EyeTrackingSettingsComponent component)
        {
            if (component.GetBody() == null) return;

            resetBlendShapesGroups = new EyeResetBlendShapesGroup[component.m_ResettingGroups.Length];

            for(int i =0; i < resetBlendShapesGroups.Length; i++)
            {
                resetBlendShapesGroups[i] = new EyeResetBlendShapesGroup(
                    component.m_ResettingGroups[i].m_ResetClipBoth,
                    component.m_ResettingGroups[i].m_ResetClipLeft, 
                    component.m_ResettingGroups[i].m_ResetClipRight);
            }
        }
        void OnEnable()
        {
            component = target as Runtime.EyeTrackingSettingsComponent;

            eyeWeightProp = serializedObject.FindProperty(nameof(Runtime.EyeTrackingSettingsComponent.m_EyeWeight));
            eyeWeightParameterProp = serializedObject.FindProperty(nameof(Runtime.EyeTrackingSettingsComponent.m_EyeWeightParameter));


            expressionBlendShapesProp = serializedObject.FindProperty(nameof(Runtime.EyeTrackingSettingsComponent.m_ExpressionBlendShapes));
            resettingGroupsProp = serializedObject.FindProperty(nameof(Runtime.EyeTrackingSettingsComponent.m_ResettingGroups));

            maParametersProp = serializedObject.FindProperty(nameof(Runtime.EyeTrackingSettingsComponent.m_Parameters));


            init(component);
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        }
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                //Debug.Log("OnExitingEditMode");
                ExistPreview(component);
                ApplyParameters(onDestroy: true);
                //ApplyMenu();
            }
            //EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        public void ExistPreview(Runtime.EyeTrackingSettingsComponent component)
        {
            component.ExistPreview();

            foreach(var p in previewProxys)
            {
                if(p != null) DestroyImmediate(p.gameObject);
            }
            previewProxys.Clear();
        }

        void OnPreview(Runtime.EyeTrackingSettingsComponent component)
        {
            if (component.previewProxy == null && component.Body != null)
            {
                component.previewProxy = Instantiate(component.Body);

                component.m_ExpressionBlendShapes.SplitExpressionWeight(component.previewProxy);

                component.previewProxy.gameObject.SetActive(true);
                component.previewProxy.enabled = true;
                component.previewProxy.name = "preview(press Delete to delete me)";

                component.previewProxy.gameObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                component.previewProxy.hideFlags = HideFlags.NotEditable;
                component.Body.enabled = false;
                
                previewProxys.Add(component.previewProxy);
                
                for(int i = 0; i < resetBlendShapesGroups.Length; i++)
                {
                    resetBlendShapesGroups[i].SetInitialWeights(component.previewProxy);
                }
                
                
                var mesh = component.previewProxy.sharedMesh;

                if(mesh != null)
                {
                    int ftBaseIndex = mesh.GetBlendShapeIndex("-----FT-----");
                    if(ftBaseIndex != -1)
                        component.previewProxy.SetBlendShapeWeight(ftBaseIndex, 100);
                }
            }

            if (component.previewProxy != null)
            {
                int blendShapeIndexRight = component.previewProxy.sharedMesh.GetBlendShapeIndex("EyeClosedRight");
                int blendShapeIndexLeft = component.previewProxy.sharedMesh.GetBlendShapeIndex("EyeClosedLeft");

                float eyeCloseLeft = 0;
                float eyeCloseRight = 0;


                float previewEyeClosedLeftStrength = 0;
                float previewEyeClosedRightStrength = 0;

                if (component.previewMode == PreviewMode.BothEye)
                {
                    eyeCloseLeft = component.m_EyeWeight * 100 * previewEyeClosedStrength;
                    eyeCloseRight = eyeCloseLeft;

                    previewEyeClosedLeftStrength =  previewEyeClosedStrength;
                    previewEyeClosedRightStrength = previewEyeClosedStrength;


                }
                else if(component.previewMode == PreviewMode.LeftEye)
                {
                    eyeCloseLeft = component.m_EyeWeight * 100 * previewEyeClosedStrength;

                    previewEyeClosedLeftStrength = previewEyeClosedStrength;
                }
                else if (component.previewMode == PreviewMode.RightEye)
                {

                    eyeCloseRight = component.m_EyeWeight * 100 * previewEyeClosedStrength;

                    previewEyeClosedRightStrength = previewEyeClosedStrength;
                }

                if (blendShapeIndexLeft != -1)
                {
                    component.previewProxy.SetBlendShapeWeight(blendShapeIndexLeft, eyeCloseLeft);
                }
                if (blendShapeIndexRight != -1)
                {
                    component.previewProxy.SetBlendShapeWeight(blendShapeIndexRight, eyeCloseRight);
                }

                for(int i = 0; i < resetBlendShapesGroups.Length; i++ )
                {
                    var group = resetBlendShapesGroups[i];
                    group.SetBlendShapesWeight(component.previewProxy,  
                        previewEyeClosedStrength * component.m_ResettingGroups[i].m_weight,
                        previewEyeClosedLeftStrength * component.m_ResettingGroups[i].m_weight,
                        previewEyeClosedRightStrength * component.m_ResettingGroups[i].m_weight);
                }

            }

        }
         
        private void OnDestroy()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            if (component == null) return;

            ExistPreview(component);
            ApplyParameters(onDestroy: true);
        }
        public override void OnInspectorGUI()
        {
            //Show Banner
            if (banner != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace(); // Pushes the label to the center
                GUILayout.Label(banner, GUILayout.Width(196), GUILayout.Height(49));
                GUILayout.FlexibleSpace(); // Pushes the label to the center
                GUILayout.EndHorizontal();
            }

            serializedObject.Update();

            EditorGUI.BeginDisabledGroup(component.Body == null);
            EditorGUI.BeginChangeCheck();


            //EyeClosed Weight Slider
            EditorGUILayout.Slider(eyeWeightProp, 0, 1, Localization.G("eye_tracking_settings.eye_weight"));

            //Reset Weight Slider

            
            for (int i = 0; i < resettingGroupsProp.arraySize; i++)
            {
                var property = resettingGroupsProp.GetArrayElementAtIndex(i).FindPropertyRelative(nameof(Runtime.ResettingBlendshapesGroup.m_weight));
                string name = component.m_ResettingGroups[i].m_GroupName;
                EditorGUILayout.Slider(property , 0.0001f, 1, Localization.GF("eye_tracking_settings.reset", name));
            }
            if (EditorGUI.EndChangeCheck())
            {
                component.isPreview = true;
            }

            Color defaultColor = GUI.backgroundColor;

            if(component.isPreview)
            {
                GUI.backgroundColor = Color.green;

            }

            EditorGUI.BeginChangeCheck();
            if (GUILayout.Button(component.isPreview ? Localization.S("tracking_settings.end_preview") : Localization.S("tracking_settings.preview")))
            {
                component.isPreview = !component.isPreview;
                init(component);
                ApplyParameters();
            }
            if (EditorGUI.EndChangeCheck() && !component.isPreview)
            {
                ExistPreview(component);
            }

            GUI.backgroundColor = defaultColor;
            EditorGUI.EndDisabledGroup();

            if (component.isPreview)
            {
                OnPreview(component);
                component.previewMode = (PreviewMode)EditorGUILayout.EnumPopup(component.previewMode);
                previewEyeClosedStrength = EditorGUILayout.Slider(Localization.G("eye_tracking_settings.eye_closed"), previewEyeClosedStrength, 0, 1);
            }
            else
            {
                EditorGUILayout.Space();
            }

            //Creator Settings
            showHiddenSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showHiddenSettings, "Creator Settings");
            if (showHiddenSettings)
            {
                EditorGUILayout.PropertyField(maParametersProp);

                EditorGUILayout.PropertyField(expressionBlendShapesProp);
                EditorGUILayout.PropertyField(eyeWeightParameterProp);


                EditorGUILayout.EndFoldoutHeaderGroup();
                EditorGUILayout.PropertyField(resettingGroupsProp);
            }
            serializedObject.ApplyModifiedProperties();
        }

        void ApplyParameters(bool onDestroy = false)
        {
#if ENABLE_MA
            if (component == null) return;

            ModularAvatarParameters map = component.m_Parameters;
            if(map == null)
            {
                map = component.GetComponentInChildren<ModularAvatarParameters>();
            }
            if (map == null) return;

            Undo.RecordObject(map, "Apply MA Parameters");


            if(!onDestroy)
                serializedObject.ApplyModifiedProperties();

            PrefabUtility.RecordPrefabInstancePropertyModifications(component.ApplyParameters());
#endif
        }


        private void OnSceneGUI()
        {
            if (!component.isPreview) return;
            var sceneView = SceneView.currentDrawingSceneView;

            if (sceneView == null) return;
            Rect rect = sceneView.camera.pixelRect;

            Handles.BeginGUI();
            // Calculate the position for the bottom left
            float x = 10;
            float y = rect.height - 30; 
            float width = 200;
            float height = 20;

            GUILayout.BeginArea(new Rect(x, y, width, height));
            GUILayout.Label("Eye Tracking Preview");
            GUILayout.EndArea();
            Handles.EndGUI();
        }


        public static float[] GetInitialWeight(SkinnedMeshRenderer skinnedMeshRenderer, string[] targets)
        {
            if (skinnedMeshRenderer == null)
            {
                return null;
            }
            float[] weight = new float[targets.Length];
            for (int i =0; i < targets.Length; i++)
            {
                int blendShapeIndex = skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex(targets[i]);
                weight[i] = skinnedMeshRenderer.GetBlendShapeWeight(blendShapeIndex);
            }
            return weight;
        }


        public string[] GetBlendShapes(AnimationClip clip)
        {
            if(clip == null)
            {
                return System.Array.Empty<string>();
            }
            // Get all the bindings (curves) of the animation clip
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            return bindings.Where(b => b.propertyName
                .StartsWith("blendShape."))
                .Select(b => b.propertyName.Substring("blendShape.".Length))
                .ToArray();
        }
    }


    static class EyeTrackingSettingExtension
    {
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clip"></param>
        /// <param name="mappings"></param>
        //public static void ReplaceBlendshapeCurves(AnimationClip clip, ExpressionBlendShapesMap mappings)
        //{
        //    if (clip == null || mappings == null || mappings == null)
        //    {
        //        Debug.LogError("Clip or mappings are invalid!");
        //        return;
        //    }

        //    // Get all animation curves in the clip
        //    var bindings = AnimationUtility.GetCurveBindings(clip);

        //    // Create a dictionary for quick mapping
        //    var mappingDict = new Dictionary<string, (string left, string right)>();
        //    foreach (var mapping in mappings.m_ExpressionBlendShapes)
        //    {
        //        mappingDict[mapping.m_Combined] = (mapping.m_Left, mapping.m_Right);
        //    }
 
        //    // Duplicate curves for left and right
        //    foreach (var binding in bindings)
        //    {
        //        if (!binding.propertyName.StartsWith("blendShape.")) continue;

        //        // Extract blendshape name
        //        string blendshapeName = binding.propertyName.Replace("blendShape.", "");

        //        // Check if it exists in the mapping
        //        if (mappingDict.TryGetValue(blendshapeName, out var targets))
        //        {
        //            var curve = AnimationUtility.GetEditorCurve(clip, binding);

        //            // Create bindings for left and right
        //            var leftBinding = new EditorCurveBinding
        //            {
        //                path = binding.path,
        //                type = binding.type,
        //                propertyName = $"blendShape.{targets.left}"
        //            };

        //            var rightBinding = new EditorCurveBinding
        //            {
        //                path = binding.path,
        //                type = binding.type,
        //                propertyName = $"blendShape.{targets.right}"
        //            };

        //            // Assign the same curve to left and right
        //            AnimationUtility.SetEditorCurve(clip, leftBinding, curve);
        //            AnimationUtility.SetEditorCurve(clip, rightBinding, curve);

        //            // Optionally, remove the original curve
        //            //AnimationUtility.SetEditorCurve(clip, binding, null);

        //            Debug.Log($"Replaced {blendshapeName} with {targets.left} and {targets.right}.");
        //        }
        //    }
        //}



#if ENABLE_MA
        static public ModularAvatarParameters ApplyParameters(this EyeTrackingSettingsComponent component)
        {

            var map = component.m_Parameters;

            if (map == null)
            {
                map = component.GetComponentInChildren<ModularAvatarParameters>();

                if (map == null)
                {
                    map = component.gameObject.AddComponent<ModularAvatarParameters>();
                }
            }

            string eyeParameter = component.m_EyeWeightParameter;
            //string resetParameter = component.m_ResetWeightParameter;

            int eyeWeight = map.parameters.FindIndex(p => p.nameOrPrefix == eyeParameter);
            if (eyeWeight != -1)
            {
                var edited = map.parameters[eyeWeight];
                edited.defaultValue =  component.m_EyeWeight;
                edited.hasExplicitDefaultValue = true;
                edited.localOnly =  true;
                edited.syncType = ParameterSyncType.NotSynced;
                edited.saved = true;
                map.parameters[eyeWeight] = edited;
            }
            else
            {
                map.parameters.Add(new ParameterConfig() {
                    nameOrPrefix = eyeParameter,
                    defaultValue = component.m_EyeWeight,
                    hasExplicitDefaultValue = true,
                    localOnly = true,
                    syncType = ParameterSyncType.NotSynced,
                    saved = true
                });
            }


            foreach(var group in component.m_ResettingGroups)
            {
                 
                int resetWeight = map.parameters.FindIndex(p => p.nameOrPrefix == group.m_Parameter);
                if (resetWeight != -1)
                {
                    var edited = map.parameters[resetWeight];
                    edited.defaultValue = group.m_weight;
                    edited.hasExplicitDefaultValue = true;
                    edited.localOnly = true;
                    edited.syncType = ParameterSyncType.NotSynced;
                    edited.saved = true;
                    map.parameters[resetWeight] = edited;
                }
                else
                {
                    map.parameters.Add(new ParameterConfig()
                    {
                        nameOrPrefix = group.m_Parameter,
                        defaultValue = group.m_weight,
                        hasExplicitDefaultValue = true,
                        localOnly = true,
                        syncType = ParameterSyncType.NotSynced,
                        saved = true
                    });
                }
            }


            return map;
        }



#endif
        static public void ExistPreview(this Runtime.EyeTrackingSettingsComponent component)
        {
            component.isPreview = false;

            if (component.Body != null)
            {
                component.Body.enabled = true;
                if (PrefabUtility.IsPartOfPrefabInstance(component.Body))
                {
                    SerializedObject so = new SerializedObject(component.Body);
                    SerializedProperty property = so.FindProperty("m_Enabled");
                    PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
                }

            }


            if (component.previewProxy != null)
            {
                Object.DestroyImmediate(component.previewProxy.gameObject);
                component.previewProxy = null;
            }

        }


    }

}

