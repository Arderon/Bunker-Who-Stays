using System;
using System.Collections.Generic;
using System.Linq;
using Bunker.Content;
using UnityEngine;

namespace Bunker.Core.Testing
{
    // Runs a full game session from start to finish without any UI or real time,
    // driving GameSession purely through its public API by simulating random
    // (but valid) player decisions. Used both as a manual debugging tool and
    // as the foundation for automated unit tests (Unity Test Framework).
    public class HeadlessGameRunner
    {
        private readonly System.Random _decisionRandom;
        public List<string> Log { get; } = new();

        public HeadlessGameRunner(int? decisionSeed = null)
        {
            _decisionRandom = decisionSeed.HasValue
                ? new System.Random(decisionSeed.Value)
                : new System.Random();
        }

        // Builds a fresh session with fakePlayerCount players and runs it to completion.
        // Returns the final GameOverResult for assertions in tests.
        public GameOverResult RunFullSimulation(
            int fakePlayerCount,
            List<TraitPoolSO> traitPools,
            SpecialCardPoolSO specialCardPool,
            int survivorsTarget,
            int? dealSeed = null)
        {
            var players = CreateFakePlayers(fakePlayerCount);
            var config = new GameSessionConfig(survivorsTarget, dealSeed);
            var generator = new CharacterCardGenerator(traitPools, specialCardPool, dealSeed);
            var session = new GameSession(players, config, generator);

            GameOverResult finalResult = null;

            // Wire up logging + capture the terminal result via events,
            // mirroring how a real UI/network layer would react to GameSession.
            session.OnPhaseChanged += phase => LogLine($"Phase -> {phase}");
            session.OnRoundStarted += round => LogLine($"--- Round {round} started ---");
            session.OnTraitRevealed += (player, trait) =>
                LogLine($"{player.DisplayName} revealed {trait.Category}: {trait.Id}");
            session.OnSpecialCardUsed += (player, card) =>
                LogLine($"{player.DisplayName} used special card: {card.Id} ({card.EffectType})");
            session.OnVotingResolved += result =>
                LogLine($"Vote resolved: {result.ResultType} " +
                         $"({string.Join(", ", result.VoteCounts.Select(kv => $"{kv.Key}={kv.Value}"))})");
            session.OnPlayerEliminated += player =>
                LogLine($"ELIMINATED: {player.DisplayName}");
            session.OnGameOverResolved += result =>
            {
                finalResult = result;
                LogLine($"GAME OVER: {result.EndReason}, survivors: " +
                         string.Join(", ", result.Survivors.Select(p => p.DisplayName)));
            };

            var validation = session.ValidateCanStart();
            if (!validation.CanStart)
            {
                LogLine($"START FAILED: {validation.FailReason}");
                return null;
            }

            session.StartGame();
            DriveGameToCompletion(session);

            return finalResult;
        }

        // --- Player simulation ---------------------------------------------

        private List<PlayerData> CreateFakePlayers(int count)
        {
            var players = new List<PlayerData>();
            for (int i = 0; i < count; i++)
            {
                players.Add(new PlayerData($"fake_player_{i}", $"Player {i}"));
            }
            return players;
        }

