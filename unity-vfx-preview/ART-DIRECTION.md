# TRIAS · Vizuální směr W3

TRIAS je vojenské sci-fi o třech frakcích bojujících kolem stejné provozní infrastruktury. Siluetu tvoří velké zkosené plochy, vyměnitelné panely a tmavé mechanické klouby. Na pancíři převažuje matná keramika a grafit, broušený kov patří na hrany, zámky a namáhané díly. Emise vyznačuje funkční části, tým a stav. Jantarový podpis značky patří rozhraní a bezpečnostnímu značení.

| Frakce | Znak | Materiál a projev |
| --- | --- | --- |
| ION | Tři šikmé segmenty | Azurová, čisté spoje, segmentovaný průběh ozvěny, rychle stoupající digitální tón při nasazení |
| EMBER | Dvojitý klín | Jantarová, průmyslové klíny, úhlový průběh ozvěny, nižší mechanický tón |
| VIOLET | Dutý kosočtverec | Fialová, uzavřené obrysy, zvlněný průběh ozvěny, sestupný syntetický tón |

Geometrie frakčního znaku je společná pro pancíř, body, HUD, nasazení a úskok. Ozvěny mají shodnou hustotu, dobu životnosti a dither. Ve FFA zůstává značení individuální. Ukazatel štítu a zásahu zachovává tvar a kontrast i při vypnutých vizuálních efektech.

| Pilot | Silueta |
| --- | --- |
| Vektor | Šikmý průzor a asymetrická závěsná výstroj |
| Bašta | Široký pancíř, zesílené nárameníky, trojice senzorů a dvojitý zádový modul |
| Přízrak | Úzký tmavý pancíř, svislý senzor a vysoká jednostranná anténa |
| Poštolka | Dělený šípový průzor, lehký hrudník a dvojice skloněných zádových modulů |

Ženský a mužský model se liší stavbou ramen, hrudníku, pasu, končetin a helmy. Obě varianty používají stejný pohybový rig, držení zbraní a serverový hitbox. Portréty v nabídce jsou vyrenderované přímo z herních modelů.

| Aréna | Vizuální kotva |
| --- | --- |
| Relay Station | Chladná orbitální infrastruktura, překladiště a jantarové energetické jádro proti galaktickému pásu |
| Vanta Foundry | Tmavá keramika, žhavý provoz, výměníky a řídká pára nad chladicí zónou |
| Meridian Basin | Teplá minerální paleta, dálkový prach, výložníky a nákladní jednokolejka pod prstenci |
| Mechový ostrov | Porost v tlumené mechové a olivové, proudnice a pěna ve vodě, meteorologická stanice nad severním okrajem |

Pět zbraní používá různé povrchy a vnější díly. Pulse má krátký nosič, Scatter dvě zesílená ústí, Lance podlouhlou optiku a plášť, Arc dvojici vodivých čelistí a Rift hranatou svěrku jádra. Všechny mechanické díly přebíjení a body kontaktu rukou sdílejí stávající animace.

## Původ a technické provedení

- Pancéřové díly, znaky, značení, portréty ze hry, panely jednotlivých stanic, textura makrovariace louky, shadery a tóny nasazení jsou původní procedurální obsah TRIAS.
- Rig, pohybové klipy a základní sítě zbraní pocházejí ze stávajících balíčků. Licence jsou v adresářích [Pilots](Assets/Resources/TRIAS/Kit/Pilots/License.txt), [Weapons](Assets/Resources/TRIAS/Kit/Weapons/License.txt), [Nature](Assets/Resources/TRIAS/Kit/Nature/License.txt) a [Space](Assets/Resources/TRIAS/Kit/Space/License.txt). Úprava povrchu zbraní používá existující paletovou texturu z balíčku Kenney Blaster Kit.
- Dekorace jsou bez kolizí. Pancéřové díly sdílejí geometrii a materiály. Voda používá jeden neprůhledný průchod bez zrcadlové kamery a při nepodporovaném shaderu přechází na standardní materiál. Prach a pára mají strop 24 částic na mapu. Posuvník **Vizuální efekty** řídí ruch prostředí a animaci vody.
- Pro synchronizaci volby modelu použij k této verzi klienta odpovídající verzi serveru. Staré pakety bez volby používají výchozí ženskou variantu.
