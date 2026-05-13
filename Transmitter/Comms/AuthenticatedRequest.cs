using System.Collections.ObjectModel;
using Transmitter.Authentication;

namespace Transmitter.Comms;

public interface IAuthenticatedRequest : IRequest
{
    public abstract static Request FinalizeDescendantDeserialization(BinaryReader reader, RequestIntention intention, ClientSecret clientSecret, IDeserializationKey key);
}

public abstract class AuthenticatedRequest<T> : Request<T>, IRequest where T : Request<T>, IAuthenticatedRequest
{
    public static ReadOnlyCollection<ResponseTemplate> ExpectedResponse => T.ExpectedResponse;
    public static RequestIntention Intention => T.Intention;

    private ClientSecret ClientSecret { get; }

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
}