        // Drives the session through Reveal -> Discussion -> Voting (-> Tiebreaker)
        // -> next round, repeatedly, until GameOver. Safety-capped to avoid an
        // infinite loop if a rules bug causes the game to never end.
        private void DriveGameToCompletion(GameSession session)
        {
            const int maxIterations = 1000;
            int iterations = 0;

            while (session.Phase != GamePhase.GameOver && iterations < maxIterations)
            {
                iterations++;

                switch (session.Phase)
                {
                    case GamePhase.Reveal:
                        SimulateRevealPhase(session);
                        break;

                    case GamePhase.Discussion:
                        // No real timer here — immediately proceed to voting,
                        // since headless runs don't need to wait real seconds.
                        session.StartVotingPhase();
                        break;

                    case GamePhase.Voting:
                    case GamePhase.VotingTiebreaker:
                        SimulateVotingPhase(session);
                        break;

                    case GamePhase.RoundResult:
                        // GameSession auto-advances from RoundResult internally
                        // (FinishRound -> StartNextRound or EndGame), so this
                        // phase should be transient. If we ever observe it here,
                        // something didn't advance — treat as a bug signal.
                        LogLine("WARNING: observed RoundResult phase directly, possible logic gap.");
                        iterations = maxIterations; // bail out
                        break;

                    default:
                        // Lobby / Dealing should never be seen mid-loop.
                        LogLine($"WARNING: unexpected phase in drive loop: {session.Phase}");
                        iterations = maxIterations;
                        break;
                }
            }

            if (iterations >= maxIterations)
            {
                LogLine("ABORTED: exceeded max iterations, possible infinite loop in game rules.");
            }
        }

        private void SimulateRevealPhase(GameSession session)
        {
            // Keep asking the current turn player to reveal traits/use their
            // special card until a full pass completes and phase changes away
            // from Reveal (StartDiscussionPhase is called by the caller,
            // simulated here right after the pass completes).
            bool passCompleted = false;
            session.OnRevealPassCompleted += LocalHandler;
            void LocalHandler() => passCompleted = true;

            int safety = 0;
            while (session.Phase == GamePhase.Reveal && safety < 500)
            {
                safety++;
                var playerId = session.CurrentTurnPlayerId;
                if (playerId == null) break;

                var player = session.GetPlayer(playerId);

                // Small chance to use the special card before revealing, if available.
                if (!player.HasUsedSpecialCard && player.Special != null && RollChance(0.15))
                {
                    TrySimulateSpecialCardUse(session, player);
                }

                var hiddenCategory = player.Traits
                    .Select(t => t.Category)
                    .FirstOrDefault(c => !player.IsCategoryRevealed(c));

                session.RevealNextTrait(playerId, hiddenCategory);

                if (passCompleted)
                {
                    session.OnRevealPassCompleted -= LocalHandler;
                    session.StartDiscussionPhase(durationSeconds: 60);
                    break;
                }
            }

            session.OnRevealPassCompleted -= LocalHandler;
        }

        private void TrySimulateSpecialCardUse(GameSession session, PlayerData caster)
        {
            var possibleTargets = session.ActivePlayers()
                .Where(p => p.PlayerId != caster.PlayerId)
                .ToList();

            if (possibleTargets.Count == 0) return;

            var target = possibleTargets[_decisionRandom.Next(possibleTargets.Count)];

            if (caster.Special.EffectType == SpecialCardEffectType.SwapTrait)
            {
                var swappableCategory = caster.Traits
                    .Select(t => t.Category)
                    .FirstOrDefault(c => !caster.IsCategoryRevealed(c) && !target.IsCategoryRevealed(c));

                if (swappableCategory != default)
                {
                    session.UseSwapTraitSpecialCard(caster.PlayerId, target.PlayerId, swappableCategory);
                }
            }
            else
            {
                session.UseSpecialCard(caster.PlayerId, target.PlayerId);
            }
        }

        private void SimulateVotingPhase(GameSession session)
        {
            foreach (var voter in session.ActivePlayers().ToList())
            {
                var possibleTargets = session.ActivePlayers()
                    .Where(p => p.PlayerId != voter.PlayerId && !p.HasVoteImmunityThisRound)
                    .ToList();

                if (possibleTargets.Count == 0) continue;

                var target = possibleTargets[_decisionRandom.Next(possibleTargets.Count)];
                session.CastVote(voter.PlayerId, target.PlayerId);
            }

            session.ResolveVotes();
        }

        private bool RollChance(double probability)
        {
            return _decisionRandom.NextDouble() < probability;
        }

        private void LogLine(string line)
        {
            Log.Add(line);
        }
    }
}