using System;
using System.Collections.Generic;
using System.Linq;
using Colyseus;
using Colyseus.Schema;
using Unity.Services.Authentication;
using UnityEngine;
using Bunker.Core;
using Bunker.Networking;

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
        public bool IsLocalPlayerHost => HostPlayerId == LocalPlayerId;
        public int SurvivorsTarget { get; set; } = 2;
        public IGameSessionView CurrentSession { get; private set; }

        private readonly string serverUrl;
        private Client client;
        private Room<GameStateSchema> room;
        private StateCallbackStrategy<GameStateSchema> callbacks;

        // Built as soon as the room state is available — NOT when the game
        // starts. The server sends "dealtHand" before it writes the new
        // phase into the state, so a controller created on the phase change
        // would be wired up too late and miss the local player's own hand.
        private ColyseusGameSessionController session;

        private string LocalPlayerId => AuthenticationService.Instance.PlayerId;

        // BunkerRoom tracks the host by sessionId (the first client to join)
        // and does not mirror it into the schema, so the client derives it
        // from player insertion order instead: MapSchema is backed by an
        // OrderedDictionary, and the server never removes players on leave,
        // so the first entry stays the host for the room's lifetime.
        private string HostPlayerId
        {
            get
            {
                if (room?.State?.players == null) return null;
                foreach (PlayerSchema p in room.State.players.Values) return p.playerId;
                return null;
            }
        }

        public ColyseusLobbyService(string serverUrl)
        {
            this.serverUrl = serverUrl;
        }

        // --- Create / Join ---------------------------------------------------

        public async void CreateLobby(string hostDisplayName)
        {
            try
            {
                client ??= new Client(serverUrl);

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

                await WireRoomCallbacks();
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
                client ??= new Client(serverUrl);

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

                await WireRoomCallbacks();
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

        private async System.Threading.Tasks.Task WireRoomCallbacks()
        {
            // A resolved join does not mean the state has decoded yet:
            // room.State.players is still null until the first full sync,
            // so mounting callbacks any earlier throws.
            await room.WaitForFirstState();

            // Schema 3.x: Schema/MapSchema instances no longer carry their own
            // OnAdd/OnRemove/OnChange methods — callbacks go through this
            // strategy object.
            callbacks = Callbacks.Get(room);

            callbacks.OnAdd(state => state.players, (string key, PlayerSchema added) => NotifyPlayersChanged());
            callbacks.OnRemove(state => state.players, (string key, PlayerSchema removed) => NotifyPlayersChanged());

            // Wired now, before the game starts, so its "dealtHand" handler is
            // already registered when the server deals (see the `session`
            // field note). It is only published as CurrentSession once the
            // phase actually leaves Lobby.
            session = new ColyseusGameSessionController(room, LocalPlayerId);

            callbacks.OnChange(room.State, () =>
            {
                NotifyPlayersChanged();

                // BunkerRoom has no "gameStartedAck" broadcast — the only
                // signal that the game began is the phase leaving Lobby,
                // which handleStartGame writes at the end of a successful
                // start.
                if (CurrentSession == null && (GamePhase)room.State.phase != GamePhase.Lobby)
                {
                    CurrentSession = session;
                    OnGameStarted?.Invoke();
                }
            });

            room.OnMessage<ActionRejectedMessage>("actionRejected", (msg) =>
            {
                // Once the game is running, actionRejected refers to in-game
                // actions and is handled by the session, not the lobby UI.
                if (CurrentSession != null) return;
                OnStartValidationChanged?.Invoke(GameStartValidationResult.Fail(msg?.key ?? "ui_common_error_generic"));
            });

            NotifyPlayersChanged();
        }

        private void NotifyPlayersChanged()
        {
            if (room?.State?.players == null) return;

            var hostPlayerId = HostPlayerId;

            // MapSchema.Values is a non-generic ICollection — cast before LINQ.
            var players = room.State.players.Values
                .Cast<PlayerSchema>()
                .Select(p => new LobbyPlayerInfo
                {
                    PlayerId = p.playerId,
                    DisplayName = p.displayName,
                    IsHost = p.playerId == hostPlayerId,

                    // BunkerRoom has no readiness concept at all (no schema
                    // field, no "setReady" handler): joining the room is the
                    // only commitment, and the host alone gates the start.
                    // Reported as ready for the same reason LocalLobbyService
                    // does, so the list does not show a permanent "not ready".
                    IsReady = true
                })
                .ToList();

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

        // Intentionally a no-op: BunkerRoom registers no "setReady" handler,
        // so sending one would only make the server log an unhandled-message
        // error. Kept to satisfy ILobbyService (LocalLobbyService/UgsLobbyService
        // do implement readiness).
        public void SetLocalPlayerReady(bool ready)
        {
        }

        // --- Leave -------------------------------------------------------------

        public async void LeaveLobby()
        {
            var leaving = room;
            room = null;
            callbacks = null;
            session = null;
            CurrentSession = null;
            LobbyCode = null;

            if (leaving != null)
            {
                try
                {
                    await leaving.Leave();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ColyseusLobbyService] LeaveLobby failed: {ex}");
                }
            }
        }

        // --- Start game ----------------------------------------------------

        public void StartGame()
        {
            if (!IsLocalPlayerHost)
            {
                Debug.LogWarning("[ColyseusLobbyService] Only the host can start the game.");
                return;
            }

            _ = room?.Send("startGame", new Dictionary<string, object> { { "survivorsTarget", SurvivorsTarget } });
        }
    }
}
