using UnityEditor;

#if ENABLE_TFTF
using Triturbo.FaceTrackingFramework.Installer;
#endif

namespace Triturbo.FaceTrackingAddon {
    public class KipfelFaceTrackingEditor : 
#if ENABLE_TFTF
        FaceTrackingInstaller
#else
        EditorWindow
#endif
    {
        
        [MenuItem("TriturboFT/Kipfel FT")]
        public static void Init()
        { 
            GetWindow<KipfelFaceTrackingEditor>("Kipfel FT");
        }

#if !ENABLE_TFTF
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Triturbo Face Tracking Framework is not detected.\nPlease import it to enable full functionality.", MessageType.Warning);
        }
#endif
    }
}