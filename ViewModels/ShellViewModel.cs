using System.Collections.ObjectModel;
using System.Windows.Threading;
using VrchatgroupApp.clean.Services;

namespace VrchatgroupApp.clean.ViewModels
{
    public class ShellViewModel : ViewModelBase
    {
        private readonly IVRChatApiService _api;
        private readonly RootViewModel _root;
        private readonly SessionStore _session; // 🔴 NEW

        public ObservableCollection<GroupInfo> Groups { get; } = new();

        private GroupInfo? _selectedGroup;
        public GroupInfo? SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (Set(ref _selectedGroup, value))
                {
                    CurrentViewModel = value == null
                        ? new WelcomeViewModel()
                        : new GroupDetailViewModel(_api, value);
                }
            }
        }

        private object? _currentViewModel;
        public object? CurrentViewModel
        {
            get => _currentViewModel;
            set => Set(ref _currentViewModel, value);
        }

        public string DisplayName => _api.CurrentUserDisplayName ?? "Not logged in";
        public string? ProfileImageUrl => _api.CurrentUserProfilePicUrl;

        public RelayCommand LogoutCommand { get; }

        private readonly DispatcherTimer _refreshTimer;

        // ✅ UPDATED CONSTRUCTOR (3 args)
        public ShellViewModel(
            IVRChatApiService api,
            RootViewModel root,
            SessionStore session)
        {
            _api = api;
            _root = root;
            _session = session;

            CurrentViewModel = new WelcomeViewModel();

            LogoutCommand = new RelayCommand(Logout);

            _ = LoadGroupsAsync();

            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(45)
            };
            _refreshTimer.Tick += async (_, _) => await LoadGroupsAsync();
            _refreshTimer.Start();
        }

        private void Logout()
        {
            _refreshTimer.Stop();

            Groups.Clear();
            SelectedGroup = null;

            // 🔥 Navigate back to login via RootViewModel
            _root.Logout();
        }

        public async Task LoadGroupsAsync()
        {
            if (!_api.IsLoggedIn)
                return;

            var groups = await _api.GetMyManageableGroupsAsync(take: 10);

            Groups.Clear();
            foreach (var g in groups)
                Groups.Add(g);

            SelectedGroup ??= Groups.FirstOrDefault();
        }
    }
}
