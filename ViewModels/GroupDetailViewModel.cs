using System.Collections.ObjectModel;
using VrchatgroupApp.clean.Services;

namespace VrchatgroupApp.clean.ViewModels
{
    public class GroupDetailViewModel : ViewModelBase
    {
        private readonly IVRChatApiService _api;
        private readonly GroupInfo _group;

        public string GroupId => _group.Id;
        public string Name => _group.Name;
        public string? IconUrl => _group.IconUrl;
        public string? BannerUrl => _group.BannerUrl;

        public int MemberCount => _group.MemberCount;
        public int OnlineCount => _group.OnlineCount;

        private string? _statusMessage;
        public string? StatusMessage
        {
            get => _statusMessage;
            set => Set(ref _statusMessage, value);
        }

        private GroupPermission _permissions;
        public GroupPermission Permissions
        {
            get => _permissions;
            private set
            {
                if (Set(ref _permissions, value))
                {
                    OnPropertyChanged(nameof(CanModerateMembers));
                    OnPropertyChanged(nameof(CanManageRoles));
                }
            }
        }

        public bool CanModerateMembers =>
            Permissions.HasFlag(GroupPermission.ManageMembers) ||
            Permissions.HasFlag(GroupPermission.Moderate);

        public bool CanManageRoles =>
            Permissions.HasFlag(GroupPermission.ManageRoles);

        // 🔥 FORCE THE CORRECT TYPE
        public ObservableCollection<VrchatgroupApp.clean.Services.GroupMemberInfo> Members { get; } = new();

        private VrchatgroupApp.clean.Services.GroupMemberInfo? _selectedMember;
        public VrchatgroupApp.clean.Services.GroupMemberInfo? SelectedMember
        {
            get => _selectedMember;
            set => Set(ref _selectedMember, value);
        }

        public AsyncRelayCommand RefreshCommand { get; }

        public GroupDetailViewModel(IVRChatApiService api, GroupInfo group)
        {
            _api = api;
            _group = group;

            RefreshCommand = new AsyncRelayCommand(LoadAsync);

            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                StatusMessage = null;

                Permissions = await _api.GetMyPermissionsForGroupAsync(_group.Id);

                var members = await _api.GetGroupMembersAsync(_group.Id, n: 100, offset: 0);

                Members.Clear();
                foreach (var m in members)
                {
                    Members.Add(m); // ✅ same exact type now
                }

                SelectedMember ??= Members.FirstOrDefault();
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
            }
        }
    }
}
