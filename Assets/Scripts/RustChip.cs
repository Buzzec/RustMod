using System;
using Assets.Scripts;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Motherboards;
using Assets.Scripts.Objects.Pipes;
using Assets.Scripts.Util;
using Cysharp.Threading.Tasks;
using Reagents;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RustMod {
public class RustChip : Item, IProgrammable {
    private const ushort ProgramNetworkFlag = 1024;
    private const ulong ClocksPerTick = 1024 * 1024 * 1024;

    private Execution _execution;
    private ulong? _programKey;
    private ErrorState _errorState = ErrorState.None;
    private string _lastMessage = string.Empty;
    private double _sleepUntilTime;

    private ICircuitHolder _circuitHolder = null;

    public ErrorState ReadErrorState() {
        return _errorState;
    }

    public void Execute() {
        if (IsSleeping()) {
            return;
        }

        var clocks = ClocksPerTick;
        while (!NetworkManager.IsClient && _execution != null && RustMod.Instance != null && _circuitHolder != null &&
               _errorState == ErrorState.None && clocks > 0) {
            var result = _execution.Execute(ref clocks, RustMod.Instance.ProgramLibrary);
            switch (result.Status) {
                case ExecutionStatus.OutOfClocks:
                case ExecutionStatus.Yield:
                    return;
                case ExecutionStatus.Exit:
                    _lastMessage = $"Exited with status {result.Value}";
                    _errorState = ErrorState.Exit;
                    MarkProgramDirty();
                    return;
                case ExecutionStatus.Hcf:
                    HaltAndCatchFire(_circuitHolder);
                    _errorState = ErrorState.Hcf;
                    MarkProgramDirty();
                    return;
                case ExecutionStatus.Error:
                    SetRuntimeError(result.Message);
                    return;
                case ExecutionStatus.Sleep:
                    ScheduleSleep(result.Value.ToUInt64());
                    return;
                case ExecutionStatus.HostSyscall:
                    HandleHostSyscall();
                    break;
                default:
                    return;
            }
        }
    }

    private bool IsSleeping() => SleepRemainingSeconds() > 0;

    private void ScheduleSleep(ulong secondsF64Bits) {
        var seconds = BitConverter.Int64BitsToDouble(unchecked((long)secondsF64Bits));
        if (double.IsNaN(seconds) || seconds <= 0) {
            _sleepUntilTime = CurrentTime;
            return;
        }

        _sleepUntilTime = double.IsPositiveInfinity(seconds)
            ? double.MaxValue
            : Math.Min(double.MaxValue, CurrentTime + seconds);
        MarkProgramDirty();
    }

    private double SleepRemainingSeconds() => Math.Max(0, _sleepUntilTime - CurrentTime);

    private void RestoreSleep(double remainingSeconds) {
        _sleepUntilTime = remainingSeconds > 0 ? CurrentTime + remainingSeconds : 0;
    }

    private static double CurrentTime => GameManager.GameTime;

    private void HandleHostSyscall() {
        try {
            switch (_execution.SyscallId) {
                case 0x10_00: ReturnDeviceRegisters(); break;
                case 0x10_01: _execution.Return1((ulong)GetDevices().Count); break;
                case 0x10_02: ReturnDeviceList(full: false); break;
                case 0x10_03: ReturnDeviceList(full: true); break;
                case 7: _execution.Return1(DoubleBits(CurrentTime)); break;
                case 0x10_10: ReturnDeviceMeta(0); break;
                case 0x10_11: ReturnDeviceMeta(1); break;
                case 0x10_12: ReturnDeviceMeta(2); break;
                case 0x10_13: ReturnDeviceNameLength(); break;
                case 0x10_14: ReturnDeviceName(); break;
                case 0x10_15: ReturnCanLogic(subDevice: false, write: false); break;
                case 0x10_16: ReturnCanLogic(subDevice: false, write: true); break;
                case 0x10_17: ReturnCanLogic(subDevice: true, write: false); break;
                case 0x10_18: ReturnCanLogic(subDevice: true, write: true); break;
                case 0x11_00:
                    ReturnReadDevice(DeviceByRegister(_execution.SyscallArg(0)), _execution.SyscallArg(1)); break;
                case 0x11_01: ReturnReadDevice(DeviceById(_execution.SyscallArg(0)), _execution.SyscallArg(1)); break;
                case 0x11_02:
                    ReturnReadDevice(SubDevice(DeviceByRegister(_execution.SyscallArg(0)), _execution.SyscallArg(1)),
                        _execution.SyscallArg(2)); break;
                case 0x11_03:
                    ReturnReadDevice(SubDevice(DeviceById(_execution.SyscallArg(0)), _execution.SyscallArg(1)),
                        _execution.SyscallArg(2)); break;
                case 0x11_04: ReturnReadBatch(subDevice: false, named: false); break;
                case 0x11_05: ReturnReadBatch(subDevice: true, named: false); break;
                case 0x11_06: ReturnReadBatch(subDevice: false, named: true); break;
                case 0x11_07: ReturnReadBatch(subDevice: true, named: true); break;
                case 0x11_10:
                    ReturnWriteDevice(DeviceByRegister(_execution.SyscallArg(0)), _execution.SyscallArg(1),
                        _execution.SyscallArg(2)); break;
                case 0x11_11:
                    ReturnWriteDevice(DeviceById(_execution.SyscallArg(0)), _execution.SyscallArg(1),
                        _execution.SyscallArg(2)); break;
                case 0x11_12:
                    ReturnWriteDevice(SubDevice(DeviceByRegister(_execution.SyscallArg(0)), _execution.SyscallArg(1)),
                        _execution.SyscallArg(2), _execution.SyscallArg(3)); break;
                case 0x11_13:
                    ReturnWriteDevice(SubDevice(DeviceById(_execution.SyscallArg(0)), _execution.SyscallArg(1)),
                        _execution.SyscallArg(2), _execution.SyscallArg(3)); break;
                case 0x11_14: ReturnWriteBatch(subDevice: false, named: false); break;
                case 0x11_15: ReturnWriteBatch(subDevice: true, named: false); break;
                case 0x11_16: ReturnWriteBatch(subDevice: false, named: true); break;
                case 0x11_17: ReturnWriteBatch(subDevice: true, named: true); break;
                case 0x11_20:
                    ReturnReadReagent(DeviceByRegister(_execution.SyscallArg(0)), _execution.SyscallArg(1),
                        _execution.SyscallArg(2)); break;
                case 0x11_21:
                    ReturnReadReagent(DeviceById(_execution.SyscallArg(0)), _execution.SyscallArg(1),
                        _execution.SyscallArg(2)); break;
                case 0x11_30: ReturnClearStack(DeviceByRegister(_execution.SyscallArg(0))); break;
                case 0x11_31: ReturnClearStack(DeviceById(_execution.SyscallArg(0))); break;
                case 0x11_32:
                    ReturnPutStack(DeviceByRegister(_execution.SyscallArg(0)), _execution.SyscallArg(1),
                        _execution.SyscallArg(2)); break;
                case 0x11_33:
                    ReturnPutStack(DeviceById(_execution.SyscallArg(0)), _execution.SyscallArg(1),
                        _execution.SyscallArg(2)); break;
                case 0x11_34:
                    ReturnGetStack(DeviceByRegister(_execution.SyscallArg(0)), _execution.SyscallArg(1)); break;
                case 0x11_35: ReturnGetStack(DeviceById(_execution.SyscallArg(0)), _execution.SyscallArg(1)); break;
                default: _execution.ReturnError(SyscallError.InvalidSyscall); break;
            }
        } catch (Exception ex) {
            Debug.LogWarning($"[RustMod] Host syscall failed: {ex}");
            _execution.ReturnError(SyscallError.UnknownError);
        }
    }

    private void ReturnDeviceRegisters() {
        _execution.Return6(
            DeviceByRegister(0)?.GetAsThing.ReferenceId ?? 0,
            DeviceByRegister(1)?.GetAsThing.ReferenceId ?? 0,
            DeviceByRegister(2)?.GetAsThing.ReferenceId ?? 0,
            DeviceByRegister(3)?.GetAsThing.ReferenceId ?? 0,
            DeviceByRegister(4)?.GetAsThing.ReferenceId ?? 0,
            DeviceByRegister(5)?.GetAsThing.ReferenceId ?? 0
        );
    }

    private void ReturnDeviceList(bool full) {
        var ptr = _execution.SyscallArg(0);
        var len = (int)Math.Min(_execution.SyscallArg(1), 16UL);
        var offset = (int)Math.Min(_execution.SyscallArg(2), int.MaxValue);
        var devices = GetDevices();
        var count = Math.Max(0, Math.Min(len, devices.Count - Math.Min(offset, devices.Count)));
        var bytes = new byte[count * (full ? 32 : 8)];
        for (var i = 0; i < count; i++) {
            var device = devices[offset + i];
            if (full) {
                WriteUInt64(bytes, i * 32, (ulong)device.GetAsThing.ReferenceId);
                WriteUInt64(bytes, i * 32 + 8, (ulong)device.TotalSlots);
                WriteInt64(bytes, i * 32 + 16, device.GetPrefabHash());
                WriteInt64(bytes, i * 32 + 24, device.GetNameHash());
            } else {
                WriteUInt64(bytes, i * 8, (ulong)device.GetAsThing.ReferenceId);
            }
        }

        ReturnMemoryWrite(ptr, bytes, (ulong)count);
    }

    private void ReturnDeviceMeta(int field) {
        var device = DeviceById(_execution.SyscallArg(0));
        if (device == null) {
            _execution.ReturnError(SyscallError.DeviceNotFound);
            return;
        }

        _execution.Return1(field switch {
            0 => (ulong)device.TotalSlots,
            1 => unchecked((ulong)device.GetPrefabHash()),
            _ => unchecked((ulong)device.GetNameHash()),
        });
    }

    private void ReturnDeviceNameLength() {
        var device = DeviceById(_execution.SyscallArg(0));
        if (device == null) {
            _execution.ReturnError(SyscallError.DeviceNotFound);
            return;
        }

        _execution.Return1((ulong)Encoding.UTF8.GetByteCount(DeviceName(device)));
    }

    private void ReturnDeviceName() {
        var device = DeviceById(_execution.SyscallArg(0));
        if (device == null) {
            _execution.ReturnError(SyscallError.DeviceNotFound);
            return;
        }

        var ptr = _execution.SyscallArg(1);
        var len = (int)Math.Min(_execution.SyscallArg(2), 128UL);
        var offset = (int)Math.Min(_execution.SyscallArg(3), int.MaxValue);
        var nameBytes = Encoding.UTF8.GetBytes(DeviceName(device));
        var count = Math.Max(0, Math.Min(len, nameBytes.Length - Math.Min(offset, nameBytes.Length)));
        var bytes = new byte[count];
        Array.Copy(nameBytes, offset, bytes, 0, count);
        ReturnMemoryWrite(ptr, bytes, (ulong)count);
    }

    private void ReturnCanLogic(bool subDevice, bool write) {
        var device = subDevice
            ? SubDevice(DeviceById(_execution.SyscallArg(0)), _execution.SyscallArg(1))
            : DeviceById(_execution.SyscallArg(0));
        var logic = subDevice ? _execution.SyscallArg(2) : _execution.SyscallArg(1);
        if (device == null || !TryLogicType(logic, out var logicType)) {
            _execution.ReturnError(device == null ? SyscallError.DeviceNotFound : SyscallError.InvalidSyscall);
            return;
        }

        _execution.Return1((write ? device.CanLogicWrite(logicType) : device.CanLogicRead(logicType)) ? 1UL : 0UL);
    }

    private void ReturnReadDevice(ILogicable device, ulong logic) {
        if (device == null) {
            _execution.ReturnError(SyscallError.DeviceNotFound);
            return;
        }

        if (!TryLogicType(logic, out var logicType) || !device.CanLogicRead(logicType)) {
            _execution.ReturnError(SyscallError.InvalidSyscall);
            return;
        }

        _execution.Return1(DoubleBits(device.GetLogicValue(logicType)));
    }

    private void ReturnWriteDevice(ILogicable device, ulong logic, ulong valueBits) {
        if (device == null) {
            _execution.ReturnError(SyscallError.DeviceNotFound);
            return;
        }

        if (!TryLogicType(logic, out var logicType) || !device.CanLogicWrite(logicType)) {
            _execution.ReturnError(SyscallError.InvalidSyscall);
            return;
        }

        device.SetLogicValue(logicType, BitConverter.Int64BitsToDouble(unchecked((long)valueBits)));
        _execution.Return1(0);
    }

    private void ReturnReadBatch(bool subDevice, bool named) {
        var devices = MatchingDevices(_execution.SyscallArg(0), named ? _execution.SyscallArg(1) : null);
        var slotIndex = subDevice ? _execution.SyscallArg(named ? 2 : 1) : 0;
        var logicArg = _execution.SyscallArg(named ? (subDevice ? 3 : 2) : (subDevice ? 2 : 1));
        var modeArg = _execution.SyscallArg(named ? (subDevice ? 4 : 3) : (subDevice ? 3 : 2));
        if (!TryLogicType(logicArg, out var logicType) || !TryBatchMode(modeArg, out var mode)) {
            _execution.ReturnError(SyscallError.InvalidSyscall);
            return;
        }

        var values = new List<double>();
        foreach (var device in devices) {
            var target = subDevice ? SubDevice(device, slotIndex) : device;
            if (target != null && target.CanLogicRead(logicType)) {
                values.Add(target.GetLogicValue(logicType));
            }
        }

        if (values.Count == 0) {
            _execution.ReturnError(SyscallError.DeviceNotFound);
            return;
        }

        _execution.Return1(DoubleBits(ApplyBatch(values, mode)));
    }

    private void ReturnWriteBatch(bool subDevice, bool named) {
        var devices = MatchingDevices(_execution.SyscallArg(0), named ? _execution.SyscallArg(1) : null);
        var slotIndex = subDevice ? _execution.SyscallArg(named ? 2 : 1) : 0;
        var logicArg = _execution.SyscallArg(named ? (subDevice ? 3 : 2) : (subDevice ? 2 : 1));
        var valueBits = _execution.SyscallArg(named ? (subDevice ? 4 : 3) : (subDevice ? 3 : 2));
        if (!TryLogicType(logicArg, out var logicType)) {
            _execution.ReturnError(SyscallError.InvalidSyscall);
            return;
        }

        var wrote = false;
        var value = BitConverter.Int64BitsToDouble(unchecked((long)valueBits));
        foreach (var device in devices) {
            var target = subDevice ? SubDevice(device, slotIndex) : device;
            if (target != null && target.CanLogicWrite(logicType)) {
                target.SetLogicValue(logicType, value);
                wrote = true;
            }
        }

        if (!wrote) {
            _execution.ReturnError(SyscallError.DeviceNotFound);
            return;
        }

        _execution.Return1(0);
    }

    private void ReturnReadReagent(ILogicable device, ulong mode, ulong reagentHash) {
        var thing = device?.GetAsThing;
        if (thing == null || !TryReagentMode(mode, out var reagentMode)) {
            _execution.ReturnError(thing == null ? SyscallError.DeviceNotFound : SyscallError.InvalidSyscall);
            return;
        }

        if (!thing.HasReadableReagentMixture) {
            _execution.ReturnError(SyscallError.InvalidSyscall);
            return;
        }

        var mixture = thing.ReadableReagentMixture;
        if (reagentMode == LogicReagentMode.TotalContents) {
            _execution.Return1(DoubleBits(mixture.TotalReagents));
            return;
        }

        if (reagentMode != LogicReagentMode.Contents) {
            _execution.ReturnError(SyscallError.InvalidSyscall);
            return;
        }

        var reagent = Reagent.Find(unchecked((int)reagentHash));
        if (reagent == null) {
            _execution.ReturnError(SyscallError.InvalidSyscall);
            return;
        }

        var value = mixture.Get(reagent);
        _execution.Return1(DoubleBits(value));
    }

    private void ReturnClearStack(ILogicable device) {
        if (device?.GetAsThing is CircuitHousing housing) {
            housing.ClearMemory();
            _execution.Return1(0);
        } else {
            _execution.ReturnError(SyscallError.DeviceNotFound);
        }
    }

    private void ReturnPutStack(ILogicable device, ulong address, ulong valueBits) {
        if (device?.GetAsThing is CircuitHousing housing) {
            housing.WriteMemory(unchecked((int)address), BitConverter.Int64BitsToDouble(unchecked((long)valueBits)));
            _execution.Return1(0);
        } else {
            _execution.ReturnError(SyscallError.DeviceNotFound);
        }
    }

    private void ReturnGetStack(ILogicable device, ulong address) {
        if (device?.GetAsThing is CircuitHousing housing) {
            _execution.Return1(DoubleBits(housing.ReadMemory(unchecked((int)address))));
        } else {
            _execution.ReturnError(SyscallError.DeviceNotFound);
        }
    }

    private List<ILogicable> GetDevices() => _circuitHolder?.GetBatchOutput() ?? new List<ILogicable>();

    private ILogicable DeviceByRegister(ulong index) => _circuitHolder?.GetLogicableFromIndex((int)index);

    private static ILogicable DeviceById(ulong id) => Thing.Find(unchecked((long)id)) as ILogicable;

    private static ILogicable SubDevice(ILogicable device, ulong slotIndex) =>
        device?.GetSlot((int)slotIndex)?.Get<ILogicable>();

    private IEnumerable<ILogicable> MatchingDevices(ulong prefabHash, ulong? nameHash) {
        var prefab = unchecked((int)prefabHash);
        var nameHashInt = nameHash.HasValue ? unchecked((int)nameHash.Value) : 0;
        foreach (var device in GetDevices()) {
            if (device.GetPrefabHash() == prefab && (!nameHash.HasValue || device.GetNameHash() == nameHashInt)) {
                yield return device;
            }
        }
    }

    private static string DeviceName(ILogicable device) => device.GetAsThing.DisplayName ?? string.Empty;

    private void ReturnMemoryWrite(ulong ptr, byte[] bytes, ulong ret) {
        if (!_execution.WriteMemory(ptr, bytes)) {
            _execution.ReturnError(SyscallError.InvalidSyscall);
        } else {
            _execution.Return1(ret);
        }
    }

    private static bool TryLogicType(ulong raw, out LogicType logicType) {
        logicType = (LogicType)raw;
        return Enum.IsDefined(typeof(LogicType), logicType);
    }

    private static bool TryBatchMode(ulong raw, out LogicBatchMethod mode) {
        mode = (LogicBatchMethod)raw;
        return Enum.IsDefined(typeof(LogicBatchMethod), mode);
    }

    private static bool TryReagentMode(ulong raw, out LogicReagentMode mode) {
        mode = (LogicReagentMode)raw;
        return Enum.IsDefined(typeof(LogicReagentMode), mode);
    }

    private static double ApplyBatch(List<double> values, LogicBatchMethod mode) {
        var result = values[0];

        result = values.Aggregate(result, (current, value) => mode switch {
            LogicBatchMethod.Minimum => Math.Min(current, value),
            LogicBatchMethod.Maximum => Math.Max(current, value),
            _ => current + value,
        });

        return mode == LogicBatchMethod.Average ? result / values.Count : result;
    }

    private static ulong DoubleBits(double value) => unchecked((ulong)BitConverter.DoubleToInt64Bits(value));

    private static void WriteUInt64(byte[] bytes, int offset, ulong value) {
        Array.Copy(BitConverter.GetBytes(value), 0, bytes, offset, 8);
    }

    private static void WriteInt64(byte[] bytes, int offset, long value) {
        Array.Copy(BitConverter.GetBytes(value), 0, bytes, offset, 8);
    }

    private async UniTaskVoid HaltAndCatchFireFromThread(ICircuitHolder circuitHolder) {
        await UniTask.SwitchToMainThread();
        HaltAndCatchFire(circuitHolder);
    }

    private void HaltAndCatchFire(ICircuitHolder circuitHolder) {
        if (GameManager.IsThread) {
            HaltAndCatchFireFromThread(circuitHolder).Forget();
        } else {
            IsBurning = true;
            OnFireStart();
            circuitHolder.HaltAndCatchFire();
            Achievements.AchieveHaltAndCatchFire();
        }
    }

    public void AppendErrorsToActionInstance(DelayedActionInstance actionInstance, Interactable interactable,
        Interaction interaction, bool doAction) {
        actionInstance.AppendStateMessage($"Loaded Program: {(_programKey.HasValue ? _programKey.Value : "None")}");
        if (_errorState != ErrorState.None && !string.IsNullOrEmpty(_lastMessage)) {
            actionInstance.AppendStateMessage(_lastMessage);
        }
    }

    public double? ChipGetLogicValue(LogicType logicType) {
        throw new NotImplementedException();
    }

    public void SetSourceCode(string sourceCode) {
        throw new NotImplementedException();
    }

    public string GetSourceCode() {
        throw new NotImplementedException();
    }

    public void AssignProgramServer(ulong? programKey) {
        if (RustMod.Instance == null) {
            return;
        }

        try {
            _execution?.Dispose();
            _execution = programKey.HasValue ? new Execution(RustMod.Instance.ProgramLibrary, programKey.Value) : null;
            _programKey = programKey;
            _errorState = ErrorState.None;
            _lastMessage = string.Empty;
            _sleepUntilTime = 0;
        } catch (Exception ex) {
            _execution = null;
            _programKey = programKey;
            SetRuntimeError(ex.Message);
        }

        MarkProgramDirty();
    }

    public void Placed(ICircuitHolder circuitHolder) {
        _circuitHolder = circuitHolder;
        ResetRuntime();
    }

    public void Unplaced(ICircuitHolder circuitHolder) {
        _circuitHolder = null;
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
        chipSaveData.SleepRemainingSeconds = SleepRemainingSeconds();
    }

    public override void DeserializeSave(ThingSaveData savedData) {
        base.DeserializeSave(savedData);
        if (savedData is not RiscVChipSaveData chipSaveData) {
            return;
        }

        _programKey = chipSaveData.ProgramKey;
        _errorState = (ErrorState)chipSaveData.ErrorState;
        _lastMessage = chipSaveData.LastMessage ?? string.Empty;
        RestoreSleep(chipSaveData.SleepRemainingSeconds);
        RestoreExecutionBase64(chipSaveData.ExecutionState);
    }

    public override void SerializeOnJoin(RocketBinaryWriter writer) {
        base.SerializeOnJoin(writer);
        if (_programKey.HasValue) {
            writer.WriteBoolean(true);
            writer.WriteUInt64(_programKey.Value);
        } else {
            writer.WriteBoolean(false);
        }
        writer.WriteInt32((int)_errorState);
        writer.WriteString(_lastMessage ?? string.Empty);
        writer.WriteUInt64(DoubleBits(SleepRemainingSeconds()));
        Net.MessageIO.WriteBytes(writer, CaptureExecutionBytes());
    }

    public override void DeserializeOnJoin(RocketBinaryReader reader) {
        base.DeserializeOnJoin(reader);
        if (reader.ReadBoolean()) {
            _programKey = reader.ReadUInt64();
        } else {
            _programKey = null;
        }
        _errorState = (ErrorState)reader.ReadInt32();
        _lastMessage = reader.ReadString();
        RestoreSleep(BitConverter.Int64BitsToDouble(unchecked((long)reader.ReadUInt64())));
        RestoreExecutionBytes(Net.MessageIO.ReadBytes(reader));
    }

    public override void BuildUpdate(RocketBinaryWriter writer, ushort networkUpdateType) {
        base.BuildUpdate(writer, networkUpdateType);
        if (!Thing.IsNetworkUpdateRequired(ProgramNetworkFlag, networkUpdateType)) {
            return;
        }

        if (_programKey.HasValue) {
            writer.WriteBoolean(true);
            writer.WriteUInt64(_programKey.Value);
        } else {
            writer.WriteBoolean(false);
        }
        writer.WriteInt32((int)_errorState);
        writer.WriteString(_lastMessage ?? string.Empty);
        writer.WriteUInt64(DoubleBits(SleepRemainingSeconds()));
        Net.MessageIO.WriteBytes(writer, CaptureExecutionBytes());
    }

    public override void ProcessUpdate(RocketBinaryReader reader, ushort networkUpdateType) {
        base.ProcessUpdate(reader, networkUpdateType);
        if (!Thing.IsNetworkUpdateRequired(ProgramNetworkFlag, networkUpdateType)) {
            return;
        }

        if (reader.ReadBoolean()) {
            _programKey = reader.ReadUInt64();
        } else {
            _programKey = null;
        }
        _errorState = (ErrorState)reader.ReadInt32();
        _lastMessage = reader.ReadString();
        RestoreSleep(BitConverter.Int64BitsToDouble(unchecked((long)reader.ReadUInt64())));
        RestoreExecutionBytes(Net.MessageIO.ReadBytes(reader));
    }

    private void ResetRuntime() {
        if (_programKey != 0 && RustMod.Instance != null && !NetworkManager.IsClient) {
            AssignProgramServer(_programKey);
        }
    }

    private void SetRuntimeError(string message) {
        _circuitHolder?.RaiseError(1);
        _errorState = ErrorState.RuntimeError;
        _lastMessage = message ?? string.Empty;
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