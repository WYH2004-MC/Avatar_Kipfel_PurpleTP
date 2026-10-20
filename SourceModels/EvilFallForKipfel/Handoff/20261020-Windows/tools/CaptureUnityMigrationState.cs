#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CaptureUnityMigrationState
{
    const string Work = "SourceModels/EvilFallForKipfel/Handoff/20261020-Windows";
    const string Temp = "Assets/Editor/EvilFallMigrationCapture";
    static double next;
    static CaptureUnityMigrationState() { EditorApplication.update += Capture; }
    static void Capture()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + 1;
        string project = Directory.GetParent(Application.dataPath).FullName;
        string request = Path.Combine(project, Work, "capture.request.json");
        if (!File.Exists(request) || SessionState.GetBool("EvilFall.MigrationCapture.20261020.1", false)) return;
        SessionState.SetBool("EvilFall.MigrationCapture.20261020.1", true);
        var report = new JObject { ["scope"] = "SaveScene saveAsCopy only; original scene files are not saved or replaced",
            ["playMode"] = EditorApplication.isPlayingOrWillChangePlaymode };
        try
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            report["prefabStage"] = stage == null ? null : new JObject { ["assetPath"] = stage.assetPath, ["dirty"] = stage.scene.isDirty,
                ["savedByCapture"] = false };
            var scenes = new JArray(); report["scenes"] = scenes;
            if (EditorApplication.isPlayingOrWillChangePlaymode) report["status"] = "SKIPPED_PLAYMODE";
            else
            {
                Directory.CreateDirectory(Path.Combine(project, Temp, "SceneCopies"));
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    var row = new JObject { ["path"] = scene.path, ["name"] = scene.name, ["loaded"] = scene.isLoaded, ["dirtyBefore"] = scene.isDirty };
                    scenes.Add(row);
                    if (!scene.isLoaded || string.IsNullOrEmpty(scene.path) || !scene.path.StartsWith("Assets/", StringComparison.Ordinal) || !scene.path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    { row["copied"] = false; continue; }
                    string originalPath = scene.path; bool dirty = scene.isDirty;
                    string copy = Temp + "/SceneCopies/" + i + "_" + Path.GetFileNameWithoutExtension(scene.path) + "_RAM.unity";
                    bool saved = EditorSceneManager.SaveScene(scene, copy, true);
                    if (dirty && !scene.isDirty) EditorSceneManager.MarkSceneDirty(scene);
                    if (scene.path != originalPath || scene.isDirty != dirty) throw new InvalidOperationException("Original scene identity/dirty state changed");
                    row["copied"] = saved; row["copyPath"] = copy; row["dirtyAfter"] = scene.isDirty;
                    if (!saved) throw new InvalidOperationException("Scene copy failed " + originalPath);
                }
                report["status"] = "SAVED_SCENE_COPIES";
            }
        }
        catch (Exception e) { report["status"] = "FAILED"; report["exception"] = e.ToString(); Debug.LogException(e); }
        File.WriteAllText(Path.Combine(project, Work, "evidence", "unity_ram_capture.json"), report.ToString());
    }
}
#endif
