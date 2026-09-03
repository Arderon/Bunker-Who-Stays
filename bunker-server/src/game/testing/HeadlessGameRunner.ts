import { PlayerData } from "../PlayerData";
import { GameSession } from "../GameSession";
import { GameSessionConfig } from "../GameSessionConfig";
import { CharacterCardGenerator } from "../content/CharacterCardGenerator";
import { TraitPool } from "../content/TraitPool";
import { SpecialCardPool } from "../content/SpecialCardPool";
import { GamePhase } from "../GamePhase";
import { GameOverResult } from "../GameOverResult";
import { SeededRandom } from "../content/SeededRandom";
import { SpecialCardEffectType } from "../SpecialCard";

// Direct port of the C# HeadlessGameRunner (section 1.8). Drives a
// GameSession through a full game using randomized but valid decisions,
// without any real time or network - used both as a manual debugging tool
// and as the basis for automated tests.
export class HeadlessGameRunner {
  private readonly decisionRandom: SeededRandom;
  public readonly log: string[] = [];

  constructor(decisionSeed?: number) {
    this.decisionRandom = new SeededRandom(decisionSeed);
  }

  runFullSimulation(
    fakePlayerCount: number,
    traitPools: TraitPool[],
    specialCardPool: SpecialCardPool,
    survivorsTarget: number,
    dealSeed?: number
  ): GameOverResult | null {
    const players = this.createFakePlayers(fakePlayerCount);
    const config: GameSessionConfig = { survivorsTarget, randomSeed: dealSeed, allowVoteTies: false };
    const generator = new CharacterCardGenerator(traitPools, specialCardPool, dealSeed);
    const session = new GameSession(players, config, generator);

    let finalResult: GameOverResult | null = null;

    session.on("phaseChanged", (phase) => this.logLine(`Phase -> ${GamePhase[phase]}`));
    session.on("roundStarted", (round) => this.logLine(`--- Round ${round} started ---`));
    session.on("traitRevealed", (player, trait) =>
      this.logLine(`${player.displayName} revealed ${trait.category}: ${trait.id}`)
    );
    session.on("specialCardUsed", (player, card) =>
      this.logLine(`${player.displayName} used special card: ${card.id} (${card.effectType})`)
    );
    session.on("votingResolved", (result) => {
      const counts = [...result.voteCounts.entries()].map(([id, c]) => `${id}=${c}`).join(", ");
      this.logLine(`Vote resolved: ${result.resultType} (${counts})`);
    });
    session.on("playerEliminated", (player) => this.logLine(`ELIMINATED: ${player.displayName}`));
    session.on("gameOverResolved", (result) => {
      finalResult = result;
      const names = result.survivors.map((p) => p.displayName).join(", ");
      this.logLine(`GAME OVER: ${result.endReason}, survivors: ${names}`);
    });

    const validation = session.validateCanStart();
    if (!validation.canStart) {
      this.logLine(`START FAILED: ${validation.failReason}`);
      return null;
    }

    session.startGame();
    this.driveGameToCompletion(session);

    return finalResult;
  }

  // --- Player simulation ---------------------------------------------

  private createFakePlayers(count: number): PlayerData[] {
    return Array.from({ length: count }, (_, i) => new PlayerData(`fake_player_${i}`, `Player ${i}`));
  }

  // Drives the session through Reveal -> Discussion -> Voting (-> Tiebreaker)
  // -> next round, repeatedly, until GameOver. Safety-capped to avoid an
  // infinite loop if a rules bug causes the game to never end.
  private driveGameToCompletion(session: GameSession): void {
    const maxIterations = 1000;
    let iterations = 0;

    while (session.phase !== GamePhase.GameOver && iterations < maxIterations) {
      iterations++;

      switch (session.phase) {
        case GamePhase.Reveal:
          this.simulateRevealPhase(session);
          break;

        case GamePhase.Discussion:
          // No real timer - immediately proceed to voting for headless runs.
          session.startVotingPhase();
          break;

        case GamePhase.Voting:
        case GamePhase.VotingTiebreaker:
          this.simulateVotingPhase(session);
          break;

        case GamePhase.RoundResult:
          // GameSession auto-advances from RoundResult internally
          // (finishRound -> startNextRound or endGame). Observing it here
          // directly means something didn't advance - treat as a bug signal.
          this.logLine("WARNING: observed RoundResult phase directly, possible logic gap.");
          iterations = maxIterations;
          break;

        default:
          this.logLine(`WARNING: unexpected phase in drive loop: ${GamePhase[session.phase]}`);
          iterations = maxIterations;
          break;
      }
    }

    if (iterations >= maxIterations) {
      this.logLine("ABORTED: exceeded max iterations, possible infinite loop in game rules.");
    }
  }

  private simulateRevealPhase(session: GameSession): void {
    let passCompleted = false;
    const handler = () => {
      passCompleted = true;
    };
    session.on("revealPassCompleted", handler);

    let safety = 0;
    while (session.phase === GamePhase.Reveal && safety < 500) {
      safety++;
      const playerId = session.currentTurnPlayerId;
      if (!playerId) break;

      const player = session.getPlayer(playerId)!;

      // Small chance to use the special card before revealing, if available.
      if (!player.hasUsedSpecialCard && player.special && this.rollChance(0.15)) {
        this.trySimulateSpecialCardUse(session, player);
      }

      const hiddenCategory = player.getTraits().find((t) => !player.isCategoryRevealed(t.category))?.category;
      if (hiddenCategory !== undefined) {
        session.revealNextTrait(playerId, hiddenCategory);
      }

      if (passCompleted) {
        session.off("revealPassCompleted", handler);
        session.startDiscussionPhase(60);
        return;
      }
    }

    session.off("revealPassCompleted", handler);
  }

  private trySimulateSpecialCardUse(session: GameSession, caster: PlayerData): void {
    const possibleTargets = session.activePlayers().filter((p) => p.playerId !== caster.playerId);
    if (possibleTargets.length === 0) return;

    const target = possibleTargets[this.decisionRandom.nextInt(possibleTargets.length)];

    if (caster.special!.effectType === SpecialCardEffectType.SwapTrait) {
      const swappableCategory = caster
        .getTraits()
        .find((t) => !caster.isCategoryRevealed(t.category) && !target.isCategoryRevealed(t.category))?.category;

      if (swappableCategory !== undefined) {
        session.useSwapTraitSpecialCard(caster.playerId, target.playerId, swappableCategory);
      }
    } else {
      session.useSpecialCard(caster.playerId, target.playerId);
    }
  }

  private simulateVotingPhase(session: GameSession): void {
    for (const voter of session.activePlayers()) {
      const possibleTargets = session
        .activePlayers()
        .filter((p) => p.playerId !== voter.playerId && !p.hasVoteImmunityThisRound);

      if (possibleTargets.length === 0) continue;

      const target = possibleTargets[this.decisionRandom.nextInt(possibleTargets.length)];
      session.castVote(voter.playerId, target.playerId);
    }

    session.resolveVotes();
  }

  private rollChance(probability: number): boolean {
    return this.decisionRandom.nextFloat() < probability;
  }

  private logLine(line: string): void {
    this.log.push(line);
  }
}
