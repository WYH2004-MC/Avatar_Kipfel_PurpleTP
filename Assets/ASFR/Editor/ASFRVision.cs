using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.Dynamics;
using nadena.dev.modular_avatar.core;

namespace ASFR
{
    // The renderer follows the live head for culling, outside the chopped/frozen skeleton.
    public static class ASFRVision
    {
        static readonly string[] Modes = {"Darkness", "Ice", "Custom Texture"};
        const string Enabled = "ASFR/VisionEnabled", Mode = "ASFR/VisionMode", Strength = "ASFR/VisionStrength";
        public static VRCExpressionsMenu Build(AnimatorController controller, Transform root, Transform system, ModularAvatarParameters parameters, Texture2D texture, float strength, string folder)
        {
            var animator = root.GetComponent<Animator>();
            var head = animator && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (!head)
                throw new InvalidOperationException("Vision overlay requires a humanoid Head bone.");
            var shader = Shader.Find("ASFR/Frozen Vision");
            if (!shader)
                throw new InvalidOperationException("Import ASFR/Shaders/FrozenVision.shader before generating the vision overlay.");
            var go = new GameObject("Frozen Vision");
            go.transform.SetParent(system, false);
            go.transform.SetPositionAndRotation(head.position, head.rotation);
            var follow = go.AddComponent<VRCParentConstraint>();
            follow.Sources.Add(new VRCConstraintSource(head, 1));
            follow.ZeroConstraint();
            follow.ApplyConfigurationChanges();
            var mesh = ASFRScreen.CreateQuad("ASFR Vision Quad", folder + "/Vision Quad.asset");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(shader)
            {name = "ASFR Vision"};
            material.SetFloat("_Strength", strength);
            material.SetFloat("_UseTexture", texture ? 1 : 0);
            if (texture)
                material.SetTexture("_OverlayTex", texture);
            AssetDatabase.CreateAsset(material, folder + "/Vision.mat");
            ASFRScreen.AddRenderer(go, material);
            go.SetActive(false);
            AddParameter(controller, parameters, Enabled, ParameterSyncType.Bool, 1);
            AddParameter(controller, parameters, Mode, ParameterSyncType.Int, 0);
            AddParameter(controller, parameters, Strength, ParameterSyncType.Float, strength);
            bool hasLocal = false;
            foreach (var p in controller.parameters)
                if (p.name == "IsLocal")
                    hasLocal = true;
            if (!hasLocal)
                controller.AddParameter("IsLocal", AnimatorControllerParameterType.Bool);
            controller.AddLayer("ASFR Local Vision");
            var layers = controller.layers;
            var layer = layers[layers.Length - 1];
            layer.defaultWeight = 1;
            controller.layers = layers;
            var sm = layer.stateMachine;
            string path = AnimationUtility.CalculateTransformPath(go.transform, root);
            var off = sm.AddState("Hidden");
            off.writeDefaultValues = false;
            off.motion = Clip(path, folder, "Vision Hidden", false, 0, 0);
            sm.defaultState = off;
            for (int mode = 0; mode < Modes.Length; mode++)
            {
                var state = sm.AddState(Modes[mode]);
                state.writeDefaultValues = false;
                var blend = new BlendTree{name = "Vision Intensity " + mode, blendType = BlendTreeType.Simple1D, blendParameter = Strength, useAutomaticThresholds = false};
                AssetDatabase.AddObjectToAsset(blend, controller);
                blend.AddChild(Clip(path, folder, "Vision " + mode + " Zero", true, mode, 0), 0);
                blend.AddChild(Clip(path, folder, "Vision " + mode + " Full", true, mode, 1), 1);
                state.motion = blend;
                var enter = sm.AddAnyStateTransition(state);
                enter.duration = 0;
                enter.hasExitTime = false;
                enter.canTransitionToSelf = false;
                foreach (string condition in new[]{"IsLocal", "ASFR/Enabled", ASFRFaceFreeze.Frozen, Enabled})
                    enter.AddCondition(AnimatorConditionMode.If, 0, condition);
                enter.AddCondition(AnimatorConditionMode.Equals, mode, Mode);
                foreach (string condition in new[]{"IsLocal", "ASFR/Enabled", ASFRFaceFreeze.Frozen, Enabled})
                {
                    var exit = state.AddTransition(off);
                    exit.duration = 0;
                    exit.hasExitTime = false;
                    exit.AddCondition(AnimatorConditionMode.IfNot, 0, condition);
                }
            }

            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.controls.Add(Toggle("Vision overlay", Enabled, 1));
            for (int i = 0; i < Modes.Length; i++)
                menu.controls.Add(Toggle(Modes[i], Mode, i));
            menu.controls.Add(new VRCExpressionsMenu.Control{name = "Intensity", type = VRCExpressionsMenu.Control.ControlType.RadialPuppet, subParameters = new[]{new VRCExpressionsMenu.Control.Parameter{name = Strength}}});
            AssetDatabase.CreateAsset(menu, folder + "/Vision Menu.asset");
            return menu;
        }

        static VRCExpressionsMenu.Control Toggle(string label, string parameter, int value)
        {
            return new VRCExpressionsMenu.Control{name = label, type = VRCExpressionsMenu.Control.ControlType.Toggle, parameter = new VRCExpressionsMenu.Control.Parameter{name = parameter}, value = value};
        }

        static void AddParameter(AnimatorController c, ModularAvatarParameters p, string name, ParameterSyncType type, float value)
        {
            var at = type == ParameterSyncType.Bool ? AnimatorControllerParameterType.Bool : type == ParameterSyncType.Int ? AnimatorControllerParameterType.Int : AnimatorControllerParameterType.Float;
            c.AddParameter(new AnimatorControllerParameter{name = name, type = at, defaultBool = value != 0, defaultInt = (int)value, defaultFloat = value});
            p.parameters.Add(new ParameterConfig{nameOrPrefix = name, syncType = type, localOnly = true, saved = true, hasExplicitDefaultValue = true, defaultValue = value});
        }

        static AnimationClip Clip(string path, string folder, string name, bool active, int mode, float strength)
        {
            var clip = new AnimationClip{name = name};
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(GameObject), "m_IsActive"), AnimationCurve.Constant(0, 1f / 30, active ? 1 : 0));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(MeshRenderer), "material._Mode"), AnimationCurve.Constant(0, 1f / 30, mode));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(MeshRenderer), "material._Strength"), AnimationCurve.Constant(0, 1f / 30, strength));
            AssetDatabase.CreateAsset(clip, folder + "/" + name + ".anim");
            return clip;
        }
    }
}
