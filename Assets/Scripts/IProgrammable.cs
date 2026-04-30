using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Motherboards;
using Assets.Scripts.Objects.Pipes;
using JetBrains.Annotations;

namespace RustMod {
public interface IProgrammable : IReferencable {
    public ErrorState ReadErrorState();

    /// <summary>
    /// Executes the chip. Base game limits to 128 instructions.
    /// </summary>
    /// <returns>The error if encountered, null otherwise.</returns>
    [CanBeNull]
    public string Execute(ICircuitHolder holder);

    public void AppendErrorsToActionInstance(Thing.DelayedActionInstance actionInstance, Interactable interactable,
        Interaction interaction, bool doAction);

    public double? ChipGetLogicValue(LogicType logicType);
    public void SetLogicValue(LogicType logicType, double value);
    public void SetSourceCode(string sourceCode);
    public string GetSourceCode();

    /// <summary>
    /// Called when placed into a slot.
    ///
    /// Implementers should reset the chip.
    /// </summary>
    public void Placed(Device device);

    /// <summary>
    /// Called when removed from a slot.
    ///
    /// Implementers should reset the chip.
    /// </summary>
    public void Unplaced(Device device);

    public double ReadMemory(int address);
    public void WriteMemory(int address, double value);
    public void ClearMemory();
}
}