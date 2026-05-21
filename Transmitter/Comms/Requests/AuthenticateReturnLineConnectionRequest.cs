using Transmitter.Authentication;
using Transmitter.Comms.Responses;

namespace Transmitter.Comms.Requests;

public sealed class AuthenticateReturnLineConnectionRequest : AuthenticatedRequest<AuthenticateReturnLineConnectionRequest>, IAuthenticatedRequest
{
    static RequestIntention IAuthenticatedRequest.DescendantIntention { get; } = RequestIntention.Authenticate;

    public Guid ReturnLineKey { get; }

    public AuthenticateReturnLineConnectionRequest(ClientSecret clientSecret, Guid returnLineKey) : base(clientSecret)
    {
        ReturnLineKey = returnLineKey;
    }

    private AuthenticateReturnLineConnectionRequest(IDeserializationKey key, RequestIntention intention, ClientSecret clientSecret, Guid returnLineKey) : base(key, intention, clientSecret)
    {
        ReturnLineKey = returnLineKey;
    }

    static Request IAuthenticatedRequest.FinalizeDescendantDeserialization(BinaryReader reader, RequestIntention intention, ClientSecret clientSecret, IDeserializationKey key)
    {
        Span<byte> guidBuffer = stackalloc byte[16];
        reader.ReadExactly(guidBuffer);
        return new AuthenticateReturnLineConnectionRequest(key, intention, clientSecret, new(guidBuffer));
    }

    protected override void FinalizeDescendantSerialization(BinaryWriter writer)
    {
        writer.Write(ReturnLineKey.ToByteArray());
    }

    static ResponseDeserializationFinalizer IAuthenticatedRequest.FinalizeDescendantResponseDeserialization(ResponsePayloadType responsePayloadType) => responsePayloadType switch
    {
        ResponsePayloadType.Empty => EmptyResponse.FinalizeDeserialization,
        _ => throw new ArgumentOutOfRangeException(nameof(responsePayloadType), responsePayloadType.ToString(), null)
    };
}