using System.Collections;
using System.Collections.Generic;
using UnityEngine;


using VRC.SDK3.Avatars.Components;
using UnityEditor;
using System;
using Triturbo.FaceTrackingFramework.Runtime;


namespace Triturbo.FaceTrackingFramework
{

    [CustomEditor(typeof(InverseVisemeCreatorComponent))]
    public class InverseVisemeCreatorEditor : Editor
    {
        public Texture banner;
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

        }
        
    }

}
