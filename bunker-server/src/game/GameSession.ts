import { PlayerData } from "./PlayerData";
import { CharacterTrait } from "./CharacterTrait";
import { CardCategory } from "./CardCategory";
import { SpecialCard, SpecialCardEffectType } from "./SpecialCard";
import { GamePhase } from "./GamePhase";
import { GameSessionConfig } from "./GameSessionConfig";
import { CharacterCardGenerator } from "./content/CharacterCardGenerator";
import { VotingResult, VotingOutcome } from "./VotingResult";
import { GameOverResult, GameOverReason } from "./GameOverResult";
import { GameStartValidationResult, validationOk, validationFail } from "./GameStartValidationResult";
import { TypedEventEmitter } from "./events/TypedEventEmitter";
import { TurnOrderService } from "./TurnOrderService";
import { SpecialCardEffectRegistry } from "./effects/SpecialCardEffectRegistry";
import { ISpecialCardEffect } from "./effects/ISpecialCardEffect";
import { SpecialCardEffectResult, ok, fail } from "./effects/SpecialCardEffectResult";

// Event map for GameSession's TypedEventEmitter. Each entry mirrors one of
// the C# `event Action<T>` fields accumulated across sections 1.3-1.7.
// Declared as a `type` rather than an `interface` — TS's generic constraint
// check (`Events extends Record<string, any[]>` in TypedEventEmitter) only
// accepts type aliases here; a named interface without an explicit index
// signature fails that check even though the shape is identical.
type GameSessionEvents = {
  phaseChanged: [GamePhase];
  roundStarted: [number];
  traitRevealed: [PlayerData, CharacterTrait];
  revealPassCompleted: [];
  specialCardUsed: [PlayerData, SpecialCard];
  discussionStarted: [number]; // duration in seconds
  votingResolved: [VotingResult];
  playerEliminated: [PlayerData];
  gameOverResolved: [GameOverResult];
};

// Direct, fully merged port of the C# GameSession (sections 1.3-1.7 combined
// into their final form). This is the TypeScript equivalent of the complete
// C# class after all incremental additions — turn order (1.4), special card
// effects (1.5), full voting/tiebreaker logic (1.6), and start validation (1.7).
export class GameSession extends TypedEventEmitter<GameSessionEvents> {
  public readonly players: PlayerData[];
  public currentRound = 0;
  public phase: GamePhase = GamePhase.Lobby;

  protected readonly config: GameSessionConfig;
  protected readonly cardGenerator: CharacterCardGenerator;

  private readonly turnOrder = new TurnOrderService();
  private readonly effectRegistry = new SpecialCardEffectRegistry();

  private currentVotes = new Map<string, string>(); // voterId -> targetId
  private tiebreakerCandidateIds: string[] | null = null;
  private tiebreakerAttempts = 0;
  private static readonly MAX_TIEBREAKER_ATTEMPTS = 1; // one re-vote, then give up

  constructor(players: PlayerData[], config: GameSessionConfig, cardGenerator: CharacterCardGenerator) {
    super();

    if (!players || players.length === 0) {
      throw new Error("GameSession requires at least one player.");
    }

    this.players = players;
    this.config = config;
    this.cardGenerator = cardGenerator;
  }

  get currentTurnPlayerId(): string | null {
    return this.turnOrder.currentPlayerId;
  }

  // --- Pre-start validation --------------------------------------------

  // Called by the room/lobby code before allowing "Start Game", and again
  // defensively inside startGame() itself.
  validateCanStart(): GameStartValidationResult {
    if (this.phase !== GamePhase.Lobby) {
      return validationFail("Game has already started.");
    }

    if (this.players.length === 0) {
      return validationFail("No players in the lobby.");
    }

    if (this.config.survivorsTarget <= 0) {
      return validationFail("Survivors target must be at least 1.");
    }

    // Need strictly more players than the target, otherwise there is
    // nothing to play - the game would already be "won" at round 0.
    if (this.players.length <= this.config.survivorsTarget) {
      return validationFail(
        `Need more than ${this.config.survivorsTarget} players to start (currently ${this.players.length}).`
      );
    }

    // Content sanity check: every player must have a full set of traits
    // ready to be dealt. Catches a misconfigured/empty trait pool early
    // instead of failing deep inside CharacterCardGenerator.
    if (!this.cardGenerator.hasEnoughContentFor(this.players.length)) {
      return validationFail("Not enough card content configured for this many players.");
    }

    return validationOk();
  }

