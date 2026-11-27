/* Progressive enhancement: scroll-linked transforms only. No scroll interception or content hiding. */
(() => {
  "use strict";
  const motion = matchMedia("(prefers-reduced-motion: reduce)");
  const small = matchMedia("(max-width: 600px)");
  const layers = [...document.querySelectorAll("[data-parallax]")].map(el => ({
    el, scene: el.closest("section"), speed: Number(el.dataset.parallax)
  })).filter(item => item.scene && Number.isFinite(item.speed));
  let frame = 0, running = false;
  function update() {
    frame = 0;
    if (!running || document.hidden) return;
    const h = window.innerHeight;
    // Read geometry first, then batch transforms to avoid layout thrashing.
    const values = layers.map(({scene,speed}) => {
      const rect = scene.getBoundingClientRect();
      if (rect.bottom < -160 || rect.top > h + 160) return null;
      const distance = h / 2 - (rect.top + rect.height / 2);
      return Math.max(-125,Math.min(125,distance * speed));
    });
    layers.forEach(({el}, i) => {
      if (values[i] !== null) el.style.setProperty("--parallax-y", values[i].toFixed(1) + "px");
    });
  }
  function schedule() { if (running && !frame && !document.hidden) frame = requestAnimationFrame(update); }
  function sync() {
    running = !motion.matches && !small.matches;
    if (!running) {
      if (frame) cancelAnimationFrame(frame);
      frame = 0;
      layers.forEach(({el}) => el.style.removeProperty("--parallax-y"));
    } else schedule();
  }
  addEventListener("scroll", schedule, {passive:true});
  addEventListener("resize", schedule, {passive:true});
  document.addEventListener("visibilitychange", schedule);
  motion.addEventListener("change",sync);
  small.addEventListener("change",sync);
  sync();
})();
