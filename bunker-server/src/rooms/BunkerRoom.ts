import { Room, Client } from "@colyseus/core";

// Minimal placeholder room — real game logic, message handlers, and state
// schema are wired up in later stages. For now this only needs to exist
// so the server can start and rooms can be created/joined for smoke testing.
export class BunkerRoom extends Room {
  maxClients = 12;

  onCreate(options: any) {
    console.log(`[BunkerRoom] created with code: ${options.code}`);
    this.setMetadata({ code: options.code });
  }

  onJoin(client: Client, options: any) {
    // options.playerId is expected to come from Unity Authentication —
    // trusted as-is for now (see security note below).
    console.log(`[BunkerRoom] ${client.sessionId} joined as playerId=${options.playerId}`);
  }

  onLeave(client: Client, consented: boolean) {
    console.log(`[BunkerRoom] ${client.sessionId} left (consented=${consented})`);
  }

  onDispose() {
    console.log(`[BunkerRoom] disposed`);
  }
}