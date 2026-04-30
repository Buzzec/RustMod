using System;
using Assets.Scripts;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Motherboards;
using Assets.Scripts.Objects.Pipes;
using UnityEngine;

namespace RustMod {
public class RustChip : Item, IProgrammable {
    private const ushort ProgramNetworkFlag = 1024;
    private const ulong ClocksPerTick = 128;

    private Execution _execution;
    private ulong _programKey;
    private ErrorState _errorState = ErrorState.None;
    private string _lastMessage = string.Empty;

    public ErrorState ReadErrorState() {
        return _errorState;
    }

    public string Execute(ICircuitHolder circuitHolder) {
        if (NetworkManager.IsClient || _execution == null || RustMod.Instance == null) {
            return null;
        }

        var result = _execution.Execute(ClocksPerTick, RustMod.Instance.ProgramLibrary);
        switch (result.Status) {
            case ExecutionStatus.OutOfClocks:
            case ExecutionStatus.Yield:
                ClearRuntimeError();
                return null;
            case ExecutionStatus.Log:
                _lastMessage = result.Message ?? string.Empty;
                MarkProgramDirty();
                return null;
            case ExecutionStatus.Exit:
                _lastMessage = $"Exited with status {result.Value}";
                MarkProgramDirty();
                return null;
            case ExecutionStatus.Hcf:
                SetRuntimeError("Halt and catch fire");
                return _lastMessage;
            case ExecutionStatus.Error:
                SetRuntimeError(result.Message);
                return _lastMessage;
            case ExecutionStatus.Sleep:
                return null;
            default:
                return null;
        }
    }

    public void AppendErrorsToActionInstance(DelayedActionInstance actionInstance, Interactable interactable,
        Interaction interaction, bool doAction) {
        if (_errorState != ErrorState.None && !string.IsNullOrEmpty(_lastMessage)) {
            actionInstance.AppendStateMessage(_lastMessage);
        }
    }

    public double? ChipGetLogicValue(LogicType logicType) {
        return logicType == LogicType.Setting ? _programKey : null;
    }

    public new void SetLogicValue(LogicType logicType, double value) {
        if (logicType == LogicType.Setting) {
            RustMod.Instance?.AssignProgram(this, (ulong)Math.Max(0, value));
        }
    }

    public void SetSourceCode(string sourceCode) {
        if (ulong.TryParse(sourceCode, out var key)) {
            RustMod.Instance?.AssignProgram(this, key);
        }
    }

    public string GetSourceCode() {
        return _programKey == 0 ? string.Empty : _programKey.ToString();
    }

    public void AssignProgramServer(ulong programKey) {
        if (RustMod.Instance == null) {
            return;
        }

        try {
            _execution?.Dispose();
            _execution = programKey == 0 ? null : new Execution(RustMod.Instance.ProgramLibrary, programKey);
            _programKey = programKey;
            _errorState = ErrorState.None;
            _lastMessage = string.Empty;
        } catch (Exception ex) {
            _execution = null;
            _programKey = programKey;
            SetRuntimeError(ex.Message);
        }

        MarkProgramDirty();
    }

    public void Placed(Device device) {
        ResetRuntime();
    }

    public void Unplaced(Device device) {
        ResetRuntime();
    }

    public double ReadMemory(int address) {
        return 0;
    }

    public void WriteMemory(int address, double value) { }

    public void ClearMemory() {
        ResetRuntime();
    }

    public override ThingSaveData SerializeSave() {
        ThingSaveData savedData = new RiscVChipSaveData();
        InitialiseSaveData(ref savedData);
        return savedData;
    }

    protected override void InitialiseSaveData(ref ThingSaveData savedData) {
        base.InitialiseSaveData(ref savedData);
        if (savedData is not RiscVChipSaveData chipSaveData) {
            return;
        }

        chipSaveData.ProgramKey = _programKey;
        chipSaveData.ExecutionState = CaptureExecutionBase64();
        chipSaveData.ErrorState = (int)_errorState;
        chipSaveData.LastMessage = _lastMessage ?? string.Empty;
    }

