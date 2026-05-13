using Transmitter.Authentication;

namespace Transmitter.Comms.Requests;

public sealed class AuthenticateReturnLineRequest : AuthenticatedRequest<AuthenticateReturnLineRequest>, IAuthenticatedRequest
{
    private Guid ReturnLineKey { get; }

    public AuthenticateReturnLineRequest(ClientSecret clientSecret, Guid returnLineKey) : base(clientSecret)
    {
        ReturnLineKey = returnLineKey;
    }

    private AuthenticateReturnLineRequest(IDeserializationKey key, RequestIntention intention, ClientSecret clientSecret, Guid returnLineKey) : base(key, intention, clientSecret)
    {
        ReturnLineKey = returnLineKey;
    }

    static Request IAuthenticatedRequest.FinalizeDescendantDeserialization(BinaryReader reader, RequestIntention intention, ClientSecret clientSecret, IDeserializationKey key)
    {
        Span<byte> guidBuffer = stackalloc byte[16];
        reader.ReadExactly(guidBuffer);
        return new AuthenticateReturnLineRequest(key, intention, clientSecret, new(guidBuffer));
    }

    protected override void FinalizeDescendantSerialization(BinaryWriter writer)
    {
        writer.Write(ReturnLineKey.ToByteArray());
    }
}