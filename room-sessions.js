import { randomBytes } from 'node:crypto';

export const RESUME_SECONDS = 55;

export class RoomSessions {
  constructor(room) {
    this.room = room;
    this.seats = new Map();
  }

  register(socket) {
    const token = randomBytes(32).toString('base64url');
    const seat = { token, player: socket.player, socket, expires: Infinity };
    this.seats.set(token, seat);
    socket.seat = seat;
    return token;
  }

  attach(socket, token, now) {
    if (typeof token !== 'string' || !/^[\w-]{43}$/.test(token)) return false;
    const seat = this.seats.get(token);
    if (!seat || now >= seat.expires || this.room.players.get(seat.player.id) !== seat.player) return false;
    if (seat.socket) {
      const previous = seat.socket;
      this.room.humans.delete(previous);
      previous.room = null;
      previous.seat = null;
      previous.close(1008, 'Connection replaced');
    }
    const player = seat.player;
    Object.assign(socket, { id: player.id, player, room: this.room, seat });
    this.room.stopPlayerInput(player);
    Object.assign(player, { away: false, ack: 0, receivedSeq: 0, lastFireId: 0, lastEchoAction: 0 });
    Object.assign(seat, { socket, expires: Infinity });
    this.room.humans.add(socket);
    this.room.director?.onJoin();
    return true;
  }

  disconnect(socket, now, permanent = false) {
    this.room.humans.delete(socket);
    const { seat } = socket;
    socket.room = socket.seat = null;
    if (seat?.socket === socket && !permanent) {
      seat.socket = null;
      seat.expires = now + RESUME_SECONDS;
      this.room.park(socket.player, now);
      return;
    }
    if (seat?.socket === socket) this.seats.delete(seat.token);
    this.room.removePlayer(socket.id, now);
    if (this.room.humans.size) this.room.fillBots(now);
  }

  expire(now) {
    let removed = false;
    for (const [token, seat] of this.seats) {
      if (seat.socket || now < seat.expires) continue;
      this.seats.delete(token);
      this.room.removePlayer(seat.player.id, now);
      removed = true;
    }
    if (removed && this.room.humans.size) this.room.fillBots(now);
  }
}
