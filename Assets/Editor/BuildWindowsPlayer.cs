using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildWindowsPlayer
{
    [MenuItem("Catapulto/Build Windows x64")]
    public static void BuildFromMenu()
    {
        Build();
    }

    public static void Build()
    {
        string outDir = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Builds", "Windows");
        // One build only — wipe previous player before writing a new one.
        if (Directory.Exists(outDir))
            Directory.Delete(outDir, recursive: true);
        Directory.CreateDirectory(outDir);
        string exePath = Path.Combine(outDir, "CatapultoFPS.exe");

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = exePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception($"Build failed: {report.summary.result}");

        Debug.Log($"Build succeeded: {exePath}");
        EditorUtility.RevealInFinder(exePath);
    }
}
