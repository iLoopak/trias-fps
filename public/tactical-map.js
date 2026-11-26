import { ISLAND_CROSSINGS, TEAMS, creekCenter, riverCenter } from './rules.js';
import { ISLAND_TRAIL } from './island-terrain.js';

export function drawTacticalMap(canvas, layout, state, own, mode, yaw) {
  const ctx = canvas.getContext('2d');
  const width = canvas.width;
  const scale = width / (2 * layout.length + 13);
  const position = value => [width * 0.5 + value.x * scale, width * 0.5 + value.z * scale];
  const circle = (x, z, radius, style, fill) => {
    ctx.beginPath();
    ctx.arc(x, z, radius, 0, Math.PI * 2);
    if (fill) {
      ctx.fillStyle = style;
      ctx.fill();
    } else {
      ctx.strokeStyle = style;
      ctx.stroke();
    }
  };
  ctx.clearRect(0, 0, width, width);
  ctx.fillStyle = '#141b25';
  ctx.fillRect(0, 0, width, width);
  if (layout.edgeRadius) {
    circle(width / 2, width / 2, layout.edgeRadius * scale, '#344e3f', true);
    ctx.save();
    ctx.clip();
    ctx.strokeStyle = '#7cbfcb';
    ctx.lineWidth = 3.9 * scale;
    ctx.beginPath();
    ctx.moveTo(...position({ x: -layout.edgeRadius, z: riverCenter(-layout.edgeRadius) }));
    for (let x = -layout.edgeRadius + 0.5; x < layout.edgeRadius + 0.5; x += 0.5) {
      ctx.lineTo(...position({ x, z: riverCenter(x) }));
    }
    ctx.stroke();
    ctx.lineWidth = 1.2 * scale;
    ctx.beginPath();
    ctx.moveTo(...position({ x: creekCenter(-19), z: -19 }));
    for (let z = -18.5; z < -2; z += 0.5) ctx.lineTo(...position({ x: creekCenter(z), z }));
    ctx.stroke();
    ctx.fillStyle = '#e6c293';
    for (const x of ISLAND_CROSSINGS) {
      const [px, pz] = position({ x, z: riverCenter(x) });
      ctx.fillRect(px - 1.4 * scale, pz - 2.5 * scale, 2.8 * scale, 5 * scale);
    }
    ctx.lineWidth = 1.5;
    ctx.strokeStyle = '#ddbf90';
    ctx.setLineDash([3, 3]);
    ctx.beginPath();
    const [first, ...trail] = ISLAND_TRAIL;
    ctx.moveTo(...position({ x: first[0], z: first[1] }));
    for (const [x, z] of trail) ctx.lineTo(...position({ x, z }));
    ctx.stroke();
    ctx.setLineDash([]);
    for (const [x, z] of [first, ISLAND_TRAIL.at(-1)]) {
      circle(...position({ x, z }), 3, '#f0dcb7', true);
    }
    ctx.restore();
  } else {
    ctx.strokeStyle = '#46515b';
    ctx.lineWidth = 2;
    const inset = width * 0.5 - layout.length * scale;
    ctx.strokeRect(inset, inset, width - inset * 2, width - inset * 2);

    ctx.lineWidth = 14;
    circle(width / 2, width / 2, layout.roadRadius * scale, '#3c3b3c', false);
    ctx.lineWidth = 1.25;
    ctx.setLineDash([4, 6]);
    circle(width / 2, width / 2, layout.roadRadius * scale, '#c9a46e', false);
    ctx.setLineDash([]);
  }

  for (const wall of layout.cover) {
    const [x, z] = position(wall);
    ctx.save();
    ctx.translate(x, z);
    ctx.rotate(-(wall.angle ?? 0));
    ctx.fillStyle = wall.nature === 'tree' ? '#aec698' : wall.gantry ? '#7d768a' : '#93898e';
    ctx.fillRect(-wall.w * scale / 2, -wall.d * scale / 2, wall.w * scale, wall.d * scale);
    ctx.restore();
  }

  ctx.font = '700 11px monospace';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  for (const pad of layout.repairs ?? []) {
    const [x, z] = position(pad);
    circle(x, z, 7, '#213c32', true);
    ctx.fillStyle = '#a3ffb7';
    ctx.fillText('+', x, z + 1);
  }
  if (state.siege) {
    const { nodes, cores, links } = layout.siege;
    for (const [a, b] of links) {
      const first = state.siege.nodes[a];
      const second = state.siege.nodes[b];
      const lit = first.linked && second.linked && first.owner === second.owner;
      ctx.strokeStyle = lit ? TEAMS[first.owner].color : '#788498';
      ctx.lineWidth = lit ? 2.15 : 1.15;
      ctx.setLineDash(lit ? [] : [3, 4]);
      ctx.beginPath();
      ctx.moveTo(...position(nodes[a]));
      ctx.lineTo(...position(nodes[b]));
      ctx.stroke();
    }
    ctx.setLineDash([]);
    nodes.forEach((node, i) => {
      const display = state.siege.nodes[i];
      const [x, z] = position(node);
      const team = display.capture >= 0 ? display.capture : display.owner;
      circle(x, z, 8, TEAMS[team]?.color ?? '#cad6dd', true);
      ctx.fillStyle = '#0e1820';
      ctx.fillText(node.label, x, z + 1);
    });
    cores.forEach((core, i) => {
      const display = state.siege.cores[i];
      const [x, z] = position(core);
      ctx.lineWidth = display.opened ? 2.4 : 1.05;
      ctx.strokeStyle = display.hp ? TEAMS[i].color : '#596070';
      ctx.strokeRect(x - 6, z - 6, 12, 12);
      if (display.opened) circle(x, z, 2.5, '#fff2ba', true);
    });
  }
  if (state.points) layout.points.forEach((point, i) => {
    const [x, z] = position(point);
    circle(x, z, 9, TEAMS[state.points[i].owner]?.color ?? '#cad6dd', true);
    ctx.fillStyle = '#0e1820';
    ctx.fillText(point.label, x, z + 1);
  });
  if (state.flags) state.flags.forEach((flag, i) => {
    if (flag.status === 'carried' && flag.carrier !== own.id
      && !state.players.some(pilot => pilot.id === flag.carrier && pilot.team === own.team)) return;
    const [x, z] = position(flag);
    ctx.fillStyle = TEAMS[i].color;
    ctx.fillRect(x - 6, z - 6, 12, 12);
  });
  for (const vehicle of state.vehicles) {
    if (!vehicle.hp || vehicle.driver === own.id || vehicle.team !== own.team || mode === 'ffa') continue;
    const [x, z] = position(vehicle);
    ctx.strokeStyle = vehicle.driver ? '#d0fbef' : TEAMS[own.team].color;
    const size = vehicle.kind === 'bastion' ? 10 : 7;
    ctx.strokeRect(x - size / 2, z - size / 2, size, size);
  }

  if (mode !== 'ffa') for (const player of state.players) {
    if (player.hp && player.id !== own.id && player.team === own.team && (player.vehicle ?? -1) < 0) {
      const [x, z] = position(player);
      circle(x, z, 3.1, TEAMS[own.team].color, true);
    }
  }

  const [x, z] = position(own);
  ctx.save();
  ctx.translate(x, z);
  ctx.rotate(-yaw);
  ctx.beginPath();
  ctx.moveTo(0, -9);
  ctx.lineTo(5.5, 6);
  ctx.lineTo(0, 3.8);
  ctx.lineTo(-5.5, 6);
  ctx.closePath();
  ctx.lineWidth = 1.75;
  ctx.strokeStyle = '#0b1520';
  ctx.fillStyle = '#f2fff8';
  ctx.fill();
  ctx.stroke();
  ctx.restore();
}
