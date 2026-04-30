using Assets.Scripts.Objects;
using System.Xml.Serialization;

namespace RustMod {
public sealed class RustProgramLibrarySaveData : ThingSaveData {
    public const string SyntheticPrefabName = "__RustModProgramLibrary";

    [XmlElement]
    public string LibraryState = string.Empty;

    public RustProgramLibrarySaveData() {
        PrefabName = SyntheticPrefabName;
    }
}
}
