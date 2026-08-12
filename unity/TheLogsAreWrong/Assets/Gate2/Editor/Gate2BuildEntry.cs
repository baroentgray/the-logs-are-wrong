using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TheLogsAreWrong.Gate2.EditorTools
{
    /// <summary>
    /// Minimal Windows x64 Development build entry point for the Gate-2 bootstrap scene.
    /// Output goes to the ignored <c>Build/</c> folder inside the Unity project.
    /// </summary>
    public static class Gate2BuildEntry
    {
        public const string OutputDirectoryName = "Build";
        public const string PlayerName = "TheLogsAreWrongGate2Bootstrap.exe";

        public static void BuildWindows64Development()
        {
            try
            {
                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                                  ?? throw new InvalidOperationException("Project root not resolvable.");
                var outputDir = Path.Combine(projectRoot, OutputDirectoryName);
                Directory.CreateDirectory(outputDir);
                var exe = Path.Combine(outputDir, PlayerName);

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { Gate2BootstrapAuthoring.ScenePath },
                    locationPathName = exe,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.Development
                };

                var report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;

                Debug.Log($"[TLAW052] BUILD_RESULT={summary.result}");
                Debug.Log($"[TLAW052] BUILD_ERRORS={summary.totalErrors} BUILD_WARNINGS={summary.totalWarnings}");
                Debug.Log($"[TLAW052] BUILD_SIZE={summary.totalSize}");
                Debug.Log($"[TLAW052] BUILD_OUTPUT={summary.outputPath}");

                if (summary.result != BuildResult.Succeeded)
                {
                    Debug.LogError("[TLAW052] BUILD_FAILED");
                    EditorApplication.Exit(3);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[TLAW052] BUILD_EXCEPTION {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                EditorApplication.Exit(4);
            }
        }
    }
}
