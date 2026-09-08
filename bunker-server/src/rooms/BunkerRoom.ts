import { Room, Client } from "@colyseus/core";
import { StateView } from "@colyseus/schema";
import * as path from "path";

import { GameStateSchema } from "../schema/GameStateSchema";
import { PlayerSchema } from "../schema/PlayerSchema";
import { TraitSchema } from "../schema/TraitSchema";
import { SpecialCardSchema } from "../schema/SpecialCardSchema";

import { GameSession } from "../game/GameSession";
import { GameSessionConfig } from "../game/GameSessionConfig";
import { PlayerData } from "../game/PlayerData";
import { CardCategory, ALL_CARD_CATEGORIES } from "../game/CardCategory";
import { VotingResult } from "../game/VotingResult";
import { CharacterCardGenerator } from "../game/content/CharacterCardGenerator";
import { loadTraitPools, loadSpecialCardPool } from "../game/content/loadContent";
import { TraitPool } from "../game/content/TraitPool";
import { SpecialCardPool } from "../game/content/SpecialCardPool";

// Content is the same for every room instance — load it once per server
// process rather than re-reading disk on every room creation.
let cachedTraitPools: TraitPool[] | null = null;
let cachedSpecialCardPool: SpecialCardPool | null = null;

function getGameContent(): { traitPools: TraitPool[]; specialCardPool: SpecialCardPool } {
  if (!cachedTraitPools || !cachedSpecialCardPool) {
    cachedTraitPools = loadTraitPools(path.join(__dirname, "../../content/traits"));
    cachedSpecialCardPool = loadSpecialCardPool(path.join(__dirname, "../../content/specialCards.json"));
  }
  return { traitPools: cachedTraitPools, specialCardPool: cachedSpecialCardPool };
}

export class BunkerRoom extends Room<GameStateSchema> {
  maxClients = 12;

  private session: GameSession | null = null;
  private hostSessionId: string | null = null;

  // playerId (from Unity Authentication) <-> Colyseus sessionId, since
  // GameSession identifies players by playerId but Colyseus identifies
  // connections by sessionId — same dual-identity split the C# version
  // handled via _clientIdToPlayerId in NetworkGameSessionController.
  private sessionIdToPlayerId = new Map<string, string>();
  private playerIdToSessionId = new Map<string, string>();

  private discussionTimeoutHandle: { clear: () => void } | null = null;

  onCreate(options: any) {
    this.state = new GameStateSchema();
    this.setMetadata({ code: options.code });
    console.log(`[BunkerRoom] created with code: ${options.code}`);

    this.registerMessageHandlers();
  }

  // --- Join / leave -----------------------------------------------------

  onJoin(client: Client, options: any) {
    const playerId: string = options.playerId;
    const displayName: string = options.displayName ?? "Player";

    if (!playerId) {
      client.leave();
      return;
    }

    this.sessionIdToPlayerId.set(client.sessionId, playerId);
    this.playerIdToSessionId.set(playerId, client.sessionId);

    if (!this.hostSessionId) {
      this.hostSessionId = client.sessionId;
    }

    // Every client gets a StateView so @view()-gated fields (trait id/
    // localizationKey, special card) are filtered per-recipient. Public
    // fields (displayName, isEliminated, category, revealed, ...) still
    // need their owning instances explicitly added to the view — see
    // grantPublicVisibilityToClient below.
    client.view = new StateView();

    // Lobby-phase player entry: no traits yet (dealt only once the game
    // starts). Real trait/special data populated in dealAndSyncAllPlayers,
    // called from the "startGame" handler.
    const playerSchema = new PlayerSchema();
    playerSchema.playerId = playerId;
    playerSchema.displayName = displayName;
    this.state.players.set(playerId, playerSchema);

    this.grantPublicVisibilityToAllClients();

    console.log(`[BunkerRoom] ${client.sessionId} joined as playerId=${playerId}`);
  }

  onLeave(client: Client, consented?: boolean) {
    console.log(`[BunkerRoom] ${client.sessionId} left (consented=${consented})`);
    // Reconnection handling and host migration are explicitly out of scope
    // for this stage (deferred, same as the equivalent gap noted for the
    // Netcode-based implementation) — a disconnect here simply leaves the
    // player's schema and GameSession state as-is.
  }

