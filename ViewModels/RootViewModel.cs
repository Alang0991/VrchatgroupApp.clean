using VrchatgroupApp.clean.Services;

namespace VrchatgroupApp.clean.ViewModels
{
    public class RootViewModel : ViewModelBase
    {
        private object? _currentViewModel;
        public object? CurrentViewModel
        {
            get => _currentViewModel;
            set => Set(ref _currentViewModel, value);
        }

        public SessionStore Session { get; }

        private readonly IVRChatApiService _api;

        public RootViewModel(IVRChatApiService api)
        {
            _api = api;
            Session = new SessionStore();

            // Start on login
            CurrentViewModel = new LoginViewModel(_api, this, Session);
        }

        public void ShowShell()
        {
            CurrentViewModel = new ShellViewModel(_api, this, Session);
        }

        public void Logout()
        {
            // 🔥 THIS is enough
            _api.Logout();

            // Go back to login, session store stays available
            CurrentViewModel = new LoginViewModel(_api, this, Session);
        }
    }
}
