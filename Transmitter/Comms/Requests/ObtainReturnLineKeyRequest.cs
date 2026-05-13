using System;
using System.Collections.Generic;
using System.Text;
using Transmitter.Authentication;

namespace Transmitter.Comms.Requests;

public sealed class ObtainReturnLineKeyRequest : AuthenticatedRequest<ObtainReturnLineKeyRequest>, IAuthenticatedRequest
{
    public ObtainReturnLineKeyRequest(ClientSecret clientSecret) : base(clientSecret) { }

    private ObtainReturnLineKeyRequest(IDeserializationKey key, RequestIntention intention, ClientSecret clientSecret) : base(key, intention, clientSecret) { }

    static Request IAuthenticatedRequest.FinalizeDescendantDeserialization(BinaryReader reader, RequestIntention intention, ClientSecret clientSecret, IDeserializationKey key) => new ObtainReturnLineKeyRequest(key, intention, clientSecret);

    protected override void FinalizeDescendantSerialization(BinaryWriter writer) { }
}