  // --- Lifecycle -----------------------------------------------------

  startGame(): void {
    const validation = this.validateCanStart();
    if (!validation.canStart) {
      console.error(`[GameSession] Cannot start game: ${validation.failReason}`);
      return;
    }

    this.setPhase(GamePhase.Dealing);
    this.cardGenerator.dealToPlayers(this.players);
    this.turnOrder.initialize(this.players);

    this.currentRound = 0;
    this.startNextRound();
  }

  protected startNextRound(): void {
    this.currentRound++;

    for (const player of this.activePlayers()) {
      player.resetPerRoundFlags();
    }

    this.currentVotes.clear();
    this.turnOrder.rebuildForRound(this.currentRound, this.activePlayers());

    this.setPhase(GamePhase.Reveal);
    this.emit("roundStarted", this.currentRound);
  }

  // --- Phase transitions ---------------------------------------------

  protected setPhase(newPhase: GamePhase): void {
    this.phase = newPhase;
    this.emit("phaseChanged", newPhase);
  }

  // --- Reveal phase ----------------------------------------------------

  // Attempts to reveal one trait category for the player whose turn it
  // currently is. Returns false (does nothing further) on any invalid
  // request, mirroring the C# RevealNextTrait's bool-return contract.
  revealNextTrait(playerId: string, category: CardCategory): boolean {
    if (this.phase !== GamePhase.Reveal) {
      console.warn("[GameSession] revealNextTrait called outside of Reveal phase.");
      return false;
    }

    if (!this.turnOrder.isPlayersTurn(playerId)) {
      console.warn(`[GameSession] It is not ${playerId}'s turn.`);
      return false;
    }

    const player = this.getPlayer(playerId);
    if (!player || player.isEliminated) {
      console.warn(`[GameSession] Unknown or eliminated player: ${playerId}`);
      return false;
    }

    if (player.isCategoryRevealed(category)) {
      console.warn(`[GameSession] ${playerId} already revealed ${category}.`);
      return false;
    }

    const trait = player.getTrait(category);
    if (!trait) {
      console.warn(`[GameSession] ${playerId} has no trait for category ${category}.`);
      return false;
    }

    player.revealCategory(category);
    this.emit("traitRevealed", player, trait);

    this.advanceToNextTurn();
    return true;
  }

  private advanceToNextTurn(): void {
    const passContinues = this.turnOrder.advanceTurn();

    if (!passContinues) {
      this.emit("revealPassCompleted");
      // The caller (BunkerRoom) decides when to actually move to Discussion,
      // via startDiscussionPhase() below.
    }
  }

  // Simplified form (no double-negative), same correction already applied
  // during the C# version's own section 1.4.
  hasCompletedRevealPass(): boolean {
    return this.activePlayers().every((p) => p.getRevealedCategories().size > 0);
  }

  // --- Special cards -----------------------------------------------------

  // General-purpose entry point for effects that don't need extra parameters
  // (VoteImmunity, RevealHiddenTrait, ForceRevealAll).
  useSpecialCard(casterPlayerId: string, targetPlayerId: string | null): SpecialCardEffectResult {
    const caster = this.getPlayer(casterPlayerId);
    if (!caster || caster.isEliminated) return fail("Invalid caster.");
    if (caster.hasUsedSpecialCard) return fail("Special card already used.");
    if (!caster.special) return fail("Player has no special card.");

    const effect = this.effectRegistry.tryGet(caster.special.effectType);
    if (!effect) return fail(`Unknown effect type: ${caster.special.effectType}`);

    const target = targetPlayerId ? this.getPlayer(targetPlayerId) ?? null : null;

    if (!effect.canApply(this, caster, target)) return fail("Effect cannot be applied in this context.");

    let result: SpecialCardEffectResult;
    switch (caster.special.effectType) {
      case SpecialCardEffectType.RevealHiddenTrait:
        result = this.applyRevealHiddenTrait(caster, target!);
        break;
      case SpecialCardEffectType.VoteImmunity:
        result = this.applyVoteImmunity(effect, caster, target);
        break;
      case SpecialCardEffectType.ForceRevealAll:
        result = this.applyForceRevealAll(caster, target!);
        break;
      default:
        result = fail("Effect requires extra parameters, use the dedicated method.");
    }

    if (result.success) {
      caster.hasUsedSpecialCard = true;
      this.emit("specialCardUsed", caster, caster.special);
    }

    return result;
  }

