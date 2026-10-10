using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.modular_avatar.core;

namespace ASFR
{
    static class ASFRDuration
    {
        public const string Parameter = "ASFR/Duration", Speed = "ASFR/DurationSpeed";
        public const float ClipSeconds = 60f, InstantThreshold = .5f / 60f;
        public static void AddParameters(AnimatorController controller, float seconds)
        {
            controller.AddParameter(new AnimatorControllerParameter{name = Parameter, type = AnimatorControllerParameterType.Float, defaultFloat = Mathf.Clamp01(seconds / 60f)});
            controller.AddParameter(new AnimatorControllerParameter{name = Speed, type = AnimatorControllerParameterType.Float, defaultFloat = 60f / Mathf.Max(1, Mathf.RoundToInt(seconds))});
        }

        public static void Build(AnimatorController controller, ModularAvatarParameters parameters, VRCExpressionsMenu menu, float seconds, string folder)
        {
            parameters.parameters.Add(new ParameterConfig{nameOrPrefix = Parameter, syncType = ParameterSyncType.Float, saved = true, hasExplicitDefaultValue = true, defaultValue = Mathf.Clamp01(seconds / 60f)});
            menu.controls.Add(new VRCExpressionsMenu.Control{name = "Duration (0–60 seconds)", type = VRCExpressionsMenu.Control.ControlType.RadialPuppet, subParameters = new[]{new VRCExpressionsMenu.Control.Parameter{name = Parameter}}});
            // A lookup implements the reciprocal exactly at whole seconds. Sample only
            // while idle: changing the dial during a reveal applies to the next cycle.
            controller.AddLayer("ASFR Duration Selection");
            var layers = controller.layers;
            layers[layers.Length - 1].defaultWeight = 1;
            controller.layers = layers;
            var machine = layers[layers.Length - 1].stateMachine;
            var empty = new AnimationClip{name = "Duration parameter only"};
            AssetDatabase.CreateAsset(empty, folder + "/Duration Empty.anim");
            for (int secondsValue = 0; secondsValue <= 60; secondsValue++)
            {
                var state = machine.AddState(secondsValue + " seconds");
                state.motion = empty;
                state.writeDefaultValues = false;
                if (secondsValue == Mathf.Clamp(Mathf.RoundToInt(seconds), 0, 60))
                    machine.defaultState = state;
                var driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                driver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter{name = Speed, value = 60f / Mathf.Max(1, secondsValue)});
                var transition = machine.AddAnyStateTransition(state);
                transition.duration = 0;
                transition.hasExitTime = false;
                transition.canTransitionToSelf = false;
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, "ASFR/Enabled");
                if (secondsValue > 0)
                    transition.AddCondition(AnimatorConditionMode.Greater, (secondsValue - .5f) / 60f - .000001f, Parameter);
                if (secondsValue < 60)
                    transition.AddCondition(AnimatorConditionMode.Less, (secondsValue + .5f) / 60f, Parameter);
            }
        }
    }
}
