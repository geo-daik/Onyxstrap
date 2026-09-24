using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;

using Onyxstrap.UI.Elements.Bootstrapper.Base;
using Onyxstrap.UI.ViewModels.Bootstrapper;

namespace Onyxstrap.UI.Elements.Bootstrapper
{
    /// <summary>
    /// Onyxstrap's own cutscene-style bootstrapper dialog.
    /// </summary>
    public partial class OnyxDialog : IBootstrapperDialog
    {
        private readonly OnyxDialogViewModel _viewModel;

        public Onyxstrap.Bootstrapper? Bootstrapper { get; set; }

        private bool _isClosing;

        #region UI Elements
        public string Message
        {
            get => _viewModel.Message;
            set
            {
                string message = value;
                if (message.EndsWith("..."))
                    message = message[..^3];

                _viewModel.Message = message;
                _viewModel.OnPropertyChanged(nameof(_viewModel.Message));
            }
        }

        public ProgressBarStyle ProgressStyle
        {
            get => _viewModel.ProgressIndeterminate ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            set
            {
                _viewModel.ProgressIndeterminate = (value == ProgressBarStyle.Marquee);
                _viewModel.OnPropertyChanged(nameof(_viewModel.ProgressIndeterminate));
            }
        }

        public int ProgressMaximum
        {
            get => _viewModel.ProgressMaximum;
            set
            {
                _viewModel.ProgressMaximum = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.ProgressMaximum));
            }
        }

        public int ProgressValue
        {
            get => _viewModel.ProgressValue;
            set
            {
                _viewModel.ProgressValue = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.ProgressValue));

                // the halo tightens and brightens as the download advances
                if (ProgressMaximum > 0)
                {
                    double ratio = Math.Clamp(ProgressValue / (double)ProgressMaximum, 0, 1);
                    Halo.Opacity = 0.55 + 0.45 * ratio;
                    Halo.StrokeThickness = 2 + 1.5 * ratio;
                }
            }
        }

        public TaskbarItemProgressState TaskbarProgressState
        {
            get => _viewModel.TaskbarProgressState;
            set
            {
                _viewModel.TaskbarProgressState = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.TaskbarProgressState));
            }
        }

        public double TaskbarProgressValue
        {
            get => _viewModel.TaskbarProgressValue;
            set
            {
                _viewModel.TaskbarProgressValue = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.TaskbarProgressValue));
            }
        }

        public bool CancelEnabled
        {
            get => _viewModel.CancelEnabled;
            set
            {
                _viewModel.CancelEnabled = value;

                _viewModel.OnPropertyChanged(nameof(_viewModel.CancelEnabled));
                _viewModel.OnPropertyChanged(nameof(_viewModel.CancelButtonVisibility));
                _viewModel.OnPropertyChanged(nameof(_viewModel.VersionTextVisibility));
                _viewModel.OnPropertyChanged(nameof(_viewModel.VersionText));
            }
        }
        #endregion

        public OnyxDialog()
        {
            string version = Utilities.GetRobloxVersionStr(Bootstrapper?.IsStudioLaunch ?? false);
            _viewModel = new OnyxDialogViewModel(this, version);
            DataContext = _viewModel;
            Title = App.Settings.Prop.BootstrapperTitle;
            Icon = App.Settings.Prop.BootstrapperIcon.GetIcon().GetImageSource();

            InitializeComponent();

            Loaded += (_, _) => SpawnParticles();
        }

        /// <summary>
        /// Spawns a slow field of drifting violet motes behind the centerpiece.
        /// </summary>
        private void SpawnParticles()
        {
            var random = new Random(137);

            for (int i = 0; i < 26; i++)
            {
                double size = 1.5 + random.NextDouble() * 2.5;
                double duration = 7 + random.NextDouble() * 8;
                double delay = random.NextDouble() * 9;

                var particle = new System.Windows.Shapes.Ellipse
                {
                    Width = size,
                    Height = size,
                    Opacity = 0,
                    Fill = new SolidColorBrush(Color.FromArgb((byte)(70 + random.Next(110)), 185, 175, 255))
                };

                Canvas.SetLeft(particle, random.NextDouble() * Math.Max(ActualWidth, 1));
                Canvas.SetTop(particle, ActualHeight + 10);
                ParticleCanvas.Children.Add(particle);

                var drift = new DoubleAnimation(ActualHeight + 10, -20, TimeSpan.FromSeconds(duration))
                {
                    BeginTime = TimeSpan.FromSeconds(delay),
                    RepeatBehavior = RepeatBehavior.Forever
                };

                var flicker = new DoubleAnimation(0.1, 0.6, TimeSpan.FromSeconds(1.2 + random.NextDouble() * 1.6))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromSeconds(delay)
                };

                particle.BeginAnimation(Canvas.TopProperty, drift);
                particle.BeginAnimation(OpacityProperty, flicker);
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_isClosing)
                Bootstrapper?.Cancel();
        }

        #region IBootstrapperDialog Methods
        public void ShowBootstrapper() => this.ShowDialog();

        public void CloseBootstrapper()
        {
            _isClosing = true;
            Dispatcher.BeginInvoke(this.Close);
        }

        /// <summary>
        /// Plays an in-dialog success burst instead of throwing up a generic
        /// message box: halo flash, then hand off to the callback and terminate.
        /// </summary>
        public void ShowSuccess(string message, Action? callback)
        {
            Message = message;
            _isClosing = true;

            Halo.Opacity = 1;
            Halo.StrokeThickness = 4;

            var flash = new DoubleAnimation(0, 0.9, TimeSpan.FromSeconds(0.4))
            {
                AutoReverse = true,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            SuccessFlash.BeginAnimation(OpacityProperty, flash);

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.3)
            };

            timer.Tick += (_, _) =>
            {
                timer.Stop();
                callback?.Invoke();
                App.Terminate();
            };

            timer.Start();
        }
        #endregion
    }
}
