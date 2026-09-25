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

        /// <summary>
        /// Feedback line under the preset sections, so applying a preset is
        /// visibly acknowledged instead of silently staging flags.
        /// </summary>
        public string LastActionMessage
        {
            get => _lastActionMessage;
            private set
            {
                _lastActionMessage = value;
                OnPropertyChanged(nameof(LastActionMessage));
                OnPropertyChanged(nameof(LastActionVisibility));
            }
        }

        private string _lastActionMessage = "";

        public Visibility LastActionVisibility => string.IsNullOrEmpty(_lastActionMessage) ? Visibility.Collapsed : Visibility.Visible;

        private void ApplyQuickPreset(string name)
        {
            if (!QuickPresets.TryGetValue(name, out var flags))
                return;

            foreach (var pair in flags)
                App.FastFlags.SetValue(pair.Key, pair.Value);

            CommitPresets($"Applied '{name}' - saved to the active account's flags.");
        }

        private void ResetQuickPresets()
        {
            foreach (string key in AllPresetFlagKeys)
                App.FastFlags.SetValue(key, null);

            CommitPresets("Removed every flag set by the presets.");
        }

        /// <summary>
        /// Presets commit immediately (and sync into the active account's set)
        /// so a click has an instant, visible effect.
        /// </summary>
        private void CommitPresets(string message)
        {
            App.FastFlags.Save();
            LastActionMessage = message;
        }

        // Game presets: flag bundles tuned per game genre. The engine flags are
        // global, so "for a game" means the mix that plays best in that genre -
        // FPS headroom, render quality, and latency trade-offs.
        public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> GamePresets = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["Simulators (Blox Fruits, Pet Sim)"] = new Dictionary<string, string>
            {
                // grinding games are particle/UI heavy - everything on low, uncapped FPS
                ["DFIntTaskSchedulerTargetFps"] = "999",
                ["FFlagTaskSchedulerLimitTargetFpsTo2402"] = "False",
                ["DFFlagTextureQualityOverrideEnabled"] = "True",
                ["DFIntTextureQualityOverride"] = "0",
                ["FIntDebugForceMSAASamples"] = "1",
                ["FIntRenderShadowIntensity"] = "0",
                ["FFlagDisablePostFx"] = "True"
            },
            ["Shooters (Arsenal, Phantom Forces)"] = new Dictionary<string, string>
            {
                // latency first: uncapped FPS, low detail, D3D11 for the most stable frame pacing
                ["DFIntTaskSchedulerTargetFps"] = "999",
                ["FFlagTaskSchedulerLimitTargetFpsTo2402"] = "False",
                ["DFFlagTextureQualityOverrideEnabled"] = "True",
                ["DFIntTextureQualityOverride"] = "1",
                ["FIntDebugForceMSAASamples"] = "1",
                ["FIntRenderShadowIntensity"] = "0",
                ["FFlagDisablePostFx"] = "True",
                ["FFlagDebugGraphicsPreferD3D11"] = "True"
            },
            ["Obby / Parkour"] = new Dictionary<string, string>
            {
                // precision platforming wants frame consistency over eye candy
                ["DFIntTaskSchedulerTargetFps"] = "999",
                ["FFlagTaskSchedulerLimitTargetFpsTo2402"] = "False",
                ["DFFlagTextureQualityOverrideEnabled"] = "True",
                ["DFIntTextureQualityOverride"] = "1",
                ["FIntDebugForceMSAASamples"] = "1",
                ["FFlagDisablePostFx"] = "True"
            },
            ["Roleplay (Brookhaven, Adopt Me)"] = new Dictionary<string, string>
            {
                // social hangouts - keep it pretty, still smooth
                ["DFIntTaskSchedulerTargetFps"] = "120",
                ["FFlagTaskSchedulerLimitTargetFpsTo2402"] = "False",
                ["DFFlagTextureQualityOverrideEnabled"] = "True",
                ["DFIntTextureQualityOverride"] = "2",
                ["FIntDebugForceMSAASamples"] = "2"
            },
            ["Story / Horror (Doors)"] = new Dictionary<string, string>
            {
                // atmosphere is the point - keep quality, modest FPS target
                ["DFIntTaskSchedulerTargetFps"] = "120",
                ["FFlagTaskSchedulerLimitTargetFpsTo2402"] = "False",
                ["DFFlagTextureQualityOverrideEnabled"] = "True",
                ["DFIntTextureQualityOverride"] = "3",
                ["FIntDebugForceMSAASamples"] = "4"
            },
            ["Competitive (ranked, tournament)"] = new Dictionary<string, string>
            {
                // same as shooters but rock-solid 240 target for high-refresh monitors
                ["DFIntTaskSchedulerTargetFps"] = "240",
                ["FFlagTaskSchedulerLimitTargetFpsTo2402"] = "False",
                ["DFFlagTextureQualityOverrideEnabled"] = "True",
                ["DFIntTextureQualityOverride"] = "1",
                ["FIntDebugForceMSAASamples"] = "1",
                ["FIntRenderShadowIntensity"] = "0",
                ["FFlagDisablePostFx"] = "True",
                ["FFlagDebugGraphicsPreferD3D11"] = "True"
            }
        };

        public IReadOnlyCollection<string> GamePresetNames => GamePresets.Keys.ToList();

        private string? _selectedGamePreset;

        public string? SelectedGamePreset
        {
            get => _selectedGamePreset ?? GamePresetNames.FirstOrDefault();
            set
            {
                _selectedGamePreset = value;
                OnPropertyChanged(nameof(SelectedGamePreset));
            }
        }

        public ICommand ApplyGamePresetCommand => new RelayCommand(ApplyGamePreset);

        private void ApplyGamePreset()
        {
            if (SelectedGamePreset is not null && GamePresets.TryGetValue(SelectedGamePreset, out var flags))
            {
                foreach (var pair in flags)
                    App.FastFlags.SetValue(pair.Key, pair.Value);

                CommitPresets($"Applied '{SelectedGamePreset}' - saved to the active account's flags.");
            }
        }

        public static IEnumerable<string> AllPresetFlagKeys =>
            QuickPresets.Values.SelectMany(x => x.Keys)
            .Concat(GamePresets.Values.SelectMany(x => x.Keys))
            .Distinct();

        public Visibility CanShowFastFlagEditor => App.IsStudioInstalled ? Visibility.Visible : Visibility.Collapsed;

        // flags follow the active account - make that visible in the editor
        public bool HasAccounts => App.Accounts.HasAccounts;

        public string ActiveAccountName => App.Accounts.ActiveAccount?.Name ?? "";

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
