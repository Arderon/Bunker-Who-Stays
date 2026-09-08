using System;
using System.Collections.Generic;
using System.Linq;
using Colyseus;
using Unity.Services.Authentication;
using UnityEngine;
using Bunker.Core;
using Bunker.Networking;
using Bunker.Networking.Generated;

namespace Bunker.UI
{
    // Colyseus-backed ILobbyService. Unity Authentication is still used,
    // but only to obtain a stable PlayerId (per the stage-0 decision),
    // passed to Colyseus as a plain connection option rather than a
    // verified token.
    public class ColyseusLobbyService : ILobbyService
    {
        public event Action<List<LobbyPlayerInfo>> OnPlayerListChanged;
        public event Action<GameStartValidationResult> OnStartValidationChanged;
        public event Action OnGameStarted;
        public event Action<string> OnJoinFailed;

        public string LobbyCode { get; private set; }
        public bool IsLocalPlayerHost => room != null && room.State.hostPlayerId == LocalPlayerId;
        public int SurvivorsTarget { get; set; } = 2;
        public IGameSessionView CurrentSession { get; private set; }

        private readonly string serverUrl;
        private ColyseusClient client;
        private ColyseusRoom<GameStateSchema> room;
        private string LocalPlayerId => AuthenticationService.Instance.PlayerId;

        public ColyseusLobbyService(string serverUrl)
        {
            this.serverUrl = serverUrl;
        }

        // --- Create / Join ---------------------------------------------------

        public async void CreateLobby(string hostDisplayName)
        {
            try
            {
                client ??= new ColyseusClient(serverUrl);

                var code = GenerateLobbyCode();
                var options = new Dictionary<string, object>
                {
                    { "code", code },
                    { "playerId", LocalPlayerId },
                    { "displayName", hostDisplayName },
                };

                // Explicitly creates a new room rather than joining an
                // existing one — CreateLobby should never accidentally
                // attach to someone else's room, even on a code collision.
                room = await client.Create<GameStateSchema>("bunker_room", options);
                LobbyCode = code;

                WireRoomCallbacks();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ColyseusLobbyService] CreateLobby failed: {ex}");
                OnJoinFailed?.Invoke("ui_common_error_generic");
            }
        }

        public async void JoinLobby(string code, string displayName)
        {
            try
            {
                client ??= new ColyseusClient(serverUrl);

                var options = new Dictionary<string, object>
                {
                    { "code", code },
                    { "playerId", LocalPlayerId },
                    { "displayName", displayName },
                };

                // Join (not JoinOrCreate) — fails if no room with a matching
                // "code" exists yet, rather than silently creating a new
                // (empty, code-orphaned) one.
                room = await client.Join<GameStateSchema>("bunker_room", options);
                LobbyCode = code;

                WireRoomCallbacks();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ColyseusLobbyService] JoinLobby failed: {ex}");
                OnJoinFailed?.Invoke("ui_join_error_invalid_code");
            }
        }

        private string GenerateLobbyCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var rnd = new System.Random();
            return new string(Enumerable.Range(0, 6).Select(_ => chars[rnd.Next(chars.Length)]).ToArray());
        }

        // --- Room state -> lobby UI projection ---------------------------------

        private void WireRoomCallbacks()
        {
            room.State.players.OnAdd((_, __) => NotifyPlayersChanged());
            room.State.players.OnRemove((_, __) => NotifyPlayersChanged());
            room.State.OnChange(() => NotifyPlayersChanged());

            // NOTE: requires BunkerRoom.handleStartGame to broadcast
            // "gameStartedAck" once the game session is created server-side —
            // verify this call is present before relying on OnGameStarted here.
            room.OnMessage<object>("gameStartedAck", (_) =>
            {
                CurrentSession = new ColyseusGameSessionController(room, LocalPlayerId);
                OnGameStarted?.Invoke();
            });

            room.OnMessage<ActionRejectedMessage>("actionRejected", (msg) =>
                OnStartValidationChanged?.Invoke(GameStartValidationResult.Fail(msg.key)));

            NotifyPlayersChanged();
        }

        private void NotifyPlayersChanged()
        {
            var players = room.State.players.Values.Select(p => new LobbyPlayerInfo
            {
                PlayerId = p.playerId,
                DisplayName = p.displayName,
                IsHost = p.playerId == room.State.hostPlayerId,
                IsReady = p.isReady
            }).ToList();

            OnPlayerListChanged?.Invoke(players);

            // Cheap local pre-check for instant UI feedback (e.g. "need
            // more players") without a round-trip. The server has the
            // final say via actionRejected — it also validates trait
            // content availability, which the client cannot check itself.
            bool canStart = players.Count > SurvivorsTarget;
            OnStartValidationChanged?.Invoke(
                canStart
                    ? GameStartValidationResult.Ok()
                    : GameStartValidationResult.Fail(
                        $"Need more than {SurvivorsTarget} players to start (currently {players.Count})."));
        }

        // --- Player state ---------------------------------------------------

        public void SetLocalPlayerReady(bool ready) => room?.Send("setReady", new { ready });

        // --- Leave -------------------------------------------------------------

        public async void LeaveLobby()
        {
            if (room != null)
            {
                await room.Leave();
                room = null;
            }
            LobbyCode = null;
            CurrentSession = null;
        }

        // --- Start game ----------------------------------------------------

        public void StartGame()
        {
            if (!IsLocalPlayerHost)
            {
                Debug.LogWarning("[ColyseusLobbyService] Only the host can start the game.");
                return;
            }

            room?.Send("startGame", new { survivorsTarget = SurvivorsTarget });
        }
    }
}
