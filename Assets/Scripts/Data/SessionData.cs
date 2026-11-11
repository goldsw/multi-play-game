namespace MushroomSurvival
{
    public sealed class SessionData
    {
        public SessionStatus Status { get; }
        public string JoinCode { get; }
        public int PlayerCount { get; }
        public bool LocalIsHost { get; }
        public string Message { get; }

        public SessionData(
            SessionStatus status,
            string joinCode = "",
            int playerCount = 0,
            bool localIsHost = false,
            string message = "")
        {
            Status = status;
            JoinCode = joinCode;
            PlayerCount = playerCount;
            LocalIsHost = localIsHost;
            Message = message;
        }
    }
}
