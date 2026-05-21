using Transmitter.Authentication;
using Transmitter.Comms;
using Transmitter.Comms.Gateways;
using Transmitter.Comms.Requests;
using Transmitter.Comms.Responses;

namespace Transmitter.Client;

public partial class Client
{
    private sealed class Chat : IChat
    {
        private readonly Task ReturnLineTask;
        private readonly QAConnection connection;
        private readonly QAConnection returnLineConnection;
        private readonly ClientSecret clientSecret;

        public Chat(QAConnection connection, QAConnection returnLineConnection, ClientSecret clientSecret)
        {
            this.connection = connection;
            this.returnLineConnection = returnLineConnection;
            this.clientSecret = clientSecret;
            ReturnLineTask = Task.Run(() => StartReturnLineTask(returnLineConnection));
        }

        private async Task StartReturnLineTask(QAConnection returnLineConnection)
        {
            while (true)
            {
                Request request = await returnLineConnection.ReadRequestAsync((intention) => intention switch
                {
                    RequestIntention.AcknowledgeMailDelivery => AcknowledgeMailRequest.FinalizeDeserialization,
                    _ => throw new ArgumentOutOfRangeException(nameof(intention))
                });

                switch (request)
                {
                    case AcknowledgeMailRequest acknowledgeMailRequest:
                    {
                        await returnLineConnection.WriteResponseAsync(new EmptyResponse(RequestStatus.OK));
                        MailReceived?.Invoke(acknowledgeMailRequest.Transaction);
                    }
                    break;

                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        public event Action<Transaction>? MailReceived;

        public async Task PostMail(Mail mail, Guid recipient)
        {
            Response response = await connection.RequestAsync(new SendMailRequest(clientSecret, recipient, mail));

            Console.Write($"received response ({response.Status}): ");
            switch (response)
            {
                case GuidResponse guidResponse:
                    Console.WriteLine($"posted as trid:{guidResponse.Guid}");
                    break;

                default:
                    Console.WriteLine("n/a");
                    break;
            }
        }

        public void Dispose()
        {
            connection.Dispose();
            returnLineConnection.Dispose();
        }
    }
}
