using System.Collections.ObjectModel;

namespace transmitter_common;

public sealed class CheckInRequest : Request, IRequest
{
    public override RequestIntention Intention => RequestIntention.CheckIn;

    public override ReadOnlyCollection<ResponseTemplate> ExpectedResponse { get; } = [new(ResponsePayloadType.Empty)]; // should be static

    public CheckInRequest() { }

    private CheckInRequest(IDeserializationKey key, RequestIntention intention) : base(key, intention) { }

    public static Request FinalizeDeserialization(BinaryReader stream, RequestIntention intention, IDeserializationKey key) => new CheckInRequest(key, intention);

    protected override void SerializeFinal(BinaryWriter writer) { }
}

public sealed class CheckOutRequest : Request, IRequest
{
    public override RequestIntention Intention => RequestIntention.CheckOut;

    public override ReadOnlyCollection<ResponseTemplate> ExpectedResponse { get; } = [new(ResponsePayloadType.Empty)];

    public CheckOutRequest() { }

    private CheckOutRequest(IDeserializationKey key, RequestIntention intention) : base(key, intention) { }

    public static Request FinalizeDeserialization(BinaryReader stream, RequestIntention intention, IDeserializationKey key) => new CheckOutRequest(key, intention);

    protected override void SerializeFinal(BinaryWriter writer) { }
}