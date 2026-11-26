(() => {
  "use strict";
  const repo = "iLoopak/trias-fps";
  const title = document.getElementById("release-title");
  const status = document.getElementById("release-status");
  const list = document.getElementById("release-assets");
  const notes = document.getElementById("release-notes");
  const allowed = /\.(zip|7z|tar\.gz|tgz|exe|msi|dmg|pkg|appimage|deb|rpm|apk)$/i;
  function safeLink(raw, prefix) {
    try { const u = new URL(raw); return u.origin === "https://github.com" && u.pathname.startsWith("/" + repo + "/releases/" + prefix) ? u.href : null; } catch { return null; }
  }
  function size(bytes) {
    if (!Number.isFinite(bytes) || bytes < 0) return "";
    if (bytes < 1024) return bytes + " B";
    if (bytes < 1048576) return (bytes / 1024).toFixed(1) + " KB";
    if (bytes < 1073741824) return (bytes / 1048576).toFixed(1) + " MB";
    return (bytes / 1073741824).toFixed(2) + " GB";
  }
  function assets(release) {
    return (Array.isArray(release.assets) ? release.assets : []).filter(a => a.state === "uploaded" && typeof a.name === "string" && allowed.test(a.name) && safeLink(a.browser_download_url, "download/"));
  }
  async function load() {
    status.textContent = "Checking for published builds…";
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 8000);
    try {
      const response = await fetch("https://api.github.com/repos/" + repo + "/releases?per_page=30", { headers: { Accept: "application/vnd.github+json" }, signal: controller.signal });
      if (!response.ok) throw new Error("Release lookup failed");
      const releases = await response.json();
      if (!Array.isArray(releases)) throw new Error("Unexpected release data");
      const available = releases.filter(r => !r.draft && assets(r).length).sort((a,b) => Date.parse(b.published_at || b.created_at) - Date.parse(a.published_at || a.created_at));
      const release = available[0];
      if (!release) { title.textContent = "In development"; status.textContent = "No downloadable game packages found yet. Follow development or check the release archive."; return; }
      title.textContent = release.name || release.tag_name || "Latest build";
      const published = new Date(release.published_at);
      status.textContent = [release.prerelease ? "Pre-release / experimental" : "Public release", release.tag_name, Number.isFinite(published.getTime()) ? published.toLocaleDateString("en", {dateStyle:"medium"}) : ""].filter(Boolean).join(" · ");
      const link = safeLink(release.html_url, "tag/");
      if (link) { notes.href = link; notes.textContent = "Release notes ↗"; }
      for (const asset of assets(release)) {
        const li = document.createElement("li"), a = document.createElement("a"), body = document.createElement("span"), name = document.createElement("strong"), meta = document.createElement("small"), arrow = document.createElement("span");
        a.className = "asset"; a.href = safeLink(asset.browser_download_url, "download/");
        name.textContent = asset.label || asset.name; meta.textContent = [asset.label ? asset.name : "", size(asset.size)].filter(Boolean).join(" · ");
        arrow.textContent = "↓"; arrow.setAttribute("aria-hidden", "true");
        body.append(name, meta); a.append(body, arrow); li.append(a); list.append(li);
      }
      list.hidden = false;
      const archive = document.createElement("a"); archive.href = "https://github.com/" + repo + "/releases"; archive.className = "button secondary"; archive.textContent = "All releases ↗"; notes.after(" ", archive);
    } catch { status.textContent = "Automatic release lookup is temporarily unavailable. Check GitHub Releases for available builds."; }
    finally { clearTimeout(timeout); }
  }
  load();
})();