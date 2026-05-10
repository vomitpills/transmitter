namespace transmitter_alpha_common;

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