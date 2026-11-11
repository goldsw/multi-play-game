using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;

namespace MushroomSurvival
{
    public sealed class RelaySessionConnector : ISessionConnector
    {
        private ISession session;
        private readonly string profile;

        public RelaySessionConnector(string authenticationProfile = "default")
        {
            profile = authenticationProfile;
        }

        public async Task<string> CreateAsync()
        {
            await AuthenticateAsync();
            session = await MultiplayerService.Instance.CreateSessionAsync(
                new SessionOptions { MaxPlayers = LobbySettings.PlayerCount }
                    .WithRelayNetwork());
            return session.Code;
        }

        public async Task JoinAsync(string code)
        {
            await AuthenticateAsync();
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
        }

        public async Task LeaveAsync()
        {
            var previousSession = session;
            session = null;
            if (previousSession != null)
            {
                if (previousSession.IsHost)
                    await previousSession.AsHost().DeleteAsync();
                else
                    await previousSession.LeaveAsync();
            }
        }

        private async Task AuthenticateAsync()
        {
            await UnityServices.InitializeAsync(new InitializationOptions().SetProfile(profile));
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }
}