  // Dedicated method for SwapTrait, which needs to know which category to swap.
  useSwapTraitSpecialCard(
    casterPlayerId: string,
    targetPlayerId: string,
    category: CardCategory
  ): SpecialCardEffectResult {
    const caster = this.getPlayer(casterPlayerId);
    const target = this.getPlayer(targetPlayerId);

    if (!caster || !target || caster.isEliminated || target.isEliminated) {
      return fail("Invalid caster or target.");
    }
    if (caster.hasUsedSpecialCard) return fail("Special card already used.");
    if (caster.special?.effectType !== SpecialCardEffectType.SwapTrait) {
      return fail("Player's special card is not a swap effect.");
    }
    if (caster.isCategoryRevealed(category) || target.isCategoryRevealed(category)) {
      return fail("Category must be hidden for both players.");
    }

    const casterTrait = caster.getTrait(category)!;
    const targetTrait = target.getTrait(category)!;

    caster.replaceTrait(category, targetTrait);
    target.replaceTrait(category, casterTrait);

    caster.hasUsedSpecialCard = true;
    this.emit("specialCardUsed", caster, caster.special);

    return ok();
  }

  private applyRevealHiddenTrait(caster: PlayerData, target: PlayerData): SpecialCardEffectResult {
    const hiddenTrait = target.getTraits().find((t) => !target.isCategoryRevealed(t.category));
    if (!hiddenTrait) return fail("Target has no hidden traits.");

    // Does NOT call target.revealCategory() - stays hidden from everyone
    // else. Only the caster receives this trait in the result, delivered
    // privately by BunkerRoom (not broadcast via traitRevealed).
    return ok(hiddenTrait);
  }

  private applyVoteImmunity(
    effect: ISpecialCardEffect,
    caster: PlayerData,
    target: PlayerData | null
  ): SpecialCardEffectResult {
    effect.apply(this, caster, target);
    return ok();
  }

  private applyForceRevealAll(caster: PlayerData, target: PlayerData): SpecialCardEffectResult {
    const hiddenTrait = target.getTraits().find((t) => !target.isCategoryRevealed(t.category));
    if (!hiddenTrait) return fail("Target has no hidden traits.");

    target.revealCategory(hiddenTrait.category);
    this.emit("traitRevealed", target, hiddenTrait); // public - everyone sees it

    return ok(hiddenTrait);
  }

  // --- Discussion phase -------------------------------------------------

  // Called once the reveal pass is done (see revealPassCompleted above).
  // durationSeconds is passed by the caller (BunkerRoom) since GameSession
  // doesn't own real time.
  startDiscussionPhase(durationSeconds: number): void {
    if (this.phase !== GamePhase.Reveal) {
      console.warn("[GameSession] startDiscussionPhase called outside of Reveal phase.");
      return;
    }

    this.setPhase(GamePhase.Discussion);
    this.emit("discussionStarted", durationSeconds);
  }

  // Called by the caller's timer once discussion time is up.
  startVotingPhase(): void {
    if (this.phase !== GamePhase.Discussion) {
      console.warn("[GameSession] startVotingPhase called outside of Discussion phase.");
      return;
    }

    this.tiebreakerCandidateIds = null;
    this.tiebreakerAttempts = 0;
    this.currentVotes.clear();
    this.setPhase(GamePhase.Voting);
  }

  // --- Voting ------------------------------------------------------------

