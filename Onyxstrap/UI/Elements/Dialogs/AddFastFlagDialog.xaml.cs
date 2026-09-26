using Microsoft.Win32;
using System.Windows;
using Onyxstrap.Resources;

namespace Onyxstrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Interaction logic for AddFastFlagDialog.xaml
    /// </summary>
    public partial class AddFastFlagDialog
    {
        public MessageBoxResult Result = MessageBoxResult.Cancel;

        private bool _suppressSuggestions;

        public AddFastFlagDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Shows a filtered list of known flags under the name box - the
        /// "keyboard" that pops up when you click into the field.
        /// </summary>
        private void FlagNameTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // show known flags immediately when the field is clicked into
            if (string.IsNullOrWhiteSpace(FlagNameTextBox.Text))
            {
                SuggestionsList.ItemsSource = FastFlagCatalog.Names.Take(12).ToList();
                SuggestionsList.Visibility = Visibility.Visible;
            }
        }

        private void FlagNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_suppressSuggestions)
            {
                _suppressSuggestions = false;
                return;
            }

            var suggestions = FastFlagCatalog.Search(FlagNameTextBox.Text);

            if (suggestions.Count == 0)
            {
                SuggestionsList.Visibility = Visibility.Collapsed;
                SuggestionsList.ItemsSource = null;
                return;
            }

            SuggestionsList.ItemsSource = suggestions;
            SuggestionsList.Visibility = Visibility.Visible;
        }

        private void AcceptSuggestion()
        {
            if (SuggestionsList.SelectedItem is not string name)
            {
                if (SuggestionsList.Items.Count > 0)
                    name = (string)SuggestionsList.Items[0];
                else
                    return;
            }

            _suppressSuggestions = true;
            FlagNameTextBox.Text = name;
            FlagNameTextBox.CaretIndex = FlagNameTextBox.Text.Length;

            SuggestionsList.Visibility = Visibility.Collapsed;
            SuggestionsList.SelectedIndex = -1;

            FlagValueTextBox.Focus();
        }

        private void FlagNameTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (SuggestionsList.Visibility != Visibility.Visible)
                return;

            switch (e.Key)
            {
                case System.Windows.Input.Key.Down:
                    SuggestionsList.SelectedIndex = Math.Min(SuggestionsList.SelectedIndex + 1, SuggestionsList.Items.Count - 1);
                    SuggestionsList.ScrollIntoView(SuggestionsList.SelectedItem);
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.Up:
                    SuggestionsList.SelectedIndex = Math.Max(SuggestionsList.SelectedIndex - 1, 0);
                    SuggestionsList.ScrollIntoView(SuggestionsList.SelectedItem);
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.Enter:
                case System.Windows.Input.Key.Tab:
                    if (SuggestionsList.Items.Count > 0)
                    {
                        AcceptSuggestion();
                        e.Handled = true;
                    }
                    break;

                case System.Windows.Input.Key.Escape:
                    SuggestionsList.Visibility = Visibility.Collapsed;
                    e.Handled = true;
                    break;
            }
        }

        private void SuggestionsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (SuggestionsList.SelectedItem is string name)
            {
                _suppressSuggestions = true;
                FlagNameTextBox.Text = name;
                FlagNameTextBox.CaretIndex = FlagNameTextBox.Text.Length;
            }
        }

        private void SuggestionsList_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (SuggestionsList.SelectedItem is not null)
                AcceptSuggestion();
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = $"{Strings.FileTypes_JSONFiles}|*.json"
            };

            if (dialog.ShowDialog() != true)
                return;

            JsonTextBox.Text = File.ReadAllText(dialog.FileName);
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            Close();
        }
    }
}
