using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using PingMonitor.Core;

namespace PingMonitor.Avalonia
{
    // A message box with a text and one or two buttons - Avalonia does not ship one itself.
    internal sealed partial class MessageDialog : Window
    {
        public MessageDialog()
        {
            InitializeComponent();
            Title = DisplayState.AppName;
        }

        // Shows a notice that the user can only acknowledge.
        public static Task ShowAsync(Window owner, string text)
        {
            MessageDialog dialog = new MessageDialog();
            dialog.message.Text = text;
            dialog.AddButton("OK", true, true, true);
            return dialog.ShowDialog(owner);
        }

        // Asks a question; returns true if the user answers yes.
        public static Task<bool> AskAsync(Window owner, string question)
        {
            MessageDialog dialog = new MessageDialog();
            dialog.message.Text = question;
            dialog.AddButton("Yes", true, true, false);
            dialog.AddButton("No", false, false, true);
            return dialog.ShowDialog<bool>(owner);
        }

        // "isDefault" = the Enter key triggers the button, "isCancel" = the Escape key does.
        private void AddButton(string text, bool result, bool isDefault, bool isCancel)
        {
            Button button = new Button();
            button.Content = text;
            button.MinWidth = 80;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.IsDefault = isDefault;
            button.IsCancel = isCancel;
            button.Click += delegate { Close(result); };
            buttons.Children.Add(button);
        }
    }
}
