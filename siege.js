import { distanceSquared, rayBox, SIEGE_CORE_HP, TEAMS, TICK } from './public/rules.js';

const CAPTURE_SECONDS = 4;

export class Siege {
  constructor(match, layout) {
    this.match = match;
    this.layout = layout;
    this.neighbors = layout.nodes.map(() => []);
    for (const [a, b] of layout.links) {
      this.neighbors[a].push(b);
      this.neighbors[b].push(a);
    }
  }

  reset() {
    this.nodes = this.layout.nodes.map(node => ({ owner: node.home, capture: -1, progress: 0, contested: false, linked: false }));
    this.cores = this.layout.cores.map(core => ({ ...core, id: 'core-' + core.team, hp: SIEGE_CORE_HP, opened: 0 }));
    this.connected = this.cores.map(() => new Set());
    this.link();
  }

  alive(team) {
    return this.cores[team].hp > 0;
  }

  link() {
    for (const [team, route] of this.connected.entries()) {
      route.clear();
      if (!this.alive(team) || this.nodes[team].owner !== team) continue;
      const queue = [team];
      route.add(team);
      for (const node of queue) {
        for (const next of this.neighbors[node]) {
          if (this.nodes[next].owner !== team || route.has(next)) continue;
          route.add(next);
          queue.push(next);
        }
      }
    }
    for (const [i, node] of this.nodes.entries()) node.linked = node.owner >= 0 && this.connected[node.owner].has(i);
    for (const [team, core] of this.cores.entries()) {
      const attacker = this.nodes[team].owner;
      core.opened = core.hp > 0 && attacker >= 0 && attacker !== team && this.connected[attacker].has(team)
        ? 1 << attacker : 0;
    }
  }

  canCapture(team, index) {
    return this.alive(team) && (this.layout.nodes[index].home === team
      || this.neighbors[index].some(next => this.connected[team].has(next)));
  }

  step() {
    let changed = false;
    for (const [i, node] of this.nodes.entries()) {
      const location = this.layout.nodes[i];
      const present = new Map();
      for (const player of this.match.players.values()) {
        if (player.hp <= 0 || player.vehicle >= 0 || player.turret >= 0 || Math.abs(player.y - location.y) > 1.45
          || distanceSquared(player, location) > location.radius ** 2) continue;
        present.set(player.team, (present.get(player.team) ?? 0) + 1);
      }
      node.contested = present.size > 1;
      if (node.contested) continue;
      const [team, count] = present.entries().next().value ?? [];
      if (team === undefined || team === node.owner || !this.canCapture(team, i)) {
        node.progress = Math.max(0, node.progress - 0.31 * TICK);
        if (!node.progress) node.capture = -1;
        continue;
      }
      if (node.capture !== team) {
        node.capture = team;
        node.progress = 0;
      }
      node.progress += (1 + Math.min(2, count - 1) * 0.20) * TICK / CAPTURE_SECONDS;
      if (node.progress < 1) continue;
      Object.assign(node, { owner: team, capture: -1, progress: 0 });
      changed = true;
      this.match.events.push({ type: 'notice', text: TEAMS[team].name + ' obsadil uzel ' + location.label });
    }
    if (changed) {
      const before = this.cores.map(core => core.opened);
      this.link();
      for (const [i, core] of this.cores.entries()) {
        if (core.hp > 0 && before[i] !== core.opened) {
          this.match.events.push({ type: 'notice', text: 'Jádro ' + TEAMS[i].name + (core.opened ? ' je odkryté' : ' je opět chráněné') });
        }
      }
    }
  }

  distance(core, origin, direction) {
    return rayBox(origin, direction,
      { x: core.x - core.radius, y: core.y - core.height, z: core.z - core.radius },
      { x: core.x + core.radius, y: core.y + core.height, z: core.z + core.radius });
  }

  damage(attacker, core, amount, now) {
    if (!core.hp || !this.alive(attacker.team) || !(core.opened & (1 << attacker.team))) return null;
    const damage = Math.min(core.hp, Math.max(1, Math.round(amount)));
    core.hp -= damage;
    if (core.hp === 0) {
      for (const player of this.match.players.values()) {
        if (player.team !== core.team) continue;
        this.match.unmount(player);
        this.match.fleet.release(player, now);
        player.hp = 0;
        player.respawnAt = 0;
      }
      for (const node of this.nodes) {
        if (node.owner === core.team) Object.assign(node, { owner: -1, capture: -1, progress: 0, contested: false });
      }
      this.link();
      this.match.events.push({ type: 'core-wreck', team: core.team, position: [core.x, core.y, core.z] });
      this.match.events.push({ type: 'notice', text: 'Jádro ' + TEAMS[core.team].name + ' zničeno' });
    }
    return { id: core.id, health: damage, absorbed: 0, eliminated: core.hp === 0, kind: 'core' };
  }

  winner(timeout) {
    const standing = this.cores.filter(core => core.hp > 0);
    if (!timeout && standing.length > 1) return null;
    const ranked = standing.map(core => ({ team: core.team, label: TEAMS[core.team].name, score: core.hp,
      links: this.connected[core.team].size }));
    ranked.sort((a, b) => b.score - a.score || b.links - a.links);
    const [lead, next] = ranked;
    if (!lead || next && lead.score === next.score && lead.links === next.links) return { label: 'Remíza' };
    return lead;
  }

  botGoal(player) {
    const team = player.team;
    if (this.nodes[team].owner !== team) return { target: this.layout.nodes[team] };
    for (const core of this.cores) {
      if (core.opened & (1 << team)) return { target: core, core };
    }
    const candidates = this.layout.nodes.filter((_, i) => this.nodes[i].owner !== team && this.canCapture(team, i));
    if (!candidates.length) return { target: this.layout.nodes[team] };
    candidates.sort((a, b) => distanceSquared(a, player) - distanceSquared(b, player));
    return { target: candidates[player.slot >= 2 && candidates.length > 1 ? 1 : 0] };
  }

  snapshot() {
    return { nodes: this.nodes, cores: this.cores.map(({ hp, opened }) => ({ hp, opened })) };
  }
}
