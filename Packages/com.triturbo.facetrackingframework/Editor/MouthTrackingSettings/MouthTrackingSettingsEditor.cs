using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;

using Triturbo.FaceTrackingFramework.Runtime;

#if ENABLE_MA
using nadena.dev.modular_avatar.core;
#endif

namespace Triturbo.FaceTrackingFramework
{
    [CustomEditor(typeof(MouthTrackingSettingsComponent))]
    public class MouthTrackingSettingsEditor : Editor
    {
        public Texture banner;
        MouthTrackingSettingsComponent component;


        public SerializedProperty mouthResetWeightProp;
        public SerializedProperty mouthResetWeightParameterProp;
        public SerializedProperty resetClipProp;
        public SerializedProperty jawDropStrengthProp;
        public SerializedProperty jawDropParameterProp;
        public SerializedProperty maParametersProp;

        public Transform root;

        private static List<SkinnedMeshRenderer> previewProxys = new List<SkinnedMeshRenderer>();
        private bool showHiddenSettings = false;

        public float previewMouthClosedStrength = 0;
        public float previewJawOpenStrength = 1;

        public ResetBlendShapeGroup resetBlendShapesGroup;
        
        void init(Runtime.MouthTrackingSettingsComponent component)
        {
            if (component.GetBody() == null) return;
            resetBlendShapesGroup = new ResetBlendShapeGroup(component.m_ResetClip);
        }
        void OnEnable()
        {
            component = target as Runtime.MouthTrackingSettingsComponent;
            maParametersProp = serializedObject.FindProperty(nameof(Runtime.MouthTrackingSettingsComponent.m_Parameters));

            mouthResetWeightProp = serializedObject.FindProperty(nameof(Runtime.MouthTrackingSettingsComponent.m_MouthResetWeight));
            mouthResetWeightParameterProp = serializedObject.FindProperty(nameof(Runtime.MouthTrackingSettingsComponent.m_MouthResetWeightParameter));
            resetClipProp = serializedObject.FindProperty(nameof(Runtime.MouthTrackingSettingsComponent.m_ResetClip));

            jawDropStrengthProp = serializedObject.FindProperty(nameof(Runtime.MouthTrackingSettingsComponent.m_JawDropStrength));
            jawDropParameterProp = serializedObject.FindProperty(nameof(Runtime.MouthTrackingSettingsComponent.m_JawDropParameter));


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
        }

        void ExistPreview(Runtime.MouthTrackingSettingsComponent component)
        {
            component.ExistPreview();

            foreach(var p in previewProxys)
            {
                if(p != null) DestroyImmediate(p.gameObject);
            }
            previewProxys.Clear();
        }

