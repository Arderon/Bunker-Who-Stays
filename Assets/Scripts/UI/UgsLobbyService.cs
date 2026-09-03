using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Bunker.Content;
using Bunker.Core;
using Bunker.Networking;

namespace Bunker.UI
{
    // Real UGS-backed implementation of ILobbyService, replacing LocalLobbyService.
    // Implements the exact same interface, so no UI script needs to change —
    // only LobbyServiceLocator.Current is reassigned at bootstrap.
    //
    // Scope of this stage: only the player list and lobby metadata (survivors
    // target, ready state, game-started flag) are synchronized through UGS
    // Lobby. Actual gameplay state sync (reveals, votes) requires Relay +
    // Netcode for GameObjects, wired up in the next stage.
    public class UgsLobbyService : ILobbyService
    {
        public event Action<List<LobbyPlayerInfo>> OnPlayerListChanged;
        public event Action<GameStartValidationResult> OnStartValidationChanged;
        public event Action OnGameStarted;
        public event Action<string> OnJoinFailed;

        public string LobbyCode { get; private set; }
        public bool IsLocalPlayerHost => _lobby != null && _lobby.HostId == AuthenticationService.Instance.PlayerId;
        public int SurvivorsTarget { get; set; } = 2;
        public GameSession CurrentSession { get; private set; }

        private const int MaxPlayers = 12;
        private const float HeartbeatIntervalSeconds = 15f;

        private Lobby _lobby;
        private ILobbyEvents _lobbyEvents;
        private CancellationTokenSource _heartbeatCts;

        private readonly List<TraitPoolSO> _traitPools;
        private readonly SpecialCardPoolSO _specialCardPool;

        public UgsLobbyService(List<TraitPoolSO> traitPools, SpecialCardPoolSO specialCardPool)
        {
            _traitPools = traitPools;
            _specialCardPool = specialCardPool;
        }

        // --- Create / Join ---------------------------------------------------

        public async void CreateLobby(string hostDisplayName)
        {
            try
            {
                var options = new CreateLobbyOptions
                {
                    IsPrivate = false, // set true if you don't want lobbies discoverable by listing
                    Player = BuildLocalPlayer(hostDisplayName),
                    Data = new Dictionary<string, DataObject>
                    {
                        { LobbyDataKeys.SurvivorsTarget, new DataObject(DataObject.VisibilityOptions.Member, SurvivorsTarget.ToString()) },
                        { LobbyDataKeys.GameStarted, new DataObject(DataObject.VisibilityOptions.Member, "false") },
                    }
                };

                _lobby = await LobbyService.Instance.CreateLobbyAsync($"Bunker_{hostDisplayName}", MaxPlayers, options);
                LobbyCode = _lobby.LobbyCode;

                await SubscribeToLobbyEvents();
                StartHeartbeat();
                NotifyPlayersChanged();
            }
            catch (LobbyServiceException ex)
            {
                Debug.LogError($"[UgsLobbyService] CreateLobby failed: {ex}");
                OnJoinFailed?.Invoke("ui_common_error_generic");
            }
        }

        public async void JoinLobby(string code, string displayName)
        {
            try
            {
                var options = new JoinLobbyByCodeOptions
                {
                    Player = BuildLocalPlayer(displayName)
                };

                _lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(code, options);
                LobbyCode = _lobby.LobbyCode;

                await SubscribeToLobbyEvents();
                NotifyPlayersChanged();
            }
            catch (LobbyServiceException ex)
            {
                Debug.LogWarning($"[UgsLobbyService] JoinLobby failed: {ex.Reason}");
                OnJoinFailed?.Invoke(MapJoinFailureToLocalizationKey(ex));
            }
        }

