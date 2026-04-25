using System;
using System.Collections.Generic;
using System.Text;

namespace transmitter_alpha_common;

public class CommonException : Exception
{
    public string Fault { get; }
    public virtual string Specifier { get; } = "unknown";

    public CommonException(string fault) : base()
    {
        Fault = fault;
    }

    public CommonException(string fault, string specifier) : base()
    {
        Fault = fault;
        Specifier = specifier;
    }

    public string Serialize() => $"{Specifier}:{Fault}";
    public static CommonException Deserialize(string text)
    {
        var split = text.Split(':');
        return new(split[1], split[0]);
    }
}

public class MessageStructureException(string fault) : CommonException(fault, "structure") { }

public class FaultyDataException(string fault) : CommonException(fault, "data") { }

public class StateException(string fault) : CommonException(fault, "state") { }