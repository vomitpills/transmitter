using System.Text;

namespace Transmitter.Comms;

internal static class ProtocolMeta
{
    private const string SIGNATURE = "TRNSMTTR";
    private const ushort PROTOCOL_VERSION = 0;

    private static ReadOnlyMemory<byte> Header { get; } = (byte[])[.. Encoding.ASCII.GetBytes(SIGNATURE), .. BitConverter.GetBytes(PROTOCOL_VERSION)];

    public static void WriteHeader(BinaryWriter writer) => writer.Write(Header.Span);
    public static void ReadHeader(BinaryReader reader)
    {
        Span<byte> headerBuffer = stackalloc byte[Header.Length];
        reader.ReadExactly(headerBuffer);
        if (!Header.Span.SequenceEqual(headerBuffer))
            throw new InvalidDataException("Incompatible header");
    }
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

    public static bool IsValidResponse<TRequest>(Response response) where TRequest : IRequest
    {
        foreach (var template in TRequest.ExpectedResponse)
        {
            bool statusPassed = template.RequestStatus is null || template.RequestStatus == response.Status;
            bool payloadPassed = template.PayloadType == response.PayloadType;
            if (statusPassed && payloadPassed)
                return true;
        }
        return false;
    }
}