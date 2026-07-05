using Transmitter.IO;
using Transmitter.Security;

namespace Transmitter.Comms;

public readonly struct Transaction : ISerializable<Transaction>
{
    public Guid Id { get; }
    public HashedId SenderOrRecipient { get; private init; }
    public DateTimeOffset FormedAt { get; }
    public ReadOnlyMemory<byte> Payload { get; }

    public Transaction(HashedId senderOrRecipient, ReadOnlyMemory<byte> payload) : this(Guid.NewGuid(), senderOrRecipient, DateTimeOffset.UtcNow, payload) { }

    private Transaction(Guid id, HashedId senderOrRecipient, DateTimeOffset formedAt, ReadOnlyMemory<byte> payload)
    {
        Id = id;
        SenderOrRecipient = senderOrRecipient;
        FormedAt = formedAt;
        Payload = payload;
    }

    public Transaction SwapUser(HashedId recipientOrSender)
        => this with { SenderOrRecipient = recipientOrSender };

    public static Transaction FromSerializable<T>(HashedId senderOrRecipient, T payload) where T : ISerializable<T>
        => new(senderOrRecipient, payload.ToBytes());

    public static Transaction FromSerializable(HashedId senderOrRecipient, Message payload)
        => new(senderOrRecipient, payload.ToBytes());

    public T Interpret<T>() where T : ISerializable<T>
        => ISerializable<T>.FromBytes(Payload.ToArray());

    public static Transaction Deserialize(BinaryReader reader)
    {
        Span<byte> guidBuffer = stackalloc byte[16];

        reader.ReadExactly(guidBuffer);
        Guid id = new(guidBuffer);

        HashedId senderOrRecipient = HashedId.DeserializeHash(reader);

        DateTimeOffset formedAt = new(reader.ReadInt64(), TimeSpan.Zero);

        ushort payloadLength = reader.ReadUInt16();
        byte[] payloadBuffer = new byte[payloadLength];
        reader.ReadExactly(payloadBuffer);

        return new(id, senderOrRecipient, formedAt, payloadBuffer);
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Id.ToByteArray());

        SenderOrRecipient.SerializeHash(writer);

        writer.Write(FormedAt.Ticks);

        writer.Write((ushort)Payload.Length);
        writer.Write(Payload.Span);
    }
}