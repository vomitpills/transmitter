using Transmitter.Comms;
using Transmitter.Comms.Gateways;
using Transmitter.Comms.Requests;
using Transmitter.Comms.Responses;
using Transmitter.Security;

namespace Transmitter.Client;

internal static class QAConnectionExtensions
{
    extension (QAConnection connection)
    {
        public async Task<Guid> GetFeedbackLineKey(ReadOnlyMemory<byte> clientAuth)
        {
            Response response = await connection.RequestAsync(new ObtainFeedbackLineKeyRequest(clientAuth));
            if (response is not GuidResponse guidResponse)
                throw new NotImplementedException();
            return guidResponse.Guid;
        }

        public async Task AuthenticateCommConnection(ReadOnlyMemory<byte> clientAuth)
        {
            Response response = await connection.RequestAsync(new AuthenticateCommConnectionRequest(clientAuth));
            if (response.Status is not RequestStatus.OK)
                throw new NotImplementedException();
        }

        public async Task<(ReadOnlyMemory<byte> Auth, HashedId? Referrer)> ObtainClientSecret(Guid inviteCode, Profile profile)
        {
            Response response = await connection.RequestAsync(new ObtainClientSecretRequest(inviteCode, profile));
            return response switch
            {
                AuthKeyAndReferrerResponse authKeyAndReferrerResponse => (authKeyAndReferrerResponse.AuthKey, authKeyAndReferrerResponse.Referrer),
                _ => throw new NotImplementedException()
            };
        }
    }

    extension (QAConnection feedbackConnection)
    {
        public async Task AuthenticateFeedbackLine(ReadOnlyMemory<byte> clientAuth, Guid feedbackLineKey)
        {
            Response response = await feedbackConnection.RequestAsync(new AuthenticateFeedbackLineConnectionRequest(clientAuth, feedbackLineKey));
            if (response.Status is not RequestStatus.OK)
                throw new NotImplementedException();
        }
    }
}
