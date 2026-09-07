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
  // for this stage — a disconnect here simply leaves the player's schema
  // and GameSession state as-is.
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
        const trait = playerData.getTrait(category);
        const slot = new TraitSchema();
        slot.category = category;
        slot.revealed = playerData.isCategoryRevealed(category);
        slot.id = trait?.id ?? "";
        slot.localizationKey = trait?.localizationKey ?? "";
        schema.traits.push(slot);
      }

      if (playerData.special) {
        const specialSchema = new SpecialCardSchema();
        specialSchema.id = playerData.special.id;
        specialSchema.effectType = playerData.special.effectType;
        specialSchema.localizationKey = playerData.special.localizationKey;
        schema.special = specialSchema;
      }

      this.grantOwnerVisibility(playerData.playerId, schema);
    }

    // Public trait slots (category/revealed) and player entries themselves
    // need to be visible to everyone, same as at join time.
    this.grantPublicVisibilityToAllClients();
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
    this.syncTraitSlot(playerId, category as CardCategory);
    this.syncTraitSlot(targetPlayerId, category as CardCategory);

    const casterSchema = this.state.players.get(playerId);
    if (casterSchema) casterSchema.hasUsedSpecialCard = true;
  }

  private syncTraitSlot(playerId: string, category: CardCategory): void {
    const playerData = this.session?.getPlayer(playerId);
    const schema = this.state.players.get(playerId);
    if (!playerData || !schema) return;

    const trait = playerData.getTrait(category);
    const slot = schema.traits.find((t) => t.category === category);
    if (!slot || !trait) return;

    slot.id = trait.id;
    slot.localizationKey = trait.localizationKey;
    // revealed flag is untouched here — a swap does not reveal the trait.
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
        this.grantTraitVisibilityToAllClients(slot);
      }
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

  // --- StateView management ---------------------------------------------

  // Grants every connected client visibility into every player's public
  // fields (displayName, isEliminated, hasUsedSpecialCard, and each trait
  // slot's category/revealed flag). Must be re-run whenever a new player
  // or a new client joins, since StateView visibility is opt-in per
  // instance, not automatic just because a field isn't @view()-gated.
  private grantPublicVisibilityToAllClients(): void {
    for (const client of this.clients) {
      if (!client.view) continue;
      for (const playerSchema of this.state.players.values()) {
        client.view.add(playerSchema);
        for (const trait of playerSchema.traits) {
          client.view.add(trait);
        }
      }
    }
  }

  // Grants the owning client visibility into their own hidden trait content
  // (id/localizationKey, regardless of revealed state) and their own special
  // card — equivalent to the `isOwner` branch in the C# BuildPlayerSnapshot.
  private grantOwnerVisibility(playerId: string, schema: PlayerSchema): void {
    const sessionId = this.playerIdToSessionId.get(playerId);
    const client = sessionId ? this.clients.find((c) => c.sessionId === sessionId) : undefined;
    if (!client?.view) return;

    for (const trait of schema.traits) {
      client.view.add(trait);
    }
    if (schema.special) {
      client.view.add(schema.special);
    }
  }

  // Grants every connected client visibility into one specific trait's
  // @view()-gated fields (id/localizationKey) once it's been revealed —
  // equivalent to the public traitRevealed ClientRpc broadcast in the
  // Netcode version.
  private grantTraitVisibilityToAllClients(trait: TraitSchema): void {
    for (const client of this.clients) {
      client.view?.add(trait);
    }
  }
}
