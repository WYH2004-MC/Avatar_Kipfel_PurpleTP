using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


using nadena.dev.ndmf;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;

namespace Triturbo.FaceTrackingFramework
{
    public class EmoteLayersModifierPass : Pass<EmoteLayersModifierPass>
    {
        public override string DisplayName => "EmoteLayersModifierPass";

        protected override void Execute(BuildContext context)
        {
            var component = context.AvatarRootTransform.GetComponentInChildren<Runtime.EmoteLayersModifierComponent>();
            if (component == null)
            {
                return;
            }
            var avatardesciptor = context.AvatarDescriptor;
            
            RuntimeAnimatorController controller = avatardesciptor.baseAnimationLayers[4].animatorController;
            if (controller == null)
            {
                return;
            }

            AnimatorController animatorController = controller as AnimatorController;

            // Add parameter
            if (!animatorController.parameters.Any(p => p.name == component.m_Parameter))
            {
                animatorController.AddParameter(component.m_Parameter, AnimatorControllerParameterType.Bool);
            }

            int[] targets = component.GetTargetLayers(avatardesciptor);


            bool writeDefault = Util.AnimationUtil.IsAllStatesWriteDefaultOn(animatorController);

            AnimationClip defaultClip = null;

            if (!writeDefault && component.m_AutoAnimation)
            {
                var body = Util.AnimationUtil.FindTransformByPath(avatardesciptor.transform, component.m_MeshPath)
                    ?.GetComponent<SkinnedMeshRenderer>();
                HashSet<AnimationClip> animationClips = new HashSet<AnimationClip>();
                foreach (int index in targets)
                {
                    if ((animatorController.layers.Length <= index)) continue;

                    if (component.IsExclude(animatorController.layers[index].name)) continue;

                    var sm = animatorController.layers[index].stateMachine;
                    EmoteLayersModifierUtil.GetClipsFromStateMachine(sm, animationClips);

                }

                if (body != null)
                {
                    defaultClip = EmoteLayersModifierUtil.GenerateDefaultClip(body, component.m_MeshPath,
                        EmoteLayersModifierUtil.GetAnimatedBlendshapes(animationClips, component.m_MeshPath));
                    defaultClip.name = "EmoteLayersModifierDefaultClip";
                }
            }
            else
            {
                defaultClip = component.m_DefaultAnimation;
            }




            foreach (int index in targets)
            {
                if ((animatorController.layers.Length <= index)) continue;

                if (component.IsExclude(animatorController.layers[index].name)) continue;

                var sm = animatorController.layers[index].stateMachine;

                foreach (var t in sm.anyStateTransitions)
                {
                    t.AddCondition(AnimatorConditionMode.IfNot, 0, component.m_Parameter);
                }

                if (component.m_DoOverwriteTrackingControl)
                {
                    foreach (var state in sm.states)
                    {
                        foreach (var behave in state.state.behaviours)
                        {
                            if (behave is VRCAnimatorTrackingControl trackingControl)
                            {
                                trackingControl.trackingEyes = component.m_OverwriteTrackingEyes;
                                trackingControl.trackingMouth = component.m_OverwriteTrackingMouth;
                            }
                        }
                    }

                }

                var defaultState = sm.AddState("EmoteLayersModifierDefault",
                    sm.anyStatePosition + new Vector3(0, 200, 0));

                if (component.m_TrackingEyes != VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.NoChange ||
                    component.m_TrackingMouth != VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.NoChange)
                {
                    var behaviour = defaultState
                        .AddStateMachineBehaviour<VRC.SDK3.Avatars.Components.VRCAnimatorTrackingControl>();
                    behaviour.trackingEyes = component.m_TrackingEyes;
                    behaviour.trackingMouth = component.m_TrackingMouth;
                }

                if (defaultClip != null)
                {
                    defaultState.motion = defaultClip;
                }

                defaultState.writeDefaultValues = writeDefault;

                var anyToDefault = sm.AddAnyStateTransition(defaultState);


                anyToDefault.AddCondition(AnimatorConditionMode.If, 0, component.m_Parameter);
                anyToDefault.canTransitionToSelf = false;
                anyToDefault.duration = component.m_Duration;
                anyToDefault.hasExitTime = false;

                var exit = defaultState.AddExitTransition();
                exit.AddCondition(AnimatorConditionMode.IfNot, 0, component.m_Parameter);
                exit.duration = component.m_Duration;
                exit.hasExitTime = false;
            }
        }
    }
}
