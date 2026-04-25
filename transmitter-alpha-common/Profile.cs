using System;
using System.Collections.Generic;
using System.Text;

namespace transmitter_alpha_common;

public class Profile
{
    public string DisplayName { get; }
    public string? Status { get; init; }
    public Media? ProfilePicture { get; init; }

    public Profile(string displayName)
    {
        DisplayName = displayName;
    }
}
