using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using VrchatgroupApp.clean.Services;
using VrchatgroupApp.clean.ViewModels;

namespace VrchatgroupApp.clean
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;

            var api = App.Services.GetRequiredService<IVRChatApiService>();
            var store = App.Services.GetRequiredService<SessionStore>();

            var root = new RootViewModel(api);
            DataContext = root;

            await TryRestoreSessionAsync(root, api, store);
        }

        private async Task TryRestoreSessionAsync(
            RootViewModel root,
            IVRChatApiService api,
            SessionStore store)
        {
            StoredSession? session = await store.LoadAsync();

            if (session != null)
            {
                var result = await api.RestoreSessionAsync(
                    session.AuthCookie,
                    session.TwoFactorCookie
                );

                if (result.Success)
                {
                    root.ShowShell(); // ✅ MVVM navigation
                    return;
                }

                await store.ClearAsync();
            }

            // Default: show login (RootViewModel already does this)
        }
    }
}
