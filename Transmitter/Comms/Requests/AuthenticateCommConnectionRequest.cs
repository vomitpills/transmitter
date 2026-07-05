using Transmitter.Comms.Responses;

namespace Transmitter.Comms.Requests;

public sealed class AuthenticateCommConnectionRequest : AuthenticatedRequest<AuthenticateCommConnectionRequest>, IAuthenticatedRequest
{
    static RequestIntention IAuthenticatedRequest.DescendantIntention { get; } = RequestIntention.Authenticate;

    public AuthenticateCommConnectionRequest(ReadOnlyMemory<byte> clientAuth) : base(clientAuth) { }

    private AuthenticateCommConnectionRequest(IDeserializationKey key, RequestIntention intention, ReadOnlyMemory<byte> clientAuth) : base(key, intention, clientAuth) { }

    static Request IAuthenticatedRequest.FinalizeDescendantDeserialization(BinaryReader reader, RequestIntention intention, ReadOnlyMemory<byte> clientAuth, IDeserializationKey key)
        => new AuthenticateCommConnectionRequest(key, intention, clientAuth);

    protected override void FinalizeDescendantSerialization(BinaryWriter writer) { }

    static ResponseDeserializationFinalizer IAuthenticatedRequest.FinalizeDescendantResponseDeserialization(ResponsePayloadType responsePayloadType) => responsePayloadType switch
    {
        ResponsePayloadType.Empty => EmptyResponse.FinalizeDeserialization,
        _ => throw new ArgumentOutOfRangeException(nameof(responsePayloadType), responsePayloadType.ToString(), null)
    };
}