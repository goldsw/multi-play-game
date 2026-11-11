namespace MushroomSurvival
{
    public static class LobbySettings
    {
        public const int PlayerCount = 4;
        public const int DefaultCharacterId = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public const int MinimumPlayersToStart = 1;
#else
        public const int MinimumPlayersToStart = PlayerCount;
#endif
    }
}
