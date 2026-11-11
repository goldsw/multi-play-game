using System;
using System.Collections.Generic;

namespace MushroomSurvival
{
    public sealed class SessionData
    {
        public SessionStatus Status { get; }
        public string JoinCode { get; }
        public int PlayerCount { get; }
        public bool LocalIsHost { get; }
        public string Message { get; }
        public IReadOnlyList<LobbySlotData> Slots { get; }

        public SessionData(
            SessionStatus status,
            string joinCode = "",
            int playerCount = 0,
            bool localIsHost = false,
            string message = "",
            LobbySlotData[] slots = null)
        {
            Status = status;
            JoinCode = joinCode;
            PlayerCount = playerCount;
            LocalIsHost = localIsHost;
            Message = message;
            Slots = Array.AsReadOnly(slots ?? new LobbySlotData[LobbySettings.PlayerCount]);
        }
    }
}