  onDispose() {
    this.discussionTimeoutHandle?.clear();
    console.log(`[BunkerRoom] disposed`);
  }

  // --- Message handlers ---------------------------------------------------

  private registerMessageHandlers(): void {
    this.onMessage("startGame", (client) => this.handleStartGame(client));

    this.onMessage("revealTrait", (client, message: { category: number }) =>
      this.handleRevealTrait(client, message.category)
    );

    this.onMessage("startDiscussionPhase", (client, message: { durationSeconds: number }) =>
      this.handleStartDiscussionPhase(client, message.durationSeconds)
    );

    this.onMessage("skipDiscussion", (client) => this.handleSkipDiscussion(client));

    this.onMessage("castVote", (client, message: { targetPlayerId: string }) =>
      this.handleCastVote(client, message.targetPlayerId)
    );

    this.onMessage("resolveVotes", (client) => this.handleResolveVotes(client));

    this.onMessage("useSpecialCard", (client, message: { targetPlayerId: string | null }) =>
      this.handleUseSpecialCard(client, message.targetPlayerId)
    );

    this.onMessage(
      "useSwapTraitSpecialCard",
      (client, message: { targetPlayerId: string; category: number }) =>
        this.handleUseSwapTraitSpecialCard(client, message.targetPlayerId, message.category)
    );
  }

  private resolvePlayerId(client: Client): string | null {
    return this.sessionIdToPlayerId.get(client.sessionId) ?? null;
  }

  private isHost(client: Client): boolean {
    return client.sessionId === this.hostSessionId;
  }

  private reject(client: Client, key: string): void {
    client.send("actionRejected", { key });
  }

  // --- Start game ----------------------------------------------------

  private handleStartGame(client: Client): void {
    if (!this.isHost(client)) {
      this.reject(client, "ui_common_error_generic");
      return;
    }

    const players = [...this.state.players.values()].map(
      (p) => new PlayerData(p.playerId, p.displayName)
    );

    const config: GameSessionConfig = { survivorsTarget: 2, allowVoteTies: false };
    const { traitPools, specialCardPool } = getGameContent();
    const generator = new CharacterCardGenerator(traitPools, specialCardPool);

    const session = new GameSession(players, config, generator);
    const validation = session.validateCanStart();

    if (!validation.canStart) {
      console.warn(`[BunkerRoom] Cannot start: ${validation.failReason}`);
      this.reject(client, "ui_common_error_generic");
      return;
    }

    this.session = session;
    this.wireSessionEvents(session);

    session.startGame();
    this.dealAndSyncAllPlayers(session);

    this.state.phase = session.phase;
    this.state.currentRound = session.currentRound;
    this.state.currentTurnPlayerId = session.currentTurnPlayerId ?? "";
  }

  // Populates real trait/special data into every PlayerSchema right after
  // dealing, and grants each client private visibility into their own cards.
  private dealAndSyncAllPlayers(session: GameSession): void {
    for (const playerData of session.players) {
      const schema = this.state.players.get(playerData.playerId);
      if (!schema) continue;

      schema.traits.clear();
      for (const category of ALL_CARD_CATEGORIES) {
        const slot = new TraitSchema();
        slot.category = category;
        slot.revealed = playerData.isCategoryRevealed(category);
        schema.traits.push(slot);
      }

      // special still uses @view() successfully (verified against a live
      // server) — direct field references on a Schema behave differently
      // from Schema instances nested inside a collection, so this one
      // stays as-is.
      if (playerData.special) {
        const specialSchema = new SpecialCardSchema();
        specialSchema.id = playerData.special.id;
        specialSchema.effectType = playerData.special.effectType;
        specialSchema.localizationKey = playerData.special.localizationKey;
        schema.special = specialSchema;
      }

      this.grantOwnerVisibility(playerData.playerId, schema);
      this.sendDealtHand(playerData);
    }

    // Public trait slots (category/revealed) and player entries themselves
    // need to be visible to everyone, same as at join time.
    this.grantPublicVisibilityToAllClients();
  }

