using Assets.Scripts.Objects.Motherboards;
using HarmonyLib;
using JetBrains.Annotations;
using LaunchPadBooster;
using RustMod.Patch;
using System;
using StationeersMods.Interface;
using UnityEngine;

namespace RustMod {
[StationeersMod(PluginGuid, PluginName, PluginVersion)]
public class RustMod : ModBehaviour {
    private const string PluginGuid = "net.buzzec.RustMod";
    public const string PluginName = "RustMod";
    private const string PluginVersion = "0.1.0";
    private const string PluginLongVersion = PluginVersion;
    private static readonly Mod MOD = new(PluginName, PluginLongVersion);
    private static bool _initialized = false;

    [CanBeNull] private Harmony _harmony = null;
    [CanBeNull] private WebServer _webServer = null;
    [CanBeNull] private ProgramLibrary _programLibrary = null;

    public override void OnLoaded(ContentHandler content) {
        base.OnLoaded(content);
        try {
            if (!_initialized) {
                MOD.AddPrefabs(content.prefabs);
                MOD.SetupPrefabs().IgnoreEmpty().SetBlueprintMaterials();
                MOD.SetupPrefabs("TestChip").SetPaintableColor(ColorType.Black);

                Debug.Log($"[{PluginName}] Loaded {content.prefabs.Count} prefabs");

                _initialized = true;
            }

            _harmony = new Harmony(PluginGuid);
            _harmony.CreateClassProcessor(typeof(CircuitHousingPatch)).Patch();

            RustFFI.EnsureLoaded();

            _webServer = new WebServer();
            _programLibrary = _webServer.ProgramLibrary();

            Debug.LogWarning($"[{PluginName}] Loaded");
        } catch (Exception ex) {
            Debug.LogError($"[{PluginName}] Error during OnLoaded: {ex}");
        }
    }

    private void OnDestroy() {
        _harmony?.UnpatchSelf();
        _programLibrary?.Dispose();
        _programLibrary = null;
        _webServer?.Dispose();
        _webServer = null;
    }
}
}
