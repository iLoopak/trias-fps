#!/usr/bin/env node
// Open a TRIAS bot room on a local server and keep its host seat connected, so a native player can join
// it with `-trias-invite http://127.0.0.1:3000/join/<CODE>` for unattended playtests.
//
//   node tools/playtest/host-room.mjs <CODE> <mode> [arena] [seconds]
//
// The host seat stays idle (no input) and leaves after `seconds` (default 180).
import WebSocket from 'ws';
import { NETWORK_VERSION } from '../../public/netcode.js';
import { LAYOUT_SET } from '../../public/rules.js';

const [code = 'PLAYTEST', mode = 'tdm', arena = 'relay', seconds = '180'] = process.argv.slice(2);
const socket = new WebSocket(process.env.TRIAS_SERVER ?? 'ws://127.0.0.1:3000/play');
socket.on('open', () => socket.send(JSON.stringify({
  type: 'join', v: NETWORK_VERSION, room: code, name: 'Host', mode, arena, layoutSet: LAYOUT_SET,
  intent: 'create', bots: true, character: 'vector', frame: 'male',
})));
socket.on('message', data => {
  const text = data.toString();
  if (text.startsWith('{')) {
    const message = JSON.parse(text);
    if (message.type === 'joined') console.log(`ROOM ${message.room} ${message.mode} ${message.arena} bots=${message.bots}`);
    if (message.type === 'error' || message.type === 'rejected') { console.error('REJECTED', text); process.exit(1); }
  }
});
socket.on('error', error => { console.error('SOCKET', error.message); process.exit(1); });
setTimeout(() => { socket.close(); process.exit(0); }, Number(seconds) * 1000);
