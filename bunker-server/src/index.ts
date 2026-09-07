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

gameServer
  .define("bunker_room", BunkerRoom)
  .filterBy(["code"]);

if (process.env.NODE_ENV !== "production") {
  app.use("/playground", playground());
}

httpServer.listen(port, () => {
  console.log(`[bunker-server] listening on ws://localhost:${port}`);
});