namespace Transmitter.Comms;

public class Mail
{
    public IEnumerable<MailContent> Content { get; }
}

public abstract class MailContent
{

}

public sealed class MailTextContent : MailContent
{
    public string Text { get; }
}