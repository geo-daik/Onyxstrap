using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

using Onyxstrap.Enums.FlagPresets;

namespace Onyxstrap.UI.ViewModels.Settings
{
    public class FastFlagsViewModel : NotifyPropertyChangedViewModel
    {
        private Dictionary<string, object>? _preResetFlags;

        public event EventHandler? RequestPageReloadEvent;
        
        public event EventHandler? OpenFlagEditorEvent;

        private void OpenFastFlagEditor() => OpenFlagEditorEvent?.Invoke(this, EventArgs.Empty);

        public ICommand OpenFastFlagEditorCommand => new RelayCommand(OpenFastFlagEditor);

        // Onyx quick presets: curated, known-working flag bundles applied to
        // the active account's flag set. Experimental ones are marked in the UI.
        public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> QuickPresets = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["Max FPS"] = new Dictionary<string, string>
            {
                ["DFIntTaskSchedulerTargetFps"] = "999",
                ["FFlagTaskSchedulerLimitTargetFpsTo2402"] = "False"
            },
            ["Balanced"] = new Dictionary<string, string>
            {
                ["FIntDebugForceMSAASamples"] = "2",
                ["DFFlagTextureQualityOverrideEnabled"] = "True",
                ["DFIntTextureQualityOverride"] = "2"
            },
            ["Potato"] = new Dictionary<string, string>
            {
                ["FIntDebugForceMSAASamples"] = "1",
                ["DFFlagTextureQualityOverrideEnabled"] = "True",
                ["DFIntTextureQualityOverride"] = "0"
            },
            ["Future lighting"] = new Dictionary<string, string>
            {
                ["FFlagDebugForceFutureIsBrightPhase1"] = "False",
                ["FFlagDebugForceFutureIsBrightPhase2"] = "False",
                ["FFlagDebugForceFutureIsBrightPhase3"] = "True"
            }
        };

        public static readonly IEnumerable<string> QuickPresetFlagKeys = QuickPresets.Values.SelectMany(x => x.Keys).Distinct();

        public IReadOnlyCollection<string> QuickPresetNames => QuickPresets.Keys.ToList();

        public ICommand ApplyQuickPresetCommand => new RelayCommand<string>(ApplyQuickPreset!);

        public ICommand ResetQuickPresetsCommand => new RelayCommand(ResetQuickPresets);

        private void ApplyQuickPreset(string name)
        {
            if (!QuickPresets.TryGetValue(name, out var flags))
                return;

            foreach (var pair in flags)
                App.FastFlags.SetValue(pair.Key, pair.Value);
        }

        private void ResetQuickPresets()
        {
            foreach (string key in QuickPresetFlagKeys)
                App.FastFlags.SetValue(key, null);
        }

        public Visibility CanShowFastFlagEditor => App.IsStudioInstalled ? Visibility.Visible : Visibility.Collapsed;

        public bool UseFastFlagManager
        {
            get => App.Settings.Prop.UseFastFlagManager;
            set => App.Settings.Prop.UseFastFlagManager = value;
        }

        public IReadOnlyDictionary<MSAAMode, string?> MSAALevels => FastFlagManager.MSAAModes;

        public MSAAMode SelectedMSAALevel
        {
            get => MSAALevels.FirstOrDefault(x => x.Value == App.FastFlags.GetPreset("Rendering.MSAA")).Key;
            set => App.FastFlags.SetPreset("Rendering.MSAA", MSAALevels[value]);
        }

        public bool FixDisplayScaling
        {
            get => App.FastFlags.GetPreset("Rendering.DisableScaling") == "True";
            set => App.FastFlags.SetPreset("Rendering.DisableScaling", value ? "True" : null);
        }

        public IReadOnlyDictionary<TextureQuality, string?> TextureQualities => FastFlagManager.TextureQualityLevels;

        public TextureQuality SelectedTextureQuality
        {
            get => TextureQualities.Where(x => x.Value == App.FastFlags.GetPreset("Rendering.TextureQuality.Level")).FirstOrDefault().Key;
            set
            {
                if (value == TextureQuality.Default)
                {
                    App.FastFlags.SetPreset("Rendering.TextureQuality", null);
                }
                else
                {
                    App.FastFlags.SetPreset("Rendering.TextureQuality.OverrideEnabled", "True");
                    App.FastFlags.SetPreset("Rendering.TextureQuality.Level", TextureQualities[value]);
                }
            }
        }
        public bool ResetConfiguration
        {
            get => _preResetFlags is not null;

            set
            {
                if (value)
                {
                    _preResetFlags = new(App.FastFlags.Prop);
                    App.FastFlags.Prop.Clear();
                }
                else
                {
                    App.FastFlags.Prop = _preResetFlags!;
                    _preResetFlags = null;
                }

                RequestPageReloadEvent?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
