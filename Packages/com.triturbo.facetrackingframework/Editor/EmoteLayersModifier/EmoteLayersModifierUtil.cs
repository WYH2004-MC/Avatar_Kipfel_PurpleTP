using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using UnityEditor.Animations;
using System.Linq;
using System;
using UnityEditor;

using Triturbo.FaceTrackingFramework.Runtime;
namespace Triturbo.FaceTrackingFramework
{
    public static class EmoteLayersModifierUtil 
    {
        

        public static int[] GetTargetLayers(this EmoteLayersModifierComponent component, VRCAvatarDescriptor avatardesciptor)
        {
            List<int> matchingLayerIndices = new List<int>(); // List to store matching layer indices

            AnimatorController animator = avatardesciptor.baseAnimationLayers[4].animatorController as AnimatorController;


            return component.GetTargetLayers(animator);
        }
        public static int[] GetTargetLayers(this EmoteLayersModifierComponent component, AnimatorController animator)
        {
            if (animator == null)
            {
                return Array.Empty<int>();
            }
            
            if (component.m_LayerParameters == null)
            {
                component.m_LayerParameters = new string[] { "GestureLeft", "GestureRight" };
            }
            List<int> matchingLayerIndices = new List<int>(); // List to store matching layer indices

            for (int i = 0; i < animator.layers.Length; i++)
            {
                AnimatorStateMachine stateMachine = animator.layers[i].stateMachine;

                if (!IsAnimating(stateMachine, component.m_MeshPath)) continue;

                if (component.m_LayerParameters.Length == 0)
                {
                    matchingLayerIndices.Add(i);
                    continue;
                }

                if (stateMachine.anyStateTransitions.Any(t => t.conditions.Any(c => component.m_LayerParameters.Contains(c.parameter))))
                {
                    matchingLayerIndices.Add(i);
                    continue;
                }
                
                foreach (ChildAnimatorState state in stateMachine.states)
                {
                    if (HasParametersCondition(state.state.transitions, component.m_LayerParameters))
                    {
                        matchingLayerIndices.Add(i);
                        break;
                    }
                }
            }
            return matchingLayerIndices.ToArray();
        }
        private static bool HasParametersCondition(AnimatorStateTransition[] transitions, string[] parameters)
        {
            if(parameters == null)
            {
                return false;
            }
            foreach (var transition in transitions)
            {
                if (transition.conditions.Any(c => parameters.Contains(c.parameter)))
                {
                    return true;
                }
            }

            return false;
        }

        // return true if clip is animatig a SkinnedMeshRenderer with the path ;
        public static bool IsAnimating(AnimationClip clip, string path)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);

            foreach (EditorCurveBinding binding in bindings)
            {
                if (binding.type == typeof(SkinnedMeshRenderer) && binding.path == path)
                {
                    return true;
                }
            }
            return false;
        }
        public static bool IsAnimating(AnimatorStateMachine stateMachine, string path)
        {
            foreach (var state in stateMachine.states)
            {
                AnimatorState animatorState = state.state;
                if (animatorState.motion is AnimationClip)
                {
                    if (IsAnimating(animatorState.motion as AnimationClip, path))
                    {
                        return true;
                    }
                }
                else if (animatorState.motion is BlendTree)
                {
                    if (IsAnimating(animatorState.motion as BlendTree, path))
                    {
                        return true;
                    }
                }
            }

            foreach (var subStateMachine in stateMachine.stateMachines)
            {
                if (IsAnimating(subStateMachine.stateMachine, path))
                {
                    return true;
                }
            }

            return false;
        }
        public static bool IsAnimating(BlendTree blendTree, string path)
        {
            foreach (var child in blendTree.children)
            {
                if (child.motion is AnimationClip)
                {
                    if (IsAnimating(child.motion as AnimationClip, path))
                    {
                        return true;
                    }
                }
                else if (child.motion is BlendTree)
                {
                    
                    if (IsAnimating(child.motion as BlendTree, path))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static AnimationClip GenerateDefaultClip(SkinnedMeshRenderer body, string path, HashSet<string> blendshapes)
        {
            AnimationClip clip = new AnimationClip();
            //clip.legacy = true; // Make the clip legacy if needed

            foreach (string blendshape in blendshapes)
            {
                int index = body.sharedMesh.GetBlendShapeIndex(blendshape);
                if (index != -1)
                {
                    string propertyName = $"blendShape.{blendshape}";
                    AnimationCurve curve = new AnimationCurve(new Keyframe(0, body.GetBlendShapeWeight(index)));
                    clip.SetCurve(path, typeof(SkinnedMeshRenderer), propertyName, curve);
                }
                else
                {
                    Debug.LogWarning($"Blendshape '{blendshape}' not found in the SkinnedMeshRenderer.");
                }
            }

            return clip;
        }


        public static HashSet<string> GetAnimatedBlendshapes(IEnumerable<AnimationClip> clips, string meshPath)
        {
            HashSet<string> blendshapes = new HashSet<string>();

            foreach (AnimationClip clip in clips)
            {
                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);

                foreach (EditorCurveBinding binding in bindings)
                {
                    if (binding.type == typeof(SkinnedMeshRenderer) && binding.path == meshPath)
                    {
                        if (binding.propertyName.StartsWith("blendShape."))
                        {
                            string blendshapeName = binding.propertyName.Replace("blendShape.", "");
                            blendshapes.Add(blendshapeName);
                        }
                    }
                }
            }

            return blendshapes;
        }
        public static void GetClipsFromStateMachine(AnimatorStateMachine stateMachine, HashSet<AnimationClip> clips)
        {
            foreach (var state in stateMachine.states)
            {
                AnimatorState animatorState = state.state;
                if (animatorState.motion is AnimationClip)
                {
                    clips.Add(animatorState.motion as AnimationClip);
                }
                else if (animatorState.motion is BlendTree)
                {
                    GetClipsFromBlendTree(animatorState.motion as BlendTree, clips);
                }
            }

            foreach (var subStateMachine in stateMachine.stateMachines)
            {
                GetClipsFromStateMachine(subStateMachine.stateMachine, clips);
            }
        }

        public static void GetClipsFromBlendTree(BlendTree blendTree, HashSet<AnimationClip> clips)
        {
            foreach (var child in blendTree.children)
            {
                if (child.motion is AnimationClip)
                {
                    clips.Add(child.motion as AnimationClip);
                }
                else if (child.motion is BlendTree)
                {
                    GetClipsFromBlendTree(child.motion as BlendTree, clips);
                }
            }
        }
    }
}

