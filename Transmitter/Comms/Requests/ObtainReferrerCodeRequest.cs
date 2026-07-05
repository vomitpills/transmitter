using Transmitter.Comms.Responses;

namespace Transmitter.Comms.Requests;

public sealed class ObtainReferrerCodeRequest : AuthenticatedRequest<ObtainReferrerCodeRequest>, IAuthenticatedRequest
{
    static RequestIntention IAuthenticatedRequest.DescendantIntention { get; } = RequestIntention.ObtainReferrerKey;
    
    public ObtainReferrerCodeRequest(ReadOnlyMemory<byte> clientAuth) : base(clientAuth) { }

    private ObtainReferrerCodeRequest(IDeserializationKey key, RequestIntention intention, ReadOnlyMemory<byte> clientAuth) : base(key, intention, clientAuth) { }

    static Request IAuthenticatedRequest.FinalizeDescendantDeserialization(BinaryReader reader, RequestIntention intention, ReadOnlyMemory<byte> clientAuth, IDeserializationKey key) => new ObtainReferrerCodeRequest(key, intention, clientAuth);

    static ResponseDeserializationFinalizer IAuthenticatedRequest.FinalizeDescendantResponseDeserialization(ResponsePayloadType responsePayloadType) => responsePayloadType switch
    {
        ResponsePayloadType.Guid => GuidResponse.FinalizeDeserialization,
        ResponsePayloadType.Empty => EmptyResponse.FinalizeDeserialization,
        _ => throw new ArgumentOutOfRangeException(nameof(responsePayloadType), responsePayloadType.ToString(), null)
    };

    protected override void FinalizeDescendantSerialization(BinaryWriter writer) { }
}