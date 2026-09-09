using System;
using System.Collections.Generic;
using System.Linq;
using Bunker.Content;
using Bunker.Core;
using UnityEngine;

namespace Bunker.UI
{
    // Single-device stand-in for ILobbyService, useful for testing the UI
    // and hot-seat play before UGS Lobby/Relay (stage 4/5) is wired up.
    // Simulates a lobby with the local player plus optional fake players.
    public class LocalLobbyService : ILobbyService
    {
        public event Action<List<LobbyPlayerInfo>> OnPlayerListChanged;
        public event Action<GameStartValidationResult> OnStartValidationChanged;
        public event Action OnGameStarted;
        public event Action<string> OnJoinFailed;

        public string LobbyCode { get; private set; }
        public bool IsLocalPlayerHost { get; private set; }
        public int SurvivorsTarget { get; set; } = 2;

        // ILobbyService types this as the interface, not the concrete class —
        // _session keeps the GameSession-only surface (ValidateCanStart,
        // StartGame) reachable internally.
        public IGameSessionView CurrentSession => _session;

        private GameSession _session;
        private readonly List<LobbyPlayerInfo> _players = new();
        private readonly List<TraitPoolSO> _traitPools;
        private readonly SpecialCardPoolSO _specialCardPool;

        public LocalLobbyService(List<TraitPoolSO> traitPools, SpecialCardPoolSO specialCardPool)
        {
            _traitPools = traitPools;
            _specialCardPool = specialCardPool;
        }

        public void CreateLobby(string hostDisplayName)
        {
            LobbyCode = GenerateFakeCode();
            IsLocalPlayerHost = true;
            _players.Clear();
            _players.Add(new LobbyPlayerInfo
            {
                PlayerId = "local_host",
                DisplayName = hostDisplayName,
                IsHost = true,
                IsReady = true
            });

            NotifyPlayersChanged();
        }

        public void JoinLobby(string code, string displayName)
        {
            // No real network here — always "succeeds" for local testing.
            LobbyCode = code;
            IsLocalPlayerHost = false;

            // The joining player still has to appear in the list: without this
            // the lobby stayed empty and StartGame built a session with no
            // players at all.
            _players.Clear();
            _players.Add(new LobbyPlayerInfo
            {
                PlayerId = "local_player",
                DisplayName = displayName,
                IsHost = false,
                IsReady = true
            });

            NotifyPlayersChanged();
        }

        // Debug helper for testing the UI with multiple players without
        // real devices. Not part of the interface — cast to LocalLobbyService to use.
        public void AddFakePlayer(string displayName)
        {
            _players.Add(new LobbyPlayerInfo
            {
                PlayerId = $"fake_{_players.Count}",
                DisplayName = displayName,
                IsHost = false,
                IsReady = true
            });
            NotifyPlayersChanged();
        }

        public void SetLocalPlayerReady(bool ready)
        {
            // Local single-device host is always "ready" for MVP testing purposes.
            NotifyPlayersChanged();
        }

        public void LeaveLobby()
        {
            _players.Clear();
            LobbyCode = null;
            IsLocalPlayerHost = false;
            _session = null; // otherwise CurrentSession kept serving the finished game
            OnPlayerListChanged?.Invoke(new List<LobbyPlayerInfo>());
        }

        public void StartGame()
        {
            if (_players.Count == 0)
            {
                // GameSession's constructor throws on an empty player list, so
                // this cannot be left to ValidateCanStart.
                Debug.LogWarning("[LocalLobbyService] Cannot start: no players in the lobby.");
                OnStartValidationChanged?.Invoke(
                    GameStartValidationResult.Fail("No players in the lobby."));
                return;
            }

            var playerDatas = _players.Select(p => new PlayerData(p.PlayerId, p.DisplayName)).ToList();
            var config = new GameSessionConfig(SurvivorsTarget);
            var generator = new CharacterCardGenerator(_traitPools, _specialCardPool);

            var candidate = new GameSession(playerDatas, config, generator);
            var validation = candidate.ValidateCanStart();

            if (!validation.CanStart)
            {
                // Published only after validation passes — a rejected start
                // used to leave CurrentSession pointing at a never-started
                // session, which the UI would then bind to.
                Debug.LogWarning($"[LocalLobbyService] Cannot start: {validation.FailReason}");
                OnStartValidationChanged?.Invoke(validation);
                return;
            }

            _session = candidate;
            _session.StartGame();
            OnGameStarted?.Invoke();
        }

        private void NotifyPlayersChanged()
        {
            OnPlayerListChanged?.Invoke(new List<LobbyPlayerInfo>(_players));

            var playerDatas = _players.Select(p => new PlayerData(p.PlayerId, p.DisplayName)).ToList();
            var dummySession = new GameSession(
                playerDatas.Count > 0 ? playerDatas : new List<PlayerData> { new("placeholder", "placeholder") },
                new GameSessionConfig(SurvivorsTarget),
                new CharacterCardGenerator(_traitPools, _specialCardPool));

            OnStartValidationChanged?.Invoke(dummySession.ValidateCanStart());
        }

        private string GenerateFakeCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var rnd = new System.Random();
            return new string(Enumerable.Range(0, 6).Select(_ => chars[rnd.Next(chars.Length)]).ToArray());
        }
    }
}