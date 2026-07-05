using Transmitter.Comms;
using Transmitter.IO;

namespace Transmitter.Client;

public readonly struct TransactionReceipt(Transaction transaction, DateTimeOffset sentAt, DateTimeOffset receivedAt) : ISerializable<TransactionReceipt> // add metadata (i.e. instance)
{
    public Transaction Transaction { get; } = transaction;
    public DateTimeOffset SentAt { get; } = sentAt;
    public DateTimeOffset ReceivedAt { get; } = receivedAt;

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(SentAt.UtcTicks);
        writer.Write(ReceivedAt.UtcTicks);
        Transaction.Serialize(writer);
    }

    public static TransactionReceipt Deserialize(BinaryReader reader)
    {
        DateTimeOffset sentAt = new(reader.ReadInt64(), TimeSpan.Zero);
        DateTimeOffset receivedAt = new(reader.ReadInt64(), TimeSpan.Zero);
        Transaction transaction = Transaction.Deserialize(reader);
        return new(transaction, sentAt, receivedAt);
    }
}
