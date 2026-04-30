using Assets.Scripts.Objects;
using System.Xml.Serialization;

namespace RustMod {
public sealed class RiscVChipSaveData : DynamicThingSaveData {
    [XmlElement]
    public ulong ProgramKey;

    [XmlElement]
    public string ExecutionState = string.Empty;

    [XmlElement]
    public int ErrorState;

    [XmlElement]
    public string LastMessage = string.Empty;

    [XmlElement]
    public double SleepRemainingSeconds;
}
}
