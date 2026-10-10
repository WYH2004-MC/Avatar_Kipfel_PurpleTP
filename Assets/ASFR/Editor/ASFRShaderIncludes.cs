using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ASFR
{
    static class ASFRShaderIncludes
    {
        static readonly HashSet<string> Checked = new HashSet<string>();
        static readonly Regex Include = new Regex(@"(#\s*include(?:_with_pragmas)?\s*"")(Assets/_PoiyomiShaders/[^""]+)("")");
        [MenuItem("Tools/ASFR/Misc/Repair relocated Poiyomi includes", false, 111)]
        public static void RepairInstalled()
        {
            Checked.Clear();
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Shader"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/_PoiyomiShaders/"))
                    count += RepairPath(path);
            }

            EditorUtility.DisplayDialog("ASFR shader repair", count > 0 ? "Repaired " + count + " shader/include files. Backups are in Library/ASFRShaderBackups. Unity will recompile the shaders." : "No broken relocated Poiyomi include paths were found.", "OK");
        }

        public static void Repair(Shader shader)
        {
            if (shader)
                RepairPath(AssetDatabase.GetAssetPath(shader));
        }

        static int RepairPath(string path)
        {
            path = path.Replace('\\', '/');
            int split = path.IndexOf("/_PoiyomiShaders/", StringComparison.Ordinal);
            if (split < 0 || !path.StartsWith("Assets/", StringComparison.Ordinal) || !File.Exists(path) || !Checked.Add(path))
                return 0;
            string installedRoot = path.Substring(0, split) + "/_PoiyomiShaders/";
            var dependencies = new HashSet<string>();
            string original = File.ReadAllText(path);
            string repaired = Include.Replace(original, m =>
            {
                string missing = m.Groups[2].Value;
                if (File.Exists(missing))
                    return m.Value;
                string actual = installedRoot + missing.Substring("Assets/_PoiyomiShaders/".Length);
                if (!File.Exists(actual))
                    return m.Value;
                dependencies.Add(actual);
                return m.Groups[1].Value + actual + m.Groups[3].Value;
            });
            int count = 0;
            if (repaired != original)
            {
                string backup = "Library/ASFRShaderBackups/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fffffff") + "/" + path;
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.Copy(path, backup);
                File.WriteAllText(path, repaired);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("ASFR repaired relocated include paths in " + path + "; backup: " + backup);
                count++;
            }

            foreach (string dependency in dependencies)
                count += RepairPath(dependency);
            return count;
        }
    }
}
