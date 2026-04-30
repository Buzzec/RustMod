using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using stationeers.modding.exporter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RustMod.Editor {
[InitializeOnLoad]
public static class RustNativeExportHook {
    private const string NativeDllName = "stationeers_emu.dll";
    private const string NativePayloadName = "stationeers_emu.native";

    static RustNativeExportHook() {
        EditorApplication.delayCall += RegisterBuildHandler;
    }

    private static void RegisterBuildHandler() {
        BuildPlayerWindow.RegisterBuildPlayerHandler(OnBuildButtonPressed);
    }

    private static void OnBuildButtonPressed(BuildPlayerOptions options) {
        if (!SaveAllWithShaderDirtyTolerance()) {
            return;
        }

        if (CompileMonitor.LastPassHadErrors) {
            UnityEngine.Debug.LogError("Last build had errors, exporting stopped");
            return;
        }

        BuildNativeDll();
        LaunchPadExport.Export(options);
        CopyNativePayload();

        if ((options.options & BuildOptions.AutoRunPlayer) != 0) {
            StationeersRunner.RunStationeers();
        }
    }

    private static bool SaveAllWithShaderDirtyTolerance() {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
            return false;
        }

        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null && prefabStage.prefabContentsRoot != null && prefabStage.scene.isDirty) {
            var path = string.IsNullOrEmpty(prefabStage.assetPath) ? "(unknown prefab asset)" : prefabStage.assetPath;
            var choice = EditorUtility.DisplayDialogComplex(
                "Save Prefab Stage changes?",
                "There are unsaved changes in Prefab Mode:\n\n" + path + "\n\nSave changes before continuing?",
                "Save",
                "Cancel",
                null);

            if (choice == 1) {
                return false;
            }

            PrefabUtility.SaveAsPrefabAsset(prefabStage.prefabContentsRoot, prefabStage.assetPath, out _);
            EditorApplication.ExecuteMenuItem("File/Save");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!ExportPreflight.HasUnsavedChanges(out var dirtyItems)) {
            return true;
        }

        var blockingItems = new List<string>();
        foreach (var item in dirtyItems) {
            if (!IsShaderAsset(item)) {
                blockingItems.Add(item);
            }
        }

        if (blockingItems.Count > 0) {
            UnityEngine.Debug.LogWarning(
                "[Rust Native Export] Build canceled because these items are still unsaved:\n - " +
                string.Join("\n - ", blockingItems));
            return false;
        }

        UnityEngine.Debug.LogWarning(
            "[Rust Native Export] Continuing with shader assets that Unity still reports dirty after saving:\n - " +
            string.Join("\n - ", dirtyItems));
        return true;
    }

    private static bool IsShaderAsset(string assetPath) {
        return assetPath.EndsWith(".shader", StringComparison.OrdinalIgnoreCase) ||
               assetPath.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase) ||
               assetPath.EndsWith(".compute", StringComparison.OrdinalIgnoreCase);
    }

    [MenuItem("Tools/Rust Mod/Build Native DLL")]
    private static void BuildNativeDllMenuItem() {
        BuildNativeDll();
    }

    private static void BuildNativeDll() {
        var scriptPath = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            "Native",
            "riscv_emu",
            "build-native.ps1"));

        if (!File.Exists(scriptPath)) {
            throw new FileNotFoundException("Rust native build script was not found.", scriptPath);
        }

        var shell = File.Exists(@"C:\Program Files\PowerShell\7\pwsh.exe")
            ? @"C:\Program Files\PowerShell\7\pwsh.exe"
            : "powershell.exe";

        var startInfo = new ProcessStartInfo {
            FileName = shell,
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? throw new InvalidOperationException(),
            UseShellExecute = false,
            CreateNoWindow = true
        };

        UnityEngine.Debug.Log("Building Rust native DLL...");
        using var process = Process.Start(startInfo);
        if (process == null) {
            throw new InvalidOperationException("Failed to start Rust native build process.");
        }

        process.WaitForExit();
        if (process.ExitCode != 0) {
            throw new InvalidOperationException($"Rust native build failed with exit code {process.ExitCode}.");
        }
    }

    private static void CopyNativePayload() {
        var sourceDll = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "Plugins",
            "x86_64",
            NativeDllName));

        if (!File.Exists(sourceDll)) {
            throw new FileNotFoundException("Rust native DLL was not found after build.", sourceDll);
        }

        var nativeOutputDir = Path.Combine(LaunchPadExport.tempFolder, "Native");
        Directory.CreateDirectory(nativeOutputDir);

        var targetPayload = Path.Combine(nativeOutputDir, NativePayloadName);
        File.Copy(sourceDll, targetPayload, true);
        UnityEngine.Debug.Log($"Copied Rust native payload to {targetPayload}");
    }
}
}