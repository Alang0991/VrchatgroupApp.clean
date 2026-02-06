using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VrchatgroupApp.clean.Services;

public class StoredSession
{
    public string AuthCookie { get; set; } = "";
    public string? TwoFactorCookie { get; set; }

    public string? UserId { get; set; }
    public string? DisplayName { get; set; }
    public string? ProfilePicUrl { get; set; }

    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
}


