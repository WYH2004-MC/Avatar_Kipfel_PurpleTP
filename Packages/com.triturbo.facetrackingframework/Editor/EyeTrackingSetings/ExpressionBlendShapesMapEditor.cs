using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using System.Linq;
using System.Text.RegularExpressions;
using System;

namespace Triturbo.FaceTrackingFramework
{

    [CustomEditor(typeof(Runtime.ExpressionBlendShapesMap))]

    public class ExpressionBlendShapesMapEditor : Editor
    {
        public SkinnedMeshRenderer renderer;
       // public SerializedProperty expressionBlendShapesProp;

        public void OnEnable()
        {
          //  expressionBlendShapesProp = serializedObject.FindProperty(nameof(Runtime.ExpressionBlendShapesMap.m_ExpressionBlendShapes));
        }


        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            renderer = EditorGUILayout.ObjectField(renderer, typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;



            if(GUILayout.Button("find"))
            {

                string[] blendshapes = new string[renderer.sharedMesh.blendShapeCount];

                for(int i = 0; i < blendshapes.Length; i++)
                {
                    blendshapes[i] = renderer.sharedMesh.GetBlendShapeName(i);
                }

                var items = new List<Runtime.ExpressionBlendShapeItem>();



                for (int i = 0; i < blendshapes.Length - 2; i++)
                {
                    if ((blendshapes[i + 1].Contains("_L") && blendshapes[i + 2].Contains("_R")) || blendshapes[i + 1].Contains("_left") && blendshapes[i + 2].Contains("_right")
                        || blendshapes[i + 1].Contains("��") && blendshapes[i + 2].Contains("�E"))
                    {
                        items.Add(new Runtime.ExpressionBlendShapeItem(blendshapes[i], blendshapes[i + 1], blendshapes[i + 2]));

                        i += 2;
                    }
                }


                //for (int i = blendshapes.Length - 1; i > 1; i--)
                //{
                //    if ((blendshapes[i - 2].Contains("_L") && blendshapes[i - 1].Contains("_R")) || blendshapes[i - 2].Contains("_left") && blendshapes[i - 1].Contains("_right"))
                //    {
                //        items.Add(new Runtime.ExpressionBlendShapeItem(blendshapes[i], blendshapes[i - 2], blendshapes[i - 1]));
                //        i -= 2;
                //    }
                //}
                //items.Reverse();


                (target as Runtime.ExpressionBlendShapesMap).m_ExpressionBlendShapes  = items.ToArray();
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssets();

            }
        }
    }
}

