using Transmitter.Authentication;
using Transmitter.Comms.Responses;

namespace Transmitter.Comms.Requests;

public sealed class AuthenticateCommConnectionRequest : AuthenticatedRequest<AuthenticateCommConnectionRequest>, IAuthenticatedRequest
{
    static RequestIntention IAuthenticatedRequest.DescendantIntention { get; } = RequestIntention.Authenticate;

    public AuthenticateCommConnectionRequest(ClientSecret clientSecret) : base(clientSecret) { }

    private AuthenticateCommConnectionRequest(IDeserializationKey key, RequestIntention intention, ClientSecret clientSecret) : base(key, intention, clientSecret) { }

    static Request IAuthenticatedRequest.FinalizeDescendantDeserialization(BinaryReader reader, RequestIntention intention, ClientSecret clientSecret, IDeserializationKey key)
        => new AuthenticateCommConnectionRequest(key, intention, clientSecret);

    protected override void FinalizeDescendantSerialization(BinaryWriter writer) { }

    static ResponseDeserializationFinalizer IAuthenticatedRequest.FinalizeDescendantResponseDeserialization(ResponsePayloadType responsePayloadType) => responsePayloadType switch
    {
        ResponsePayloadType.Empty => EmptyResponse.FinalizeDeserialization,
        _ => throw new ArgumentOutOfRangeException(nameof(responsePayloadType), responsePayloadType.ToString(), null)
    };
}