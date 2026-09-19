


using System;
using UnityEditor.Animations;
using UnityEngine;

namespace Triturbo.FaceTrackingFramework.Util
{
    public static class AnimationUtil
    {
        
        /// <summary>
        /// Checks whether the "Write Defaults" option is enabled for all Animator states in an AnimatorController.
        /// </summary>
        /// <param name="animator">The AnimatorController to check.</param>
        /// <returns>
        /// True if all states in all layers have "Write Defaults" enabled; otherwise, false.
        /// Returns false if the provided AnimatorController is null.
        /// </returns>
        public static bool IsAllStatesWriteDefaultOn(AnimatorController animator)
        {
            if (animator == null)
            {
                return false;
            }
            for (int i = 0; i < animator.layers.Length; i++)
            {
                AnimatorStateMachine stateMachine = animator.layers[i].stateMachine;
                foreach (ChildAnimatorState state in stateMachine.states)
                {
                    if(state.state == null) continue;
                    if (!state.state.writeDefaultValues)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        
        
        /// <summary>
        /// Retrieves a Transform within a hierarchy based on an animation-style path.
        /// </summary>
        /// <param name="root">The root Transform from which the search begins.</param>
        /// <param name="path">A '/'-separated string representing the hierarchy path to the target Transform.</param>
        /// <returns>The Transform corresponding to the given path, or null if not found.</returns>
        public static Transform FindTransformByPath(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return root;
            }

            string[] pathSegments = path.Split('/');
            Transform current = root.transform;

            foreach (string segment in pathSegments)
            {
                current = current.Find(segment);
                if (current == null)
                {
                    return null; // Return null if any segment is not found
                }
            }

            return current;
        }

    }
}