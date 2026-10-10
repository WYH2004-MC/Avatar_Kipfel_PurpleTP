using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace ASFR
{
    static class ASFRClothing
    {
        public const string Parameter = "ASFR/Clothing";
        public const string HideSourcesClip = "ASFR Clothing Sources Hidden";
        static void Set(AnimationClip clip, Transform item, Transform root, System.Type type, string property, float value)
        {
            var binding = EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(item, root), type, property);
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0, 1f / 30, value));
        }

        static void Weight(AnimatorState state, int layer, float value)
        {
            var control = state.AddStateMachineBehaviour<VRCAnimatorLayerControl>();
            control.playable = VRC.SDKBase.VRC_AnimatorLayerControl.BlendableLayer.FX;
            control.layer = layer;
            control.goalWeight = value;
            control.blendDuration = 0;
        }

        public static bool MatchesBodyShape(string name)
        {
            return !string.IsNullOrEmpty(name) && (name.IndexOf("shrink", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("off", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("kisekae", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static void BuildBodyOverride(AnimatorController controller, Transform root, SkinnedMeshRenderer body, SkinnedMeshRenderer[] overlays, string folder)
        {
            if (!body || !body.sharedMesh)
                return;
            var names = Enumerable.Range(0, body.sharedMesh.blendShapeCount).Select(body.sharedMesh.GetBlendShapeName).Where(MatchesBodyShape).Distinct().ToArray();
            if (names.Length == 0)
            {
                Debug.Log("ASFR: no shrink/off/kisekae blendshapes on main body " + body.name + ".", body);
                return;
            }

            // Separate layer: Keep Clothing must release the shape override even though
            // its visibility layer remains active. Never change source/default weights.
            controller.AddLayer("ASFR Main Body Clothing Shapes");
            int layer = controller.layers.Length - 1;
            var layers = controller.layers;
            layers[layer].defaultWeight = 0;
            controller.layers = layers;
            var machine = controller.layers[layer].stateMachine;
            var normal = new AnimationClip{name = "Body shapes released"};
            var clear = new AnimationClip{name = "Body shapes unshrunk"};
            foreach (var target in new[]{body}.Concat(overlays).Where(r => r && r.sharedMesh).Distinct())
                foreach (string name in names)
                    if (target.sharedMesh.GetBlendShapeIndex(name) >= 0)
                        Set(clear, target.transform, root, typeof(SkinnedMeshRenderer), "blendShape." + name, 0);
            AssetDatabase.CreateAsset(normal, folder + "/Body Shapes Released.anim");
            AssetDatabase.CreateAsset(clear, folder + "/Body Shapes Unshrunk.anim");
            var released = machine.AddState("Use avatar blendshapes");
            released.motion = normal;
            released.writeDefaultValues = false;
            var removed = machine.AddState("Remove clothing - clear shrink, off and kisekae");
            removed.motion = clear;
            removed.writeDefaultValues = false;
            Weight(released, layer, 0);
            Weight(removed, layer, 1);
            machine.defaultState = released;
            var enter = released.AddTransition(removed);
            enter.hasExitTime = false;
            enter.duration = 0;
            enter.AddCondition(AnimatorConditionMode.If, 0, "ASFR/Enabled");
            enter.AddCondition(AnimatorConditionMode.Equals, 2, Parameter);
            var reset = removed.AddTransition(released);
            reset.hasExitTime = false;
            reset.duration = 0;
            reset.AddCondition(AnimatorConditionMode.IfNot, 0, "ASFR/Enabled");
            var changed = removed.AddTransition(released);
            changed.hasExitTime = false;
            changed.duration = 0;
            changed.AddCondition(AnimatorConditionMode.NotEqual, 2, Parameter);
            Debug.Log("ASFR: Remove clothing clears " + names.Length + " main-body blendshapes: " + string.Join(", ", names), body);
        }

        static void BuildSourceVisibility(AnimatorController controller, Transform root, SkinnedMeshRenderer[] originals, SkinnedMeshRenderer[] overlays, SkinnedMeshRenderer[] copies, string folder)
        {
            controller.AddLayer("ASFR Clothing Source Visibility");
            int layer = controller.layers.Length - 1;
            var layers = controller.layers;
            layers[layer].defaultWeight = 0;
            controller.layers = layers;
            var machine = layers[layer].stateMachine;
            var idleClip = new AnimationClip{name = "Clothing visibility released"};
            var hiddenClip = new AnimationClip{name = HideSourcesClip};
            var restoreClip = new AnimationClip{name = "Clothing visibility restored"};
            foreach (var original in originals)
                Set(hiddenClip, original.transform, root, typeof(SkinnedMeshRenderer), "m_Enabled", 0);
            foreach (var renderer in originals.Concat(overlays).Concat(copies).Distinct())
                Set(restoreClip, renderer.transform, root, typeof(SkinnedMeshRenderer), "m_Enabled", renderer.enabled ? 1 : 0);
            foreach (var copy in copies)
                Set(restoreClip, copy.transform, root, typeof(GameObject), "m_IsActive", 0);
            var motions = new[]{idleClip, hiddenClip, restoreClip};
            var states = new AnimatorState[3];
            for (int i = 0; i < 3; i++)
            {
                AssetDatabase.CreateAsset(motions[i], folder + "/" + motions[i].name + ".anim");
                states[i] = machine.AddState(motions[i].name);
                states[i].motion = motions[i];
                states[i].writeDefaultValues = false;
                Weight(states[i], layer, i == 0 ? 0 : 1);
            }

            machine.defaultState = states[0];
            var enter = states[0].AddTransition(states[1]);
            enter.duration = 0;
            enter.hasExitTime = false;
            enter.AddCondition(AnimatorConditionMode.If, 0, "ASFR/Enabled");
            enter.AddCondition(AnimatorConditionMode.Equals, 2, Parameter);
            var reset = states[1].AddTransition(states[2]);
            reset.duration = 0;
            reset.hasExitTime = false;
            reset.AddCondition(AnimatorConditionMode.IfNot, 0, "ASFR/Enabled");
            var changed = states[1].AddTransition(states[2]);
            changed.duration = 0;
            changed.hasExitTime = false;
            changed.AddCondition(AnimatorConditionMode.NotEqual, 2, Parameter);
            var release = states[2].AddTransition(states[0]);
            release.duration = 0;
            release.hasExitTime = true;
            release.exitTime = 1;
        }

        public static VRCExpressionsMenu Build(AnimatorController controller, Transform root, SkinnedMeshRenderer[] originals, SkinnedMeshRenderer[] overlays, SkinnedMeshRenderer[] removalCopies, string folder)
        {
            controller.AddParameter(Parameter, AnimatorControllerParameterType.Int);
            controller.AddLayer("ASFR Clothing override");
            int layer = controller.layers.Length - 1;
            var layers = controller.layers;
            layers[layer].defaultWeight = 1;
            controller.layers = layers;
            var machine = controller.layers[layer].stateMachine;
            var neutral = new AnimationClip{name = "Clothing normal"};
            var keep = new AnimationClip{name = "Clothing unchanged"};
            var remove = new AnimationClip{name = "Clothing removed"};
            var complete = new AnimationClip{name = "Clothing removal complete"};
            foreach (var overlay in overlays)
            {
                Set(keep, overlay.transform, root, typeof(GameObject), "m_IsActive", 0);
                Set(remove, overlay.transform, root, typeof(GameObject), "m_IsActive", 0);
                Set(complete, overlay.transform, root, typeof(GameObject), "m_IsActive", 0);
            }

            foreach (var copy in removalCopies)
            {
                Set(remove, copy.transform, root, typeof(GameObject), "m_IsActive", 1);
                Set(complete, copy.transform, root, typeof(GameObject), "m_IsActive", 0);
            }

            BuildSourceVisibility(controller, root, originals, overlays, removalCopies, folder);
            var motions = new[]{neutral, keep, remove, complete};
            var states = new AnimatorState[4];
            for (int i = 0; i < states.Length; i++)
            {
                AssetDatabase.CreateAsset(motions[i], folder + "/" + motions[i].name + ".anim");
                states[i] = machine.AddState(motions[i].name);
                states[i].motion = motions[i];
                states[i].writeDefaultValues = false;
                // Zero layer weight relinquishes visibility to the avatar's existing animations.
                Weight(states[i], layer, i == 0 ? 0 : 1);
            }

            machine.defaultState = states[0];
            var reset = machine.AddAnyStateTransition(states[0]);
            reset.hasExitTime = false;
            reset.duration = 0;
            reset.canTransitionToSelf = false;
            reset.AddCondition(AnimatorConditionMode.IfNot, 0, "ASFR/Enabled");
            for (int i = 0; i < 3; i++)
            {
                var t = machine.AddAnyStateTransition(states[i]);
                t.hasExitTime = false;
                t.duration = 0;
                t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0, "ASFR/Enabled");
                t.AddCondition(AnimatorConditionMode.Equals, i, Parameter);
                if (i == 2)
                    t.AddCondition(AnimatorConditionMode.IfNot, 0, "ASFR/Frozen");
            }

            var completed = machine.AddAnyStateTransition(states[3]);
            completed.hasExitTime = false;
            completed.duration = 0;
            completed.canTransitionToSelf = false;
            completed.AddCondition(AnimatorConditionMode.If, 0, "ASFR/Enabled");
            completed.AddCondition(AnimatorConditionMode.If, 0, "ASFR/Frozen");
            completed.AddCondition(AnimatorConditionMode.Equals, 2, Parameter);
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            string[] names = {"Transform everything", "Keep clothing unchanged", "Remove clothing"};
            for (int i = 0; i < 3; i++)
                menu.controls.Add(new VRCExpressionsMenu.Control{name = names[i], type = VRCExpressionsMenu.Control.ControlType.Toggle, parameter = new VRCExpressionsMenu.Control.Parameter{name = Parameter}, value = i});
            AssetDatabase.CreateAsset(menu, folder + "/Clothing Menu.asset");
            return menu;
        }
    }
}
