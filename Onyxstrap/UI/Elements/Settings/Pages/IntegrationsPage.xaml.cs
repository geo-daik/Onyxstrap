using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using Onyxstrap.Integrations;

using Onyxstrap.UI.ViewModels.Settings;

namespace Onyxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for IntegrationsPage.xaml
    /// </summary>
    public partial class IntegrationsPage
    {
        public IntegrationsPage()
        {
            DataContext = new IntegrationsViewModel();
            InitializeComponent();
        }

        private bool _capturing;
        private Key _pendingModifier = Key.None;
        private const string ShortcutHelp = "Click the shortcut to change it. Save to apply it to your current game.";

        private void ChangeShortcut(object sender, RoutedEventArgs e)
        {
            _capturing = true;
            _pendingModifier = Key.None;
            ShortcutButton.SetCurrentValue(Button.ContentProperty, "Press shortcut…");
            ShortcutHint.Text = "Press a key or hold Ctrl, Alt or Shift with another key. Esc cancels.";
            ShortcutButton.Focus();
        }

        private void FinishCapture()
        {
            _capturing = false;
            _pendingModifier = Key.None;
            ShortcutButton.GetBindingExpression(Button.ContentProperty)?.UpdateTarget();
            ShortcutHint.Text = ShortcutHelp;
        }

        private void CaptureShortcutDown(object sender, KeyEventArgs e)
        {
            if (!_capturing) return;
            e.Handled = true;
            if (e.IsRepeat) return;
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape) { FinishCapture(); return; }
            if (OverlayShortcut.ModifierFor(KeyInterop.VirtualKeyFromKey(key)) != 0)
            {
                _pendingModifier = key;
                return;
            }
            _pendingModifier = Key.None;
            AcceptShortcut(key, Keyboard.Modifiers);
        }

        private void CaptureShortcutUp(object sender, KeyEventArgs e)
        {
            if (!_capturing) return;
            e.Handled = true;
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == _pendingModifier)
                AcceptShortcut(key, Keyboard.Modifiers);
        }

        private void AcceptShortcut(Key key, ModifierKeys modifiers)
        {
            if (!OverlayShortcut.TryCreate(KeyInterop.VirtualKeyFromKey(key), (int)modifiers, out var shortcut))
            {
                _pendingModifier = Key.None;
                ShortcutHint.Text = "Choose another shortcut. Windows keys and Ctrl + Alt + S are reserved. Esc cancels.";
                return;
            }
            ((IntegrationsViewModel)DataContext).SetSpotifyShortcut(shortcut);
            FinishCapture();
        }

        private void CancelShortcutCapture(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (_capturing) FinishCapture();
        }

        private void ResetShortcut(object sender, RoutedEventArgs e)
        {
            ((IntegrationsViewModel)DataContext).SetSpotifyShortcut(OverlayShortcut.Default);
            FinishCapture();
        }

        public void CustomIntegrationSelection(object sender, SelectionChangedEventArgs e)
        {
            IntegrationsViewModel viewModel = (IntegrationsViewModel)DataContext;
            viewModel.SelectedCustomIntegration = (CustomIntegration)((ListBox)sender).SelectedItem;
            viewModel.OnPropertyChanged(nameof(viewModel.SelectedCustomIntegration));
        }
    }
}
