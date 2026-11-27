# TRIAS W6 — Interakce v aréně

W6 dává hráči důvod reagovat na letící granát, zvažovat využití ozvěny a využít odrazovou plošinu při útoku. Přepínače experimentálních pravidel patří serveru. Unity klient je přečte ze stavu zápasu a nabídne odpovídající ovládání. Zpětná vazba prostředí je součástí Unity buildu.

## Audit

| Kandidát | Rozhodnutí |
| --- | --- |
| Nepřátelský granát v dráze střely | Sestřelení zbraní s okamžitým zásahem dává hráči možnost reagovat. |
| Úskok a návrat k ozvěně | Hráč volí mezi výstřelem z uložené pozice a samotným návratem. |
| Štít proti výbuchu | Absorbující štít udrží pilota blíž k pozici, kterou brání. |
| Plošina a útok za letu | Potvrzený zásah krátce po odrazu se započítá do Momentum Chain. |
| Voda a plošiny pod palbou | Lokální vlny a krátké světelné pulzy navazují na zásah nebo explozi. |
| Odhoz spoluhráče a pěšáků pod koly | Odloženo kvůli nechtěnému narušování hry spoluhráčů a předvídatelnosti kolizí. |
| Zničení kulis a posun staveb | Odloženo. Změny krytu by vyžadovaly úpravy navigace botů a pravidel obléhání. |

## Pravidla a playtest

| Interakce | Spouštěč, odezva a protihráč | Autorita a režimy | Scénář pro playtest |
| --- | --- | --- | --- |
| Sestřel granátu | Střela pěšáka zasáhne nepřátelský granát za letu a odpálí ho na místě. Granát krytý zdí nebo zasažitelným cílem před ním střela mine. Poslední desetina sekundy pojistky je vyhrazena běžnému výbuchu. Hod z krytu nebo mimo přímou linii snižuje šanci na sestřel. | `TRIAS_GRENADE_INTERCEPT=1`. Server vyhodnotí aktuální polohu granátu, odstraní jej jednou i po zásahu více broky a výbuch připíše původnímu vrhači. V FFA jsou soupeři všichni ostatní. V týmové hře a Hordě platí strany zápasu. | V Game Feel Labu namiř do volné chodby, stiskni **F10**, sleduj oblouk granátu a sestřel ho Pulse. Zopakuj za letu nebo po úskoku v dosahu 12 m a sleduj Momentum. Ve dvou hráčích porovnej nepřátelský a vlastní granát. |
| Návrat k ozvěně | Po úskoku **Q** stiskni **Z** po dopadu pro návrat. **X** použije stejnou ozvěnu ke střelbě. Návrat vyžaduje obě pozice na zemi, volnou cestu v rozmezí 0,55–10 m a rozdíl výšek do 0,5 m. Soupeř může ozvěnu zničit, obsadit její pozici nebo počkat na zvuk návratu. | `TRIAS_ECHO_RECALL=1` a zapnutá Combat Echo. Server kontroluje okraje, překážky, obsazení cíle a zákaz návratu na plošinu. Z návratu jsou vyloučené vlajky, pásma obsazování, jádra, vozidla a věže. Po schválení server spotřebuje jednu ozvěnu, vynuluje rychlost a odloží střelbu nejméně o 0,20 s. | Na Relay se vrať po úskoku v otevřené chodbě. Zkus to znovu před doskokem, za rohem, s vozidlem na kotvě nebo s vlajkou. Po zamítnutí zkus stále živou ozvěnu použít přes **X**. |
| Stabilizace štítem | Pilota s aktivním štítem odhodí granát a Rift méně. Štítová barva indikátoru a krátké oznámení potvrdí účinek. Soupeř může nejdřív stáhnout štít a až pak použít výbuch. | `TRIAS_SHIELD_BRACE=1`. Server měří stav štítu před zásahem a počítá stejné poškození. U pilota se štítem je vodorovný přírůstek odhozu 48 % běžné hodnoty, spodní hranice svislého impulsu je 1,25 místo 2,8 násobku síly výbuchu. Vodorovný strop odhozu zachová už nabranou vyšší rychlost. Platí běžná ochrana před palbou spoluhráčů. | Postav se se štítem a poté bez něj stejně daleko od exploze na Relay. Porovnej posun a ztrátu života se stejným počátečním stavem. Sestřel granát poblíž třetího pilota se štítem a zkontroluj jeho odezvu. |
| Zásah po odrazu | Do 1,55 s po odpalu plošinou zasáhni soupeře při vodorovné rychlosti alespoň 5 m/s. Soupeř může zmizet za krytem nebo se vyhnout předvídatelné letové dráze. | `TRIAS_TRAVERSAL_STYLE=1` a zapnuté Momentum. Server musí potvrdit škodu do života nebo štítu. Platí jeden zápis za vzlet a stávající omezení opakování cílů. Řetězec je prezentační odměna a nedává herní výhodu. | Odraz se na Relay nebo ve Vanta Foundry a tref soupeře ve vzduchu. Vyzkoušej nezasaženou střelu, zásah až z podlahy a dvě trefy během téhož vzletu. |
| Odezva prostředí | Střela do plošiny rozsvítí její obrys. Výbuch poblíž plošiny vyvolá stejný pulz. Zásah mělčiny a výbuch u hladiny Kordilery pošlou od místa zásahu kruh po vodě. | Unity vykresluje efekty z již přijatých potvrzených zásahů a explozí. Používá sdílené částice a kruhy, vzdálenost do 55 m a časový limit mezi pulzy. Intenzitu určuje nastavení efektů. | Střílej do hrany plošiny na Relay a nech vedle ní vybuchnout granát. V Kordileře střel do vody a do blízkého břehu. Zopakuj se staženým nastavením efektů. |

Botům slouží stejná pravidla zásahu a štítu. Sestřel může vzniknout při jejich běžné palbě. Combat Echo používaná boty dál slouží ke střelbě. Sestřel sám nepřidává vyřazení, sérii ani body do výsledku. Běžné body za případné vyřazení výbuchem dostane vrhač. Návrat nemění munici, výzbroj, život ani náboje dvojskoku a úskoku.

## Spuštění

Veřejný server spouští experimenty jen pro přepínače nastavené na `1`. Z kořene repozitáře zapneš všechny čtyři pro lokální test takto.

```sh
HOST=127.0.0.1 TRIAS_GRENADE_INTERCEPT=1 TRIAS_ECHO_RECALL=1 \
TRIAS_SHIELD_BRACE=1 TRIAS_TRAVERSAL_STYLE=1 npm start
```

Pro Game Feel Lab nahraď `npm start` příkazem `npm run lab:feel` a v Unity vyber **TRIAS → Play Game Feel Lab**. **F10** v laboratoři vypustí tréninkový granát do prostoru před mířidly. **F6** přidá simulovaných 160 ms RTT pro kontrolu potvrzení a korekcí. Laboratoř poslouchá pouze na lokální smyčce.

Regresní testy jsou v `test/interactions.test.js`. Ověřují i chování s vypnutými pravidly, vlastnictví exploze, opakované požadavky, kombinaci sestřelu a štítu a zápis zásahu po odrazu. Spustíš je společně se zbytkem hry pomocí `npm test`.
