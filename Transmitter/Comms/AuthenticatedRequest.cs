using Transmitter.Authentication;

namespace Transmitter.Comms;

public interface IAuthenticatedRequest : IRequest
{
    public abstract static Request FinalizeDescendantDeserialization(BinaryReader reader, RequestIntention intention, ClientSecret clientSecret, IDeserializationKey key);
    public abstract static ResponseDeserializationFinalizer FinalizeDescendantResponseDeserialization(ResponsePayloadType responsePayloadType);
    public abstract static RequestIntention DescendantIntention { get; }
}

public abstract class AuthenticatedRequest<T> : Request<T>, IRequest where T : Request<T>, IAuthenticatedRequest
{
    public static RequestIntention Intention => T.DescendantIntention;

    public ClientSecret ClientSecret { get; }

    public AuthenticatedRequest(ClientSecret clientSecret)
    {
        ClientSecret = clientSecret;
    }

    protected AuthenticatedRequest(IDeserializationKey key, RequestIntention intention, ClientSecret clientSecret) : base(key, intention)
    {
        ClientSecret = clientSecret;
    }

    public static Request FinalizeDeserialization(BinaryReader reader, RequestIntention intention, IDeserializationKey key)
    {
        ClientSecret clientSecret = ClientSecret.Deserialize(reader);
        return T.FinalizeDescendantDeserialization(reader, intention, clientSecret, key);
    }

    protected override void FinalizeSerialization(BinaryWriter writer)
    {
        ClientSecret.Serialize(writer);
        FinalizeDescendantSerialization(writer);
    }
    protected abstract void FinalizeDescendantSerialization(BinaryWriter writer);

    public static ResponseDeserializationFinalizer FinalizeResponseDeserialization(ResponsePayloadType responsePayloadType) => T.FinalizeDescendantResponseDeserialization(responsePayloadType);
}