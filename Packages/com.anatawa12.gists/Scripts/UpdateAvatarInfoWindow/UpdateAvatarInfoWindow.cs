/*
 * UpdateAvatarInfoWindow
 * A simple Unity Editor window for viewing and updating VRChat avatar information without re-uploading.
 * 
 * This tool allows you to update avatar information (name, description, visibility, styles,
 * content warnings, and tags) directly from the Unity Editor without uploading the avatar again.
 * You must be logged in via the VRChat SDK Control Panel before using this window.
 * Select an avatar from the dropdown to load its current information, edit the fields,
 * and click "Save Changes" to apply the updates to the VRChat backend.
 * https://gist.github.com/anatawa12/5f0161e7d6286d82687df1b165db74f0
 *
 * Click `Tools/anatawa12 gists/UpdateAvatarInfoWindow` to open this window.
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

#if UNITY_EDITOR && (!ANATAWA12_GISTS_VPM_PACKAGE || GIST_5f0161e7d6286d82687df1b165db74f0)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDKBase.Editor.Api;

namespace anatawa12.gists
{
    internal class UpdateAvatarInfoWindow : EditorWindow
    {
        private const string GIST_NAME = "Update Avatar Info Window";
        private const int AvatarPageSize = 20;

        [MenuItem("Tools/anatawa12 gists/" + GIST_NAME)]
        static void Create() => GetWindow<UpdateAvatarInfoWindow>(GIST_NAME);

        // --- Avatar list state ---
        [NonSerialized] private List<VRCAvatar> _avatarList = new List<VRCAvatar>();
        [NonSerialized] private string[] _avatarNames = Array.Empty<string>();
        private int _selectedAvatarIndex = -1;
        [NonSerialized] private bool _avatarListLoaded;
        [NonSerialized] private bool _loadingAvatarList;
        [NonSerialized] private string _avatarListError;

        // --- Selected avatar state ---
        [NonSerialized] private VRCAvatar? _currentAvatar;
        [NonSerialized] private bool _loadingAvatar;
        [NonSerialized] private string _avatarLoadError;

        // --- Editable fields ---
        [NonSerialized] private string _name = "";
        [NonSerialized] private string _description = "";
        [NonSerialized] private int _visibilityIndex; // 0=private, 1=public
        [NonSerialized] private static readonly string[] VisibilityOptions = { "private", "public" };

        [NonSerialized] private List<VRCAvatarStyle> _styleOptions = new List<VRCAvatarStyle>();
        [NonSerialized] private string[] _styleNames = Array.Empty<string>();
        [NonSerialized] private int _primaryStyleIndex;
        [NonSerialized] private int _secondaryStyleIndex;
        [NonSerialized] private bool _loadingStyles;

        // Content warnings
        [Flags]
        private enum ContentWarningFlags
        {
            None     = 0,
            Sex      = 1 << 0,
            Adult    = 1 << 1,
            Violence = 1 << 2,
            Gore     = 1 << 3,
            Horror   = 1 << 4,
        }
        private static readonly (ContentWarningFlags Flag, string Key)[] ContentWarningMap =
        {
            (ContentWarningFlags.Sex,      "content_sex"),
            (ContentWarningFlags.Adult,    "content_adult"),
            (ContentWarningFlags.Violence, "content_violence"),
            (ContentWarningFlags.Gore,     "content_gore"),
            (ContentWarningFlags.Horror,   "content_horror"),
        };
        [NonSerialized] private ContentWarningFlags _contentWarnings;

        // Tags (non-content-warning tags)
        [NonSerialized] private List<string> _extraTags = new List<string>();
        [NonSerialized] private string _newTagInput = "";

        // Thumbnail
        [NonSerialized] private Texture2D _thumbnailTexture;
        [NonSerialized] private string _thumbnailUrl;
        [NonSerialized] private bool _loadingThumbnail;

        // Save state
        [NonSerialized] private bool _saving;
        [NonSerialized] private string _saveError;
        [NonSerialized] private string _saveSuccess;


        // Cancellation
        private CancellationTokenSource _cts = new CancellationTokenSource();

        private void OnEnable()
        {
            if (APIUser.IsLoggedIn)
                FetchAvatarList();
            else
                FetchStyles();
        }

        private void OnDisable()
        {
            _cts.Cancel();
            _cts = new CancellationTokenSource();
        }

        private async void FetchStyles()
        {
            if (_loadingStyles) return;
            _loadingStyles = true;
            try
            {
                var styles = await VRCApi.GetAvatarStyles();
                _styleOptions = styles ?? new List<VRCAvatarStyle>();
                _styleNames = _styleOptions.Select(s => s.StyleName).Prepend("(none)").ToArray();
            }
            catch (Exception)
            {
                _styleNames = new[] { "(none)" };
            }
            finally
            {
                _loadingStyles = false;
                Repaint();
            }
        }

        private void FetchAvatarList()
        {
            if (_loadingAvatarList) return;
            _loadingAvatarList = true;
            _avatarListError = null;
            _avatarList.Clear();
            _avatarNames = Array.Empty<string>();

            FetchStyles();
            FetchAvatarPage(0);
        }

        private void FetchAvatarPage(int offset)
        {
            ApiAvatar.FetchList(
                successCallback: (avatars, _) =>
                {
                    var list = new List<ApiAvatar>(avatars);
                    foreach (var a in list)
                        _avatarList.Add(new VRCAvatar { ID = a.id, Name = a.name });

                    _avatarNames = _avatarList.Select(a => a.Name).ToArray();

                    if (list.Count == AvatarPageSize)
                        FetchAvatarPage(offset + AvatarPageSize);
                    else
                    {
                        _avatarListLoaded = true;
                        _loadingAvatarList = false;
                        Repaint();
                    }
                },
                errorCallback: error =>
                {
                    _avatarListError = error;
                    _avatarListLoaded = true;
                    _loadingAvatarList = false;
                    Repaint();
                },
                owner: ApiAvatar.Owner.Mine,
                relStatus: ApiAvatar.ReleaseStatus.All,
                number: AvatarPageSize,
                offset: offset,
                heading: ApiAvatar.SortHeading.None,
                order: ApiAvatar.SortOrder.Descending,
                disableCache: false,
                compatibleVersionsOnly: false
            );
        }

        private async void LoadAvatar(string blueprintId)
        {
            if (_loadingAvatar) return;
            _loadingAvatar = true;
            _avatarLoadError = null;
            _thumbnailTexture = null;
            _thumbnailUrl = null;
            _saveError = null;
            _saveSuccess = null;
            Repaint();
            try
            {
                _currentAvatar = await VRCApi.GetAvatar(blueprintId, cancellationToken: _cts.Token);
                ApplyAvatarToFields(_currentAvatar.Value);
                if (!string.IsNullOrEmpty(_currentAvatar.Value.ThumbnailImageUrl))
                {
                    _thumbnailUrl = _currentAvatar.Value.ThumbnailImageUrl;
                    LoadThumbnail(_thumbnailUrl);
                }
            }
            catch (Exception e)
            {
                _avatarLoadError = e.Message;
                _currentAvatar = null;
            }
            finally
            {
                _loadingAvatar = false;
                Repaint();
            }
        }

        private void ApplyAvatarToFields(VRCAvatar avatar)
        {
            _name = avatar.Name ?? "";
            _description = avatar.Description ?? "";
            _visibilityIndex = avatar.ReleaseStatus == "public" ? 1 : 0;

            // Styles
            _primaryStyleIndex = 0;
            _secondaryStyleIndex = 0;
            if (_styleOptions.Count > 0)
            {
                var styles = avatar.Styles;
                for (int i = 0; i < _styleOptions.Count; i++)
                {
                    if (_styleOptions[i].ID == styles.Primary) _primaryStyleIndex = i + 1;
                    if (_styleOptions[i].ID == styles.Secondary) _secondaryStyleIndex = i + 1;
                }
            }

            // Content warnings & extra tags
            var tags = avatar.Tags ?? new List<string>();
            _contentWarnings = ContentWarningFlags.None;
            foreach (var (flag, key) in ContentWarningMap)
                if (tags.Contains(key)) _contentWarnings |= flag;

            _extraTags = tags.Where(t => ContentWarningMap.All(m => m.Key != t)).ToList();
        }

        private CancellationTokenSource _thumbnailCts = new CancellationTokenSource();
        private async void LoadThumbnail(string url)
        {
            if (_loadingThumbnail)
            {
                _thumbnailCts.Cancel();
                _thumbnailCts = new CancellationTokenSource();
            }
            var currentThumbnailCts = _thumbnailCts;
            var token = CancellationTokenSource.CreateLinkedTokenSource(currentThumbnailCts.Token, _thumbnailCts.Token).Token;
            _loadingThumbnail = true;
            try
            {
                _thumbnailTexture = await VRCApi.GetImage(url, cancellationToken: token);
            }
            catch (OperationCanceledException)
                when (currentThumbnailCts.IsCancellationRequested)
            {
                // Thumbnail load was cancelled because a new one was requested. Just ignore.
            }
            catch (Exception)
            {
                _thumbnailTexture = null;
            }
            finally
            {
                _loadingThumbnail = false;
                Repaint();
            }
        }

        private bool HasUnsavedChanges()
        {
            if (_currentAvatar == null) return false;
            var avatar = _currentAvatar.Value;

            if (_name != (avatar.Name ?? "")) return true;
            if (_description != (avatar.Description ?? "")) return true;
            if (VisibilityOptions[_visibilityIndex] != avatar.ReleaseStatus) return true;

            var primaryId = _primaryStyleIndex > 0 ? _styleOptions[_primaryStyleIndex - 1].ID : "";
            var secondaryId = _secondaryStyleIndex > 0 ? _styleOptions[_secondaryStyleIndex - 1].ID : "";
            if (primaryId != (avatar.Styles.Primary ?? "")) return true;
            if (secondaryId != (avatar.Styles.Secondary ?? "")) return true;

            var tags = avatar.Tags ?? new List<string>();
            var expectedWarnings = ContentWarningFlags.None;
            foreach (var (flag, key) in ContentWarningMap)
                if (tags.Contains(key)) expectedWarnings |= flag;
            if (_contentWarnings != expectedWarnings) return true;

            var expectedExtraTags = tags.Where(t => ContentWarningMap.All(m => m.Key != t)).ToList();
            if (!_extraTags.SequenceEqual(expectedExtraTags)) return true;

            return false;
        }

        private async void DoSaveChanges()
        {
            if (_saving || _currentAvatar == null) return;
            _saving = true;
            _saveError = null;
            _saveSuccess = null;
            Repaint();
            try
            {
                var avatar = _currentAvatar.Value;
                avatar.Name = _name;
                avatar.Description = _description;
                avatar.ReleaseStatus = VisibilityOptions[_visibilityIndex];

                var styles = avatar.Styles;
                styles.Primary = _primaryStyleIndex > 0 ? _styleOptions[_primaryStyleIndex - 1].ID : "";
                styles.Secondary = _secondaryStyleIndex > 0 ? _styleOptions[_secondaryStyleIndex - 1].ID : "";
                avatar.Styles = styles;

                var tags = new List<string>(_extraTags);
                foreach (var (flag, key) in ContentWarningMap)
                    if ((_contentWarnings & flag) != 0) tags.Add(key);
                avatar.Tags = tags;

                _currentAvatar = await VRCApi.UpdateAvatarInfo(avatar.ID, avatar, _cts.Token);
                _saveSuccess = "Saved successfully!";
                // Remote may sanitize or change some fields, so re-apply returned avatar info to ensure displayed values are correct
                ApplyAvatarToFields(_currentAvatar.Value);

                // Refresh avatar name in list
                if (_selectedAvatarIndex >= 0 && _selectedAvatarIndex < _avatarList.Count)
                {
                    _avatarList[_selectedAvatarIndex] = _currentAvatar.Value;
                    _avatarNames = _avatarList.Select(a => a.Name).ToArray();
                }
            }
            catch (Exception e)
            {
                _saveError = e.Message;
            }
            finally
            {
                _saving = false;
                Repaint();
            }
        }

        private void OnGUI()
        {
            if (!APIUser.IsLoggedIn)
            {
                EditorGUILayout.HelpBox("Please log in via the VRChat SDK Control Panel first.", MessageType.Warning);
                if (GUILayout.Button("Open Control Panel"))
                    EditorApplication.ExecuteMenuItem("VRChat SDK/Show Control Panel");
                return;
            }

            if (!_loadingAvatarList && !_avatarListLoaded)
                FetchAvatarList();

            // Top row: Selected Avatar | Name | Visibility
            using (new EditorGUILayout.HorizontalScope())
            {
                // -- Selected Avatar --
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(220)))
                {
                    EditorGUILayout.LabelField("Selected Avatar", EditorStyles.boldLabel);
                    if (_loadingAvatarList)
                    {
                        EditorGUILayout.LabelField("Loading avatars...");
                    }
                    else if (_avatarListError != null)
                    {
                        EditorGUILayout.HelpBox(_avatarListError, MessageType.Error);
                        if (GUILayout.Button("Retry")) FetchAvatarList();
                    }
                    else if (_avatarNames.Length == 0)
                    {
                        EditorGUILayout.LabelField("No avatars found.");
                        if (GUILayout.Button("Refresh")) FetchAvatarList();
                    }
                    else
                    {
                        var displayNames = _avatarNames.Prepend("-- Select Avatar --").ToArray();
                        var newIndex = EditorGUILayout.Popup(_selectedAvatarIndex + 1, displayNames) - 1;
                        if (newIndex != _selectedAvatarIndex || (newIndex >= 0 && _avatarList[newIndex].ID != _currentAvatar?.ID))
                        {
                            _selectedAvatarIndex = newIndex;
                            if (newIndex >= 0)
                                LoadAvatar(_avatarList[newIndex].ID);
                            else
                            {
                                _currentAvatar = null;
                                _thumbnailTexture = null;
                            }
                        }
                    }
                }

                // -- Name --
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField("Name", EditorStyles.boldLabel);
                    using (new EditorGUI.DisabledScope(_currentAvatar == null || _saving))
                        _name = EditorGUILayout.TextField(_name);
                }

                // -- Visibility --
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(120)))
                {
                    EditorGUILayout.LabelField("Visibility", EditorStyles.boldLabel);
                    using (new EditorGUI.DisabledScope(_currentAvatar == null || _saving))
                        _visibilityIndex = EditorGUILayout.Popup(_visibilityIndex, VisibilityOptions);
                }
            }

            EditorGUILayout.Space(4);

            // Main area: Left panel + Right thumbnail
            using (new EditorGUILayout.HorizontalScope())
            {
                // ---- LEFT PANEL ----
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    // Primary / Secondary Style
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUILayout.VerticalScope())
                        {
                            EditorGUILayout.LabelField("Primary Style", EditorStyles.boldLabel);
                            using (new EditorGUI.DisabledScope(_currentAvatar == null || _saving || _loadingStyles))
                                _primaryStyleIndex = EditorGUILayout.Popup(_primaryStyleIndex, _styleNames);
                        }

                        using (new EditorGUILayout.VerticalScope())
                        {
                            EditorGUILayout.LabelField("Secondary Style", EditorStyles.boldLabel);
                            using (new EditorGUI.DisabledScope(_currentAvatar == null || _saving || _loadingStyles))
                                _secondaryStyleIndex = EditorGUILayout.Popup(_secondaryStyleIndex, _styleNames);
                        }
                    }

                    EditorGUILayout.Space(4);

                    // Content Warnings / Tags
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        // Content Warnings (flags enum popup)
                        using (new EditorGUILayout.VerticalScope())
                        {
                            EditorGUILayout.LabelField("Content Warnings", EditorStyles.boldLabel);
                            using (new EditorGUI.DisabledScope(_currentAvatar == null || _saving))
                                _contentWarnings = (ContentWarningFlags)EditorGUILayout.EnumFlagsField(_contentWarnings);
                        }

                        // Tags
                        using (new EditorGUILayout.VerticalScope())
                        {
                            EditorGUILayout.LabelField("Tags", EditorStyles.boldLabel);
                            using (new EditorGUI.DisabledScope(_currentAvatar == null || _saving))
                            {
                                // Show existing tags as removable chips
                                for (int i = _extraTags.Count - 1; i >= 0; i--)
                                {
                                    using (new EditorGUILayout.HorizontalScope())
                                    {
                                        EditorGUILayout.LabelField(_extraTags[i], GUILayout.ExpandWidth(true));
                                        if (GUILayout.Button("✕", GUILayout.Width(22)))
                                            _extraTags.RemoveAt(i);
                                    }
                                }
                                // Add new tag
                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    _newTagInput = EditorGUILayout.TextField(_newTagInput);
                                    if (GUILayout.Button("Add", GUILayout.Width(40)) && !string.IsNullOrWhiteSpace(_newTagInput))
                                    {
                                        var tag = _newTagInput.Trim();
                                        if (!_extraTags.Contains(tag))
                                            _extraTags.Add(tag);
                                        _newTagInput = "";
                                        GUI.FocusControl(null);
                                    }
                                }
                            }
                        }
                    }

                    EditorGUILayout.Space(4);

                    // Description
                    EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
                    using (new EditorGUI.DisabledScope(_currentAvatar == null || _saving))
                        _description = EditorGUILayout.TextArea(_description, GUILayout.MinHeight(100));

                    EditorGUILayout.Space(4);

                    // Last Updated / Version
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUILayout.VerticalScope())
                        {
                            EditorGUILayout.LabelField("Last Updated", EditorStyles.boldLabel);
                            EditorGUILayout.LabelField(_currentAvatar != null
                                ? _currentAvatar.Value.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                                : "--");
                        }

                        using (new EditorGUILayout.VerticalScope())
                        {
                            EditorGUILayout.LabelField("Version", EditorStyles.boldLabel);
                            EditorGUILayout.LabelField(_currentAvatar != null ? _currentAvatar.Value.Version.ToString() : "--");
                        }
                    }

                    EditorGUILayout.Space(4);

                    // Save / status
                    using (new EditorGUI.DisabledScope(_currentAvatar == null || _saving || !HasUnsavedChanges()))
                    {
                        if (GUILayout.Button(_saving ? "Saving..." : "Save Changes"))
                            DoSaveChanges();
                    }

                    if (_saveError != null)
                        EditorGUILayout.HelpBox(_saveError, MessageType.Error);
                    if (_saveSuccess != null)
                        EditorGUILayout.HelpBox(_saveSuccess, MessageType.Info);
                    if (_avatarLoadError != null)
                        EditorGUILayout.HelpBox(_avatarLoadError, MessageType.Error);
                    if (_loadingAvatar)
                        EditorGUILayout.LabelField("Loading avatar info...");
                }

                // ---- RIGHT PANEL: Thumbnail ----
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(260)))
                {
                    var thumbRect = GUILayoutUtility.GetRect(260, 260);
                    if (_loadingThumbnail)
                        EditorGUI.LabelField(thumbRect, "Loading thumbnail...", EditorStyles.centeredGreyMiniLabel);
                    else if (_thumbnailTexture != null)
                        GUI.DrawTexture(thumbRect, _thumbnailTexture, ScaleMode.ScaleToFit);
                    else
                        EditorGUI.LabelField(thumbRect, "No thumbnail", EditorStyles.centeredGreyMiniLabel);
                }
            }
        }
    }
}
#endif
