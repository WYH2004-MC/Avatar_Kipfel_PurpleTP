using UnityEngine;
using UnityEngine.Animations;

#if UNITY_EDITOR
    using UnityEditor;
#endif


#if VRC_SDK_VRCSDK3
    using VRC.SDK3.Avatars.Components;
#endif

namespace DX.VRCraft
{
    [ExecuteInEditMode]
    public class RepositionOBJ : MonoBehaviour
    {
        public Transform RepPos;
        #if VRC_SDK_VRCSDK3 && UNITY_EDITOR

            private void Start()
            {
                Reposition();
            }

            private void Awake()
            {
                Reposition();
            }

            private void Update()
            {
                Reposition();
            }

            private void OnValidate()
            {
                Reposition();
            }

            private void Reposition()
            {
                if (EditorApplication.isPlaying){
                    return;
                }

                RepPos.position = transform.position;
                RepPos.rotation = transform.rotation;


            }

        #endif
    }
}