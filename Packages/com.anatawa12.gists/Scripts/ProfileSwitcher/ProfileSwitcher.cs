/*
 * Profile Switcher for VRChat SDK
 * https://gist.github.com/anatawa12/fc3376768f6f3c2f9e7c8e60596ad99f
 *
 * A script to switch VRChat account profile in the Unity Editor.
 * This can be useful when you have multiple accounts like personal and work accounts.
 *
 * ## Account Safety
 * This script does not store any credentials in the code, it uses VRChat's built-in multiple profile feature.
 * This script only changes the profile index and let VRCSDK to re-login with credentials VRChat stores.
 *
 * ## How to use
 * Open the window from "VRChat SDK/Profile Switcher" menu.
 * You'll see the list of profiles this script found.
 *
 * MIT License
 *
 * Copyright (c) 2025 anatawa12
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

#if UNITY_EDITOR && !COMPILER_UDONSHARP && (!ANATAWA12_GISTS_VPM_PACKAGE || GIST_fc3376768f6f3c2f9e7c8e60596ad99f)
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.Core;

namespace anatawa12.gists
{
    [InitializeOnLoad]
    public class ProfileSwitcher : EditorWindow
    {
        private static string _sessionStateKey = "com.anatawa12.profile-switcher.last-active";
        private static string _projectSettingsFilePath = "UserSettings/com.anatawa12.profile-switcher.active-profile.txt";

        static ProfileSwitcher()
        {
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                SessionState.SetInt(_sessionStateKey, GetCurrentAccountIndex());
            };
            AssemblyReloadEvents.afterAssemblyReload += () =>
            {
                var lastIndex = SessionState.GetInt(_sessionStateKey, -1);
                if (lastIndex == -1) lastIndex = LoadActiveProfileIndex();
                if (lastIndex == -1) return;

                if (lastIndex != GetCurrentAccountIndex())
                {
                    SwitchProfileTo((uint)lastIndex, user =>
                    {
                        Debug.Log($"[ProfileSwitcher] Restored profile to #{lastIndex} ({user.displayName}) after assembly reload");
                    }, error =>
                    {
                        Debug.LogWarning($"[ProfileSwitcher] Failed to restore profile to #{lastIndex} after assembly reload: {error}");
                    });
                }
            };
        }

        [MenuItem("VRChat SDK/Profile Switcher", priority = 201)]
        [MenuItem("Tools/anatawa12 gists/Profile Switcher")]
        private static void ShowWindow0() => GetWindow<ProfileSwitcher>("Profile Switcher");

        [NonSerialized]
        private ProfileInfo[]? _accounts;
        Vector2 _scrollPosition;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("VRChat Profile Switcher", EditorStyles.boldLabel);
            var currentIndex = GetCurrentAccountIndex();
            _accounts ??= FetchAccounts(currentIndex);
            if (_accounts.Length == 0)
            {
                EditorGUILayout.HelpBox("No accounts found.", MessageType.Info);
            }
            else
            {
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                var anyCurrent = false;
                foreach (var account in _accounts)
                {
                    EditorGUILayout.LabelField(account.index == 0 ? "Default Profile" : $"Profile #{account.index}", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField(account.index == currentIndex ? $"* {account.username}" : account.username);
                    if (account.index == currentIndex)
                    {
                        EditorGUILayout.HelpBox("Current active account.", MessageType.Info);
                        anyCurrent = true;
                    }
                    else
                    {
                        var skin = GUI.skin.button;
                        var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, skin);
                        if (GUI.Button(rect, "Switch to this account", skin))
                        {
                            SwitchProfileTo((uint)account.index);
                        }
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(); 

                if (anyCurrent)
                {
                    if (GUILayout.Button("Switch to new account"))
                        CreateNewProfile(_accounts);
                }
            }

            EditorGUILayout.Space(); 
            EditorGUILayout.Space(); 

            if (GUILayout.Button("Refresh"))
            {
                _accounts = FetchAccounts(currentIndex);
            }
        }

        private static void CreateNewProfile(ProfileInfo[] accounts)
        {
            var minimumOpenAccountIndex = 1;
            while (accounts.Any(account => account.index == minimumOpenAccountIndex))
                minimumOpenAccountIndex++;
            
            ApiCredentials.SetProfileIndex((uint)minimumOpenAccountIndex);
            ApiCredentials.Load();
            LogOutNoApi();
        }

        private void SwitchProfileTo(uint profileIndex) => SwitchProfileTo(profileIndex, user =>
        {
            EditorUtility.DisplayDialog("Profile Switcher", $"Switched profile to #{profileIndex} ({user.displayName})", "OK");
            Repaint();
        }, error =>
        {
            EditorUtility.DisplayDialog("Profile Switcher", $"Failed to fetch user data: {error}", "OK");
            Repaint();
        });

        private static void SwitchProfileTo(uint profileIndex, Action<APIUser> onSuccess, Action<string?> onError)
        {
            SaveActiveProfileIndex(profileIndex);

            ApiCredentials.SetProfileIndex(profileIndex);
            ApiCredentials.Load();
            LogOutNoApi();

            if (!ConfigManager.RemoteConfig.IsInitialized())
            {
                API.SetOnlineMode(true);
            }

            APIUser.InitialFetchCurrentUser(c =>
            {
                AnalyticsSDK.LoggedInUserChanged(c.Model as APIUser);
                onSuccess((APIUser)c.Model);
            }, e =>
            {
                onError(e.Error);
            });
        }

        private static void LogOutNoApi()
        {
            // similar to APIUser.Logout but no API call

            //APIUser.CurrentUser = null; // private setter
            typeof(APIUser).GetProperty("CurrentUser")?.SetValue(null, null);
            ApiUserPlatforms.CurrentUserPlatforms?.Clear();
        }

        private static ProfileInfo[] FetchAccounts(int currentIndex)
        {
            // from ApiCredentials.SECURE_PLAYER_PREFS_PW
            var password = typeof(ApiCredentials).GetField("SECURE_PLAYER_PREFS_PW", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as string;
            var list = new List<ProfileInfo>();
            list.Add(new ProfileInfo()
            {
                index = 0,
                username = SecurePlayerPrefs.GetString("humanName", password) ?? "(not logged in)",
            });
            for (int i = 1, missingCount = 0; missingCount < 10 || i < currentIndex; i++)
            {
                var humanName = SecurePlayerPrefs.GetString($"humanName[{i}]", password);
                if (string.IsNullOrEmpty(humanName))
                {
                    if (i == currentIndex)
                    {
                        missingCount = 0;
                        list.Add(new ProfileInfo
                        {
                            index = i,
                            username = "(not logged in)",
                        });
                    }
                    else
                    {
                        missingCount++;
                    }
                }
                else
                {
                    missingCount = 0;
                    list.Add(new ProfileInfo
                    {
                        index = i,
                        username = humanName,
                    });
                }
            }

            return list.ToArray();
        }

        private static FieldInfo? _apiCredentialsIndex;
        private static int GetCurrentAccountIndex()
        {
            // reflection of
            //return ApiCredentials.index ?? 0;
            _apiCredentialsIndex ??= typeof(ApiCredentials).GetField("index", BindingFlags.NonPublic | BindingFlags.Static);
            var value = (uint?)_apiCredentialsIndex.GetValue(null);
            return (int)(value ?? 0);
        }

        struct ProfileInfo
        {
            // 0 for default account
            public int index;
            public string username;
        }

        private static void SaveActiveProfileIndex(uint index)
        {
            try
            {
                File.WriteAllText(_projectSettingsFilePath, index.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ProfileSwitcher] Failed to save active profile index to {_projectSettingsFilePath}: {e}");
            }
        }

        private static int LoadActiveProfileIndex()
        {
            try
            {
                var text = File.ReadAllText(_projectSettingsFilePath);
                if (uint.TryParse(text.Trim(), out var index))
                    return (int)index;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ProfileSwitcher] Failed to load active profile index from {_projectSettingsFilePath}: {e}");
            }
            return -1;
        }
    }
}

#endif
