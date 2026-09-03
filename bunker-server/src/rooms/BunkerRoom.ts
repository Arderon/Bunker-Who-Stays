import { Room, Client } from "@colyseus/core";

// Minimal placeholder room — real game logic, message handlers, and state
// schema (GameSession wiring) are added in stage 3/4 of the migration plan.
// For now this only needs to exist so the server can start and rooms can
// be created/joined for smoke testing (see stage 1, steps 9-10).
export class BunkerRoom extends Room {
  maxClients = 12;

  onCreate(options: any) {
    console.log(`[BunkerRoom] created with code: ${options.code}`);
    this.setMetadata({ code: options.code });
  }

  onJoin(client: Client, options: any) {
    // options.playerId is expected to come from Unity Authentication —
    // trusted as-is for now (see security note in the migration plan
    // about onAuth-based verification as a future hardening step).
    console.log(`[BunkerRoom] ${client.sessionId} joined as playerId=${options.playerId}`);
  }

  onLeave(client: Client, consented: boolean) {
    console.log(`[BunkerRoom] ${client.sessionId} left (consented=${consented})`);
  }

  onDispose() {
    console.log(`[BunkerRoom] disposed`);
  }
}
