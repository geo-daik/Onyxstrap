using System.Windows;

namespace Onyxstrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Generic single-value text prompt, used for renaming accounts and naming presets.
    /// </summary>
    public partial class TextPromptDialog
    {
        public MessageBoxResult Result = MessageBoxResult.Cancel;

        /// <summary>
        /// The trimmed input, or null if the dialog was cancelled.
        /// </summary>
        public string? TextInput { get; private set; }

        public TextPromptDialog(string title, string label, string initialValue)
        {
            InitializeComponent();

            Title = title;
            LabelTextBlock.Text = label;
            InputTextBox.Text = initialValue;
            InputTextBox.SelectAll();
            InputTextBox.Focus();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            TextInput = InputTextBox.Text.Trim();
            Close();
        }
    }
}
