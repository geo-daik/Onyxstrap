namespace Onyxstrap.Integrations
{
    internal sealed class OverlayToggle
    {
        private bool _wasDown;
        public bool IsOpen { get; private set; }

        public bool Update(bool shortcutDown, bool gameFocused)
        {
            if (shortcutDown && !_wasDown && gameFocused) IsOpen = !IsOpen;
            _wasDown = shortcutDown;
            return IsOpen && gameFocused;
        }

        public void Close() => IsOpen = false;
    }
}
