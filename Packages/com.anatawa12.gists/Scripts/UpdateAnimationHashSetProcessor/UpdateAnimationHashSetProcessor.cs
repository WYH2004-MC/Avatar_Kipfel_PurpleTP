/*
 * UpdateAnimationHashSetProcessor
 * A IVRCSDKPreprocessAvatarCallback to update animation hashset of VRCAvatarDescriptor.
 * https://gist.github.com/anatawa12/e4b346a295d44476763814a7cad6bfd9
 *
 * Updates animation hashset of VRCAvatarDescriptor as the last IVRCSDKPreprocessAvatarCallback
 *
 * MIT License
 * 
 * Copyright (c) 2023 anatawa12
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

#if UNITY_EDITOR && (!ANATAWA12_GISTS_VPM_PACKAGE || GIST_e4b346a295d44476763814a7cad6bfd9)

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;
using Object = UnityEngine.Object;

namespace anatawa12.gists
{
    internal class UpdateAnimationHashSetProcessor : IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => int.MaxValue;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            var avatar = avatarGameObject.GetComponent<VRCAvatarDescriptor>();

            avatar.animationHashSet.Clear();

            foreach (var animLayer in avatar.baseAnimationLayers)
            {
                var controller = animLayer.animatorController as AnimatorController;
                if (controller == null) continue;

                foreach (var layer in controller.layers)
                {
                    ProcessStateMachine(layer.stateMachine, "");
                    continue;

                    void ProcessStateMachine(AnimatorStateMachine stateMachine, string prefix)
                    {
                        prefix = prefix + stateMachine.name + ".";

                        foreach (var state in stateMachine.states)
                        {
                            var fullName = prefix + state.state.name;
                            avatar.animationHashSet.Add(new VRCAvatarDescriptor.DebugHash
                            {
                                hash = Animator.StringToHash(fullName),
                                name = fullName.Remove(0, fullName.IndexOf('.') + 1),
                            });
                        }

                        foreach (var subMachine in stateMachine.stateMachines)
                            ProcessStateMachine(subMachine.stateMachine, prefix);
                    }
                }
            }

            return true;
        }
    }
}

#endif
