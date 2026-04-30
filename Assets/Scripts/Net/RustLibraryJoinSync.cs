using Assets.Scripts.Networking;
using LaunchPadBooster.Networking;

namespace RustMod.Net {
internal sealed class RustLibraryJoinSync : IJoinSuffixSerializer {
    public void SerializeJoinSuffix(RocketBinaryWriter writer) {
        MessageIO.WriteBytes(writer, RustMod.Instance?.CaptureLibraryState());
    }

    public void DeserializeJoinSuffix(RocketBinaryReader reader) {
        RustMod.Instance?.ApplyLibrarySnapshot(MessageIO.ReadBytes(reader));
    }
}
}
