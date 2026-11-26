# TRIAS přes internet

Hráči potřebují klienta TRIAS pro svůj systém. Jeden host provozuje Node.js server odpovídající verze. Windows klient se spouští z rozbalené složky souborem `TRIAS.exe`. Složku `TRIAS_Data` a knihovny ponech vedle něj.

## 1. Ověř server lokálně

Nainstaluj Node.js v podporované LTS verzi alespoň 22. V kořeni repozitáře nebo rozbaleného serverového balíčku spusť:

```sh
npm ci --omit=dev
TRIAS_NATIVE_ONLY=1 HOST=127.0.0.1 PORT=3000 npm start
```

Varianta pro PowerShell:

```powershell
npm ci --omit=dev
$env:TRIAS_NATIVE_ONLY = '1'
$env:HOST = '127.0.0.1'
$env:PORT = '3000'
npm start
```

Na stroji se serverem otevři v klientu adresu `ws://127.0.0.1:3000`. Zadej přezdívku a kód místnosti. Server lze také ověřit na `http://127.0.0.1:3000/health`. [Node.js LTS](https://nodejs.org/en/download)

## 2. Zpřístupni server přes vlastní doménu

1. Na Linux VPS spusť server z kroku 1. Pro stálý provoz ho nech běžet jako službu pod vlastním uživatelem.
2. Nasměruj DNS záznam domény, třeba `trias.example.com`, na veřejnou IP VPS. Zpřístupni příchozí TCP 80 a 443. Node port 3000 zůstane na lokální smyčce.
3. Nainstaluj Caddy a do jeho `Caddyfile` vlož následující konfiguraci se svou skutečnou doménou:

   ```caddyfile
   trias.example.com {
       reverse_proxy 127.0.0.1:3000
   }
   ```

4. Načti konfiguraci Caddy a z jiného připojení ověř `https://trias.example.com/health`. Caddy obslouží certifikát a předá WebSocket na server. [Automatické HTTPS](https://caddyserver.com/docs/automatic-https) · [Reverse proxy](https://caddyserver.com/docs/caddyfile/directives/reverse_proxy)
5. Hráčům pošli adresu `wss://trias.example.com` a stejný kód místnosti. Klient cestu `/play` doplní sám. Arénu, režim a doplnění boty určí první připojený hráč. Pro soukromou partii zvol náhodný kód a sdílej ho jen s hráči. Kód funguje jako pozvánka do místnosti.

Pokud se `/health` načte a klient se nepřipojí, ověř v proxy předávání WebSocket na `/play` a verzi serveru. Pokud se hráči nepotkají, porovnej adresu a kód jejich místnosti. Game Feel Lab běží samostatně na lokální smyčce a pro internetové zápasy ho spouštět nemusíš.
