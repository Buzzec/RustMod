using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Motherboards;
using Assets.Scripts.Objects.Pipes;
using UnityEngine;

namespace RustMod {
public class TestChip : Item, IProgrammable {
    public ErrorState ReadErrorState() {
        Debug.LogError("RustChip::ErrorState()");
        return ErrorState.None;
    }

    public string Execute(ICircuitHolder circuitHolder) {
        Debug.LogError("RustChip::Execute()");
        return null;
    }

    public void AppendErrorsToActionInstance(DelayedActionInstance actionInstance, Interactable interactable,
        Interaction interaction, bool doAction) {
        Debug.LogError(
            $"RustChip::AppendErrorsToActionInstance(${actionInstance}, {interactable.Action}, {interaction}, {doAction})");
    }

    public double? ChipGetLogicValue(LogicType logicType) {
        Debug.LogError($"RustChip::GetLogicValue(${logicType})");
        return null;
    }

    public void SetSourceCode(string sourceCode) {
        Debug.LogError($"RustChip::SetSourceCode(${sourceCode})");
    }

    public string GetSourceCode() {
        Debug.LogError($"RustChip::GetSourceCode()");
        return "";
    }

    public void Placed(Device device) {
        Debug.LogError($"RustChip::Placed(${device.name})");
    }

    public void Unplaced(Device device) {
        Debug.LogError($"RustChip::Unplaced(${device.name})");
    }

    public double ReadMemory(int address) {
        Debug.LogError($"RustChip::ReadMemory(${address})");
        return 0;
    }

    public void WriteMemory(int address, double value) {
        Debug.LogError($"RustChip::WriteMemory(${address}, {value})");
    }

    public void ClearMemory() {
        Debug.LogError($"RustChip::ClearMemory()");
    }
}
}