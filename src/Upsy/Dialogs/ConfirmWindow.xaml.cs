using System.Windows;

namespace Upsy.Dialogs;

/// <summary>One confirmation that names the object and says how to undo it; Cancel, Enter and Escape keep things as they are.</summary>
public partial class ConfirmWindow : DialogWindow
{
    public ConfirmWindow(string heading, string body, string confirm)
    {
        InitializeComponent();
        Title = heading;
        Heading.Text = heading;
        Body.Text = body;
        ConfirmButton.Content = confirm;
        // The safe choice has focus: Enter keeps things as they are.
        Loaded += (_, _) => CancelButton.Focus();
    }

    private void OnConfirm(object sender, RoutedEventArgs e) => DialogResult = true;
}
