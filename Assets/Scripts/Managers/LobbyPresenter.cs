using System;
using System.Threading.Tasks;
using UnityEngine;

namespace MushroomSurvival
{
    public sealed class LobbyPresenter : MonoBehaviour
    {
        private SessionManager manager;
        private LobbyView view;
        private LobbyPreviewView preview;
        private bool destroyed;

        public void Initialize(SessionManager sessionManager, LobbyView lobbyView, LobbyPreviewView lobbyPreview)
        {
            if (sessionManager == null)
                throw new ArgumentNullException(nameof(sessionManager));
            if (lobbyView == null)
                throw new ArgumentNullException(nameof(lobbyView));
            if (lobbyPreview == null)
                throw new ArgumentNullException(nameof(lobbyPreview));
            if (destroyed)
                return;

            Unsubscribe();
            manager = sessionManager;
            view = lobbyView;
            preview = lobbyPreview;
            manager.StateChanged += OnStateChanged;
            view.HostRequested += OnHostRequested;
            view.JoinRequested += OnJoinRequested;
            view.LeaveRequested += OnLeaveRequested;
            view.StartRequested += OnStartRequested;
            OnStateChanged(manager.State);
        }

        private void OnStateChanged(SessionData state)
        {
            if (destroyed)
                return;
            if (view != null)
                view.Show(state);
            if (preview != null)
                preview.Show(state);
        }

        private void OnHostRequested() => RunRequest(() => manager.HostAsync());
        private void OnJoinRequested(string code) => RunRequest(() => manager.JoinAsync(code.Trim()));
        private void OnLeaveRequested() => RunRequest(() => manager.LeaveAsync());

        private void OnStartRequested()
        {
            if (destroyed || manager == null)
                return;
            try
            {
                manager.StartMatch();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private async void RunRequest(Func<Task> request)
        {
            if (destroyed || manager == null)
                return;
            try
            {
                await request();
            }
            catch (Exception exception)
            {
                if (!destroyed)
                    Debug.LogException(exception);
            }
        }

        private void OnDestroy()
        {
            destroyed = true;
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (manager != null)
                manager.StateChanged -= OnStateChanged;
            if (view != null)
            {
                view.HostRequested -= OnHostRequested;
                view.JoinRequested -= OnJoinRequested;
                view.LeaveRequested -= OnLeaveRequested;
                view.StartRequested -= OnStartRequested;
            }
            manager = null;
            view = null;
            preview = null;
        }
    }
}
