# TRIAS website design direction

Purpose: persuade visitors to explore the game and download an available public build. The site is an atmospheric game microsite, not a product dashboard.

Visual world: orbital deployment. Preserve the game menu's charcoal, warm white, amber and faction accents. Large condensed-feeling system display type and restrained body copy. Avoid decorative HUD clutter, repeated cards, fake metrics, stock artwork and invented gameplay renders.

Signature: layered orbital artwork with a large TRIAS wordmark; scroll brings the real battlefield and pilot lineup into view. Keep all content available without JavaScript. Scroll motion is a progressive enhancement, disabled for reduced-motion preferences and narrow screens. No scroll interception, autoplay video or mandatory animation.

Assets: orbit.jpg and eclipse.jpg are existing repository artwork. pilots.png is the current Unity visor-pilots atlas (copied from Assets/Resources/TRIAS/visor-pilots.png). The five files in public/assets/gameplay/ are authentic user-supplied gameplay captures from November 27, 2026, encoded as WebP at their original 720 × 405 resolution. The four arena views form the gallery and the Vanta Foundry ground-level capture illustrates combat. The older moss-island.webp capture is no longer displayed. Never pass concept art off as gameplay. Future character renders should be exported from the actual Unity models, ideally transparent WebP/PNG, once the W3 art pass stabilizes.

Checks: one primary action, keyboard-visible focus, honest release status and prerelease labeling, reduced motion, mobile layout, no network/build dependencies. Test at 360, 390, 768, 1280 and 1920 px and with keyboard navigation before production deployment. The static source was reviewed; Hallmark and Impeccable CLI checks and browser rendering were not available in this run.