  castVote(voterPlayerId: string, targetPlayerId: string): boolean {
    const inTiebreaker = this.phase === GamePhase.VotingTiebreaker;

    if (this.phase !== GamePhase.Voting && !inTiebreaker) {
      console.warn("[GameSession] Vote cast outside of Voting phase, ignored.");
      return false;
    }

    const voter = this.getPlayer(voterPlayerId);
    if (!voter || voter.isEliminated) {
      console.warn(`[GameSession] Invalid voter: ${voterPlayerId}`);
      return false;
    }

    if (voterPlayerId === targetPlayerId) {
      console.warn("[GameSession] Self-voting is not allowed.");
      return false;
    }

    const target = this.getPlayer(targetPlayerId);
    if (!target || target.isEliminated) {
      console.warn(`[GameSession] Invalid vote target: ${targetPlayerId}`);
      return false;
    }

    if (target.hasVoteImmunityThisRound) {
      console.warn(`[GameSession] ${targetPlayerId} is immune this round, vote rejected.`);
      return false;
    }

    // During a tiebreaker, votes may only go to the tied candidates from
    // the previous round of voting.
    if (inTiebreaker && !this.tiebreakerCandidateIds!.includes(targetPlayerId)) {
      console.warn("[GameSession] Vote target is not part of the tiebreaker candidates.");
      return false;
    }

    this.currentVotes.set(voterPlayerId, targetPlayerId);
    return true;
  }

  // Called by the caller once every active player has voted (or a voting
  // timer expired). Resolves the round and transitions phases accordingly.
  resolveVotes(): VotingResult {
    const result = this.tallyVotes();
    this.emit("votingResolved", result);

    switch (result.resultType) {
      case VotingOutcome.PlayerEliminated:
        result.eliminatedPlayer!.isEliminated = true;
        this.emit("playerEliminated", result.eliminatedPlayer!);
        this.finishRound();
        break;

      case VotingOutcome.TieRequiresRevote:
        this.tiebreakerCandidateIds = result.tiedCandidates!.map((p) => p.playerId);
        this.tiebreakerAttempts++;
        this.currentVotes.clear();
        this.setPhase(GamePhase.VotingTiebreaker);
        break;

      case VotingOutcome.TieUnresolvedNoElimination:
      case VotingOutcome.NoVotesCast:
        this.finishRound();
        break;
    }

    return result;
  }

  private tallyVotes(): VotingResult {
    const voteCounts = new Map<string, number>();

    for (const targetId of this.currentVotes.values()) {
      voteCounts.set(targetId, (voteCounts.get(targetId) ?? 0) + 1);
    }

    if (voteCounts.size === 0) {
      return {
        resultType: VotingOutcome.NoVotesCast,
        eliminatedPlayer: null,
        tiedCandidates: null,
        voteCounts,
      };
    }

    const topCount = Math.max(...voteCounts.values());
    const topIds = [...voteCounts.entries()].filter(([, count]) => count === topCount).map(([id]) => id);

    if (topIds.length === 1) {
      return {
        resultType: VotingOutcome.PlayerEliminated,
        eliminatedPlayer: this.getPlayer(topIds[0]) ?? null,
        tiedCandidates: null,
        voteCounts,
      };
    }

    // Tie: decide whether to allow a re-vote or give up.
    const tiedPlayers = topIds.map((id) => this.getPlayer(id)!).filter(Boolean);
    const canRetry = this.tiebreakerAttempts < GameSession.MAX_TIEBREAKER_ATTEMPTS;

    return {
      resultType: canRetry ? VotingOutcome.TieRequiresRevote : VotingOutcome.TieUnresolvedNoElimination,
      eliminatedPlayer: null,
      tiedCandidates: tiedPlayers,
      voteCounts,
    };
  }

  // --- Round wrap-up -------------------------------------------------

  private finishRound(): void {
    this.setPhase(GamePhase.RoundResult);

    if (this.isGameOver()) {
      this.endGame();
    } else {
      this.startNextRound();
    }
  }

  // --- Win condition -----------------------------------------------------

  protected isGameOver(): boolean {
    return this.activePlayers().length <= this.config.survivorsTarget;
  }

  protected endGame(): void {
    this.setPhase(GamePhase.GameOver);

    const survivors = this.activePlayers();
    const reason =
      survivors.length > 0 ? GameOverReason.SurvivorsTargetReached : GameOverReason.AllPlayersEliminated;

    this.emit("gameOverResolved", { endReason: reason, survivors });
  }

  // --- Helpers ---------------------------------------------------------

  activePlayers(): PlayerData[] {
    return this.players.filter((p) => !p.isEliminated);
  }

  getPlayer(playerId: string): PlayerData | undefined {
    return this.players.find((p) => p.playerId === playerId);
  }
}
