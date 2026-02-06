using System.Collections.Generic;

namespace VrchatgroupApp.clean.Services;

public class LoginResult
{
    public bool Success { get; set; }
    public bool Requires2FA { get; set; }
    public List<string> TwoFactorTypes { get; set; } = new();
    public string? Message { get; set; }
}
