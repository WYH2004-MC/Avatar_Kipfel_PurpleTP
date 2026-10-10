using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.modular_avatar.core;

namespace ASFR
{
    static class ASFRProgress
    {
        public const string Value = "ASFR/Progress", Target = "ASFR/Amount", Active = "ASFR/Active", Moving = "ASFR/Moving", Reversing = "ASFR/Reversing";
        public static void AddParameters(AnimatorController c)
        {
            c.AddParameter(Value, AnimatorControllerParameterType.Float);
            c.AddParameter(new AnimatorControllerParameter{name = Target, type = AnimatorControllerParameterType.Float, defaultFloat = 1});
            foreach (var name in new[]{Active, Moving, Reversing})
                c.AddParameter(name, AnimatorControllerParameterType.Bool);
        }

        static AnimatorStateTransition Go(AnimatorState a, AnimatorState b, bool atEnd = false)
        {
            var t = a.AddTransition(b);
            t.duration = 0;
            t.hasExitTime = atEnd;
            t.exitTime = 1;
            return t;
        }

        static void Bool(AnimatorStateTransition t, string p, bool value) => t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, p);
        static void Above(AnimatorStateTransition t, int i) => t.AddCondition(AnimatorConditionMode.Greater, (i + .5f) / 100f, Target);
        static void Below(AnimatorStateTransition t, int i) => t.AddCondition(AnimatorConditionMode.Less, (i - .5f) / 100f, Target);
        public static void Build(AnimatorController c, ModularAvatarParameters parameters, VRCExpressionsMenu menu, string folder)
        {
            // Existing feature layers stay active throughout the reverse dissolve.
            foreach (var layer in c.layers.Where(l => !l.name.Contains("Contact") && !l.name.Contains("Petrify") && !l.name.Contains("Duration")))
                foreach (var state in layer.stateMachine.states)
                    foreach (var t in state.state.transitions.Concat(layer.stateMachine.anyStateTransitions))
                    {
                        var conditions = t.conditions;
                        for (int n = 0; n < conditions.Length; n++)
                            if (conditions[n].parameter == "ASFR/Enabled")
                                conditions[n].parameter = Active;
                        t.conditions = conditions;
                    }

            parameters.parameters.Add(new ParameterConfig{nameOrPrefix = Target, syncType = ParameterSyncType.Float, saved = true, hasExplicitDefaultValue = true, defaultValue = 1});
            menu.controls.Add(new VRCExpressionsMenu.Control{name = "Transformation amount", type = VRCExpressionsMenu.Control.ControlType.RadialPuppet, subParameters = new[]{new VRCExpressionsMenu.Control.Parameter{name = Target}}});
            c.AddLayer("ASFR Progress Timeline");
            var layers = c.layers;
            layers[layers.Length - 1].defaultWeight = 1;
            c.layers = layers;
            var sm = layers[layers.Length - 1].stateMachine;
            AnimatorState Make(string name, int a, int b)
            {
                var clip = new AnimationClip{name = name};
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), Value), AnimationCurve.Linear(0, a / 100f, .6f, b / 100f));
                AssetDatabase.CreateAsset(clip, folder + "/" + name + ".anim");
                var s = sm.AddState(name);
                s.motion = clip;
                s.writeDefaultValues = false;
                s.speedParameter = ASFRDuration.Speed;
                s.speedParameterActive = true;
                var d = s.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                // A partial hold retains the last direction. Contacts can update one
                // frame after the plane stops; they must still be allowed to thaw.
                foreach (var pair in new[]{(Active, a > 0 || b > 0), (Moving, a != b), (Reversing, b < a)})
                    if (pair.Item1 != Reversing || a != b || a == 0 || a == 100)
                        d.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter{name = pair.Item1, value = pair.Item2 ? 1 : 0});
                var owner = s.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                owner.localOnly = true;
                owner.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter{name = "ASFR/Frozen", value = a == 100 && b == 100 ? 1 : 0});
                return s;
            }

            var hold = Enumerable.Range(0, 101).Select(i => Make("Progress Hold " + i, i, i)).ToArray();
            var up = Enumerable.Range(0, 100).Select(i => Make("Progress Up " + i, i, i + 1)).ToArray();
            var down = Enumerable.Range(1, 100).Select(i => Make("Progress Down " + i, i, i - 1)).ToArray();
            sm.defaultState = hold[0];
            for (int i = 0; i <= 100; i++)
            {
                if (i < 100)
                {
                    var t = Go(hold[i], up[i]);
                    Bool(t, "ASFR/Enabled", true);
                    Above(t, i);
                }

                if (i > 0)
                {
                    Bool(Go(hold[i], down[i - 1]), "ASFR/Enabled", false);
                    var t = Go(hold[i], down[i - 1]);
                    Bool(t, "ASFR/Enabled", true);
                    Below(t, i);
                }

                // The zero-second duration jumps directly, including partial targets.
                var instant = sm.AddAnyStateTransition(hold[i]);
                instant.duration = 0;
                instant.hasExitTime = false;
                instant.canTransitionToSelf = false;
                Bool(instant, "ASFR/Enabled", true);
                instant.AddCondition(AnimatorConditionMode.Less, ASFRDuration.InstantThreshold, ASFRDuration.Parameter);
                if (i > 0)
                    instant.AddCondition(AnimatorConditionMode.Greater, (i - .5f) / 100f - .000001f, Target);
                if (i < 100)
                    instant.AddCondition(AnimatorConditionMode.Less, (i + .5f) / 100f, Target);
            }

            var instantReset = sm.AddAnyStateTransition(hold[0]);
            instantReset.duration = 0;
            instantReset.hasExitTime = false;
            instantReset.canTransitionToSelf = false;
            Bool(instantReset, "ASFR/Enabled", false);
            instantReset.AddCondition(AnimatorConditionMode.Less, ASFRDuration.InstantThreshold, ASFRDuration.Parameter);
            for (int i = 0; i < 100; i++)
            {
                if (i < 99)
                {
                    var t = Go(up[i], up[i + 1], true);
                    Bool(t, "ASFR/Enabled", true);
                    Above(t, i + 1);
                }

                Go(up[i], hold[i + 1], true);
                if (i > 0)
                {
                    Bool(Go(down[i], down[i - 1], true), "ASFR/Enabled", false);
                    var t = Go(down[i], down[i - 1], true);
                    Bool(t, "ASFR/Enabled", true);
                    Below(t, i);
                }

                Go(down[i], hold[i], true);
            }
        }
    }
}
