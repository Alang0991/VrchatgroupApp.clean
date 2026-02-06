using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using VrchatgroupApp.clean.Services;

namespace VrchatgroupApp.clean.ViewModels;

public class DashboardViewModel : INotifyPropertyChanged
{
    private readonly IVRChatApiService _api;

    private string _displayName = "Unknown";
    public string DisplayName
    {
        get => _displayName;
        set => Set(ref _displayName, value);
    }

    private string? _profileImageUrl;
    public string? ProfileImageUrl
    {
        get => _profileImageUrl;
        set => Set(ref _profileImageUrl, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => Set(ref _isLoading, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => Set(ref _isBusy, value);
    }

    private string _groupIdInput = string.Empty;
    public string GroupIdInput
    {
        get => _groupIdInput;
        set => Set(ref _groupIdInput, value);
    }

    private string? _statusMessage;
    public string? StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    public ObservableCollection<GroupInfo> OwnedGroups { get; } = new();

    public DashboardViewModel(IVRChatApiService api)
    {
        _api = api;
    }

    public async Task LoadAsync()
    {
        if (!_api.IsLoggedIn)
            return;

        IsLoading = true;

        DisplayName = _api.CurrentUserDisplayName ?? "Unknown";
        ProfileImageUrl = _api.CurrentUserProfilePicUrl;

        OwnedGroups.Clear();
        foreach (var g in await _api.GetMyManageableGroupsAsync())
            OwnedGroups.Add(g);

        IsLoading = false;
    }

    public async Task AddGroupAsync()
    {
        if (!IsValidGroupId(GroupIdInput))
        {
            StatusMessage = "Invalid Group ID.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Adding group...";

            await _api.JoinGroupAsync(GroupIdInput);
            await RefreshGroupsAsync();

            GroupIdInput = string.Empty;
            StatusMessage = "Group added successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to add group: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RemoveGroupAsync(string groupId)
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Leaving group...";

            await _api.LeaveGroupAsync(groupId);
            await RefreshGroupsAsync();

            StatusMessage = "Group removed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to remove group: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshGroupsAsync()
    {
        OwnedGroups.Clear();
        foreach (var g in await _api.GetMyManageableGroupsAsync())
            OwnedGroups.Add(g);
    }

    private static bool IsValidGroupId(string groupId)
    {
        return !string.IsNullOrWhiteSpace(groupId)
               && groupId.StartsWith("grp_", StringComparison.OrdinalIgnoreCase)
               && groupId.Length > 10;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (!Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
