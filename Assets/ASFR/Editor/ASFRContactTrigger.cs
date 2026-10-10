using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using nadena.dev.modular_avatar.core;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace ASFR
{
    static class ASFRContactTrigger
    {
        const string Input = "Petrify";
        public const string AllowOthers = "ASFR/AllowOthers", Send = "ASFR/PetrifyOthers";
        public static void Build(AnimatorController controller, Transform root, Transform system, ModularAvatarParameters parameters, string folder)
        {
            var hips = root.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips);
            if (!hips)
                throw new InvalidOperationException("Petrify contacts require a humanoid Hips bone.");
            int contacts = root.GetComponentsInChildren<VRCContactReceiver>(true).Length + root.GetComponentsInChildren<VRCContactSender>(true).Length;
            if (contacts + 2 > 256)
                throw new InvalidOperationException("ASFR Petrify needs two additional contacts; total would exceed 256.");
            var receiverObject = new GameObject("Petrify Receiver");
            receiverObject.transform.SetParent(system, false);
            var receiver = receiverObject.AddComponent<VRCContactReceiver>();
            receiver.rootTransform = hips;
            receiver.shapeType = ContactBase.ShapeType.Sphere;
            receiver.radius = .25f / Mathf.Max(.0001f, Mathf.Abs(hips.lossyScale.x));
            receiver.allowSelf = false;
            receiver.allowOthers = true;
            receiver.localOnly = true;
            receiver.receiverType = ContactReceiver.ReceiverType.Constant;
            receiver.parameter = Input;
            receiver.collisionTags = new List<string>{"Petrify"};
            var senderObject = new GameObject("Petrify Others Sender");
            senderObject.transform.SetParent(system, false);
            var sender = senderObject.AddComponent<VRCContactSender>();
            sender.rootTransform = hips;
            sender.shapeType = ContactBase.ShapeType.Sphere;
            sender.radius = 1f / Mathf.Max(.0001f, Mathf.Abs(hips.lossyScale.x));
            sender.collisionTags = new List<string>{"Petrify"};
            senderObject.SetActive(false);
            controller.AddParameter(AllowOthers, AnimatorControllerParameterType.Bool);
            controller.AddParameter(Send, AnimatorControllerParameterType.Bool);
            parameters.parameters.Add(new ParameterConfig{nameOrPrefix = AllowOthers, syncType = ParameterSyncType.Bool, localOnly = true, saved = true, hasExplicitDefaultValue = true, defaultValue = 0});
            parameters.parameters.Add(new ParameterConfig{nameOrPrefix = Send, syncType = ParameterSyncType.Bool, localOnly = false, saved = false, hasExplicitDefaultValue = true, defaultValue = 0});
            SenderLayer(controller, root, senderObject, folder);
            var types = new HashSet<AnimatorControllerParameterType>();
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            if (descriptor.expressionParameters)
                foreach (var p in descriptor.expressionParameters.parameters)
                    if (p.name == Input)
                        types.Add(p.valueType == VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Bool ? AnimatorControllerParameterType.Bool : p.valueType == VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Int ? AnimatorControllerParameterType.Int : AnimatorControllerParameterType.Float);
            var controllers = descriptor.baseAnimationLayers.Concat(descriptor.specialAnimationLayers).Select(l => l.animatorController).Concat(root.GetComponentsInChildren<ModularAvatarMergeAnimator>(true).Select(m => m.animator));
            foreach (var runtime in controllers)
            {
                var resolved = runtime;
                while (resolved is AnimatorOverrideController overrides)
                    resolved = overrides.runtimeAnimatorController;
                if (resolved is AnimatorController ac)
                    foreach (var p in ac.parameters)
                        if (p.name == Input)
                            types.Add(p.type);
            }

            foreach (var config in root.GetComponentsInChildren<ModularAvatarParameters>(true).SelectMany(p => p.parameters))
                if ((config.nameOrPrefix == Input || config.remapTo == Input) && config.syncType != ParameterSyncType.NotSynced)
                    types.Add(config.syncType == ParameterSyncType.Bool ? AnimatorControllerParameterType.Bool : config.syncType == ParameterSyncType.Int ? AnimatorControllerParameterType.Int : AnimatorControllerParameterType.Float);
            if (types.Count > 1 || types.Contains(AnimatorControllerParameterType.Trigger))
                throw new InvalidOperationException("Petrify must use a consistent Bool, Int or Float type in the avatar's parameters/controllers.");
            // Float also supports a proximity receiver. Contacts write directly to animator parameters.
            var type = types.Count == 0 ? AnimatorControllerParameterType.Float : types.Single();
            controller.AddParameter(Input, type);
            if (!controller.parameters.Any(p => p.name == "IsLocal"))
                controller.AddParameter("IsLocal", AnimatorControllerParameterType.Bool);
            controller.AddLayer("ASFR Petrify Contact");
            var layers = controller.layers;
            layers[layers.Length - 1].defaultWeight = 1;
            controller.layers = layers;
            var sm = layers[layers.Length - 1].stateMachine;
            var empty = new AnimationClip{name = "Petrify Contact Empty"};
            AssetDatabase.CreateAsset(empty, folder + "/Petrify Contact.anim");
            var ready = sm.AddState("Ready");
            ready.motion = empty;
            ready.writeDefaultValues = false;
            sm.defaultState = ready;
            var fired = sm.AddState("Triggered - wait for release");
            fired.motion = empty;
            fired.writeDefaultValues = false;
            var driver = fired.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            driver.localOnly = true;
            driver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter{name = "ASFR/Enabled", value = 1});
            var start = ready.AddTransition(fired);
            start.duration = 0;
            start.hasExitTime = false;
            start.AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
            start.AddCondition(AnimatorConditionMode.If, 0, AllowOthers);
            start.AddCondition(type == AnimatorControllerParameterType.Bool ? AnimatorConditionMode.If : AnimatorConditionMode.Greater, type == AnimatorControllerParameterType.Float ? .001f : 0, Input);
            // No continuous writes: a held contact cannot override the user's Reset.
            var release = fired.AddTransition(ready);
            release.duration = 0;
            release.hasExitTime = false;
            release.AddCondition(type == AnimatorControllerParameterType.Bool ? AnimatorConditionMode.IfNot : AnimatorConditionMode.Less, type == AnimatorControllerParameterType.Int ? 1 : 0.0001f, Input);
        }

        public static void AddMenu(VRCExpressionsMenu menu)
        {
            foreach (var pair in new[]{new[]{"Allow Others", AllowOthers}, new[]{"Petrify Others", Send}})
                menu.controls.Add(new VRCExpressionsMenu.Control{name = pair[0], type = VRCExpressionsMenu.Control.ControlType.Toggle, parameter = new VRCExpressionsMenu.Control.Parameter{name = pair[1]}, value = 1});
        }

        static void SenderLayer(AnimatorController controller, Transform root, GameObject sender, string folder)
        {
            controller.AddLayer("ASFR Petrify Sender");
            var layers = controller.layers;
            layers[layers.Length - 1].defaultWeight = 1;
            controller.layers = layers;
            var sm = layers[layers.Length - 1].stateMachine;
            var states = new AnimatorState[2];
            for (int i = 0; i < 2; i++)
            {
                var clip = new AnimationClip{name = "Petrify Sender " + (i == 0 ? "Off" : "On")};
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(sender.transform, root), typeof(GameObject), "m_IsActive"), AnimationCurve.Constant(0, 1f / 30, i));
                AssetDatabase.CreateAsset(clip, folder + "/" + clip.name + ".anim");
                states[i] = sm.AddState(i == 0 ? "Off" : "On");
                states[i].motion = clip;
                states[i].writeDefaultValues = false;
            }

            sm.defaultState = states[0];
            for (int i = 0; i < 2; i++)
            {
                var t = states[i].AddTransition(states[1 - i]);
                t.hasExitTime = false;
                t.duration = 0;
                t.AddCondition(i == 0 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, Send);
            }
        }
    }
}
