// Authoring helper. Kept outside Assets; run only from the scoped menu-restoration transaction.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EvilFallForKipfel.MenuRestore
{
    public static class OriginalAnimationsRestorer
    {
        const string Destination = "Assets/YaokiSubWork/EvilFallFullSet/ForKipfel";
        const string OldPrefix = "EvilFall/";
        const string NewPrefix = "EvilFallKipfel/";
        const float Tiny = 0.000001f;

        sealed class Context
        {
            public GameObject source, target;
            public Dictionary<Transform, Transform> transforms;
            public Dictionary<Component, Component> components;
            public Dictionary<string, Transform> beforePaths;
            public Dictionary<Transform, RestPose> sourceRest = new Dictionary<Transform, RestPose>();
            public Dictionary<Transform, RestPose> targetRest = new Dictionary<Transform, RestPose>();
            public List<JObject> clipReports = new List<JObject>();
        }
        struct RestPose
        {
            public Vector3 position, scale;
            public Quaternion rotation;
            public Matrix4x4 parentToWorld, worldToParent;
            public static RestPose Capture(Transform transform)
            {
                return new RestPose
                {
                    position = transform.localPosition, rotation = transform.localRotation,
                    scale = transform.localScale,
                    parentToWorld = transform.parent ? transform.parent.localToWorldMatrix : Matrix4x4.identity,
                    worldToParent = transform.parent ? transform.parent.worldToLocalMatrix : Matrix4x4.identity
                };
            }
        }
        sealed class BoundCurve
        {
            public EditorCurveBinding binding;
            public AnimationCurve curve;
            public Transform sourceTransform, targetTransform;
            public Component targetComponent;
        }

        public static AnimatorController Apply(GameObject source, GameObject target,
            Dictionary<Transform, Transform> map, Dictionary<Component, Component> componentMap,
            Dictionary<string, Transform> beforeTargetPaths, JObject report)
        {
            Require(source && target, "Source and target prefab roots are required.");
            Require(map != null && componentMap != null && beforeTargetPaths != null, "Mapping dictionaries are required.");
            var context = new Context { source = source, target = target, transforms = map,
                components = componentMap, beforePaths = beforeTargetPaths };
            foreach (var t in source.GetComponentsInChildren<Transform>(true)) context.sourceRest[t] = RestPose.Capture(t);
            foreach (var t in target.GetComponentsInChildren<Transform>(true)) context.targetRest[t] = RestPose.Capture(t);
            var controllerPath = Destination + "/Accessories.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            Require(controller, "The original controller copy must already be imported at " + controllerPath);
            Require(controller.layers.Length == 26, "Expected the original 26 layers, not the simplified controller.");
            Require(controller.parameters.Length == 32 && controller.parameters.All(p => p.type == AnimatorControllerParameterType.Bool),
                "Expected the original 32 boolean animator parameters.");
            var states = AllStates(controller).ToArray();
            var clips = states.Select(s => s.motion).Where(m => m != null).Distinct().ToArray();
            Require(clips.All(m => m is AnimationClip), "Unexpected non-clip motion; refusing to discard blend trees.");
            EnsureFolder(Destination + "/OriginalAnimations");
            var adapted = new Dictionary<AnimationClip, AnimationClip>();
            foreach (var motion in clips.Cast<AnimationClip>())
                adapted[motion] = RestoreClip(context, motion);
            foreach (var state in states)
            {
                if (state.motion is AnimationClip original) state.motion = adapted[original];
                RenameTransitions(state.transitions);
                RenameBehaviours(state.behaviours);
                EditorUtility.SetDirty(state);
            }
            var layers = controller.layers;
            foreach (var layer in layers)
            {
                layer.name = Rename(layer.name);
                VisitMachines(layer.stateMachine, machine =>
                {
                    RenameTransitions(machine.anyStateTransitions);
                    RenameTransitions(machine.entryTransitions);
                    foreach (var child in machine.stateMachines)
                        RenameTransitions(machine.GetStateMachineTransitions(child.stateMachine));
                    RenameBehaviours(machine.behaviours);
                    EditorUtility.SetDirty(machine);
                });
            }
            controller.layers = layers;
            var parameters = controller.parameters;
            foreach (var p in parameters) p.name = Rename(p.name);
            controller.parameters = parameters;
            Require(controller.parameters.Select(p => p.name).Distinct().Count() == 32, "Parameter names collided.");
            Require(controller.parameters.All(p => p.name.StartsWith(NewPrefix, StringComparison.Ordinal)), "An old parameter namespace remains.");
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            var driverCount = states.Sum(s => s.behaviours.Count(b => b && b.GetType().Name.Contains("ParameterDriver")));
            Require(driverCount == 12, "Expected all 12 original parameter drivers, found " + driverCount);
            report["originalAnimations"] = new JObject
            {
                ["controller"] = controllerPath, ["parameterCount"] = 32, ["allParametersBool"] = true,
                ["layerCount"] = 26, ["stateCount"] = states.Length,
                ["parameterDriverCount"] = driverCount, ["clipCount"] = adapted.Count,
                ["unresolvedBindings"] = 0, ["clips"] = new JArray(context.clipReports),
                ["transformRetarget"] = "Source-rest delta to accepted target rest; quaternion continuity sampled at 60 Hz where needed."
            };
            return controller;
        }

        static AnimationClip RestoreClip(Context context, AnimationClip original)
        {
            var originalPath = AssetDatabase.GetAssetPath(original);
            int weaponState = WeaponState(original.name);
            var payload = weaponState >= 0
                ? AssetDatabase.LoadAssetAtPath<AnimationClip>(Destination + "/Accessory_Weapon_" + weaponState + ".anim")
                : null;
            Require(weaponState < 0 || payload, "The accepted weapon pose clip is missing for state " + weaponState);
            var result = Object.Instantiate(original);
            result.name = original.name;
            foreach (var binding in AnimationUtility.GetCurveBindings(result)) AnimationUtility.SetEditorCurve(result, binding, null);
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(result)) AnimationUtility.SetObjectReferenceCurve(result, binding, null);
            var originals = AnimationUtility.GetCurveBindings(original);
            var transformed = new List<BoundCurve>();
            int sourceMapped = 0, payloadMapped = 0, bodySupplement = 0;
            if (payload)
            {
                foreach (var binding in AnimationUtility.GetCurveBindings(payload))
                {
                    var destination = RemapAcceptedBinding(context, binding);
                    AnimationUtility.SetEditorCurve(result, destination, AnimationUtility.GetEditorCurve(payload, binding));
                    payloadMapped++;
                }
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(payload))
                    AnimationUtility.SetObjectReferenceCurve(result, RemapAcceptedBinding(context, binding),
                        RemapReferenceKeys(context, AnimationUtility.GetObjectReferenceCurve(payload, binding)));
            }
            foreach (var binding in originals)
            {
                var mapped = ResolveSourceBinding(context, binding);
                mapped.curve = AnimationUtility.GetEditorCurve(original, binding);
                Require(mapped.curve != null, "Curve data is missing: " + Describe(binding));
                // The accepted full weapon poses replace only source transform payload. Original visibility and shape semantics remain.
                if (binding.type == typeof(Transform))
                {
                    if (!payload) transformed.Add(mapped);
                }
                else AnimationUtility.SetEditorCurve(result, mapped.binding, mapped.curve);
                sourceMapped++;
            }
            foreach (var group in transformed.GroupBy(c => c.sourceTransform)) RetargetTransformCurves(context, result, group.ToArray());
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(original))
                AnimationUtility.SetObjectReferenceCurve(result, ResolveSourceBinding(context, binding).binding,
                    RemapReferenceKeys(context, AnimationUtility.GetObjectReferenceCurve(original, binding)));
            var supplement = ClothingSupplement(original.name);
            if (supplement != null)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Destination + "/" + supplement + ".anim");
                Require(clip, "The accepted clothing supplement is missing: " + supplement);
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (!binding.path.Equals("Body_Base", StringComparison.Ordinal) &&
                        !binding.path.EndsWith("/Body_Base", StringComparison.Ordinal)) continue;
                    // These avatar-external curves are deliberately not resolved against the outfit-only prefab.
                    Require(binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal),
                        "Unexpected Body_Base supplement binding: " + Describe(binding));
                    AnimationUtility.SetEditorCurve(result, binding, AnimationUtility.GetEditorCurve(clip, binding));
                    bodySupplement++;
                }
            }
            result.EnsureQuaternionContinuity();
            var path = Destination + "/OriginalAnimations/" + SafeFileName(original.name) + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing)
            {
                EditorUtility.CopySerialized(result, existing);
                Object.DestroyImmediate(result);
                result = existing;
                EditorUtility.SetDirty(result);
            }
            else AssetDatabase.CreateAsset(result, path);
            AssetDatabase.SaveAssetIfDirty(result);
            Require(sourceMapped == originals.Length, "Source binding count changed unexpectedly.");
            var bindings = AnimationUtility.GetCurveBindings(result);
            foreach (var binding in bindings)
            {
                if (binding.path == "Body_Base" || binding.path.EndsWith("/Body_Base", StringComparison.Ordinal)) continue;
                ValidateDestination(context, binding);
            }
            context.clipReports.Add(new JObject
            {
                ["source"] = originalPath, ["destination"] = path,
                ["sourceBindingCount"] = originals.Length, ["sourceBindingsResolved"] = sourceMapped,
                ["adaptedBindingCount"] = bindings.Length, ["acceptedWeaponPayloadBindings"] = payloadMapped,
                ["bodyBaseSupplementBindings"] = bodySupplement,
                ["sourceObjectReferenceBindings"] = AnimationUtility.GetObjectReferenceCurveBindings(original).Length,
                ["unresolvedBindings"] = 0
            });
            return result;
        }

        static BoundCurve ResolveSourceBinding(Context context, EditorCurveBinding binding)
        {
            var sourceTransform = FindPath(context.source.transform, binding.path);
            Require(sourceTransform, "Source animation path does not exist: " + Describe(binding));
            Transform destination;
            Component destinationComponent = null;
            if (binding.type == typeof(Transform) || binding.type == typeof(GameObject))
            {
                Require(context.transforms.TryGetValue(sourceTransform, out destination) && destination,
                    "No target transform mapping: " + Describe(binding));
            }
            else
            {
                var originalComponents = sourceTransform.GetComponents(binding.type);
                Require(originalComponents.Length == 1, "Source animation component is missing or ambiguous: " + Describe(binding));
                Require(context.components.TryGetValue(originalComponents[0], out destinationComponent) && destinationComponent,
                    "No exact target component mapping: " + Describe(binding));
                Require(binding.type.IsAssignableFrom(destinationComponent.GetType()), "Mapped component type differs: " + Describe(binding));
                destination = destinationComponent.transform;
            }
            var remapped = binding;
            remapped.path = TargetPath(context, destination);
            ValidateShape(destinationComponent, remapped);
            return new BoundCurve { binding = remapped, sourceTransform = sourceTransform,
                targetTransform = destination, targetComponent = destinationComponent };
        }

        static EditorCurveBinding RemapAcceptedBinding(Context context, EditorCurveBinding binding)
        {
            Transform destination;
            Require(context.beforePaths.TryGetValue(binding.path, out destination) && destination,
                "Accepted clip's pre-migration path does not exist: " + Describe(binding));
            Component component = null;
            if (binding.type != typeof(GameObject) && binding.type != typeof(Transform))
            {
                // A previous accepted clip may address Fit while the real original PhysBone lives on the source rig.
                // Prefer the original component authority to avoid choosing a same-named fit transform with no component.
                if (binding.type.Name.Contains("PhysBone"))
                {
                    var candidates = context.components.Where(pair => pair.Key && pair.Value &&
                        binding.type.IsAssignableFrom(pair.Key.GetType()) && pair.Key.transform.name == destination.name).ToArray();
                    Require(candidates.Length == 1, "Accepted PhysBone binding has no unique original component mapping: " + Describe(binding));
                    component = candidates[0].Value;
                    destination = component.transform;
                }
                else
                {
                    var candidates = destination.GetComponents(binding.type);
                    Require(candidates.Length == 1, "Accepted binding's target component is missing or ambiguous: " + Describe(binding));
                    component = candidates[0];
                }
            }
            var remapped = binding;
            remapped.path = TargetPath(context, destination);
            ValidateShape(component, remapped);
            return remapped;
        }

        static void ValidateDestination(Context context, EditorCurveBinding binding)
        {
            var destination = FindPath(context.target.transform, binding.path);
            Require(destination, "Adapted animation path does not exist: " + Describe(binding));
            if (binding.type == typeof(GameObject) || binding.type == typeof(Transform)) return;
            var components = destination.GetComponents(binding.type);
            Require(components.Length == 1, "Adapted animation component is missing or ambiguous: " + Describe(binding));
            ValidateShape(components[0], binding);
        }
        static void ValidateShape(Component component, EditorCurveBinding binding)
        {
            if (!binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal)) return;
            var renderer = component as SkinnedMeshRenderer;
            Require(renderer && renderer.sharedMesh, "Blend shape binding has no target skinned mesh: " + Describe(binding));
            Require(renderer.sharedMesh.GetBlendShapeIndex(binding.propertyName.Substring("blendShape.".Length)) >= 0,
                "Target blend shape is missing: " + Describe(binding));
        }

        static void RetargetTransformCurves(Context context, AnimationClip result, BoundCurve[] curves)
        {
            var source = curves[0].sourceTransform;
            var destination = curves[0].targetTransform;
            Require(curves.All(c => c.targetTransform == destination), "One source transform maps to multiple target transforms.");
            Require(context.sourceRest.ContainsKey(source) && context.targetRest.ContainsKey(destination), "Rest pose snapshot is missing.");
            var sourceRest = context.sourceRest[source];
            var targetRest = context.targetRest[destination];
            var position = new BoundCurve[3]; var scale = new BoundCurve[3];
            var euler = new BoundCurve[3]; var quaternion = new BoundCurve[4];
            foreach (var curve in curves)
            {
                var property = curve.binding.propertyName;
                int axis = Axis(property);
                Require(axis >= 0, "Unrecognized transform animation property: " + property);
                if (property.StartsWith("m_LocalPosition.", StringComparison.Ordinal)) position[axis] = curve;
                else if (property.StartsWith("m_LocalScale.", StringComparison.Ordinal)) scale[axis] = curve;
                else if (property.StartsWith("m_LocalRotation.", StringComparison.Ordinal)) quaternion[axis] = curve;
                else if (property.StartsWith("localEulerAngles", StringComparison.Ordinal) || property.StartsWith("m_LocalEulerAngles.", StringComparison.Ordinal))
                { Require(axis < 3, "Invalid Euler axis."); euler[axis] = curve; }
                else throw new InvalidOperationException("Unsupported transform property: " + property);
            }
            if (position.Any(c => c != null))
            {
                var matrix = targetRest.worldToParent * sourceRest.parentToWorld;
                var times = KeyTimes(position);
                for (int axis = 0; axis < 3; axis++)
                {
                    var output = new AnimationCurve();
                    for (int k = 0; k < times.Count; k++)
                    {
                        var time = times[k];
                        var sourcePosition = EvaluateVector(position, time, sourceRest.position);
                        var value = targetRest.position + matrix.MultiplyVector(sourcePosition - sourceRest.position);
                        var inTangent = TransformDerivative(matrix, position, time, false, axis);
                        var outTangent = TransformDerivative(matrix, position, time, true, axis);
                        output.AddKey(new Keyframe(time, value[axis], inTangent, outTangent));
                    }
                    CopyWrap(position.First(c => c != null).curve, output);
                    SetTransformCurve(context, result, destination, "m_LocalPosition." + "xyz"[axis], output);
                }
            }
            for (int axis = 0; axis < 3; axis++)
            {
                var original = scale[axis]; if (original == null) continue;
                float denominator = sourceRest.scale[axis];
                Require(Mathf.Abs(denominator) > Tiny, "Cannot retarget a zero-scale source rest axis at " + source.name);
                var multiplier = targetRest.scale[axis] / denominator;
                var output = new AnimationCurve(original.curve.keys.Select(key =>
                {
                    key.value *= multiplier; key.inTangent *= multiplier; key.outTangent *= multiplier; return key;
                }).ToArray());
                CopyWrap(original.curve, output);
                SetTransformCurve(context, result, destination, "m_LocalScale." + "xyz"[axis], output);
            }
            bool hasEuler = euler.Any(c => c != null), hasQuaternion = quaternion.Any(c => c != null);
            Require(!(hasEuler && hasQuaternion), "A transform has both Euler and quaternion source curves: " + source.name);
            if (hasEuler || hasQuaternion)
            {
                var input = hasEuler ? euler : quaternion;
                var times = KeyTimes(input);
                float first = times.First(), last = times.Last();
                bool varying = input.Any(c => c != null && c.curve.keys.Select(k => k.value).Distinct().Count() > 1);
                if (varying && last > first)
                {
                    for (int frame = Mathf.CeilToInt(first * 60f); frame <= Mathf.FloorToInt(last * 60f); frame++) times.Add(frame / 60f);
                    times = times.Distinct().OrderBy(t => t).ToList();
                }
                var values = new List<Quaternion>();
                foreach (float time in times)
                {
                    Quaternion animated;
                    if (hasEuler) animated = Quaternion.Euler(EvaluateVector(euler, time, sourceRest.rotation.eulerAngles));
                    else animated = Normalize(new Quaternion(Evaluate(quaternion[0], time, sourceRest.rotation.x),
                        Evaluate(quaternion[1], time, sourceRest.rotation.y), Evaluate(quaternion[2], time, sourceRest.rotation.z),
                        Evaluate(quaternion[3], time, sourceRest.rotation.w)));
                    var value = Normalize(targetRest.rotation * Quaternion.Inverse(sourceRest.rotation) * animated);
                    if (Quaternion.Angle(animated, sourceRest.rotation) < 0.0001f) value = targetRest.rotation;
                    if (values.Count > 0 && Quaternion.Dot(values[values.Count - 1], value) < 0) value = Negate(value);
                    values.Add(value);
                }
                for (int axis = 0; axis < 4; axis++)
                {
                    var output = new AnimationCurve();
                    for (int k = 0; k < times.Count; k++)
                    {
                        float value = values[k][axis];
                        float incoming = k == 0 ? 0 : (value - values[k - 1][axis]) / (times[k] - times[k - 1]);
                        float outgoing = k == times.Count - 1 ? 0 : (values[k + 1][axis] - value) / (times[k + 1] - times[k]);
                        output.AddKey(new Keyframe(times[k], value, incoming, outgoing));
                    }
                    CopyWrap(input.First(c => c != null).curve, output);
                    SetTransformCurve(context, result, destination, "m_LocalRotation." + "xyzw"[axis], output);
                }
            }
        }

        static float TransformDerivative(Matrix4x4 matrix, BoundCurve[] input, float time, bool outgoing, int destinationAxis)
        {
            float result = 0;
            for (int axis = 0; axis < 3; axis++)
            {
                float weight = matrix[destinationAxis, axis];
                if (Mathf.Abs(weight) < 0.0000001f || input[axis] == null) continue;
                float derivative = Derivative(input[axis].curve, time, outgoing);
                // Unity encodes stepped keys with infinite tangents. Preserve the step instead of producing NaN.
                if (float.IsInfinity(derivative)) return float.PositiveInfinity;
                result += weight * derivative;
            }
            return result;
        }
        static float Derivative(AnimationCurve curve, float time, bool outgoing)
        {
            var keys = curve.keys;
            if (keys.Length < 2) return 0;
            for (int k = 0; k < keys.Length; k++)
                if (Mathf.Abs(keys[k].time - time) < 0.0000001f) return outgoing ? keys[k].outTangent : keys[k].inTangent;
            if (time < keys[0].time || time > keys[keys.Length - 1].time) return 0;
            for (int k = 0; k < keys.Length - 1; k++)
            {
                var a = keys[k]; var b = keys[k + 1];
                if (!(time > a.time && time < b.time)) continue;
                if (float.IsInfinity(a.outTangent) || float.IsInfinity(b.inTangent)) return float.PositiveInfinity;
                float duration = b.time - a.time, u = (time - a.time) / duration;
                if (a.weightedMode != WeightedMode.None || b.weightedMode != WeightedMode.None)
                {
                    float epsilon = Mathf.Min(duration * 0.0001f, 0.00001f);
                    return (curve.Evaluate(time + epsilon) - curve.Evaluate(time - epsilon)) / (2 * epsilon);
                }
                return ((6 * u * u - 6 * u) * a.value + (3 * u * u - 4 * u + 1) * duration * a.outTangent +
                    (-6 * u * u + 6 * u) * b.value + (3 * u * u - 2 * u) * duration * b.inTangent) / duration;
            }
            return 0;
        }
        static List<float> KeyTimes(IEnumerable<BoundCurve> input)
        {
            var result = input.Where(c => c != null).SelectMany(c => c.curve.keys.Select(k => k.time)).Distinct().OrderBy(t => t).ToList();
            Require(result.Count > 0, "A transform group contains no keyframes.");
            return result;
        }
        static Vector3 EvaluateVector(BoundCurve[] input, float time, Vector3 fallback)
        {
            return new Vector3(Evaluate(input[0], time, fallback.x), Evaluate(input[1], time, fallback.y), Evaluate(input[2], time, fallback.z));
        }
        static float Evaluate(BoundCurve input, float time, float fallback) { return input == null ? fallback : input.curve.Evaluate(time); }
        static void SetTransformCurve(Context context, AnimationClip result, Transform target, string property, AnimationCurve curve)
        {
            AnimationUtility.SetEditorCurve(result, EditorCurveBinding.FloatCurve(TargetPath(context, target), typeof(Transform), property), curve);
        }
        static void CopyWrap(AnimationCurve source, AnimationCurve destination) { destination.preWrapMode = source.preWrapMode; destination.postWrapMode = source.postWrapMode; }
        static Quaternion Normalize(Quaternion value)
        {
            float size = Mathf.Sqrt(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
            Require(size > Tiny, "Animation produced an invalid quaternion.");
            return new Quaternion(value.x / size, value.y / size, value.z / size, value.w / size);
        }
        static Quaternion Negate(Quaternion value) { return new Quaternion(-value.x, -value.y, -value.z, -value.w); }
        static int Axis(string property)
        {
            if (property.Length < 2 || property[property.Length - 2] != '.') return -1;
            return "xyzw".IndexOf(property[property.Length - 1]);
        }

        static ObjectReferenceKeyframe[] RemapReferenceKeys(Context context, ObjectReferenceKeyframe[] keys)
        {
            var result = keys.ToArray();
            for (int k = 0; k < result.Length; k++)
            {
                var key = result[k]; var component = key.value as Component; var gameObject = key.value as GameObject;
                if (component && component.transform.IsChildOf(context.source.transform))
                {
                    Component mapped;
                    if (component is Transform)
                    {
                        Transform target;
                        Require(context.transforms.TryGetValue(component.transform, out target) && target, "Object-reference transform mapping is missing.");
                        key.value = target;
                    }
                    else
                    {
                        Require(context.components.TryGetValue(component, out mapped) && mapped, "Object-reference component mapping is missing.");
                        key.value = mapped;
                    }
                }
                else if (gameObject && gameObject.transform.IsChildOf(context.source.transform))
                {
                    Transform target;
                    Require(context.transforms.TryGetValue(gameObject.transform, out target) && target, "Object-reference GameObject mapping is missing.");
                    key.value = target.gameObject;
                }
                result[k] = key;
            }
            return result;
        }
        static void RenameTransitions(IEnumerable<AnimatorTransitionBase> transitions)
        {
            foreach (var transition in transitions)
            {
                var conditions = transition.conditions;
                for (int k = 0; k < conditions.Length; k++) conditions[k].parameter = Rename(conditions[k].parameter);
                transition.conditions = conditions;
                EditorUtility.SetDirty(transition);
            }
        }
        static void RenameBehaviours(IEnumerable<StateMachineBehaviour> behaviours)
        {
            foreach (var behaviour in behaviours)
            {
                Require(behaviour, "A source state-machine behaviour is missing.");
                var serialized = new SerializedObject(behaviour);
                var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.String && property.stringValue.StartsWith(OldPrefix, StringComparison.Ordinal))
                        property.stringValue = Rename(property.stringValue);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(behaviour);
            }
        }
        static IEnumerable<AnimatorState> AllStates(AnimatorController controller)
        {
            var result = new List<AnimatorState>();
            foreach (var layer in controller.layers)
                VisitMachines(layer.stateMachine, machine => result.AddRange(machine.states.Select(s => s.state)));
            return result;
        }
        static void VisitMachines(AnimatorStateMachine machine, Action<AnimatorStateMachine> action)
        {
            Require(machine, "A source animator state machine is missing.");
            action(machine);
            foreach (var child in machine.stateMachines) VisitMachines(child.stateMachine, action);
        }
        static string Rename(string value)
        { return value != null && value.StartsWith(OldPrefix, StringComparison.Ordinal) ? NewPrefix + value.Substring(OldPrefix.Length) : value; }
        static Transform FindPath(Transform root, string path) { return string.IsNullOrEmpty(path) ? root : root.Find(path); }
        static string TargetPath(Context context, Transform target)
        {
            Require(target == context.target.transform || target.IsChildOf(context.target.transform), "Mapped target is outside the prefab.");
            return AnimationUtility.CalculateTransformPath(target, context.target.transform);
        }
        static string Describe(EditorCurveBinding binding) { return binding.path + " :: " + binding.type.Name + "." + binding.propertyName; }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        static string SafeFileName(string value)
        { foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_'); return value; }
        static int WeaponState(string name)
        {
            if (name.StartsWith("EvilFall_ 36 ", StringComparison.Ordinal)) return 0;
            if (name.StartsWith("EvilFall_ 37 ", StringComparison.Ordinal)) return 1;
            if (name.StartsWith("EvilFall_ 38 ", StringComparison.Ordinal)) return 2;
            if (name.StartsWith("EvilFall_ 39 ", StringComparison.Ordinal)) return 3;
            return -1;
        }
        static string ClothingSupplement(string name)
        {
            var mapping = new Dictionary<int, string> { { 5, "Jacket_Off" }, { 6, "Jacket_On" }, { 7, "JacketACC_Off" },
                { 8, "JacketACC_On" }, { 9, "Bodysuit_Off" }, { 10, "Bodysuit_On" }, { 11, "Glove_Off" },
                { 12, "Glove_On" }, { 13, "Shoes_Off" }, { 14, "Shoes_On" }, { 15, "BodyTube_Off" }, { 16, "BodyTube_On" } };
            foreach (var pair in mapping) if (name.StartsWith("EvilFall_ " + pair.Key + " ", StringComparison.Ordinal)) return pair.Value;
            return null;
        }
    }
}
