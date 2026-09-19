using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Immutable;

namespace Triturbo.FaceTrackingFramework
{
    [InitializeOnLoad]
    public static class Localization
    {
        private const string DEFAULT_KEY = "en-US";
        private const string LANG_PREF_KEY = "en-US";
        private const string localizationPathGuid = "1795daade784576459b4f6f42a292cd9";
        private static string localizationPathRoot = AssetDatabase.GUIDToAssetPath(localizationPathGuid);
        private static Dictionary<string, string> languageFiles = new ();
        private static Dictionary<string, string> currentDictionary;
        private static string currentLanguage = "en-US";

        private static readonly ImmutableDictionary<string, string> SupportedLanguageDisplayNames
            = ImmutableDictionary<string, string>.Empty
                .Add("en-US", "English")
                .Add("ja-JP", "日本語")
                .Add("zh-Hans", "简体中文")
                .Add("zh-Hant", "繁體中文")
                .Add("ko-KR", "한국어");


        static Localization()
        {
            LoadAvailableLanguages();
            LoadLanguage(EditorPrefs.GetString(LANG_PREF_KEY, currentLanguage));
        }

        public static void LoadAvailableLanguages()
        {
            if (string.IsNullOrEmpty(localizationPathRoot) || !Directory.Exists(localizationPathRoot)) return;
            
            languageFiles.Clear();
            foreach (string file in Directory.GetFiles(localizationPathRoot, "*.json"))
            {
                string langKey = Path.GetFileNameWithoutExtension(file);
                languageFiles[langKey] = file;
            }
        }

        private static Dictionary<string, string> LoadLanguageFromFile(string langKey)
        {
            Dictionary<string, string> langDict = new Dictionary<string, string>();

            if (languageFiles.TryGetValue(langKey, out string filePath))
            {
                string content = File.ReadAllText(filePath);
                langDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);
            }
            
            if (langKey != DEFAULT_KEY && languageFiles.TryGetValue(DEFAULT_KEY, out string fallbackPath))
            {
                string fallbackContent = File.ReadAllText(fallbackPath);
                var fallbackDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(fallbackContent) ?? new Dictionary<string, string>();

                // Merge missing keys from fallback language
                foreach (var kvp in fallbackDict)
                {
                    if (!langDict.ContainsKey(kvp.Key))
                    {
                        langDict[kvp.Key] = kvp.Value;
                    }
                }
            }
            
            return langDict;
        }

        public static void LoadLanguage(string langKey)
        {
            if (languageFiles.ContainsKey(langKey))
            {
                currentLanguage = langKey;
                currentDictionary = LoadLanguageFromFile(langKey);
                EditorPrefs.SetString(LANG_PREF_KEY, langKey);
            }
        }


        public static string S(string key)
        {
            return S(key, key);
        }

        public static string S(string key, string defaultValue)
        {
            if (currentDictionary != null && currentDictionary.TryGetValue(key, out string value))
                return value;
            return defaultValue;
        }
        public static string SF(string key, params object[] format)
        {
          return string.Format(S(key), format);
        }
        public static GUIContent G(string key)
        {
            var tooltip = S(key + ".tooltip", null);
            return tooltip != null ? new GUIContent(S(key), tooltip) : new GUIContent(S(key));
        }
        
        public static GUIContent UE(string blendshape)
        {
            string key = $"ue.{blendshape}";
            var tooltip = S(key + ".tooltip", null);
            return tooltip != null ? new GUIContent(S(key, blendshape), tooltip) : new GUIContent(S(key, blendshape));
        }
        public static GUIContent GF(string key, params object[] format)
        {
            var tooltip = S(key + ".tooltip", null);
            string text = string.Format(S(key), format);
            return tooltip != null ? new GUIContent(text, string.Format(tooltip, format)) : new GUIContent(text);
        }

        public static void DrawLanguageSelection()
        {
            string[] langKeys = new List<string>(languageFiles.Keys).ToArray();
            string[] langDisplayNames = new string[langKeys.Length];

            for (int i = 0; i < langKeys.Length; i++)
            {
                langDisplayNames[i] = SupportedLanguageDisplayNames.TryGetValue(langKeys[i], out var displayName) ? displayName : langKeys[i];
            }

            int index = System.Array.IndexOf(langKeys, currentLanguage);
            int newIndex = EditorGUILayout.Popup(G("lang"), index, langDisplayNames);
            if (newIndex != index && newIndex >= 0)
            {
                LoadLanguage(langKeys[newIndex]);
            }
        }
    }
}
