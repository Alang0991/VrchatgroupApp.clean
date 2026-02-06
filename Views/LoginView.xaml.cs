using System.Windows;
using System.Windows.Controls;
using VrchatgroupApp.clean.ViewModels;

namespace VrchatgroupApp.clean.Views
{
    public partial class LoginView : UserControl
    {
        public LoginView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is not LoginViewModel)
                return;

            PasswordBox.PasswordChanged -= OnPasswordChanged;
            PasswordBox.PasswordChanged += OnPasswordChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            PasswordBox.PasswordChanged -= OnPasswordChanged;
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel vm)
                vm.Password = PasswordBox.Password;
        }
    }
}
