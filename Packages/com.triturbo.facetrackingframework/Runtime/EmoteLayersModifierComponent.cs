using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

using VRC.SDKBase;

namespace Triturbo.FaceTrackingFramework.Runtime
{
    [DisallowMultipleComponent]
    [AddComponentMenu("TriturboFT/Emote Layers Modifier")]
    public class EmoteLayersModifierComponent : MonoBehaviour, IEditorOnly

    {

        public string m_MeshPath = "Body";
        public bool m_AutoAnimation = true;
        public AnimationClip m_DefaultAnimation;
        public string m_Parameter;
        public float m_Duration;
        public string[] m_ExcludeLayers;
        public VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType m_TrackingEyes;
        public VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType m_TrackingMouth;

        public bool m_DoOverwriteTrackingControl = true;
        public VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType m_OverwriteTrackingEyes;
        public VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType m_OverwriteTrackingMouth;
        public string[] m_LayerParameters = new string[] { "GestureLeft", "GestureRight" };
        
        public bool IsExclude(string layerName)
        {
            return m_ExcludeLayers.Contains(layerName);
        }

    }
}