        void OnPreview(Runtime.MouthTrackingSettingsComponent component)
        {
            if (component.previewProxy == null && component.Body != null)
            {
                component.previewProxy = Instantiate(component.Body);
                component.previewProxy.gameObject.SetActive(true);
                component.previewProxy.enabled = true;
                component.previewProxy.name = "preview(press Delete to delete me)";

                component.previewProxy.gameObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                component.previewProxy.hideFlags = HideFlags.NotEditable;
                component.Body.enabled = false;

                previewProxys.Add(component.previewProxy);
                resetBlendShapesGroup.SetInitialWeights(component.previewProxy);

                var mesh = component.previewProxy.sharedMesh;
                if(mesh != null)
                {
                    int jawOpen = mesh.GetBlendShapeIndex("JawOpen");
                    if(jawOpen != -1)
                        component.previewProxy.SetBlendShapeWeight(jawOpen, 0);
                    
                    
                    int ftBaseIndex = mesh.GetBlendShapeIndex("-----FT-----");
                    if(ftBaseIndex != -1)
                        component.previewProxy.SetBlendShapeWeight(ftBaseIndex, 100);
                    
                    int mouthTrackingBase = mesh.GetBlendShapeIndex("MouthTrackingBase*");
                    if(mouthTrackingBase != -1)
                        component.previewProxy.SetBlendShapeWeight(mouthTrackingBase, 100);
                }

            }

            if (component.previewProxy != null)
            {
                int mouthOpen = component.previewProxy.sharedMesh.GetBlendShapeIndex("MouthOpen*");
                int jawDrop = component.previewProxy.sharedMesh.GetBlendShapeIndex("JawDrop*");

                int mouthClosed = component.previewProxy.sharedMesh.GetBlendShapeIndex("MouthClosed");
                int jawClosed = component.previewProxy.sharedMesh.GetBlendShapeIndex("JawClosed*");


                if (jawDrop != -1 && mouthOpen != -1)
                {

                    component.previewProxy.SetBlendShapeWeight(mouthOpen, previewJawOpenStrength * 100);
                    component.previewProxy.SetBlendShapeWeight(jawDrop, component.m_JawDropStrength * previewJawOpenStrength * 100);

                    if(mouthClosed != -1)
                    {
                        component.previewProxy.SetBlendShapeWeight(mouthClosed, previewMouthClosedStrength * 100);


                        if(jawClosed != -1)
                            component.previewProxy.SetBlendShapeWeight(jawClosed, component.m_JawDropStrength  * previewMouthClosedStrength * 100);
                    }

                }

                float resetWeight = (previewJawOpenStrength - previewMouthClosedStrength) * component.m_MouthResetWeight;
                resetBlendShapesGroup.SetBlendShapesWeight(component.previewProxy, resetWeight);
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
            
            EditorGUILayout.Slider(jawDropStrengthProp, 0, 1, 
                Localization.G("mouth_tracking_settings.jaw_strength")
            );
            EditorGUILayout.Slider(mouthResetWeightProp, 0.0001f, 1,
                Localization.G("mouth_tracking_settings.reset_weight"));
            mouthResetWeightProp.floatValue = mouthResetWeightProp.floatValue < 0.0001f ? 0.0001f : mouthResetWeightProp.floatValue;

            
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

                EditorGUI.BeginChangeCheck();
                previewJawOpenStrength = EditorGUILayout.Slider(Localization.G("ue.JawOpen"), previewJawOpenStrength, 0, 1);
                if (EditorGUI.EndChangeCheck())
                {
                    if (previewMouthClosedStrength > previewJawOpenStrength)
                    {
                        previewMouthClosedStrength = previewJawOpenStrength;
                    }
                }

                EditorGUI.BeginChangeCheck();
                previewMouthClosedStrength = EditorGUILayout.Slider(Localization.G("ue.MouthClosed"), previewMouthClosedStrength, 0, 1);
                if (EditorGUI.EndChangeCheck())
                {
                    if (previewMouthClosedStrength > previewJawOpenStrength)
                    {
                        previewJawOpenStrength = previewMouthClosedStrength;
                    }
                }

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
                EditorGUILayout.PropertyField(resetClipProp);
                EditorGUILayout.PropertyField(mouthResetWeightParameterProp);
                EditorGUILayout.PropertyField(jawDropParameterProp);
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
            serializedObject.ApplyModifiedProperties();
        }

        void ApplyParameters(bool onDestroy = false)
        {
#if ENABLE_MA
            if (component == null) return;

            // ModularAvatarParameters map = component.m_Parameters;
            // if(map == null)
            // {
            //     map = component.GetComponentInChildren<ModularAvatarParameters>();
            // }
            // if (map == null) return;
            //
            // Undo.RecordObject(map, "Apply MA Parameters");
            
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
            GUILayout.Label("Mouth Tracking Preview");
            GUILayout.EndArea();
            Handles.EndGUI();
        }




    }


    static class MouthTrackingSettingExtension
    {

#if ENABLE_MA
        static public ModularAvatarParameters ApplyParameters(this MouthTrackingSettingsComponent component)
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
            
            int jawParameterIndex = map.parameters.FindIndex(p => p.nameOrPrefix == component.m_JawDropParameter);
            if (jawParameterIndex != -1)
            {
                var edited = map.parameters[jawParameterIndex];
                edited.defaultValue =  component.m_JawDropStrength;
                edited.hasExplicitDefaultValue = true;
                edited.localOnly =  true;
                edited.syncType = ParameterSyncType.NotSynced;
                edited.saved = true;
                map.parameters[jawParameterIndex] = edited;
            }
            else
            {
                map.parameters.Add(new ParameterConfig() {
                    nameOrPrefix = component.m_JawDropParameter,
                    defaultValue = component.m_JawDropStrength,
                    hasExplicitDefaultValue = true,
                    localOnly = true,
                    syncType = ParameterSyncType.NotSynced,
                    saved = true
                });
            }

            int mouthParameterIndex = map.parameters.FindIndex(p => p.nameOrPrefix == component.m_MouthResetWeightParameter);
            if (mouthParameterIndex != -1)
            {
                var edited = map.parameters[mouthParameterIndex];
                edited.defaultValue = component.m_MouthResetWeight;
                edited.hasExplicitDefaultValue = true;
                edited.localOnly = true;
                edited.syncType = ParameterSyncType.NotSynced;
                edited.saved = true;
                map.parameters[mouthParameterIndex] = edited;
            }
            else
            {
                map.parameters.Add(new ParameterConfig()
                {
                    nameOrPrefix = component.m_MouthResetWeightParameter,
                    defaultValue = component.m_MouthResetWeight,
                    hasExplicitDefaultValue = true,
                    localOnly = true,
                    syncType = ParameterSyncType.NotSynced,
                    saved = true
                });
            }

            return map;
        }
#endif
        static public void ExistPreview(this Runtime.MouthTrackingSettingsComponent component)
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

