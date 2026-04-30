using Assets.Scripts.Objects.Motherboards;
using Assets.Scripts.Networking;
using Assets.Scripts;
using HarmonyLib;
using JetBrains.Annotations;
using LaunchPadBooster;
using LaunchPadBooster.Networking;
using RustMod.Net;
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
    public static RustMod Instance { get; private set; }

    [CanBeNull] private Harmony _harmony = null;
    [CanBeNull] private WebServer _webServer = null;
    [CanBeNull] private ProgramLibrary _programLibrary = null;

    public ProgramLibrary ProgramLibrary =>
        _programLibrary ?? throw new InvalidOperationException("RustMod program library is not initialized");

    public override void OnLoaded(ContentHandler content) {
        base.OnLoaded(content);
        try {
            if (!_initialized) {
                MOD.Networking.Required = true;
                MOD.AddSaveDataType<RustProgramLibrarySaveData>();
                MOD.AddSaveDataType<RiscVChipSaveData>();
                MOD.Networking.RegisterMessage<ProgramUploadMessage>();
                MOD.Networking.RegisterMessage<ProgramLibrarySnapshotMessage>();
                MOD.Networking.RegisterMessage<ProgramRenameMessage>();
                MOD.Networking.RegisterMessage<ProgramDeleteMessage>();
                MOD.Networking.RegisterMessage<ChipAssignProgramMessage>();
                MOD.Networking.JoinSuffixSerializer = new RustLibraryJoinSync();
                MOD.AddPrefabs(content.prefabs);
                MOD.SetupPrefabs().IgnoreEmpty().SetBlueprintMaterials();
                MOD.SetupPrefabs("RustChip").SetPaintableColor(ColorType.Black);

                Debug.Log($"[{PluginName}] Loaded {content.prefabs.Count} prefabs");

                _initialized = true;
            }

            _harmony = new Harmony(PluginGuid);
            _harmony.CreateClassProcessor(typeof(CircuitHousingPatch)).Patch();
            _harmony.CreateClassProcessor(typeof(RustProgramLibrarySavePatch)).Patch();

            RustFFI.EnsureLoaded();

            Instance = this;
            _webServer = GameManager.IsBatchMode ? null : new WebServer();
            _programLibrary = _webServer?.ProgramLibrary() ?? new ProgramLibrary();

            Debug.LogWarning($"[{PluginName}] Loaded");
        } catch (Exception ex) {
            Debug.LogError($"[{PluginName}] Error during OnLoaded: {ex}");
        }
    }

    private void Update() {
        if (_webServer == null || _programLibrary == null) {
            return;
        }

        _webServer.ApplyPending();
        foreach (var upload in _webServer.DrainPendingUploads()) {
            if (NetworkManager.IsClient) {
                ModNetworkingExtensions.SendToHost(new ProgramUploadMessage {
                    Name = upload.Name,
                    Bytes = upload.Bytes,
                });
            } else if (NetworkManager.IsServer) {
                BroadcastLibrarySnapshot();
            }
        }
    }

    public byte[] CaptureLibraryState() {
        return _programLibrary?.Serialize() ?? Array.Empty<byte>();
    }

    public string CaptureLibraryStateBase64() {
        var bytes = CaptureLibraryState();
        return bytes.Length == 0 ? string.Empty : Convert.ToBase64String(bytes);
    }

    public void ApplyLibraryStateBase64(string base64) {
        if (_programLibrary == null || string.IsNullOrWhiteSpace(base64)) {
            return;
        }

        try {
            ApplyLibrarySnapshot(Convert.FromBase64String(base64));
        } catch (Exception ex) {
            Debug.LogError($"[{PluginName}] Failed to load saved RISC-V library: {ex}");
        }
    }

    public void ApplyLibrarySnapshot(byte[] state) {
        if (_programLibrary == null || state == null || state.Length == 0) {
            return;
        }

        if (!_programLibrary.Deserialize(state)) {
            Debug.LogError($"[{PluginName}] Failed to apply RISC-V program library snapshot");
        }
    }

    public ulong AcceptProgramUpload(string name, byte[] bytes, bool broadcast) {
        if (_programLibrary == null) {
            return 0;
        }

        var key = _programLibrary.AddElf(name, bytes);
        if (key != 0 && broadcast) {
            BroadcastLibrarySnapshot();
        }

        return key;
    }

    public void RenameProgram(ulong key, string name, bool broadcast) {
        if (_programLibrary != null && _programLibrary.Rename(key, name) && broadcast) {
            BroadcastLibrarySnapshot();
        }
    }

    public void DeleteProgram(ulong key, bool broadcast) {
        if (_programLibrary != null && _programLibrary.Delete(key) && broadcast) {
            BroadcastLibrarySnapshot();
        }
    }

    public void AssignProgram(RustChip chip, ulong programKey) {
        if (chip == null) {
            return;
        }

        if (NetworkManager.IsClient) {
            ModNetworkingExtensions.SendToHost(new ChipAssignProgramMessage {
                ChipId = chip.ReferenceId,
                ProgramKey = programKey,
            });
        } else {
            chip.AssignProgramServer(programKey);
        }
    }

    public void BroadcastLibrarySnapshot() {
        if (!NetworkManager.IsServer || _programLibrary == null) {
            return;
        }

        ModNetworkingExtensions.SendAll(new ProgramLibrarySnapshotMessage {
            LibraryState = _programLibrary.Serialize(),
        }, -1);
    }

    private void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
        _harmony?.UnpatchSelf();
        _programLibrary?.Dispose();
        _programLibrary = null;
        _webServer?.Dispose();
        _webServer = null;
    }
}
}
