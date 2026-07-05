using System.Collections.ObjectModel;

using Transmitter.Comms;
using Transmitter.IO;

namespace Transmitter.Client;

public sealed partial class PersistentClientState
{
    public Profile Profile { get; set; }
    public Dictionary<Guid, InstanceRegistration> Instances { get; private init; } = [];
    public Dictionary<Guid, Recipient> Recipients { get; private init; } = [];

    public Dictionary<Guid, Collection<Guid>> ChatMembers { get; private init; } = [];
    public Dictionary<Guid, AppendOnlyStorage<TransactionReceipt>> ChatMessageHistory { get; private init; } = [];

    private Dictionary<Guid, PendingTransaction> PendingTransactions { get; init; } = [];

    public void StoreMailReceipt(Guid chatId, TransactionReceipt mailReceipt)
    {
        if (!ChatMessageHistory.TryGetValue(chatId, out var messages))
        {
            messages = new(GetChatFilename(chatId));
            ChatMessageHistory.Add(chatId, messages);
        }
        messages.Append(mailReceipt);
    }

    public void RecordPendingTransaction(Guid transactionId, Guid chatId, DateTimeOffset sentAt, Mail mail)
    {
        PendingTransactions.Add(transactionId, new(chatId, sentAt, mail));
        Save();
    }

    public void AddInstance(Guid guid, InstanceRegistration registration)
    {
        Instances.Add(guid, registration);
        Save();
    }

    private record PendingTransaction(Guid ChatId, DateTimeOffset SentAt, Mail Mail) : ISerializable<PendingTransaction>
    {
        public void Serialize(BinaryWriter writer)
        {
            writer.Write(ChatId.ToByteArray());
            writer.Write(SentAt.UtcTicks);
            Mail.Serialize(writer);
        }

        public static PendingTransaction Deserialize(BinaryReader reader)
        {
            Span<byte> guidBuffer = stackalloc byte[16];
            reader.ReadExactly(guidBuffer);
            Guid chatId = new(guidBuffer);

            long sentAt = reader.ReadInt64();

            Mail mail = Mail.Deserialize(reader);
            
            return new(chatId, new(sentAt, TimeSpan.Zero), mail);
        }
    }
}
