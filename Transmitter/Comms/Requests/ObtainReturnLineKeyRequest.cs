using Transmitter.Authentication;
using Transmitter.Comms.Responses;

namespace Transmitter.Comms.Requests;

public sealed class ObtainReturnLineKeyRequest : AuthenticatedRequest<ObtainReturnLineKeyRequest>, IAuthenticatedRequest
{
    static RequestIntention IAuthenticatedRequest.DescendantIntention { get; } = RequestIntention.ObtainReturnLineKey;
    
    public ObtainReturnLineKeyRequest(ClientSecret clientSecret) : base(clientSecret) { }

    private ObtainReturnLineKeyRequest(IDeserializationKey key, RequestIntention intention, ClientSecret clientSecret) : base(key, intention, clientSecret) { }

    static Request IAuthenticatedRequest.FinalizeDescendantDeserialization(BinaryReader reader, RequestIntention intention, ClientSecret clientSecret, IDeserializationKey key) => new ObtainReturnLineKeyRequest(key, intention, clientSecret);

    static ResponseDeserializationFinalizer IAuthenticatedRequest.FinalizeDescendantResponseDeserialization(ResponsePayloadType responsePayloadType) => responsePayloadType switch
    {
        ResponsePayloadType.Guid => GuidResponse.FinalizeDeserialization,
        ResponsePayloadType.Empty => EmptyResponse.FinalizeDeserialization,
        _ => throw new ArgumentOutOfRangeException(nameof(responsePayloadType), responsePayloadType.ToString(), null)
    };

    protected override void FinalizeDescendantSerialization(BinaryWriter writer) { }
}