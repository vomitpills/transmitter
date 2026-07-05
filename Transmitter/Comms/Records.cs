using Transmitter.Comms.Gateways;
using Transmitter.IO;
using Transmitter.Security;

namespace Transmitter.Comms;

public record InstanceAddress(GatewayAddress CommAddress, GatewayAddress FeedbackLineAddress) : ISerializable<InstanceAddress>
{
    public void Serialize(BinaryWriter writer)
    {
        if (CommAddress is not WebGatewayAddress webCommAddress || FeedbackLineAddress is not WebGatewayAddress webFeedbackLineAddress)
            throw new NotImplementedException();
        webCommAddress.Serialize(writer);
        webFeedbackLineAddress.Serialize(writer);
    }

    public static InstanceAddress Deserialize(BinaryReader reader)
    {
        WebGatewayAddress comm = WebGatewayAddress.Deserialize(reader);
        WebGatewayAddress feedback = WebGatewayAddress.Deserialize(reader);
        return new(comm, feedback);
    }
}

public record InstanceRegistration(InstanceAddress InstanceAddress, ReadOnlyMemory<byte> Auth) : ISerializable<InstanceRegistration>
{
    public void Serialize(BinaryWriter writer)
    {
        InstanceAddress.Serialize(writer);
        HashedId.SerializeKey(writer, Auth.Span);
    }

    public static InstanceRegistration Deserialize(BinaryReader reader)
    {
        InstanceAddress address = InstanceAddress.Deserialize(reader);
        ReadOnlyMemory<byte> auth = HashedId.DeserializeKey(reader).ToArray();
        return new(address, auth);
    }
}

public record InstanceInvite(InstanceAddress InstanceAddress, Guid Code) : ISerializable<InstanceInvite>
{
    private const string HEADER = "TRMTR--";

    public override string ToString()
    {
        MemoryStream stream = new();
        BinaryWriter writer = new(stream);
        Serialize(writer);
        return HEADER + Convert.ToBase64String(stream.ToArray());
    }

    public static InstanceInvite FromString(string data)
    {
        if (!data.StartsWith(HEADER))
            throw new ArgumentOutOfRangeException(nameof(data));

        byte[] buffer = Convert.FromBase64String(data[HEADER.Length..]);
        MemoryStream memoryStream = new(buffer);
        BinaryReader reader = new(memoryStream);
        return Deserialize(reader);
    }

    public void Serialize(BinaryWriter writer)
    {
        InstanceAddress.Serialize(writer);
        writer.Write(Code.ToByteArray());
    }

    public static InstanceInvite Deserialize(BinaryReader reader)
    {
        InstanceAddress address = InstanceAddress.Deserialize(reader);
        Span<byte> guidBuffer = stackalloc byte[16];
        reader.ReadExactly(guidBuffer);
        return new(address, new(guidBuffer));
    }
}

public record RecipientContact(InstanceAddress InstanceAddress, HashedId Passcode) : ISerializable<RecipientContact>
{
    public void Serialize(BinaryWriter writer)
    {
        InstanceAddress.Serialize(writer);
        Passcode.SerializeHash(writer);
    }

    public static RecipientContact Deserialize(BinaryReader reader)
    {
        InstanceAddress address = InstanceAddress.Deserialize(reader);
        HashedId passcode = HashedId.DeserializeHash(reader);
        return new(address, passcode);
    }
};

public record Recipient(HashSet<RecipientContact> Contacts) : ISerializable<Recipient>
{
    public void Serialize(BinaryWriter writer)
    {
        writer.Write(checked((byte)Contacts.Count));
        foreach (var item in Contacts)
            item.Serialize(writer);
    }

    public static Recipient Deserialize(BinaryReader reader)
    {
        ICollection<RecipientContact> contacts = [];
        for (int i = 0; i < reader.ReadByte(); i++)
            contacts.Add(RecipientContact.Deserialize(reader));
        return new([.. contacts]);
    }
};