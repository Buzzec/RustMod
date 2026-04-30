using Assets.Scripts;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using LaunchPadBooster.Networking;

namespace RustMod.Net {
internal static class MessageIO {
    public static byte[] ReadBytes(RocketBinaryReader reader) {
        var len = reader.ReadInt32();
        if (len <= 0) {
            return new byte[0];
        }

        var bytes = new byte[len];
        for (var i = 0; i < len; i++) {
            bytes[i] = reader.ReadByte();
        }

        return bytes;
    }

    public static void WriteBytes(RocketBinaryWriter writer, byte[] bytes) {
        bytes ??= new byte[0];
        writer.WriteInt32(bytes.Length);
        for (var i = 0; i < bytes.Length; i++) {
            writer.WriteByte(bytes[i]);
        }
    }
}

internal sealed class ProgramUploadMessage : INetworkMessage {
    public string Name = string.Empty;
    public byte[] Bytes = new byte[0];

    public void Process(long clientId) {
        if (NetworkManager.IsServer) {
            RustMod.Instance?.AcceptProgramUpload(Name, Bytes, broadcast: true);
        }
    }

    public void Deserialize(RocketBinaryReader reader) {
        Name = reader.ReadString();
        Bytes = MessageIO.ReadBytes(reader);
    }

    public void Serialize(RocketBinaryWriter writer) {
        writer.WriteString(Name ?? string.Empty);
        MessageIO.WriteBytes(writer, Bytes);
    }
}

internal sealed class ProgramLibrarySnapshotMessage : INetworkMessage {
    public byte[] LibraryState = new byte[0];

    public void Process(long clientId) {
        if (NetworkManager.IsClient) {
            RustMod.Instance?.ApplyLibrarySnapshot(LibraryState);
        }
    }

    public void Deserialize(RocketBinaryReader reader) {
        LibraryState = MessageIO.ReadBytes(reader);
    }

    public void Serialize(RocketBinaryWriter writer) {
        MessageIO.WriteBytes(writer, LibraryState);
    }
}

internal sealed class ProgramRenameMessage : INetworkMessage {
    public ulong Key;
    public string Name = string.Empty;

    public void Process(long clientId) {
        if (NetworkManager.IsServer) {
            RustMod.Instance?.RenameProgram(Key, Name, broadcast: true);
        }
    }

    public void Deserialize(RocketBinaryReader reader) {
        Key = reader.ReadUInt64();
        Name = reader.ReadString();
    }

    public void Serialize(RocketBinaryWriter writer) {
        writer.WriteUInt64(Key);
        writer.WriteString(Name ?? string.Empty);
    }
}

internal sealed class ProgramDeleteMessage : INetworkMessage {
    public ulong Key;

    public void Process(long clientId) {
        if (NetworkManager.IsServer) {
            RustMod.Instance?.DeleteProgram(Key, broadcast: true);
        }
    }

    public void Deserialize(RocketBinaryReader reader) {
        Key = reader.ReadUInt64();
    }

    public void Serialize(RocketBinaryWriter writer) {
        writer.WriteUInt64(Key);
    }
}

internal sealed class ChipAssignProgramMessage : INetworkMessage {
    public long ChipId;
    public ulong ProgramKey;

    public void Process(long clientId) {
        if (!NetworkManager.IsServer) {
            return;
        }

        if (Thing.Find<Thing>(ChipId) is RustChip chip) {
            chip.AssignProgramServer(ProgramKey);
        }
    }

    public void Deserialize(RocketBinaryReader reader) {
        ChipId = reader.ReadInt64();
        ProgramKey = reader.ReadUInt64();
    }

    public void Serialize(RocketBinaryWriter writer) {
        writer.WriteInt64(ChipId);
        writer.WriteUInt64(ProgramKey);
    }
}
}
