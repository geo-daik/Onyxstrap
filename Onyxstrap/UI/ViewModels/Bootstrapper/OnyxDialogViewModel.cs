using System.Windows;
namespace Onyxstrap.UI.ViewModels.Bootstrapper
{
    public class OnyxDialogViewModel : BootstrapperDialogViewModel
    {
        public Visibility VersionTextVisibility => CancelEnabled ? Visibility.Collapsed : Visibility.Visible;

        public string VersionText { get; init; }

        public OnyxDialogViewModel(IBootstrapperDialog dialog, string version) : base(dialog)
        {
            VersionText = version;
        }
    }
}