  // Privately delivers a player's own full hand (all 7 traits' real
  // content) via a direct message, sent once at deal time. Replaces the
  // earlier attempt to expose this through @view()-gated TraitSchema
  // fields, which — verified against a live server — never reliably
  // reached even the owning client for Schema instances nested inside an
  // ArraySchema.
  private sendDealtHand(playerData: PlayerData): void {
    const sessionId = this.playerIdToSessionId.get(playerData.playerId);
    const client = sessionId ? this.clients.find((c) => c.sessionId === sessionId) : undefined;
    if (!client) return;

    const traits = ALL_CARD_CATEGORIES.map((category) => {
      const trait = playerData.getTrait(category);
      return { category, id: trait?.id ?? "", localizationKey: trait?.localizationKey ?? "" };
    });

    client.send("dealtHand", { traits });
  }

  // --- Reveal phase ----------------------------------------------------

  private handleRevealTrait(client: Client, category: number): void {
    const playerId = this.resolvePlayerId(client);
    if (!playerId || !this.session) return;

    const success = this.session.revealNextTrait(playerId, category as CardCategory);
    if (!success) {
      this.reject(client, "ui_common_error_generic");
      return;
    }

    this.state.currentTurnPlayerId = this.session.currentTurnPlayerId ?? "";
  }

  // --- Discussion phase -------------------------------------------------

  private handleStartDiscussionPhase(client: Client, durationSeconds: number): void {
    if (!this.isHost(client) || !this.session) return;
    this.session.startDiscussionPhase(durationSeconds);
  }

  private handleSkipDiscussion(client: Client): void {
    if (!this.session) return;
    this.discussionTimeoutHandle?.clear();
    this.session.startVotingPhase();
  }

  // --- Voting ------------------------------------------------------------

  private handleCastVote(client: Client, targetPlayerId: string): void {
    const playerId = this.resolvePlayerId(client);
    if (!playerId || !this.session) return;

    const success = this.session.castVote(playerId, targetPlayerId);
    if (!success) {
      const target = this.session.getPlayer(targetPlayerId);
      const key = target?.hasVoteImmunityThisRound
        ? "ui_voting_error_target_immune"
        : "ui_common_error_generic";
      this.reject(client, key);
    }
  }

  private handleResolveVotes(client: Client): void {
    if (!this.isHost(client) || !this.session) return;
    this.session.resolveVotes();
  }

  // --- Special cards -----------------------------------------------------

  private handleUseSpecialCard(client: Client, targetPlayerId: string | null): void {
    const playerId = this.resolvePlayerId(client);
    if (!playerId || !this.session) return;

    const result = this.session.useSpecialCard(playerId, targetPlayerId);

    if (!result.success) {
      this.reject(client, "ui_special_card_error_already_used");
      return;
    }

    // Private payload (RevealHiddenTrait) goes only to the caster's client
    // as a one-off message — never written into the shared Schema, or it
    // would need per-client filtering there too for no real benefit.
    if (result.revealedTrait) {
      client.send("privateTraitPeek", {
        category: result.revealedTrait.category,
        id: result.revealedTrait.id,
        localizationKey: result.revealedTrait.localizationKey,
      });
    }

    const schema = this.state.players.get(playerId);
    if (schema) schema.hasUsedSpecialCard = true;
  }

  private handleUseSwapTraitSpecialCard(client: Client, targetPlayerId: string, category: number): void {
    const playerId = this.resolvePlayerId(client);
    if (!playerId || !this.session) return;

    const result = this.session.useSwapTraitSpecialCard(playerId, targetPlayerId, category as CardCategory);

    if (!result.success) {
      this.reject(client, "ui_special_card_error_already_used");
      return;
    }

    // Swap changes trait content without necessarily "revealing" it, so it
    // isn't covered by the traitRevealed event — resync both affected slots
    // directly from the now-updated GameSession player data.
    // Swap changes the content of a still-hidden trait for both players
    // without revealing it — since trait content is now delivered via
    // private messages (not Schema), each affected player gets a private
    // update for just this one category rather than a Schema resync.
    this.sendTraitUpdated(playerId, category as CardCategory);
    this.sendTraitUpdated(targetPlayerId, category as CardCategory);

    const casterSchema = this.state.players.get(playerId);
    if (casterSchema) casterSchema.hasUsedSpecialCard = true;
  }

  // Privately informs one player that a single category in their own hand
  // changed content (used by SwapTrait, which alters hidden trait content
  // without revealing it publicly).
  private sendTraitUpdated(playerId: string, category: CardCategory): void {
    const playerData = this.session?.getPlayer(playerId);
    const sessionId = this.playerIdToSessionId.get(playerId);
    const client = sessionId ? this.clients.find((c) => c.sessionId === sessionId) : undefined;
    if (!playerData || !client) return;

    const trait = playerData.getTrait(category);
    client.send("traitUpdated", {
      category,
      id: trait?.id ?? "",
      localizationKey: trait?.localizationKey ?? "",
    });
  }

