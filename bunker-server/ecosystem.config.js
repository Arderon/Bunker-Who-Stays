// pm2 process definition for the dedicated Colyseus server.
// Usage on the VPS: pm2 start ecosystem.config.js
module.exports = {
  apps: [
    {
      name: "bunker-server",
      script: "build/index.js",
      cwd: __dirname,
      instances: 1,
      exec_mode: "fork",
      // Colyseus rooms live in a single process's memory — running more
      // than one instance/cluster mode would split players across
      // processes that can't see each other's rooms.
      autorestart: true,
      max_restarts: 10,
      restart_delay: 2000,
      env: {
        NODE_ENV: "production",
        PORT: 2567,
      },
    },
  ],
};
