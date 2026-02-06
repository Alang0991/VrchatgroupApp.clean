using System.Threading.Tasks;
using System.Windows.Media;
using VrchatgroupApp.clean.Services;

namespace VrchatgroupApp.clean.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly IVRChatApiService _api;
        private readonly SessionStore _sessionStore;
        private readonly RootViewModel _root; // 🔴 NEW

        public LoginViewModel(
            IVRChatApiService api,
            RootViewModel root,          // 🔴 NEW
            SessionStore sessionStore)
        {
            _api = api;
            _root = root;                // 🔴 NEW
            _sessionStore = sessionStore;

            LoginCommand = new RelayCommand(async () => await LoginAsync(), () => !IsLoading);
            Verify2FACommand = new RelayCommand(async () => await Verify2FAAsync(), () => !IsLoading);
        }

        // ───── Bindings ─────
        private string _username = "";
        public string Username
        {
            get => _username;
            set => Set(ref _username, value);
        }

        public string Password { get; set; } = "";

        private string _twoFactorCode = "";
        public string TwoFactorCode
        {
            get => _twoFactorCode;
            set => Set(ref _twoFactorCode, value);
        }

        private bool _showTwoFactor;
        public bool ShowTwoFactor
        {
            get => _showTwoFactor;
            set => Set(ref _showTwoFactor, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        private string? _statusMessage;
        public string? StatusMessage
        {
            get => _statusMessage;
            set => Set(ref _statusMessage, value);
        }

        private Brush _statusColor = Brushes.LightGray;
        public Brush StatusColor
        {
            get => _statusColor;
            set => Set(ref _statusColor, value);
        }

        public RelayCommand LoginCommand { get; }
        public RelayCommand Verify2FACommand { get; }

        // ───── Login flow ─────

        private async Task LoginAsync()
        {
            IsLoading = true;
            StatusMessage = null;

            try
            {
                var result = await _api.LoginAsync(Username, Password);

                if (result.Requires2FA)
                {
                    ShowTwoFactor = true;
                    StatusMessage = "2FA required";
                    StatusColor = Brushes.Orange;
                    return;
                }

                if (!result.Success)
                {
                    StatusMessage = result.Message ?? "Login failed";
                    StatusColor = Brushes.IndianRed;
                    return;
                }

                await SaveSessionAsync();

                // 🔥 THIS IS THE MISSING PIECE
                _root.ShowShell();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task Verify2FAAsync()
        {
            IsLoading = true;
            StatusMessage = null;

            try
            {
                var result = await _api.Verify2FAAsync(TwoFactorCode, "totp");

                if (!result.Success)
                {
                    StatusMessage = result.Message ?? "Invalid 2FA code";
                    StatusColor = Brushes.IndianRed;
                    return;
                }

                ShowTwoFactor = false;

                await SaveSessionAsync();

                // 🔥 ALSO GO TO SHELL HERE
                _root.ShowShell();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SaveSessionAsync()
        {
            await _sessionStore.SaveAsync(new StoredSession
            {
                AuthCookie = _api.GetAuthCookie()!,
                TwoFactorCookie = _api.GetTwoFactorCookie(),
                UserId = _api.CurrentUserId,
                DisplayName = _api.CurrentUserDisplayName,
                ProfilePicUrl = _api.CurrentUserProfilePicUrl
            });
        }
    }
}
