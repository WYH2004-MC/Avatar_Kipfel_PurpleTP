using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Triturbo.FaceTrackingFramework
{
    public class ResetBlendShapeGroup
    {
        public string[] resetBlendShapes;
        public float[] initialWeights;
        public float[] targetWeights;
        
        public ResetBlendShapeGroup(AnimationClip clip)
        {
            if (clip == null)
            {
                resetBlendShapes = System.Array.Empty<string>();
                targetWeights = System.Array.Empty<float>();
                return;
            }

            // Get all the curve bindings of the animation clip
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);

            // Filter only blendshape bindings
            var blendShapeBindings = bindings
                .Where(b => b.propertyName.StartsWith("blendShape."))
                .ToArray();

            resetBlendShapes = new string[blendShapeBindings.Length];
            targetWeights = new float[blendShapeBindings.Length];
            initialWeights =  new float[blendShapeBindings.Length];
            
            
            for (int i = 0; i < blendShapeBindings.Length; i++)
            {
                var binding = blendShapeBindings[i];
                string blendShapeName = binding.propertyName.Substring("blendShape.".Length);
                resetBlendShapes[i] = blendShapeName;

                // Get the animation curve for this binding
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);

                if (curve != null && curve.length > 0)
                {
                    // Get the last keyframe value (last frame weight)
                    targetWeights[i] = curve.keys[curve.length - 1].value;
                }
                else
                {
                    // Default to 0 if no curve exists
                    targetWeights[i] = 0f;
                }
            }
        }
        
        
        public void SetInitialWeights(SkinnedMeshRenderer skinnedMeshRenderer)
        {
            if (skinnedMeshRenderer == null)
            {
                return;
            }
            initialWeights = new float[resetBlendShapes.Length];
            for (int i =0; i < resetBlendShapes.Length; i++)
            {
                int blendShapeIndex = skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex(resetBlendShapes[i]);
                initialWeights[i] = skinnedMeshRenderer.GetBlendShapeWeight(blendShapeIndex);
            }
        }
        
        
        public void SetBlendShapesWeight(SkinnedMeshRenderer skinnedMeshRenderer, float weight)
        {
            if(skinnedMeshRenderer == null)
            {
                return;
            }
            if(resetBlendShapes.Length != initialWeights.Length || resetBlendShapes.Length != targetWeights.Length)
            {
                return;
            }
            if (skinnedMeshRenderer.sharedMesh == null) return;
            for (int i = 0; i < resetBlendShapes.Length; i++)
            {

                int blendShapeIndex = skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex(resetBlendShapes[i]);
                if(blendShapeIndex != -1)
                {
                    skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, initialWeights[i] + (targetWeights[i] - initialWeights[i]) * weight);
                }
                
                
            }
        }
    }

}
