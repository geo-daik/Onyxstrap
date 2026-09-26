using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

using Onyxstrap.UI.ViewModels.Settings;

namespace Onyxstrap.UI.Elements.Settings.Pages
{
    public partial class AccountsPage
    {
        private bool _initialLoad = false;

        private AccountsViewModel _viewModel = null!;

        public AccountsPage()
        {
            SetupViewModel();
            InitializeComponent();
        }

        private void SetupViewModel()
        {
            _viewModel = new AccountsViewModel();
            DataContext = _viewModel;
        }

        /// <summary>
        /// Rebuilds the view model, e.g. when background avatar fetches complete.
        /// </summary>
        public void ReloadViewModel()
        {
            SetupViewModel();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // refresh the view model on page load to reflect any vault changes
            if (!_initialLoad)
            {
                _initialLoad = true;
                return;
            }

            SetupViewModel();
        }

        /// <summary>
        /// Staggers the account cards rising into place as they load.
        /// </summary>
        private void AccountCard_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Wpf.Ui.Controls.Card card || card.DataContext is not AccountsViewModel.AccountEntry entry)
                return;

            int index = 0;
            if (_viewModel is not null)
            {
                var entries = _viewModel.AccountEntries.ToList();
                index = Math.Max(0, entries.FindIndex(x => x.Id == entry.Id));
            }

            // transforms declared in a DataTemplate arrive frozen; swap in a
            // live unfrozen copy so the entrance animation can run
            if (card.RenderTransform is not TranslateTransform transform || transform.IsFrozen)
            {
                transform = new TranslateTransform(0, 14);
                card.RenderTransform = transform;
            }

            transform.BeginAnimation(
                TranslateTransform.YProperty,
                new System.Windows.Media.Animation.DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(320))
                {
                    BeginTime = TimeSpan.FromMilliseconds(index * 55),
                    EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                }
            );
        }
    }
}
