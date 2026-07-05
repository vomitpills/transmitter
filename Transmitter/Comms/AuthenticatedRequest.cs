using Transmitter.Security;

namespace Transmitter.Comms;

public interface IAuthenticatedRequest : IRequest
{
    public abstract static Request FinalizeDescendantDeserialization(BinaryReader reader, RequestIntention intention, ReadOnlyMemory<byte> clientSecret, IDeserializationKey key);
    public abstract static ResponseDeserializationFinalizer FinalizeDescendantResponseDeserialization(ResponsePayloadType responsePayloadType);
    public abstract static RequestIntention DescendantIntention { get; }
}

public abstract class AuthenticatedRequest<T> : Request<T>, IRequest where T : Request<T>, IAuthenticatedRequest
{
    public static RequestIntention Intention => T.DescendantIntention;

    public ReadOnlyMemory<byte> ClientAuth { get; }

    public AuthenticatedRequest(ReadOnlyMemory<byte> clientAuth)
    {
        ClientAuth = clientAuth;
    }

    protected AuthenticatedRequest(IDeserializationKey key, RequestIntention intention, ReadOnlyMemory<byte> clientAuth) : base(key, intention)
    {
        ClientAuth = clientAuth;
    }

    public static Request FinalizeDeserialization(BinaryReader reader, RequestIntention intention, IDeserializationKey key)
    {
        ReadOnlyMemory<byte> clientSecret = HashedId.DeserializeKey(reader);
        return T.FinalizeDescendantDeserialization(reader, intention, clientSecret, key);
    }

    protected override void FinalizeSerialization(BinaryWriter writer)
    {
        HashedId.SerializeKey(writer, ClientAuth.Span);
        FinalizeDescendantSerialization(writer);
    }
    protected abstract void FinalizeDescendantSerialization(BinaryWriter writer);

    public static ResponseDeserializationFinalizer FinalizeResponseDeserialization(ResponsePayloadType responsePayloadType) => T.FinalizeDescendantResponseDeserialization(responsePayloadType);
}