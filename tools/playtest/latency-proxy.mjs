#!/usr/bin/env node
// WebSocket proxy that adds one-way delay and jitter between a native player and a local TRIAS server,
// so prediction, reconciliation and remote interpolation can be playtested as a networked match.
//
//   node tools/playtest/latency-proxy.mjs <listen-port> <upstream ws://host:port/play> <one-way-ms> [jitter-ms]
//
// Invite the player through the proxy port (http://127.0.0.1:<listen-port>/join/<CODE>); the client derives
// ws://127.0.0.1:<listen-port>/play from it. Message order is preserved in both directions.
import { WebSocketServer, WebSocket } from 'ws';

const [port = '3101', upstream = 'ws://127.0.0.1:3000/play', delay = '50', jitter = '0'] = process.argv.slice(2);
const oneWay = Number(delay), spread = Number(jitter);
const server = new WebSocketServer({ port: Number(port), path: '/play' });

function delayed(target) {
  let last = 0;
  return (data, isBinary) => {
    const due = Math.max(last, Date.now() + oneWay + Math.random() * spread);
    last = due;
    setTimeout(() => { if (target.readyState === WebSocket.OPEN) target.send(data, { binary: isBinary }); }, due - Date.now());
  };
}

server.on('connection', client => {
  const far = new WebSocket(upstream);
  const pending = [];
  const toFar = delayed(far), toClient = delayed(client);
  far.on('open', () => { for (const [data, binary] of pending.splice(0)) toFar(data, binary); });
  client.on('message', (data, binary) => far.readyState === WebSocket.OPEN ? toFar(data, binary) : pending.push([data, binary]));
  far.on('message', (data, binary) => toClient(data, binary));
  const close = () => { client.close(); far.close(); };
  client.on('close', close); far.on('close', close);
  client.on('error', close); far.on('error', close);
});
console.log(`latency proxy :${port}/play -> ${upstream} one-way ${oneWay} ms + ${spread} ms jitter`);
