using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;


#if ENABLE_MA
using nadena.dev.modular_avatar.core;
#endif

namespace Triturbo.FaceTrackingFramework.Runtime
{

    [AddComponentMenu("TriturboFT/Mouth Tracking Settings")]
    [DisallowMultipleComponent]
    public class MouthTrackingSettingsComponent : MonoBehaviour, IEditorOnly

    {

#if ENABLE_MA
        public ModularAvatarParameters m_Parameters;
#else
        public MonoBehaviour m_Parameters;
#endif

        public float m_MouthResetWeight = 1;
        public float m_JawDropStrength = 1;


        public AnimationClip m_ResetClip;

        public string m_MouthResetWeightParameter = "FT/Settings/MouthResetWeight";

        public string m_JawDropParameter = "FT/Settings/JawDropStrength";
        
        [System.NonSerialized]
        private SkinnedMeshRenderer body;

        [System.NonSerialized]
        public SkinnedMeshRenderer previewProxy;

        [System.NonSerialized]
        public bool isPreview = false;


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
}

