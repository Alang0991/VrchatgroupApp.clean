using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VrchatgroupApp.clean.ViewModels;

public class GroupInfo : ViewModelBase
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    public string? IconUrl { get; set; }

    // ✅ ADD THIS
    public string? BannerUrl { get; set; }

    public int MemberCount { get; set; }

    private int _onlineCount;
    public int OnlineCount
    {
        get => _onlineCount;
        set => Set(ref _onlineCount, value);
    }

    public bool HasOnlineMembers => OnlineCount > 0;
}







