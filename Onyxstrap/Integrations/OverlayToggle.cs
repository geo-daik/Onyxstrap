namespace Onyxstrap.Integrations
{
    internal sealed class OverlayToggle
    {
        private bool _wasDown;
        public bool IsOpen { get; private set; }

        public bool Update(bool rightShiftDown, bool gameFocused)
        {
            if (rightShiftDown && !_wasDown && gameFocused) IsOpen = !IsOpen;
            _wasDown = rightShiftDown;
            return IsOpen && gameFocused;
        }

        public void Close() => IsOpen = false;
    }
}