        private Player BuildLocalPlayer(string displayName)
        {
            return new Player(
                id: AuthenticationService.Instance.PlayerId,
                data: new Dictionary<string, PlayerDataObject>
                {
                    { PlayerDataKeys.DisplayName, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, displayName) },
                    { PlayerDataKeys.IsReady, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "true") },
                });
        }

        private string MapJoinFailureToLocalizationKey(LobbyServiceException ex)
        {
            return ex.Reason switch
            {
                LobbyExceptionReason.LobbyNotFound => "ui_join_error_invalid_code",
                LobbyExceptionReason.LobbyFull => "ui_join_error_lobby_full",
                _ => "ui_common_error_generic"
            };
        }

        // --- Realtime updates --------------------------------------------------

        private async Task SubscribeToLobbyEvents()
        {
            var callbacks = new LobbyEventCallbacks();
            callbacks.LobbyChanged += OnLobbyChanged;
            callbacks.KickedFromLobby += OnKickedFromLobby;
            callbacks.LobbyEventConnectionStateChanged += OnConnectionStateChanged;

            _lobbyEvents = await LobbyService.Instance.SubscribeToLobbyEventsAsync(_lobby.Id, callbacks);
        }

        private void OnLobbyChanged(ILobbyChanges changes)
        {
            if (changes.LobbyDeleted)
            {
                LeaveLobby();
                return;
            }

            changes.ApplyToLobby(_lobby);
            NotifyPlayersChanged();

            bool gameStarted = _lobby.Data != null
                && _lobby.Data.TryGetValue(LobbyDataKeys.GameStarted, out var flag)
                && flag.Value == "true";

            if (gameStarted && CurrentSession == null && !IsLocalPlayerHost)
            {
                // Non-host clients currently have no synchronized GameSession yet
                // (that arrives with Relay/Netcode in the next stage). For now,
                // simply notify the UI so it can react (e.g. show a "starting..."
                // state) without crashing on a null CurrentSession.
                Debug.LogWarning("[UgsLobbyService] Host started the game; gameplay sync not yet implemented for non-host clients.");
                OnGameStarted?.Invoke();
            }
        }

        private void OnKickedFromLobby()
        {
            Debug.LogWarning("[UgsLobbyService] Local player was removed from the lobby.");
            CleanupLocalState();
        }

        private void OnConnectionStateChanged(LobbyEventConnectionState state)
        {
            Debug.Log($"[UgsLobbyService] Lobby event connection state: {state}");
        }

        // --- Player state ---------------------------------------------------

        public async void SetLocalPlayerReady(bool ready)
        {
            if (_lobby == null) return;

            try
            {
                await LobbyService.Instance.UpdatePlayerAsync(_lobby.Id, AuthenticationService.Instance.PlayerId,
                    new UpdatePlayerOptions
                    {
                        Data = new Dictionary<string, PlayerDataObject>
                        {
                            { PlayerDataKeys.IsReady, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, ready ? "true" : "false") }
                        }
                    });
            }
            catch (LobbyServiceException ex)
            {
                Debug.LogError($"[UgsLobbyService] SetLocalPlayerReady failed: {ex}");
            }
        }

        // --- Leave -------------------------------------------------------------

        public async void LeaveLobby()
        {
            StopHeartbeat();

            if (_lobbyEvents != null)
            {
                await _lobbyEvents.UnsubscribeAsync();
                _lobbyEvents = null;
            }

            if (_lobby != null)
            {
                try
                {
                    await LobbyService.Instance.RemovePlayerAsync(_lobby.Id, AuthenticationService.Instance.PlayerId);
                }
                catch (LobbyServiceException ex)
                {
                    // Non-fatal: the lobby may already be gone (e.g. host left first).
                    Debug.LogWarning($"[UgsLobbyService] RemovePlayerAsync failed: {ex}");
                }
            }

            CleanupLocalState();
        }

        private void CleanupLocalState()
        {
            _lobby = null;
            LobbyCode = null;
            CurrentSession = null;
        }

        // --- Heartbeat (host only, keeps the lobby alive in UGS) --------------

        private void StartHeartbeat()
        {
            _heartbeatCts = new CancellationTokenSource();
            HeartbeatLoop(_heartbeatCts.Token);
        }

        private void StopHeartbeat()
        {
            _heartbeatCts?.Cancel();
            _heartbeatCts = null;
        }

        private async void HeartbeatLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _lobby != null)
            {
                try
                {
                    await LobbyService.Instance.SendHeartbeatPingAsync(_lobby.Id);
                }
                catch (LobbyServiceException ex)
                {
                    Debug.LogWarning($"[UgsLobbyService] Heartbeat failed: {ex}");
                }

                await Task.Delay(TimeSpan.FromSeconds(HeartbeatIntervalSeconds), token)
                    .ContinueWith(_ => { }); // swallow TaskCanceledException on cancellation
            }
        }

        // --- Start game ----------------------------------------------------

        public async void StartGame()
        {
            if (!IsLocalPlayerHost)
            {
                Debug.LogWarning("[UgsLobbyService] Only the host can start the game.");
                return;
            }

            var playerDatas = BuildPlayerDatas();
            var config = new GameSessionConfig(SurvivorsTarget);
            var generator = new CharacterCardGenerator(_traitPools, _specialCardPool);

            var session = new GameSession(playerDatas, config, generator);
            var validation = session.ValidateCanStart();

            if (!validation.CanStart)
            {
                Debug.LogWarning($"[UgsLobbyService] Cannot start: {validation.FailReason}");
                OnStartValidationChanged?.Invoke(validation);
                return;
            }

            try
            {
                await LobbyService.Instance.UpdateLobbyAsync(_lobby.Id, new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        { LobbyDataKeys.GameStarted, new DataObject(DataObject.VisibilityOptions.Member, "true") }
                    }
                });
            }
            catch (LobbyServiceException ex)
            {
                Debug.LogError($"[UgsLobbyService] Failed to flag lobby as started: {ex}");
            }

            // Host builds and owns the authoritative session locally for now.
            // Non-host clients receive the GameStarted flag via OnLobbyChanged
            // but do not yet get this same GameSession instance — that
            // requires state sync over Netcode (next stage).
            CurrentSession = session;
            session.StartGame();
            OnGameStarted?.Invoke();
        }

        private List<PlayerData> BuildPlayerDatas()
        {
            return _lobby.Players
                .Select(p => new PlayerData(p.Id, GetPlayerDisplayName(p)))
                .ToList();
        }

        private string GetPlayerDisplayName(Player player)
        {
            return player.Data != null && player.Data.TryGetValue(PlayerDataKeys.DisplayName, out var data)
                ? data.Value
                : "Player";
        }

        // --- Player list projection -------------------------------------------

        private void NotifyPlayersChanged()
        {
            if (_lobby == null) return;

            var players = _lobby.Players.Select(p => new LobbyPlayerInfo
            {
                PlayerId = p.Id,
                DisplayName = GetPlayerDisplayName(p),
                IsHost = p.Id == _lobby.HostId,
                IsReady = p.Data != null
                          && p.Data.TryGetValue(PlayerDataKeys.IsReady, out var ready)
                          && ready.Value == "true"
            }).ToList();

            OnPlayerListChanged?.Invoke(players);

            // Same validation-preview pattern as LocalLobbyService: build a
            // throwaway session purely to run ValidateCanStart() and report
            // the result to the UI (e.g. disable Start button with a reason),
            // without actually starting anything.
            var previewPlayers = players.Select(p => new PlayerData(p.PlayerId, p.DisplayName)).ToList();
            if (previewPlayers.Count == 0) previewPlayers.Add(new PlayerData("placeholder", "placeholder"));

            var previewSession = new GameSession(
                previewPlayers,
                new GameSessionConfig(SurvivorsTarget),
                new CharacterCardGenerator(_traitPools, _specialCardPool));

            OnStartValidationChanged?.Invoke(previewSession.ValidateCanStart());
        }
    }
}