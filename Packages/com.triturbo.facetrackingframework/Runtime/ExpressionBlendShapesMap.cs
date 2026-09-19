using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Triturbo.FaceTrackingFramework.Runtime
{
    [CreateAssetMenu(menuName = "TriturboFT/ExpressionBlendShapesMap")]
    public class ExpressionBlendShapesMap : ScriptableObject
    {
        public ExpressionBlendShapeItem[] m_ExpressionBlendShapes;


        //Split combined blendshape to Left, Right
        public void SplitExpressionWeight(SkinnedMeshRenderer body)
        {
            if (body == null) return;
            if (body.sharedMesh == null) return;

            foreach (var item in m_ExpressionBlendShapes)
            {
                int combinedIndex = body.sharedMesh.GetBlendShapeIndex(item.m_Combined);

                float combinedWeight = 0;

                if (combinedIndex == -1) continue;


                combinedWeight = body.GetBlendShapeWeight(combinedIndex);

                if (combinedWeight == 0) continue;
                //body.SetBlendShapeWeight(combinedIndex, 0);
                //Debug.Log(item.m_Combined);
                
                int leftIndex = body.sharedMesh.GetBlendShapeIndex(item.m_Left);
                int rightIndex = body.sharedMesh.GetBlendShapeIndex(item.m_Right);

                float leftWeight = 0;
                float rightWeight = 0;

                if (leftIndex == -1) continue;


                leftWeight = body.GetBlendShapeWeight(leftIndex);
                    //body.SetBlendShapeWeight(leftIndex, leftWeight + combinedWeight)
                
                if (rightIndex == -1) continue;

                rightWeight = body.GetBlendShapeWeight(rightIndex);
                    //body.SetBlendShapeWeight(rightIndex, rightWeight + combinedWeight);
                
                float spareL = 100.0f - leftWeight;
                float spareR = 100.0f - rightWeight;

                float spare = spareL > spareR ? spareR : spareL;

                body.SetBlendShapeWeight(combinedIndex, spare > combinedWeight ? 0 : (combinedWeight - spare));

                body.SetBlendShapeWeight(leftIndex, spare > combinedWeight ? leftWeight + combinedWeight : 100.0f);
                body.SetBlendShapeWeight(rightIndex, spare > combinedWeight ? rightWeight + combinedWeight : 100.0f);

            }

        }

    }

    [System.Serializable]
    public class ExpressionBlendShapeItem
    {
        public ExpressionBlendShapeItem(string combined, string left, string right)
        {
            m_Combined = combined;
            m_Left = left;
            m_Right = right;
        }
        public string m_Combined;
        public string m_Left;
        public string m_Right;
    }
}

