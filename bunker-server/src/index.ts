import { Server } from "@colyseus/core";
import { WebSocketTransport } from "@colyseus/ws-transport";
import { playground } from "@colyseus/playground";
import express from "express";
import cors from "cors";
import { createServer } from "http";
import { BunkerRoom } from "./rooms/BunkerRoom";

const port = Number(process.env.PORT || 2567);

const app = express();
app.use(cors());
app.use(express.json());

const httpServer = createServer(app);

const gameServer = new Server({
  transport: new WebSocketTransport({ server: httpServer }),
});

// Room definition with built-in code-based matchmaking:
// clients call client.joinOrCreate("bunker_room", { code: "A1B2C3" }),
// and Colyseus automatically joins an existing room with the same "code"
// option, or creates a new one if none exists — this is what replaces
// UGS Lobby's "create/join by code" flow entirely, per the stage-0 decision.
gameServer
  .define("bunker_room", BunkerRoom)
  .filterBy(["code"]);

// Dev-only tools: browser-based room inspector/tester.
// Remove or guard behind an env check before deploying to production.
if (process.env.NODE_ENV !== "production") {
  app.use("/playground", playground());
}

httpServer.listen(port, () => {
  console.log(`[bunker-server] listening on ws://localhost:${port}`);
});