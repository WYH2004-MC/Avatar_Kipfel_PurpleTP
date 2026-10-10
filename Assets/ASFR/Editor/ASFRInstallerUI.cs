using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace ASFR
{
    public partial class ASFRInstaller
    {
        [SerializeField]
        string meshSearch = "";
        [SerializeField]
        bool showMeshes = true, showFeatures = true, showAdvanced;
        [SerializeField]
        int activeTab;
        Vector2 windowScroll;
        string validationSummary;
        bool validationFailed;
        void OnEnable()
        {
            minSize = new Vector2(790, 500);
            renderers.Clear();
            if (avatar)
                renderers.AddRange(avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh));
            selected.RemoveAll(r => !r || !renderers.Contains(r));
            clothing.RemoveAll(r => !r || !renderers.Contains(r));
            innerCoating.RemoveAll(r => !r || !renderers.Contains(r));
            if (mainBody && !renderers.Contains(mainBody))
                mainBody = null;
        }

        GUIStyle cardStyle, titleStyle, captionStyle, sectionStyle, tabStyle;
        Font displayFont, textFont;
        bool styledDark;
        Color Accent => EditorGUIUtility.isProSkin ? new Color(.95f, .25f, .32f) : new Color(.68f, .08f, .16f);
        void PrepareStyles()
        {
            if (cardStyle != null && styledDark == EditorGUIUtility.isProSkin)
                return;
            styledDark = EditorGUIUtility.isProSkin;
            if (!displayFont)
                displayFont = Font.CreateDynamicFontFromOSFont(ASFRLanguage.Japanese ? new[]{"Yu Gothic", "Meiryo", "Noto Sans CJK JP"} : new[]{"Bahnschrift", "Segoe UI", "Arial"}, 42);
            if (!textFont)
                textFont = Font.CreateDynamicFontFromOSFont(ASFRLanguage.Japanese ? new[]{"Yu Gothic", "Meiryo", "Noto Sans CJK JP"} : new[]{"Segoe UI", "Arial"}, 13);
            cardStyle = new GUIStyle(EditorStyles.helpBox)
            {padding = new RectOffset(20, 20, 18, 18), margin = new RectOffset(12, 12, 7, 7)};
            titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {font = displayFont, fontSize = 42, fixedHeight = 54};
            sectionStyle = new GUIStyle(EditorStyles.boldLabel)
            {font = displayFont, fontSize = 18, fixedHeight = 30};
            tabStyle = new GUIStyle(EditorStyles.boldLabel)
            {font = textFont, alignment = TextAnchor.MiddleCenter, fontSize = 13};
            captionStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {font = textFont, fontSize = 12, padding = new RectOffset(0, 0, 4, 8)};
        }

        void BeginCard(string title, string subtitle = null)
        {
            EditorGUILayout.BeginVertical(cardStyle);
            if (!string.IsNullOrEmpty(title))
            {
                var accentLine = GUILayoutUtility.GetRect(1, 2, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(accentLine, new Color(Accent.r, Accent.g, Accent.b, .45f));
                GUILayout.Space(8);
                EditorGUILayout.LabelField(title, sectionStyle);
            }

            if (!string.IsNullOrEmpty(subtitle))
                EditorGUILayout.LabelField(subtitle, captionStyle);
        }

        void EndCard()
        {
            EditorGUILayout.EndVertical();
        }

        static bool TableToggle(bool value, float width, string tooltip)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Width(width));
            GUILayout.FlexibleSpace();
            value = GUILayout.Toggle(value, new GUIContent("", tooltip), GUILayout.Width(18));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            return value;
        }

        bool FeatureSwitch(string title, string description, bool enabled)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Space(8);
            EditorGUILayout.BeginVertical();
            GUILayout.Space(7);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(description, captionStyle);
            GUILayout.Space(4);
            EditorGUILayout.EndVertical();
            var old = GUI.backgroundColor;
            GUI.backgroundColor = enabled ? Accent : old;
            bool result = GUILayout.Toggle(enabled, enabled ? (ASFRLanguage.Japanese ? "有効" : "ON") : (ASFRLanguage.Japanese ? "無効" : "OFF"), GUI.skin.button, GUILayout.Width(66), GUILayout.Height(34));
            GUI.backgroundColor = old;
            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();
            return result;
        }

        void OnGUI()
        {
            int language = EditorGUILayout.Popup("Language / 言語", ASFRLanguage.Japanese ? 1 : 0, new[]{"English", "日本語"});
            if ((language == 1) != ASFRLanguage.Japanese)
            {
                ASFRLanguage.Japanese = language == 1;
                cardStyle = null;
                if (displayFont)
                    DestroyImmediate(displayFont);
                if (textFont)
                    DestroyImmediate(textFont);
                displayFont = textFont = null;
                validationSummary = null;
                Repaint();
            }

            PrepareStyles();
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 195;
            EditorGUILayout.BeginVertical();
            var banner = GUILayoutUtility.GetRect(1, 114, GUILayout.ExpandWidth(true));
            Color left = EditorGUIUtility.isProSkin ? new Color(.105f, .035f, .055f) : new Color(.97f, .9f, .91f);
            Color right = EditorGUIUtility.isProSkin ? new Color(.29f, .065f, .10f) : new Color(.93f, .72f, .76f);
            for (int x = 0; x < 80; x++)
                EditorGUI.DrawRect(new Rect(banner.x + banner.width * x / 80, banner.y, banner.width / 80 + 1, banner.height), Color.Lerp(left, right, x / 79f));
            GUI.Label(new Rect(28, banner.y + 17, 220, 48), "ASFR", titleStyle);
            GUI.Label(new Rect(30, banner.y + 69, 420, 22), ASFRLanguage.T("Transform. Freeze. Make it yours."), EditorStyles.label);
            var statusStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {alignment = TextAnchor.MiddleRight};
            GUI.Label(new Rect(banner.width - 300, banner.y + 27, 270, 20), ASFRLanguage.T("PC AVATAR STUDIO"), statusStyle);
            GUI.Label(new Rect(banner.width - 340, banner.y + 55, 310, 22), selected.Count(r => r) + ASFRLanguage.T(" meshes   /   ") + overlayMaterials.Count + ASFRLanguage.T(" materials"), statusStyle);
            EditorGUI.DrawRect(new Rect(0, banner.yMax - 2, banner.width, 2), Accent);
            var nav = GUILayoutUtility.GetRect(1, 48, GUILayout.ExpandWidth(true));
            string[] tabs = {ASFRLanguage.T("01  Appearance"), ASFRLanguage.T("02  Meshes"), ASFRLanguage.T("03  Interactions"), ASFRLanguage.T("04  Advanced")};
            for (int i = 0; i < tabs.Length; i++)
            {
                var tabRect = new Rect(nav.x + i * nav.width / 4, nav.y, nav.width / 4, nav.height);
                if (activeTab == i)
                {
                    EditorGUI.DrawRect(tabRect, EditorGUIUtility.isProSkin ? new Color(.26f, .075f, .10f) : new Color(.96f, .8f, .83f));
                    EditorGUI.DrawRect(new Rect(tabRect.x + 20, tabRect.yMax - 3, tabRect.width - 40, 3), Accent);
                }

                if (GUI.Button(tabRect, tabs[i], tabStyle) && activeTab != i)
                {
                    activeTab = i;
                    windowScroll = Vector2.zero;
                    GUI.FocusControl(null);
                }
            }

            EditorGUI.BeginChangeCheck();
            windowScroll = EditorGUILayout.BeginScrollView(windowScroll);
            BeginCard(null);
            EditorGUI.BeginChangeCheck();
            avatar = (VRCAvatarDescriptor)EditorGUILayout.ObjectField(ASFRLanguage.T("Original avatar"), avatar, typeof(VRCAvatarDescriptor), true);
            if (EditorGUI.EndChangeCheck())
            {
                Refresh();
                validationSummary = null;
            }

            if (GUILayout.Button(ASFRLanguage.T("Use selected avatar")))
            {
                avatar = Selection.activeGameObject ? Selection.activeGameObject.GetComponentInParent<VRCAvatarDescriptor>() : null;
                Refresh();
                validationSummary = null;
            }

            EndCard();
            InitializeEffectMaterials();
            if (activeTab == 0)
            {
                BeginCard(ASFRLanguage.T("Transformation materials"), ASFRLanguage.T("Choose up to six looks. Every selected mesh shares the same rising transformation."));
                for (int row = 0; row < overlayMaterials.Count; row += 2)
                {
                    EditorGUILayout.BeginHorizontal();
                    for (int col = 0; col < 2; col++)
                    {
                        int i = row + col;
                        if (i >= overlayMaterials.Count)
                        {
                            GUILayout.FlexibleSpace();
                            break;
                        }

                        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Width((position.width - 98) / 2));
                        var material = overlayMaterials[i];
                        Texture preview = material ? AssetPreview.GetAssetPreview(material) ?? AssetPreview.GetMiniThumbnail(material) : null;
                        GUILayout.Label(preview ? new GUIContent(preview) : new GUIContent("+"), new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                        {fontSize = 28}, GUILayout.Width(88), GUILayout.Height(100));
                        EditorGUILayout.BeginVertical();
                        GUILayout.Space(13);
                        EditorGUILayout.LabelField(ASFRLanguage.T("TransformationMat ") + (i + 1).ToString("00"), EditorStyles.boldLabel);
                        overlayMaterials[i] = (Material)EditorGUILayout.ObjectField(material, typeof(Material), false);
                        EditorGUILayout.LabelField(material ? ASFRLanguage.T("Ready to transform") : ASFRLanguage.T("Drop a material here"), captionStyle);
                        GUILayout.Space(10);
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(overlayMaterials.Count >= 6))
                    if (GUILayout.Button(ASFRLanguage.T("Add TransformationMat")))
                        overlayMaterials.Add(null);
                using (new EditorGUI.DisabledScope(overlayMaterials.Count <= 1))
                    if (GUILayout.Button(ASFRLanguage.T("Remove last TransformationMat")))
                        overlayMaterials.RemoveAt(overlayMaterials.Count - 1);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(ASFRLanguage.T("Poiyomi or full lilToon • up to six TransformationMats"), EditorStyles.miniLabel);
                seconds = EditorGUILayout.Slider(ASFRLanguage.T("Default duration (seconds)"), seconds, 0, 60);
                EditorGUILayout.LabelField(ASFRLanguage.T("In game: Options → Transformation has duration and amount dials. Lower the amount or turn Transform off to gradually reverse the effect."), captionStyle);
                EndCard();
                BeginCard(ASFRLanguage.T("Keep the small details"), ASFRLanguage.T("Materials in this list keep their original appearance without a coating."));
                EditorGUILayout.LabelField(ASFRLanguage.T("Blush and face-effect materials are suggested automatically. Add anything else you want to leave uncoated."), captionStyle);
                for (int i = 0; i < preservedEffectMaterials.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    preservedEffectMaterials[i] = (Material)EditorGUILayout.ObjectField(ASFRLanguage.T("Effect ") + (i + 1), preservedEffectMaterials[i], typeof(Material), false);
                    if (GUILayout.Button(ASFRLanguage.T("Remove"), GUILayout.Width(65)))
                    {
                        preservedEffectMaterials.RemoveAt(i--);
                    }

                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button(ASFRLanguage.T("Add preserved effect material")))
                    preservedEffectMaterials.Add(null);
                EndCard();
            }

            if (activeTab == 1)
            {
                BeginCard(ASFRLanguage.T("Your avatar, piece by piece"), ASFRLanguage.T("Choose what transforms and mark your clothing and main body."));
                showMeshes = EditorGUILayout.Foldout(showMeshes, selected.Count(r => r) + ASFRLanguage.T(" coated  ·  ") + clothing.Count(r => r) + ASFRLanguage.T(" clothing  ·  ") + innerCoating.Count(r => r && selected.Contains(r)) + ASFRLanguage.T(" inner coatings"), true);
                if (showMeshes)
                {
                    meshSearch = EditorGUILayout.TextField(ASFRLanguage.T("Search meshes"), meshSearch);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(ASFRLanguage.T("Include all")))
                    {
                        selected.Clear();
                        selected.AddRange(renderers.Where(r => r));
                    }

                    if (GUILayout.Button(ASFRLanguage.T("Include none")))
                        selected.Clear();
                    if (GUILayout.Button(ASFRLanguage.T("Clear clothing marks")))
                        clothing.Clear();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.LabelField(ASFRLanguage.T("Include controls coating. Clothing marks also enable removal for uncoated meshes."), EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField(ASFRLanguage.T("Extra inner coatings: ") + innerCoating.Count(r => r && selected.Contains(r)) + ASFRLanguage.T(" (only checked, included meshes)"), EditorStyles.miniLabel);
                    EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                    GUILayout.Label(ASFRLanguage.T("COAT / MESH"), EditorStyles.miniBoldLabel);
                    GUILayout.Label(ASFRLanguage.T("CLOTHING"), EditorStyles.miniBoldLabel, GUILayout.Width(85));
                    GUILayout.Label(ASFRLanguage.T("INNER FACES"), EditorStyles.miniBoldLabel, GUILayout.Width(100));
                    GUILayout.Label(ASFRLanguage.T("MAIN BODY"), EditorStyles.miniBoldLabel, GUILayout.Width(85));
                    GUILayout.Space(16);
                    EditorGUILayout.EndHorizontal();
                    int rowIndex = 0;
                    scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Clamp(position.height - 400, 220, 480)));
                    foreach (var r in renderers.Where(r => r))
                    {
                        string label = Path(r.transform, avatar.transform);
                        if (!string.IsNullOrWhiteSpace(meshSearch) && label.IndexOf(meshSearch, StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        Rect row = EditorGUILayout.BeginHorizontal(GUILayout.MinHeight(28));
                        if (Event.current.type == EventType.Repaint && rowIndex % 2 == 0)
                            EditorGUI.DrawRect(row, EditorGUIUtility.isProSkin ? new Color(1, 1, 1, .035f) : new Color(0, 0, 0, .035f));
                        rowIndex++;
                        bool include = EditorGUILayout.ToggleLeft(new GUIContent(r.name, label), selected.Contains(r));
                        if (include && !selected.Contains(r))
                            selected.Add(r);
                        else if (!include)
                            selected.Remove(r);
                        bool cloth = TableToggle(clothing.Contains(r), 85, ASFRLanguage.T("Mark as clothing for the clothing menu modes"));
                        if (cloth && !clothing.Contains(r))
                        {
                            clothing.Add(r);
                            if (mainBody == r)
                                mainBody = null;
                        }
                        else if (!cloth)
                            clothing.Remove(r);
                        using (new EditorGUI.DisabledScope(!include))
                        {
                            bool inside = TableToggle(innerCoating.Contains(r), 100, ASFRLanguage.T("Add an inward-facing coating to this mesh"));
                            if (inside && !innerCoating.Contains(r))
                                innerCoating.Add(r);
                            else if (!inside)
                                innerCoating.Remove(r);
                        }

                        bool isBody = TableToggle(mainBody == r, 85, ASFRLanguage.T("Main body: restore shrink/off/kisekae blendshapes in Remove clothing mode"));
                        if (isBody && mainBody != r)
                        {
                            mainBody = r;
                            clothing.Remove(r);
                        }
                        else if (!isBody && mainBody == r)
                            mainBody = null;
                        EditorGUILayout.EndHorizontal();
                    }

                    if (rowIndex == 0)
                        EditorGUILayout.LabelField(ASFRLanguage.T("No meshes match your search."), captionStyle);
                    EditorGUILayout.EndScrollView();
                    if (mainBody && mainBody.sharedMesh)
                    {
                        var matches = Enumerable.Range(0, mainBody.sharedMesh.blendShapeCount).Select(mainBody.sharedMesh.GetBlendShapeName).Where(ASFRClothing.MatchesBodyShape).ToArray();
                        EditorGUILayout.LabelField(new GUIContent(ASFRLanguage.T("Main body: ") + mainBody.name + " • " + matches.Length + ASFRLanguage.T(" matching blendshapes"), string.Join("\n", matches)), EditorStyles.wordWrappedMiniLabel);
                    }

                    EditorGUILayout.LabelField(ASFRLanguage.T("Is main body: only Remove clothing holds names containing shrink, off or kisekae at 0 (case-insensitive)."), EditorStyles.wordWrappedMiniLabel);
                }

                EndCard();
            }

            if (activeTab == 2)
            {
                BeginCard(ASFRLanguage.T("Feel the transformation"), ASFRLanguage.T("Vision effects, statue handles and interactions with other players."));
                showFeatures = EditorGUILayout.Foldout(showFeatures, ASFRLanguage.T("Vision and statue interaction"), true);
                if (showFeatures)
                {
                    addVisionOverlay = FeatureSwitch(ASFRLanguage.T("Frozen vision"), ASFRLanguage.T("Darkness, ice or your own overlay when the head freezes."), addVisionOverlay);
                    if (addVisionOverlay)
                    {
                        visionStrength = EditorGUILayout.Slider(ASFRLanguage.T("Default intensity"), visionStrength, 0, 1);
                        visionTexture = (Texture2D)EditorGUILayout.ObjectField(ASFRLanguage.T("Custom overlay (RGBA)"), visionTexture, typeof(Texture2D), false);
                        GUILayout.Space(8);
                    }

                    addStatueCamera = FeatureSwitch(ASFRLanguage.T("Grabbable statue"), ASFRLanguage.T("Two handles to move and turn. Stereo vision follows the statue."), addStatueCamera);
                    if (addStatueCamera)
                    {
                        statueEyeSeparation = EditorGUILayout.Slider(ASFRLanguage.T("Camera eye spacing (mm)"), statueEyeSeparation * 1000, 50, 80) / 1000;
                        EditorGUILayout.HelpBox(ASFRLanguage.T("Cyan moves • Orange turns / tilts\nGrabbing activates the stereo view until Reset. Match eye spacing to your headset IPD. Hide the view or both handles from Options."), MessageType.Info);
                    }

                    EditorGUILayout.LabelField(ASFRLanguage.T("Petrify contacts are included. Allow Others and Petrify Others both start off in Options."), EditorStyles.wordWrappedMiniLabel);
                }

                EndCard();
            }

            if (activeTab == 3)
            {
                BeginCard(ASFRLanguage.T("Fine tuning"));
                showAdvanced = EditorGUILayout.Foldout(showAdvanced, ASFRLanguage.T("Armature & coating offset"), true);
                if (showAdvanced)
                {
                    mainArmature = (Transform)EditorGUILayout.ObjectField(ASFRLanguage.T("Main armature"), mainArmature, typeof(Transform), true);
                    shellOffset = EditorGUILayout.Slider(ASFRLanguage.T("Shell offset (metres)"), shellOffset, 0, .003f);
                    if (mainArmature && avatar)
                        EditorGUILayout.LabelField(ASFRLanguage.T("Initial capture bones"), CaptureBones(avatar.transform, mainArmature).Length.ToString());
                    EditorGUILayout.LabelField(ASFRLanguage.T("Final constraints also depend on merged outfits and enabled features."), EditorStyles.wordWrappedMiniLabel);
                }

                EndCard();
            }

            EditorGUILayout.EndScrollView();
            if (EditorGUI.EndChangeCheck())
                validationSummary = null;
            BeginCard(null);
            if (!string.IsNullOrEmpty(validationSummary))
                EditorGUILayout.HelpBox(validationSummary, validationFailed ? MessageType.Error : MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(ASFRLanguage.T("Validate setup"), GUILayout.Width(160), GUILayout.Height(42)))
                {
                    var errors = CheckSetup();
                    validationFailed = errors.Count > 0;
                    validationSummary = validationFailed ? string.Join("\n", errors.Take(5)) + (errors.Count > 5 ? "\n… " + (errors.Count - 5) + ASFRLanguage.T(" more (see Console).") : "") : ASFRLanguage.T("Setup checks passed. Run Build & Test after generation.");
                    if (validationFailed)
                        Debug.LogWarning("ASFR setup:\n" + string.Join("\n", errors));
                }

                var oldBackground = GUI.backgroundColor;
                GUI.backgroundColor = Accent;
                using (new EditorGUI.DisabledScope(!avatar || selected.Count(r => r) == 0))
                    if (GUILayout.Button(ASFRLanguage.T("Create transformation avatar"), GUILayout.Height(42)))
                        try
                        {
                            Generate();
                            validationSummary = null;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                            validationFailed = true;
                            validationSummary = ASFRLanguage.Japanese ? "生成に失敗しました。以下の技術情報を確認してください：\n" + ex.Message : ex.Message;
                        }

                GUI.backgroundColor = oldBackground;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.LabelField(EditorApplication.isPlayingOrWillChangePlaymode ? ASFRLanguage.T("Exit Play Mode to generate.") : EditorApplication.isCompiling ? ASFRLanguage.T("Waiting for Unity to finish compiling…") : ASFRLanguage.T("Original assets are preserved. Test the generated copy before uploading."), captionStyle);
            EndCard();
            EditorGUILayout.EndVertical();
            EditorGUIUtility.labelWidth = oldLabelWidth;
        }
    }
}