  // --- GameSession event wiring ------------------------------------------

  private wireSessionEvents(session: GameSession): void {
    session.on("phaseChanged", (phase) => {
      this.state.phase = phase;
    });

    session.on("roundStarted", (round) => {
      this.state.currentRound = round;
      this.state.currentTurnPlayerId = session.currentTurnPlayerId ?? "";
    });

    session.on("traitRevealed", (player, trait) => {
      const schema = this.state.players.get(player.playerId);
      const slot = schema?.traits.find((t) => t.category === trait.category);
      if (slot) {
        slot.revealed = true;
      }

      // Real content delivered via broadcast message rather than Schema
      // @view() (see TraitSchema.ts for why) — every client updates their
      // local model of this player's revealed trait from this message.
      this.broadcast("traitRevealed", {
        playerId: player.playerId,
        category: trait.category,
        id: trait.id,
        localizationKey: trait.localizationKey,
      });
    });

    session.on("revealPassCompleted", () => {
      this.broadcast("revealPassCompleted");
    });

    session.on("specialCardUsed", (player) => {
      const schema = this.state.players.get(player.playerId);
      if (schema) schema.hasUsedSpecialCard = true;
    });

    session.on("discussionStarted", (durationSeconds) => {
      this.broadcast("discussionStarted", { durationSeconds });
      this.scheduleAutoAdvanceToVoting(durationSeconds);
    });

    session.on("votingResolved", (result) => {
      this.broadcast("votingResolved", this.serializeVotingResult(result));
    });

    session.on("playerEliminated", (player) => {
      const schema = this.state.players.get(player.playerId);
      if (schema) schema.isEliminated = true;
    });

    session.on("gameOverResolved", (result) => {
      this.broadcast("gameOver", {
        endReason: result.endReason,
        survivorPlayerIds: result.survivors.map((p) => p.playerId),
      });
    });
  }

  // Server-authoritative discussion timer — an improvement over the earlier
  // Netcode version, where the client UI drove the countdown and merely
  // called StartVotingPhaseServerRpc when it ran out. Here the room itself
  // (not any specific client) owns the timeout, using Colyseus's built-in
  // clock, so voting reliably starts even if every client's local timer UI
  // were to misbehave.
  private scheduleAutoAdvanceToVoting(durationSeconds: number): void {
    this.discussionTimeoutHandle?.clear();
    this.discussionTimeoutHandle = this.clock.setTimeout(() => {
      this.session?.startVotingPhase();
    }, durationSeconds * 1000);
  }

  private serializeVotingResult(result: VotingResult) {
    return {
      resultType: result.resultType,
      eliminatedPlayerId: result.eliminatedPlayer?.playerId ?? null,
      tiedCandidateIds: result.tiedCandidates?.map((p) => p.playerId) ?? [],
      voteCounts: Object.fromEntries(result.voteCounts),
    };
  }

  // --- StateView management (special card only — see TraitSchema.ts for
  // why traits no longer use this mechanism) ---------------------------

  // Grants every connected client visibility into every player's public
  // Schema fields (displayName, isEliminated, hasUsedSpecialCard, and each
  // trait slot's category/revealed flag — none of which are @view()-gated
  // anymore). Must be re-run whenever a new player or client joins, since
  // StateView visibility is opt-in per instance.
  private grantPublicVisibilityToAllClients(): void {
    for (const client of this.clients) {
      if (!client.view) continue;
      for (const playerSchema of this.state.players.values()) {
        client.view.add(playerSchema);
      }
    }
  }

  // Grants the owning client visibility into their own special card.
  // Confirmed working against a live server — unlike TraitSchema's fields,
  // `special` is a direct field reference (not nested inside a collection),
  // and @view() behaves correctly for that case.
  private grantOwnerVisibility(playerId: string, schema: PlayerSchema): void {
    const sessionId = this.playerIdToSessionId.get(playerId);
    const client = sessionId ? this.clients.find((c) => c.sessionId === sessionId) : undefined;
    if (!client?.view || !schema.special) return;

    client.view.add(schema.special);
  }
}