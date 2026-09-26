using System.Windows;
namespace Onyxstrap.UI.ViewModels.Bootstrapper
{
    public class OnyxDialogViewModel : BootstrapperDialogViewModel
    {
        private string _percentText = "";

        public string PercentText
        {
            get => _percentText;
            set
            {
                _percentText = value;
                OnPropertyChanged(nameof(PercentText));
                OnPropertyChanged(nameof(PercentTextVisibility));
            }
        }

        public Visibility PercentTextVisibility => ProgressIndeterminate ? Visibility.Collapsed : Visibility.Visible;

        public Visibility VersionTextVisibility => CancelEnabled ? Visibility.Collapsed : Visibility.Visible;

        public string VersionText { get; init; }

        public OnyxDialogViewModel(IBootstrapperDialog dialog, string version) : base(dialog)
        {
            VersionText = version;
        }
    }
}
