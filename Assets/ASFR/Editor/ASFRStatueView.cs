using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using nadena.dev.modular_avatar.core;

namespace ASFR
{
    static class ASFRStatueView
    {
        const string Grab = "ASFR/StatueHandle_IsGrabbed", TurnGrab = "ASFR/StatueTurn_IsGrabbed", CameraEnabled = "ASFR/StatueCamera", AllowGrab = "ASFR/StatueGrabbable", HideBall = "ASFR/HideGrabBall";
        static Transform Child(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static VRCParentConstraint Follow(Transform target, Transform source)
        {
            var c = target.gameObject.AddComponent<VRCParentConstraint>();
            c.Sources.Add(new VRCConstraintSource(source, 1));
            c.ZeroConstraint();
            c.ApplyConfigurationChanges();
            return c;
        }

        public static void Build(AnimatorController controller, ASFRRigBinding marker, Transform root, ModularAvatarParameters parameters, VRCExpressionsMenu menu, string folder, float eyeSeparation = .064f)
        {
            var animator = root.GetComponent<Animator>();
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (!hips || !head)
                throw new InvalidOperationException("Statue camera requires humanoid hips and head.");
            var shader = Shader.Find("ASFR/Statue Camera View");
            if (!shader)
                throw new InvalidOperationException("Import StatueCameraView.shader before generating.");
            var anchor = Child("Statue Grab Anchor", marker.transform);
            marker.statueGrabAnchor = anchor;
            float scale = Mathf.Max(.0001f, Mathf.Abs(root.lossyScale.x));
            Vector3 hip = root.InverseTransformPoint(hips.position);
            float length = 1f / scale;
            var chain = Child("Statue Handle Chain", anchor);
            chain.localPosition = hip - Vector3.up * length;
            var tip = Child("Grab Here", chain);
            tip.localPosition = Vector3.up * length;
            var pb = chain.gameObject.AddComponent<VRCPhysBone>();
            pb.rootTransform = chain;
            pb.pull = 1;
            pb.spring = 0;
            pb.stiffness = 0;
            pb.gravity = 0;
            pb.immobile = 1;
            pb.radius = .12f / scale;
            pb.grabMovement = 1;
            pb.maxStretch = 2;
            pb.maxSquish = 1;
            pb.snapToHand = false;
            pb.parameter = "ASFR/StatueHandle";
            pb.resetWhenDisabled = true;
            var serial = new SerializedObject(pb);
            SetEnum(serial, "allowGrabbing", "True");
            SetEnum(serial, "allowPosing", "True");
            SetEnum(serial, "allowCollision", "False");
            serial.ApplyModifiedPropertiesWithoutUndo();
            // The simulated segment aims back toward its fixed root. Its rotation is
            // NOT the grabber's hand rotation and must never drive the statue.
            // Inherit orientation from the world-locked anchor; copy only tip position.
            var rigid = Child("Rigid Handle Pose", anchor);
            var translation = rigid.gameObject.AddComponent<VRCPositionConstraint>();
            translation.Sources.Add(new VRCConstraintSource(tip, 1));
            translation.SolveInLocalSpace = false;
            translation.ZeroConstraint();
            translation.FreezeToWorld = false;
            translation.ApplyConfigurationChanges();
            // Independent direction lever: its pivot translates with the move handle,
            // and is never parented to the rotation it drives (no feedback loop).
            var turn = Child("Statue Turn Chain", rigid);
            var turnTip = Child("Turn Here", turn);
            turnTip.localPosition = Vector3.forward * (.45f / scale);
            var turnEnd = Child("Turn Endpoint", turnTip);
            turnEnd.localPosition = Vector3.forward * (.04f / scale);
            var turnPhysics = turn.gameObject.AddComponent<VRCPhysBone>();
            turnPhysics.rootTransform = turn;
            turnPhysics.pull = 1;
            turnPhysics.spring = 0;
            turnPhysics.stiffness = 0;
            turnPhysics.gravity = 0;
            turnPhysics.immobile = 1;
            turnPhysics.radius = .08f / scale;
            turnPhysics.radiusCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.8f, 1), new Keyframe(1, 1));
            turnPhysics.grabMovement = 1;
            turnPhysics.maxStretch = 0;
            turnPhysics.maxSquish = 0;
            turnPhysics.snapToHand = false;
            turnPhysics.parameter = "ASFR/StatueTurn";
            turnPhysics.resetWhenDisabled = true;
            var turnSerial = new SerializedObject(turnPhysics);
            SetEnum(turnSerial, "allowGrabbing", "True");
            SetEnum(turnSerial, "allowPosing", "True");
            SetEnum(turnSerial, "allowCollision", "False");
            turnSerial.ApplyModifiedPropertiesWithoutUndo();
            var rotated = Child("Statue Rotation Pivot", rigid);
            var rotation = rotated.gameObject.AddComponent<VRCRotationConstraint>();
            rotation.Sources.Add(new VRCConstraintSource(turn, 1));
            rotation.ZeroConstraint();
            rotation.ApplyConfigurationChanges();
            var origin = Child("Carried Avatar Origin", rotated);
            origin.localPosition = -hip;
            marker.statueOrigin = origin;
            // A visible target on the rigid follower does not become part of the simulated chain.
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Statue Grab Handle";
            ball.transform.SetParent(rigid, false);
            ball.transform.localScale = Vector3.one * (.12f / scale);
            UnityEngine.Object.DestroyImmediate(ball.GetComponent<Collider>());
            var handleMat = new Material(Shader.Find("Unlit/Color"))
            {name = "Statue Handle"};
            handleMat.color = new Color(.1f, .8f, 1);
            AssetDatabase.CreateAsset(handleMat, folder + "/Statue Handle.mat");
            ball.GetComponent<Renderer>().sharedMaterial = handleMat;
            ball.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var turnBall = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            turnBall.name = "Statue Turn Handle";
            turnBall.transform.SetParent(rigid, false);
            turnBall.transform.localScale = Vector3.one * (.10f / scale);
            UnityEngine.Object.DestroyImmediate(turnBall.GetComponent<Collider>());
            Follow(turnBall.transform, turnTip);
            var turnMaterial = new Material(handleMat)
            {name = "Statue Turn Handle"};
            turnMaterial.color = new Color(1, .45f, .08f);
            AssetDatabase.CreateAsset(turnMaterial, folder + "/Statue Turn Handle.mat");
            turnBall.GetComponent<Renderer>().sharedMaterial = turnMaterial;
            turnBall.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            chain.gameObject.SetActive(false);
            turn.gameObject.SetActive(false);
            ball.SetActive(false);
            turnBall.SetActive(false);
            // Camera mount stays outside HeadChop's scaled subtree. Build pass retargets its constraint.
            var mount = Child("Statue Camera Mount", marker.transform);
            Follow(mount, head);
            marker.statueCameraMount = mount;
            var cameraObject = Child("Statue Eye Camera", mount);
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            cameraObject.localPosition = Quaternion.Inverse(head.rotation) * (root.TransformPoint(descriptor.ViewPosition) - head.position) / scale;
            // Calibrate to avatar view-forward even on rigs with rotated head bone axes.
            cameraObject.localRotation = Quaternion.Inverse(head.rotation) * root.rotation;
            const float fieldOfView = 140;
            var left = EyeCamera(cameraObject, folder, "Left", -eyeSeparation / (2 * scale), fieldOfView);
            var right = EyeCamera(cameraObject, folder, "Right", eyeSeparation / (2 * scale), fieldOfView);
            cameraObject.gameObject.SetActive(false);
            var screen = Child("Statue Camera Screen", marker.transform);
            screen.gameObject.layer = 10;
            Follow(screen, head);
            var mesh = ASFRScreen.CreateQuad("Statue View Quad", folder + "/Statue View Quad.asset");
            screen.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(shader)
            {name = "Statue Camera Screen"};
            material.mainTexture = left;
            material.SetTexture("_RightTex", right);
            material.SetFloat("_StereoEnabled", 1);
            material.SetFloat("_TanHalfFov", Mathf.Tan(fieldOfView * Mathf.Deg2Rad * .5f));
            AssetDatabase.CreateAsset(material, folder + "/Statue View.mat");
            material.renderQueue = 5000;
            ASFRScreen.AddRenderer(screen.gameObject, material);
            screen.GetComponent<MeshRenderer>().sortingOrder = 32767;
            screen.gameObject.SetActive(false);
            controller.AddParameter(Grab, AnimatorControllerParameterType.Bool);
            controller.AddParameter(TurnGrab, AnimatorControllerParameterType.Bool);
            Param(controller, parameters, AllowGrab, false);
            Param(controller, parameters, CameraEnabled, true);
            Param(controller, parameters, HideBall, false, false);
            foreach (var pair in new[]{new[]{"Allow Statue Grab", AllowGrab}, new[]{"Statue Camera View", CameraEnabled}, new[]{"Hide Grab Ball", HideBall}})
                menu.controls.Add(new VRCExpressionsMenu.Control{name = pair[0], type = VRCExpressionsMenu.Control.ControlType.Toggle, parameter = new VRCExpressionsMenu.Control.Parameter{name = pair[1]}, value = 1});
            var sm = Layer(controller, "ASFR Statue Handle");
            var off = State(sm, "Unavailable", ActiveClip(root, folder, "Statue Handle Off", false, chain.gameObject, ball, turn.gameObject, turnBall));
            var on = State(sm, "Grabbable", ActiveClip(root, folder, "Statue Handle On", true, chain.gameObject, ball, turn.gameObject, turnBall));
            sm.defaultState = off;
            var enter = Transition(off, on);
            foreach (string p in new[]{"ASFR/Enabled", "ASFR/Frozen", AllowGrab})
                enter.AddCondition(AnimatorConditionMode.If, 0, p);
            foreach (string p in new[]{"ASFR/Enabled", "ASFR/Frozen", AllowGrab})
                Transition(on, off).AddCondition(AnimatorConditionMode.IfNot, 0, p);
            // Visibility only: never disable the PhysBone or its grab target here.
            var visibility = Layer(controller, "ASFR Grab Ball Visibility");
            AnimationClip BallClip(string name, bool visible)
            {
                var clip = new AnimationClip{name = name};
                foreach (var handle in new[]{ball, turnBall})
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(handle.transform, root), typeof(MeshRenderer), "m_Enabled"), AnimationCurve.Constant(0, 1f / 30, visible ? 1 : 0));
                AssetDatabase.CreateAsset(clip, folder + "/" + name + ".anim");
                return clip;
            }

            var shown = State(visibility, "Visible", BallClip("Grab Ball Visible", true));
            var invisible = State(visibility, "Hidden", BallClip("Grab Ball Hidden", false));
            visibility.defaultState = shown;
            Transition(shown, invisible).AddCondition(AnimatorConditionMode.If, 0, HideBall);
            Transition(invisible, shown).AddCondition(AnimatorConditionMode.IfNot, 0, HideBall);
            // Latch first grab until Reset, even after releasing or posing the handle.
            var latch = Layer(controller, "ASFR Statue Camera Latch");
            var idle = State(latch, "Waiting for grab", ActiveClip(root, folder, "Statue View Waiting", false, cameraObject.gameObject, screen.gameObject));
            latch.defaultState = idle;
            var view = State(latch, "View from statue", ActiveClip(root, folder, "Statue View Active", true, cameraObject.gameObject, screen.gameObject));
            var hidden = State(latch, "View manually hidden", ActiveClip(root, folder, "Statue View Hidden", false, cameraObject.gameObject, screen.gameObject));
            foreach (var grabParameter in new[]{Grab, TurnGrab})
            {
                enter = Transition(idle, view);
                foreach (string parameter in new[]{"IsLocal", "ASFR/Enabled", "ASFR/Frozen", grabParameter, CameraEnabled})
                    enter.AddCondition(AnimatorConditionMode.If, 0, parameter);
                enter = Transition(idle, hidden);
                foreach (string parameter in new[]{"IsLocal", "ASFR/Enabled", "ASFR/Frozen", grabParameter})
                    enter.AddCondition(AnimatorConditionMode.If, 0, parameter);
                enter.AddCondition(AnimatorConditionMode.IfNot, 0, CameraEnabled);
            }

            Transition(view, hidden).AddCondition(AnimatorConditionMode.IfNot, 0, CameraEnabled);
            enter = Transition(hidden, view);
            enter.AddCondition(AnimatorConditionMode.If, 0, CameraEnabled);
            enter.AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
            enter.AddCondition(AnimatorConditionMode.If, 0, "ASFR/Enabled");
            enter.AddCondition(AnimatorConditionMode.If, 0, "ASFR/Frozen");
            foreach (var state in new[]{view, hidden})
                foreach (string p in new[]{"ASFR/Enabled", "ASFR/Frozen", "IsLocal"})
                    Transition(state, idle).AddCondition(AnimatorConditionMode.IfNot, 0, p);
        }

        static RenderTexture EyeCamera(Transform parent, string folder, string eye, float offset, float fov)
        {
            var cameraObject = Child("Statue " + eye + " Eye", parent);
            cameraObject.localPosition = Vector3.right * offset;
            var camera = cameraObject.gameObject.AddComponent<Camera>();
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.fieldOfView = fov;
            camera.aspect = 1;
            camera.nearClipPlane = .03f;
            camera.farClipPlane = 500;
            camera.depth = -10;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;
            camera.cullingMask = ~((1 << 5) | (1 << 10) | (1 << 12) | (1 << 18) | (1 << 19) | (1 << 20));
            var texture = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32)
            {name = "Statue " + eye + " Eye Feed", antiAliasing = 1, useMipMap = false};
            AssetDatabase.CreateAsset(texture, folder + "/Statue " + eye + " Eye Feed.renderTexture");
            camera.targetTexture = texture;
            return texture;
        }

        static void SetEnum(SerializedObject obj, string name, string value)
        {
            var prop = obj.FindProperty(name);
            if (prop == null)
                throw new InvalidOperationException("PhysBone SDK field missing: " + name);
            int index = Array.FindIndex(prop.enumNames, n => string.Equals(n, value, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                throw new InvalidOperationException("PhysBone SDK value missing: " + name + "/" + value);
            prop.enumValueIndex = index;
        }

        static void Param(AnimatorController c, ModularAvatarParameters p, string name, bool local, bool defaultValue = true)
        {
            c.AddParameter(new AnimatorControllerParameter{name = name, type = AnimatorControllerParameterType.Bool, defaultBool = defaultValue});
            p.parameters.Add(new ParameterConfig{nameOrPrefix = name, syncType = ParameterSyncType.Bool, localOnly = local, saved = false, hasExplicitDefaultValue = true, defaultValue = defaultValue ? 1 : 0});
        }

        static AnimatorStateMachine Layer(AnimatorController c, string name)
        {
            c.AddLayer(name);
            var l = c.layers;
            l[l.Length - 1].defaultWeight = 1;
            c.layers = l;
            return l[l.Length - 1].stateMachine;
        }

        static AnimatorState State(AnimatorStateMachine sm, string name, AnimationClip clip)
        {
            var s = sm.AddState(name);
            s.motion = clip;
            s.writeDefaultValues = false;
            return s;
        }

        static AnimatorStateTransition Transition(AnimatorState a, AnimatorState b)
        {
            var t = a.AddTransition(b);
            t.duration = 0;
            t.hasExitTime = false;
            return t;
        }

        static AnimationClip ActiveClip(Transform root, string folder, string name, bool active, params GameObject[] targets)
        {
            var clip = new AnimationClip{name = name};
            foreach (var t in targets)
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(t.transform, root), typeof(GameObject), "m_IsActive"), AnimationCurve.Constant(0, 1f / 30, active ? 1 : 0));
            AssetDatabase.CreateAsset(clip, folder + "/" + name + ".anim");
            return clip;
        }
    }
}
