#!/usr/bin/env node
// Generate TRIAS sound effects with the ElevenLabs Sound Effects API from tools/audio/sfx-manifest.json.
//
//   ELEVENLABS_API_KEY=... node tools/audio/generate-sfx.mjs [--force] [id-prefix ...]
//
// The key is read only from the environment and never written anywhere. Raw MP3s go to the ignored
// output/audio-revamp/raw; processed clips go to unity-vfx-preview/Assets/Resources/TRIAS/Audio/Sfx/<id>.wav (ambience .ogg).
// One-shots are mono, start-trimmed (fire response), faded and peak-normalised to -1 dBFS;
// ambience loops stay stereo and are loudness-normalised to -24 LUFS.
import { execFileSync } from "node:child_process";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
const manifest = JSON.parse(readFileSync(join(root, "tools/audio/sfx-manifest.json"), "utf8"));
const raw = join(root, "output/audio-revamp/raw");
const out = join(root, "unity-vfx-preview/Assets/Resources/TRIAS/Audio/Sfx");
const args = process.argv.slice(2);
const force = args.includes("--force");
const only = args.filter((a) => !a.startsWith("--"));
const key = process.env.ELEVENLABS_API_KEY;
if (!key) throw new Error("ELEVENLABS_API_KEY is not set");
mkdirSync(raw, { recursive: true });
mkdirSync(out, { recursive: true });

function ffmpeg(...a) {
  return execFileSync("ffmpeg", ["-hide_banner", "-loglevel", "error", "-y", ...a], { encoding: "utf8" });
}

async function generate(id, sound) {
  const ambience = sound.mono === false;
  // One-shots are stored as 16-bit WAV (Unity compresses on import); ambience loops as stereo Ogg Vorbis.
  const target = join(out, id + (ambience ? ".ogg" : ".wav"));
  if (existsSync(target) && !force) return "kept";
  const body = {
    text: `${sound.p}, ${manifest.style}`,
    duration_seconds: sound.d,
    prompt_influence: sound.influence ?? 0.55,
    model_id: "eleven_text_to_sound_v2",
  };
  if (sound.loop) body.loop = true;
  let response;
  for (let attempt = 0; attempt < 3; attempt++) {
    response = await fetch("https://api.elevenlabs.io/v1/sound-generation?output_format=mp3_44100_192", {
      method: "POST", headers: { "xi-api-key": key, "Content-Type": "application/json" }, body: JSON.stringify(body),
    });
    if (response.ok) break;
    if (response.status !== 429 && response.status < 500) break;
    await new Promise((r) => setTimeout(r, 3000 * (attempt + 1)));
  }
  if (!response.ok) return `failed ${response.status} ${(await response.text()).slice(0, 160)}`;
  const mp3 = join(raw, id + ".mp3");
  writeFileSync(mp3, Buffer.from(await response.arrayBuffer()));
  if (ambience) {
    ffmpeg("-i", mp3, "-af", "loudnorm=I=-24:TP=-2:LRA=11", "-ar", "44100", "-ac", "2", "-c:a", "vorbis", "-strict", "-2", "-b:a", "160k", target);
  } else {
    // Trim leading silence so shots start on the trigger frame, fade the tail, peak-normalise.
    const shaped = join(raw, id + ".shaped.wav");
    ffmpeg("-i", mp3, "-ac", "1", "-af",
      `silenceremove=start_periods=1:start_threshold=-48dB:start_silence=0.004,afade=t=out:st=${Math.max(0.05, sound.d - 0.06)}:d=0.06`,
      "-ar", "44100", shaped);
    const stats = execFileSync("sh", ["-c", `ffmpeg -hide_banner -i "${shaped}" -af volumedetect -f null - 2>&1 | grep max_volume`],
      { encoding: "utf8" });
    const max = parseFloat(stats.split("max_volume:")[1]);
    const gain = Number.isFinite(max) ? -1 - max : 0;
    ffmpeg("-i", shaped, "-af", `volume=${gain.toFixed(2)}dB`, "-c:a", "pcm_s16le", target);
  }
  return "generated";
}

const entries = Object.entries(manifest.sounds).filter(([id]) => only.length === 0 || only.some((p) => id.startsWith(p)));
let failures = 0;
for (const [id, sound] of entries) {
  const result = await generate(id, sound);
  if (result.startsWith("failed")) failures++;
  console.log(`${id.padEnd(16)} ${result}`);
}
console.log(`SFX DONE ${entries.length} entries, ${failures} failed`);
process.exit(failures ? 1 : 0);
