namespace SCJam.UISystem
{
    public readonly struct PopupNextLevelData
    {
        public readonly int CompletedLevel;
        public readonly bool HasNextLevel;

        public PopupNextLevelData(int completedLevel, bool hasNextLevel)
        {
            CompletedLevel = completedLevel;
            HasNextLevel = hasNextLevel;
        }
    }
}
