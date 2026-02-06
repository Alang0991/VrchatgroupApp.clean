using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using VrchatgroupApp.clean.Services;
using VrchatgroupApp.clean.ViewModels;

namespace VrchatgroupApp.clean
{
    public partial class App : Application
    {
        public static ServiceProvider Services { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            var services = new ServiceCollection();

            // Services
            services.AddSingleton<IVRChatApiService, VRChatApiService>();
            services.AddSingleton<SessionStore>();

            // ViewModels
            services.AddSingleton<LoginViewModel>();
            services.AddSingleton<ShellViewModel>();
            services.AddSingleton<WelcomeViewModel>();

            Services = services.BuildServiceProvider();

            base.OnStartup(e);
        }
    }
}
