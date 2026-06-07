using UnityEngine;

#if UNITY_EDITOR
    using UnityEditor;
#endif


#if VRC_SDK_VRCSDK3
    using VRC.SDK3.Avatars.Components;
#endif

namespace DX.VRCraft
{
    [ExecuteInEditMode]
    public class ViewpointHelper : MonoBehaviour
    {
        public Transform Viewpoint;
        #if VRC_SDK_VRCSDK3 && UNITY_EDITOR

            private void Start()
            {
                PositionViewPoint();
            }

            private void Awake()
            {
                PositionViewPoint();
            }

            private void Update()
            {
                PositionViewPoint();
            }

            private void OnValidate()
            {
                PositionViewPoint();
            }

            private void PositionViewPoint()
            {
                if (EditorApplication.isPlaying){
                    return;
                }

                var avatar = GetComponentInParent<VRCAvatarDescriptor>();

                if (avatar)
                {
                    Viewpoint.position = avatar.transform.TransformPoint(avatar.ViewPosition);

                }

            }

        #endif
    }
}