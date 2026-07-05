using Transmitter.IO;
using Transmitter.Security;

namespace Transmitter.Comms;

public readonly struct Transaction : ISerializable<Transaction>
{
    public Guid Id { get; }
    public HashedId SenderOrRecipient { get; private init; }
    public DateTimeOffset FormedAt { get; }
    public ReadOnlyMemory<byte> Message { get; }

    public Transaction(HashedId senderOrRecipient, ReadOnlyMemory<byte> message) : this(Guid.NewGuid(), senderOrRecipient, DateTimeOffset.UtcNow, message) { }

    private Transaction(Guid id, HashedId senderOrRecipient, DateTimeOffset formedAt, ReadOnlyMemory<byte> message)
    {
        Id = id;
        SenderOrRecipient = senderOrRecipient;
        FormedAt = formedAt;
        Message = message;
    }

    public Transaction SwapUser(HashedId recipientOrSender)
        => this with { SenderOrRecipient = recipientOrSender };

    public static Transaction FromSerializable<T>(HashedId senderOrRecipient, T message) where T : ISerializable<T>
        => new(senderOrRecipient, message.ToBytes());

    public static Transaction FromSerializable(HashedId senderOrRecipient, Message message)
        => new(senderOrRecipient, message.ToBytes());

    public T Interpret<T>() where T : ISerializable<T>
        => ISerializable<T>.FromBytes(Message.ToArray());

    public static Transaction Deserialize(BinaryReader reader)
    {
        Span<byte> guidBuffer = stackalloc byte[16];

        reader.ReadExactly(guidBuffer);
        Guid id = new(guidBuffer);

        HashedId senderOrRecipient = HashedId.DeserializeHash(reader);

        DateTimeOffset formedAt = new(reader.ReadInt64(), TimeSpan.Zero);

        ushort messageLength = reader.ReadUInt16();
        byte[] messageBuffer = new byte[messageLength];
        reader.ReadExactly(messageBuffer);

        return new(id, senderOrRecipient, formedAt, messageBuffer);
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Id.ToByteArray());

        SenderOrRecipient.SerializeHash(writer);

        writer.Write(FormedAt.Ticks);

        writer.Write((ushort)Message.Length);
        writer.Write(Message.Span);
    }
}