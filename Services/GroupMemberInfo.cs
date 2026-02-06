using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class GroupMemberInfo
{
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public bool IsOwner { get; set; }
    public bool IsModerator { get; set; }
}

