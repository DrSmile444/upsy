using System.Windows;
using System.Windows.Input;
using Upsy.Core.Protocol.EcoFlow;

namespace Upsy.Dialogs;

/// <summary>EcoFlow sign-in (DESIGN.md "Sign-in window"): the password goes to EcoFlow once and is never kept.</summary>
public partial class SignInWindow : DialogWindow
{
    private readonly Func<string, string, CancellationToken, Task<EcoFlowSignIn>> _signIn;
    private bool _closed;

    public SignInWindow(string model, Func<string, string, CancellationToken, Task<EcoFlowSignIn>> signIn)
    {
        _signIn = signIn;
        InitializeComponent();
        Intro.Text = $"Your {model} shares data only with the EcoFlow account it is added to. upsy asks EcoFlow once for that account’s user ID and keeps only the ID, encrypted for your Windows account. Your password is not stored.";
        Loaded += (_, _) => Keyboard.Focus(EmailBox);
        // A sign-in still on its way when the window closes is ignored; the request itself is short (15 s timeout).
        Closed += (_, _) => _closed = true;
    }

    /// <summary>The account's user ID after a successful sign-in; null when cancelled.</summary>
    public string? UserId { get; private set; }

    private async void OnSignIn(object sender, RoutedEventArgs e)
    {
        var email = EmailBox.Text.Trim();
        if (email.Length == 0)
        {
            Keyboard.Focus(EmailBox);
            return;
        }

        if (PasswordBox.Password.Length == 0)
        {
            Keyboard.Focus(PasswordBox);
            return;
        }

        SetBusy(true);
        EcoFlowSignIn result;
        try
        {
            result = await _signIn(email, PasswordBox.Password, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            result = new EcoFlowSignIn(null, "EcoFlow did not answer in time. Signing in needs internet once.");
        }

        if (_closed)
        {
            return;
        }

        SetBusy(false);
        if (result.Succeeded)
        {
            UserId = result.UserId;
            PasswordBox.Clear();
            DialogResult = true;
            return;
        }

        ErrorText.Text = result.Error;
        ErrorBar.Visibility = Visibility.Visible;
        PasswordBox.SelectAll();
        Keyboard.Focus(PasswordBox);
    }

    private void SetBusy(bool busy)
    {
        SignInButton.IsEnabled = !busy;
        EmailBox.IsEnabled = !busy;
        PasswordBox.IsEnabled = !busy;
        SignInButton.Content = busy ? "Signing in…" : "Sign in";
    }
}
