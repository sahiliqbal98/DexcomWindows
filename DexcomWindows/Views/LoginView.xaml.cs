using DexcomWindows.Models;
using DexcomWindows.Services;
using DexcomWindows.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DexcomWindows.Views;

public sealed partial class LoginView : UserControl
{
    public event EventHandler? LoginSucceeded;

    private GlucoseViewModel? _viewModel;

    public LoginView()
    {
        InitializeComponent();
        LoginMethodRadio.SelectionChanged += LoginMethodRadio_SelectionChanged;
    }

    public void SetViewModel(GlucoseViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    private void LoginMethodRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var isAccountId = LoginMethodRadio.SelectedIndex == 1;
        UsernamePanel.Visibility = isAccountId ? Visibility.Collapsed : Visibility.Visible;
        AccountIdPanel.Visibility = isAccountId ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        // Validate inputs
        var isAccountId = LoginMethodRadio.SelectedIndex == 1;
        var identifier = isAccountId ? AccountIdTextBox.Text.Trim() : UsernameTextBox.Text.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrEmpty(identifier))
        {
            ShowError(isAccountId ? "Please enter your Account ID" : "Please enter your username or email");
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowError("Please enter your password");
            return;
        }

        if (isAccountId && !DexcomShareAPI.IsValidAccountId(identifier))
        {
            ShowError("Invalid Account ID format. It should be a 32-character hexadecimal string.");
            return;
        }

        // Get selected server
        var serverItem = (ComboBoxItem)ServerComboBox.SelectedItem;
        var server = serverItem.Tag?.ToString() == "International"
            ? DexcomShareAPI.Server.International
            : DexcomShareAPI.Server.US;

        // Show loading state
        SetLoading(true);
        HideError();

        try
        {
            if (isAccountId)
            {
                await _viewModel.LoginWithAccountIdAsync(identifier, password, server);
            }
            else
            {
                await _viewModel.LoginAsync(identifier, password, server);
            }

            if (_viewModel.IsAuthenticated)
            {
                LoginSucceeded?.Invoke(this, EventArgs.Empty);
            }
            else if (_viewModel.Error != null)
            {
                ShowError(_viewModel.Error.Message, _viewModel.Error.RecoverySuggestion);
            }
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void AccountIdHelp_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Account ID",
            Content = new TextBlock
            {
                Text = "Your Account ID is a 32-character code found in the Dexcom app:\n\n" +
                       "1. Open the Dexcom G7 app\n" +
                       "2. Go to Settings → Account\n" +
                       "3. Look for 'Account ID'\n\n" +
                       "It looks like: a1b2c3d4-e5f6-7890-abcd-ef1234567890\n\n" +
                       "You can use this if username login doesn't work.",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 350
            },
            CloseButtonText = "OK",
            XamlRoot = XamlRoot
        };

        _ = dialog.ShowAsync();
    }

    private void SetLoading(bool isLoading)
    {
        LoginButton.IsEnabled = !isLoading;
        LoadingRing.IsActive = isLoading;
        LoadingRing.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        UsernameTextBox.IsEnabled = !isLoading;
        AccountIdTextBox.IsEnabled = !isLoading;
        PasswordBox.IsEnabled = !isLoading;
        ServerComboBox.IsEnabled = !isLoading;
    }

    private void ShowError(string message, string? details = null)
    {
        ErrorInfoBar.Message = details != null ? $"{message}\n{details}" : message;
        ErrorInfoBar.IsOpen = true;
    }

    private void HideError()
    {
        ErrorInfoBar.IsOpen = false;
    }

    public void ClearInputs()
    {
        UsernameTextBox.Text = "";
        AccountIdTextBox.Text = "";
        PasswordBox.Password = "";
        HideError();
    }
}
