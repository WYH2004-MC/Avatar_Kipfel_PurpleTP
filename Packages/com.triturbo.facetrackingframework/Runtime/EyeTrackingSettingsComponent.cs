using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;
#if ENABLE_MA
using nadena.dev.modular_avatar.core;
#endif


namespace Triturbo.FaceTrackingFramework.Runtime
{
    [HelpURL("https://tr1turbo.github.io/Docs/face-tracking-addon/eye-tracking-settings.html")]
    [AddComponentMenu("TriturboFT/Eye Tracking Settings")]
    [DisallowMultipleComponent]
    public class EyeTrackingSettingsComponent : MonoBehaviour, IEditorOnly

    {

        public float m_EyeWeight = 1;
        public float m_ResetWeight = 0;


        public ResettingBlendshapesGroup[] m_ResettingGroups;

        public ExpressionBlendShapesMap m_ExpressionBlendShapes;
        public string m_BodyName = "Body";
        public string m_EyeWeightParameter = "FT/Control/EyeWeight";

#if ENABLE_MA
        public ModularAvatarParameters m_Parameters;
#else
        public MonoBehaviour m_Parameters;
#endif

        [System.NonSerialized]
        private SkinnedMeshRenderer body;

        [System.NonSerialized]
        public SkinnedMeshRenderer previewProxy;

        [System.NonSerialized]
        public bool isPreview = false;

        [System.NonSerialized]
        public PreviewMode previewMode = PreviewMode.BothEye;

        public SkinnedMeshRenderer Body
        {
            get
            {
                if (body == null)
                    body = GetRootParent(transform)?.Find("Body")?.GetComponent<SkinnedMeshRenderer>();

                return body;
            }
        }
        public SkinnedMeshRenderer GetBody()
        {
            body = GetRootParent(transform)?.Find("Body")?.GetComponent<SkinnedMeshRenderer>();
            return body;
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

    }
    public enum PreviewMode
    {
        BothEye,
        LeftEye,
        RightEye
    }


    [System.Serializable]
    public class ResettingBlendshapesGroup
    {
        public string m_GroupName;
        public string m_Parameter;
        public float m_weight;

        public AnimationClip m_ResetClipBoth;
        public AnimationClip m_ResetClipLeft;
        public AnimationClip m_ResetClipRight;
    }

}


