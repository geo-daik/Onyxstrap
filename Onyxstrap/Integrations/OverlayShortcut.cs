using System.Windows.Input;

namespace Onyxstrap.Integrations
{
    internal readonly record struct OverlayShortcut(int VirtualKey, int Modifiers)
    {
        // Modifier bits match WPF ModifierKeys: Alt=1, Control=2, Shift=4.
        internal static OverlayShortcut Default => new(0xA1, 0);
        internal static int ModifierFor(int key) => key switch
        {
            0xA0 or 0xA1 => 4, 0xA2 or 0xA3 => 2, 0xA4 or 0xA5 => 1, _ => 0
        };
        internal static bool TryCreate(int key, int modifiers, out OverlayShortcut shortcut)
        {
            shortcut = Default;
            bool validKey = key is 8 or 9 or 13 or 19 or 20 or >= 32 and <= 40 or 45 or 46
                or >= 48 and <= 57 or >= 65 and <= 90 or >= 96 and <= 111
                or >= 112 and <= 135 or 144 or 145 or >= 160 and <= 165
                or >= 186 and <= 192 or >= 219 and <= 222 or 226;
            if (!validKey || (modifiers & ~7) != 0) return false;
            modifiers &= ~ModifierFor(key);
            // The Spotify Discord presence shortcut is already Ctrl+Alt+S.
            if (key == 0x53 && modifiers == 3) return false;
            shortcut = new(key, modifiers);
            return true;
        }
        internal static OverlayShortcut FromSettings(int key, int modifiers) =>
            TryCreate(key, modifiers, out var shortcut) ? shortcut : Default;

        internal bool IsDown(Func<int, bool> down)
        {
            if (!down(VirtualKey) || down(0x5B) || down(0x5C)) return false;
            int held = (down(0xA4) || down(0xA5) ? 1 : 0)
                | (down(0xA2) || down(0xA3) ? 2 : 0)
                | (down(0xA0) || down(0xA1) ? 4 : 0);
            return (held & ~ModifierFor(VirtualKey)) == Modifiers;
        }

        internal string Label
        {
            get
            {
                string key = VirtualKey switch
                {
                    0xA0 => "Left Shift", 0xA1 => "Right Shift", 0xA2 => "Left Ctrl", 0xA3 => "Right Ctrl",
                    0xA4 => "Left Alt", 0xA5 => "Right Alt", 32 => "Space", 33 => "Page Up", 34 => "Page Down",
                    >= 48 and <= 57 => ((char)VirtualKey).ToString(),
                    _ => KeyInterop.KeyFromVirtualKey(VirtualKey).ToString()
                };
                return ((Modifiers & 2) != 0 ? "Ctrl + " : "")
                    + ((Modifiers & 1) != 0 ? "Alt + " : "")
                    + ((Modifiers & 4) != 0 ? "Shift + " : "") + key;
            }
        }
    }
}
