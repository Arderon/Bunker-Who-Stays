import { Schema, type, MapSchema } from "@colyseus/schema";
import { PlayerSchema } from "./PlayerSchema";

// Root state schema for the room. Equivalent of the C# GameStateSnapshotDto,
// minus the manual per-recipient serialization — Colyseus's delta encoding
// sends only the changed fields to each client automatically, rather than
// us re-broadcasting a full JSON snapshot on every change (as
// NetworkGameSessionController.BroadcastSnapshotToAll had to do).
//
// players is a MapSchema keyed by playerId rather than an array — gives
// O(1) lookup by playerId both server- and client-side, and Colyseus's
// MapSchema handles add/remove more efficiently than diffing array indices
// on every player join/leave/elimination.
export class GameStateSchema extends Schema {
  @type("uint8") phase: number = 0;
  @type("uint16") currentRound: number = 0;
  @type("string") currentTurnPlayerId: string = "";

  @type({ map: PlayerSchema }) players = new MapSchema<PlayerSchema>();
}
