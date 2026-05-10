using System.Text;

namespace transmitter_alpha_common;

internal static class ProtocolMeta
{
    public const string PROTOCOL_VERSION = "TRNSMTR-A03";

    public static byte[] ProtocolVersionSignature { get; } = Encoding.UTF8.GetBytes(PROTOCOL_VERSION);
    public static int ProtocolVersionSignatureLength => ProtocolVersionSignature.Length;
}

public sealed class ResponseTemplate
{
    public RequestStatus? RequestStatus { get; }
    public ResponsePayloadType PayloadType { get; }

    public ResponseTemplate(RequestStatus status, ResponsePayloadType payloadType)
    {
        RequestStatus = status;
        PayloadType = payloadType;
    }

    public ResponseTemplate(ResponsePayloadType payloadType)
    {
        PayloadType = payloadType;
    }

    public static bool IsValidResponse(Request request, Response response)
    {
        foreach (var template in request.ExpectedResponse)
        {
            bool statusPassed = template.RequestStatus is null || template.RequestStatus == response.Status;
            bool payloadPassed = template.PayloadType == response.PayloadType;
            if (statusPassed && payloadPassed)
                return true;
        }
        return false;
    }
}