    public override void DeserializeSave(ThingSaveData savedData) {
        base.DeserializeSave(savedData);
        if (savedData is not RiscVChipSaveData chipSaveData) {
            return;
        }

        _programKey = chipSaveData.ProgramKey;
        _errorState = (ErrorState)chipSaveData.ErrorState;
        _lastMessage = chipSaveData.LastMessage ?? string.Empty;
        RestoreExecutionBase64(chipSaveData.ExecutionState);
    }

    public override void SerializeOnJoin(RocketBinaryWriter writer) {
        base.SerializeOnJoin(writer);
        writer.WriteUInt64(_programKey);
        writer.WriteInt32((int)_errorState);
        writer.WriteString(_lastMessage ?? string.Empty);
        Net.MessageIO.WriteBytes(writer, CaptureExecutionBytes());
    }

    public override void DeserializeOnJoin(RocketBinaryReader reader) {
        base.DeserializeOnJoin(reader);
        _programKey = reader.ReadUInt64();
        _errorState = (ErrorState)reader.ReadInt32();
        _lastMessage = reader.ReadString();
        RestoreExecutionBytes(Net.MessageIO.ReadBytes(reader));
    }

    public override void BuildUpdate(RocketBinaryWriter writer, ushort networkUpdateType) {
        base.BuildUpdate(writer, networkUpdateType);
        if (!Thing.IsNetworkUpdateRequired(ProgramNetworkFlag, networkUpdateType)) {
            return;
        }

        writer.WriteUInt64(_programKey);
        writer.WriteInt32((int)_errorState);
        writer.WriteString(_lastMessage ?? string.Empty);
        Net.MessageIO.WriteBytes(writer, CaptureExecutionBytes());
    }

    public override void ProcessUpdate(RocketBinaryReader reader, ushort networkUpdateType) {
        base.ProcessUpdate(reader, networkUpdateType);
        if (!Thing.IsNetworkUpdateRequired(ProgramNetworkFlag, networkUpdateType)) {
            return;
        }

        _programKey = reader.ReadUInt64();
        _errorState = (ErrorState)reader.ReadInt32();
        _lastMessage = reader.ReadString();
        RestoreExecutionBytes(Net.MessageIO.ReadBytes(reader));
    }

    private void ResetRuntime() {
        if (_programKey != 0 && RustMod.Instance != null && !NetworkManager.IsClient) {
            AssignProgramServer(_programKey);
        }
    }

    private void SetRuntimeError(string message) {
        _errorState = ErrorState.RuntimeError;
        _lastMessage = message ?? string.Empty;
        MarkProgramDirty();
    }

    private void ClearRuntimeError() {
        if (_errorState == ErrorState.None) {
            return;
        }

        _errorState = ErrorState.None;
        _lastMessage = string.Empty;
        MarkProgramDirty();
    }

    private void MarkProgramDirty() {
        if (NetworkManager.IsServer) {
            NetworkUpdateFlags |= ProgramNetworkFlag;
        }
    }

    private string CaptureExecutionBase64() {
        var bytes = CaptureExecutionBytes();
        return bytes.Length == 0 ? string.Empty : Convert.ToBase64String(bytes);
    }

    private byte[] CaptureExecutionBytes() {
        try {
            return _execution?.Serialize() ?? Array.Empty<byte>();
        } catch (Exception ex) {
            Debug.LogError($"[RustMod] Failed to serialize RISC-V execution: {ex}");
            return Array.Empty<byte>();
        }
    }

    private void RestoreExecutionBase64(string base64) {
        if (string.IsNullOrWhiteSpace(base64)) {
            _execution = null;
            return;
        }

        try {
            RestoreExecutionBytes(Convert.FromBase64String(base64));
        } catch (Exception ex) {
            _execution = null;
            SetRuntimeError($"Failed to load execution state: {ex.Message}");
        }
    }

    private void RestoreExecutionBytes(byte[] bytes) {
        _execution?.Dispose();
        _execution = null;
        if (bytes == null || bytes.Length == 0) {
            return;
        }

        try {
            _execution = Execution.Deserialize(bytes);
            _programKey = _execution.ProgramKey;
        } catch (Exception ex) {
            SetRuntimeError($"Failed to load execution state: {ex.Message}");
        }
    }
}
}
