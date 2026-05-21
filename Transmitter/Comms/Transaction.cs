namespace Transmitter.Comms;

public class Transaction : ISerializable<Transaction>
{
    public Guid TransactionId { get; }
    public Guid SenderId { get; }
    public Mail Mail { get; }

    public Transaction(Guid senderId, Mail mail) : this(Guid.NewGuid(), senderId, mail) { }

    private Transaction(Guid transactionId, Guid senderId, Mail mail)
    {
        TransactionId = transactionId;
        SenderId = senderId;
        Mail = mail;
    }

    public static Transaction Deserialize(BinaryReader reader)
    {
        Span<byte> guidBuffer = stackalloc byte[16];
        reader.ReadExactly(guidBuffer);
        Guid transactionId = new(guidBuffer);
        reader.ReadExactly(guidBuffer);
        Guid senderId = new(guidBuffer);
        Mail mail = Mail.Deserialize(reader); // allow custom types
        return new(transactionId, senderId, mail);
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(TransactionId.ToByteArray());
        writer.Write(SenderId.ToByteArray());
        Mail.Serialize(writer);
    }
}