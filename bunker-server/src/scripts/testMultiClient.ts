import { Client, Room } from "colyseus.js";

// Simulates N real players connecting to the same room over WebSocket and
// playing through Reveal -> Discussion -> Voting, logging every incoming
// state change per-client. This is the network-level equivalent of the
// HeadlessGameRunner (section 2.8) — same purpose, but exercises the real
// BunkerRoom message handlers and StateView filtering instead of calling
// GameSession directly.

const SERVER_URL = "ws://localhost:2567";
const PLAYER_COUNT = 4;
const ROOM_CODE = "TESTROOM";

interface SimClient {
  playerId: string;
  displayName: string;
  room: Room;
}

async function main() {
  const clients: SimClient[] = [];

  // First client creates the room, the rest join by code.
  for (let i = 0; i < PLAYER_COUNT; i++) {
    const playerId = `player_${i}`;
    const displayName = `Player ${i}`;
    const client = new Client(SERVER_URL);

    const room =
      i === 0
        ? await client.create("bunker_room", { code: ROOM_CODE, playerId, displayName })
        : await client.joinById(
            (await client.getAvailableRooms("bunker_room")).find((r) => r.metadata?.code === ROOM_CODE)!.roomId,
            { playerId, displayName }
          );

    room.onStateChange((state) => {
      console.log(`[${playerId}] state: phase=${state.phase} round=${state.currentRound} turn=${state.currentTurnPlayerId}`);
    });

    room.onMessage("actionRejected", (msg) => console.log(`[${playerId}] REJECTED: ${msg.key}`));
    room.onMessage("revealPassCompleted", () => console.log(`[${playerId}] reveal pass completed`));
    room.onMessage("discussionStarted", (msg) => console.log(`[${playerId}] discussion started: ${msg.durationSeconds}s`));
    room.onMessage("votingResolved", (msg) => console.log(`[${playerId}] voting resolved:`, msg));
    room.onMessage("gameOver", (msg) => console.log(`[${playerId}] GAME OVER:`, msg));
    room.onMessage("privateTraitPeek", (msg) => console.log(`[${playerId}] PRIVATE PEEK:`, msg));

    clients.push({ playerId, displayName, room });
    console.log(`[${playerId}] joined room ${room.roomId}`);
  }

  await sleep(500);

  // --- Privacy check: before the game starts, no one should see any
  // trait content yet (traits array is empty until dealt). ---

  // Host starts the game.
  clients[0].room.send("startGame");
  await sleep(1000);

  // --- Privacy check: each client's OWN traits should be populated with
  // real id/localizationKey, but traits belonging to OTHER players should
  // show revealed=false with EMPTY id/localizationKey (StateView filtering
  // working as intended from section 3/4). ---
  for (const c of clients) {
    const state = c.room.state as any;
    const ownPlayer = state.players.get(c.playerId);
    const otherPlayer = [...state.players.values()].find((p: any) => p.playerId !== c.playerId);

    console.log(`\n[${c.playerId}] own first trait:`, ownPlayer.traits[0]);
    console.log(`[${c.playerId}] other player's first trait (should be empty id/key if unrevealed):`, otherPlayer.traits[0]);
    console.log(`[${c.playerId}] own special card:`, ownPlayer.special);
  }

  // --- Drive the reveal phase: each player reveals their traits in turn. ---
  for (let round = 0; round < PLAYER_COUNT; round++) {
    const state = clients[0].room.state as any;
    const currentTurnId = state.currentTurnPlayerId;
    const actingClient = clients.find((c) => c.playerId === currentTurnId);
    if (!actingClient) break;

    actingClient.room.send("revealTrait", { category: 0 }); // reveal Gender slot
    await sleep(300);
  }

  await sleep(500);

  // Host starts discussion, then skips it immediately for test speed.
  clients[0].room.send("startDiscussionPhase", { durationSeconds: 30 });
  await sleep(300);
  clients[0].room.send("skipDiscussion");
  await sleep(500);

  // Everyone votes for player_1 (except player_1 themselves, who votes for player_2).
  for (const c of clients) {
    const target = c.playerId === "player_1" ? "player_2" : "player_1";
    c.room.send("castVote", { targetPlayerId: target });
  }
  await sleep(300);

  clients[0].room.send("resolveVotes");
  await sleep(1000);

  console.log("\n--- Test sequence complete, leaving rooms ---");
  for (const c of clients) {
    await c.room.leave();
  }
  process.exit(0);
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

main().catch((err) => {
  console.error("Test script failed:", err);
  process.exit(1);
});