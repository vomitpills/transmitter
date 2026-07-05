using Transmitter.Comms.Responses;

namespace Transmitter.Comms.Requests;

public sealed class AuthenticateFeedbackLineConnectionRequest : AuthenticatedRequest<AuthenticateFeedbackLineConnectionRequest>, IAuthenticatedRequest
{
    static RequestIntention IAuthenticatedRequest.DescendantIntention { get; } = RequestIntention.Authenticate;

    public Guid FeedbackLineKey { get; }

    public AuthenticateFeedbackLineConnectionRequest(ReadOnlyMemory<byte> clientAuth, Guid feedbackLineKey) : base(clientAuth)
    {
        FeedbackLineKey = feedbackLineKey;
    }

    private AuthenticateFeedbackLineConnectionRequest(IDeserializationKey key, RequestIntention intention, ReadOnlyMemory<byte> clientAuth, Guid feedbackLineKey) : base(key, intention, clientAuth)
    {
        FeedbackLineKey = feedbackLineKey;
    }

    static Request IAuthenticatedRequest.FinalizeDescendantDeserialization(BinaryReader reader, RequestIntention intention, ReadOnlyMemory<byte> clientAuth, IDeserializationKey key)
    {
        Span<byte> guidBuffer = stackalloc byte[16];
        reader.ReadExactly(guidBuffer);
        return new AuthenticateFeedbackLineConnectionRequest(key, intention, clientAuth, new(guidBuffer));
    }

    protected override void FinalizeDescendantSerialization(BinaryWriter writer)
    {
        writer.Write(FeedbackLineKey.ToByteArray());
    }

    static ResponseDeserializationFinalizer IAuthenticatedRequest.FinalizeDescendantResponseDeserialization(ResponsePayloadType responsePayloadType) => responsePayloadType switch
    {
        ResponsePayloadType.Empty => EmptyResponse.FinalizeDeserialization,
        _ => throw new ArgumentOutOfRangeException(nameof(responsePayloadType), responsePayloadType.ToString(), null)
    };
}