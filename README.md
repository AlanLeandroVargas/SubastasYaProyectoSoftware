# SubastaYa

Plataforma web de subastas en tiempo real con billetera virtual, garantías (*escrow*),
regla anti-sniping y adjudicación automática por proceso en segundo plano.

Trabajo práctico de la cátedra **Proyecto de Software** — Ingeniería en Informática.

| | |
|---|---|
| **Backend** | C# / .NET 9 · ASP.NET Core Web API |
| **Base de datos** | SQL Server · Entity Framework Core 9 (Code-First) |
| **Frontend** | HTML5 + CSS + JavaScript (ES Modules) + Bootstrap 5 |
| **Documentación** | OpenAPI / Swagger UI autogenerada |

> **Convención de idioma.** Los **identificadores están en inglés**: nombres de archivos,
> carpetas, namespaces, clases, métodos, variables, tablas y columnas de la base de datos, y el
> contrato JSON de la API. Todo lo que se lee en castellano está en **español**: los
> comentarios y la documentación XML del código, los textos de la interfaz, los mensajes de
> error que se muestran en pantalla y esta documentación.

---

## Arquitectura

Arquitectura en capas con **regla de dependencia hacia adentro**: cada capa conoce sólo a la
que tiene por debajo y el dominio no conoce a ninguna.

```
┌─────────────────────────────────────────────────────────┐
│  SubastaYa.Api          Controladores REST, composición │
│  (Presentación)         de dependencias, frontend       │
└───────────────┬─────────────────────────┬───────────────┘
                │                         │
                ▼                         ▼
┌─────────────────────────────┐  ┌──────────────────────────────┐
│  SubastaYa.Application      │  │  SubastaYa.Infrastructure    │
│  Casos de uso y puertos     │◄─┤  Implementación de puertos   │
└───────────────┬─────────────┘  └──────────────────────────────┘
                │
                ▼
┌─────────────────────────────────────────────────────────┐
│  SubastaYa.Domain      Entidades con comportamiento,    │
│  (sin dependencias)    invariantes y reglas de negocio  │
└─────────────────────────────────────────────────────────┘
```

| Capa | Responsabilidad | Qué **no** hace |
|---|---|---|
| **Domain** | Invariantes del negocio: validación de pujas, incremento mínimo, anti-sniping, movimientos de saldo, transiciones de estado. | No conoce EF Core, HTTP ni JSON. |
| **Application** | Orquesta casos de uso, define los puertos (repositorios, unidad de trabajo, notificador) y delimita las transacciones. | No instancia `DbContext` ni construye respuestas HTTP. |
| **Infrastructure** | Implementa los puertos: persistencia, seguridad, reloj del sistema. | No contiene reglas de negocio. |
| **Api** | Traduce HTTP ↔ casos de uso y hospeda el frontend. | No valida reglas de negocio: sólo delega. |

**La inversión de dependencias es literal**: `Application` declara las interfaces e
`Infrastructure` las implementa, por lo que la flecha de compilación va de la infraestructura
hacia la aplicación y no al revés.

---

## Modelo de dominio

El dominio es **rico, no anémico**: las entidades deciden qué es válido y no se limitan a
guardar datos. Un servicio nuevo no puede saltearse una invariante porque no tiene forma de
modificar el estado por fuera de estos métodos.

| Entidad | Responsabilidad |
|---|---|
| `Auction` | Admisión de pujas, cálculo del próximo monto válido, extensión anti-sniping y transiciones de estado. |
| `Wallet` | Único punto por donde se mueve dinero: acreditar, congelar, liberar y liquidar garantías. |
| `Bid` | Oferta inmutable: una vez creada forma parte del historial y no se modifica. |
| `User` | Participante; nace siempre con su billetera asociada. |
| `Category` | Clasificación temática del catálogo. |
| `LedgerEntry` | Asiento inmutable del libro mayor: todo movimiento de saldo deja su huella. |
| `AuditRecord` | Traza inmutable de los eventos críticos, incluidos los intentos rechazados. |

### Reglas implementadas

* **Incremento mínimo**: cada oferta debe alcanzar `oferta líder + incremento mínimo`; la primera
  puja debe alcanzar el precio base.
* **Anti-sniping**: una puja dentro de los últimos **60 segundos** desplaza el cierre **2 minutos**.
  Los parámetros viven en `AuctionRules`, no dispersos por el código.
* **Elegibilidad**: un vendedor no puede ofertar en su propia subasta y el postor líder no puede
  volver a pujar contra sí mismo.
* **Ventana temporal**: el cierre debe ser posterior al inicio, la subasta debe durar al menos un
  minuto y no puede publicarse una subasta que ya venció.
* **Garantías (escrow)**: `AvailableBalance = TotalBalance - HeldBalance`. No se puede congelar
  más de lo disponible ni liberar más de lo retenido.
* **Trazabilidad contable**: todo movimiento de saldo deja un asiento, de modo que el saldo
  siempre pueda reconstruirse sumando el historial. Los asientos son inmutables: una
  corrección se expresa con un asiento nuevo, nunca editando el anterior.

Los estados de la subasta son `Scheduled`, `Active`, `Completed` y `Unsold`. El dominio **no mueve
dinero por su cuenta**: `PlaceBid` devuelve un `BidPlacementResult` con el postor superado y el
monto a liberar, para que el caso de uso lo resuelva dentro de una transacción atómica.

### Preparación para la concurrencia

`Auction` y `Wallet` exponen una propiedad `Version` que la capa de persistencia mapea como
`rowversion`. Es el mecanismo de **bloqueo optimista** exigido por la consigna. Se modeló desde el
dominio antes de que existiera la base de datos, y hoy protege tanto los movimientos de saldo
como el registro de pujas.

---

## Pruebas

```bash
dotnet test
```

43 pruebas unitarias cubren las invariantes del negocio sin tocar infraestructura: incremento
mínimo, ventana anti-sniping (45 s extiende, 60 s extiende, 61 s no), puja del vendedor en su
propia subasta, puja del postor que ya lidera, subasta programada o vencida, transiciones a
`Completed` / `Unsold`, las reglas de la billetera (retención, liberación, liquidación y
traspaso de liderazgo entre postores) y el libro mayor, incluida la prueba de que los asientos
reconstruyen exactamente el saldo de la billetera.

Se comprueba además que `EnsureBidIsAdmissible` **no mutile el agregado**: el caso de uso valida
antes de tocar las billeteras, así que esa comprobación tiene que poder fallar sin dejar la
subasta a medio modificar, y que la liquidación final mueva exactamente el mismo importe fuera
del comprador y dentro del vendedor.

---

## API REST

Las rutas se apoyan en **sustantivos en plural y jerarquías de recursos**; no hay verbos en las
URLs. Base: `/api/v1`.

| Método | Ruta | Auth | Propósito |
|---|---|:---:|---|
| `GET` | `/health` | — | Sonda de disponibilidad |
| `POST` | `/sessions` | — | Abrir sesión y obtener el token JWT |
| `GET` | `/sessions/current` | ✔ | Perfil del usuario autenticado |
| `GET` | `/categories` | — | Listado de categorías |
| `GET` | `/auctions` | — | Catálogo con filtros, orden y paginación |
| `POST` | `/auctions` | ✔ | Publicar una subasta |
| `GET` | `/auctions/{id}` | — | Detalle de la subasta con su historial de ofertas |
| `GET` | `/auctions/{id}/bids` | — | Historial de ofertas, con los postores seudonimizados |
| `POST` | `/auctions/{id}/bids` | ✔ | Registrar una oferta |
| `GET` | `/wallets/me` | ✔ | Saldo total, retenido y disponible |
| `POST` | `/wallets/me/deposits` | ✔ | Acreditar fondos simulados |
| `GET` | `/wallets/me/transactions` | ✔ | Historial de movimientos |
| `GET` | `/audit-logs` | ✔ | Traza de auditoría, de sólo lectura |
| `WS` | `/hubs/auctions` | — | Canal SignalR de la sala en vivo |

**Filtros de `GET /auctions`**: `status` (`Active` · `Scheduled` · `Completed` · `Unsold`),
`categoryId`, `minPrice`, `maxPrice`, `search`, `sort` (`EndingSoonest` · `HighestBid` ·
`Newest`), `page`, `pageSize`.

```bash
curl -s "http://localhost:5080/api/v1/auctions?status=Active&sort=HighestBid"
curl -s "http://localhost:5080/api/v1/auctions/1"
```

Los errores se devuelven como `ProblemDetails` (RFC 7807), con el mensaje ya en español porque
se muestra tal cual en pantalla:

```json
{
  "title": "Recurso inexistente",
  "status": 404,
  "detail": "No se encontró la subasta con identificador '999'.",
  "instance": "/api/v1/auctions/999"
}
```

`GlobalExceptionMiddleware` es el único lugar donde se traduce una excepción de dominio a un
código HTTP, de modo que un error de negocio nunca se degrada en un 500 genérico.

---

## Autenticación

Autenticación **stateless con JWT**: la API no guarda sesiones en memoria, así que puede
escalarse horizontalmente sin sesiones pegajosas.

```bash
TOKEN=$(curl -s -X POST http://localhost:5080/api/v1/sessions   -H "Content-Type: application/json"   -d '{"email":"comprador1@test.com","password":"Password123!"}'   | python -c "import sys,json;print(json.load(sys.stdin)['token'])")

curl -s http://localhost:5080/api/v1/sessions/current -H "Authorization: Bearer $TOKEN"
```

En Swagger UI alcanza con el botón **Authorize** y pegar el token.

### Decisiones

* **Contraseñas con PBKDF2-SHA256**: 120.000 iteraciones, sal aleatoria por usuario y comparación
  en tiempo constante, sin dependencias externas. El formato almacenado
  (`iteraciones.sal.hash`) es autocontenido, de modo que se puede subir el costo de derivación
  más adelante sin invalidar los hashes existentes.
* **Respuesta uniforme ante el fallo**: usuario inexistente y contraseña incorrecta devuelven
  exactamente el mismo `401`, para no revelar qué direcciones están registradas.
* **`ClockSkew = TimeSpan.Zero`**: sin la tolerancia de 5 minutos que trae ASP.NET por defecto, la
  expiración de la sesión es exacta.
* **`ICurrentUser`** vive en la capa de aplicación y lo implementa la de presentación leyendo las
  afirmaciones del token. Los casos de uso conocen al solicitante sin tocar `HttpContext`.
* **El catálogo sigue siendo público**: sólo se protege lo que requiere identidad.

> El registro de usuarios no está expuesto por API: la consigna define un conjunto fijo de
> cuentas de prueba y todas se crean en la semilla.

### Cuentas de prueba

Todas usan la contraseña **`Password123!`**.

| Email | Rol en la demo |
|---|---|
| `vendedor@test.com` | Publica las subastas del catálogo |
| `comprador1@test.com` | Postor líder de la subasta activa |
| `comprador2@test.com` | Ganador pendiente de liquidación |
| `sinfondos@test.com` | Servirá para probar el rechazo por saldo |

> Si ya tenías la base creada de antes, hay que **regenerarla**. La semilla sólo corre sobre una
> base vacía, así que un esquema viejo se queda sin el hash de las contraseñas y sin los asientos
> del libro mayor:
> ```bash
> dotnet ef database drop --force --project src/SubastaYa.Infrastructure --startup-project src/SubastaYa.Api
> ```

---

## Billetera y libro mayor

Cada usuario tiene una billetera con tres cifras: **total**, **retenido** y **disponible**. El
disponible es siempre `total - retenido` y **no se persiste**: es lo único que puede gastarse.

Toda alteración del saldo deja un **asiento** en el libro mayor. La consecuencia práctica es que
el saldo nunca es un número sin explicación: sumando el historial se lo reconstruye entero, y
cualquier diferencia delata un error.

| Tipo de asiento | Cuándo se registra |
|---|---|
| `Deposit` | Acreditación manual de fondos simulados |
| `Hold` | El usuario pasa a liderar una subasta y su garantía queda congelada |
| `Release` | El usuario es superado y su garantía vuelve al disponible |
| `Payment` | Débito final al comprador cuando se le adjudica la subasta |
| `Payout` | Acreditación final al vendedor por la venta |

### Decisiones

* **`me` en lugar del identificador**: no hay ninguna ruta que acepte el identificador de una
  billetera ajena, así que consultar el saldo de otro no es una cuestión de permisos sino de
  rutas que no existen.
* **Saldo y asiento en un único guardado**: la unidad de trabajo vuelca ambos cambios en una sola
  operación atómica. No hay forma de que el dinero entre sin dejar rastro ni de que quede un
  asiento sin respaldo, ni siquiera si el proceso muere en el medio.
* **Importes siempre positivos**: el signo lo aporta el tipo de asiento, no el número. Eso permite
  que la base exija `Amount > 0` para todo movimiento.
* **Tope por operación**: `AuctionRules.MaximumDepositPerOperation` ($10.000.000) es una política
  de la plataforma, por eso vive junto al resto de los parámetros de negocio y no dentro de
  `Wallet`, que sólo conoce la invariante de que el monto debe ser positivo.
* **`IUnitOfWork` expone hoy sólo `SaveChangesAsync`**: un único guardado de EF Core ya es
  atómico, así que abrir una transacción explícita acá sería ceremonia sin efecto. La transacción
  explícita aparecerá cuando el registro de pujas necesite tocar dos billeteras y una subasta.

### Conflictos de concurrencia

`Wallets.Version` es un `rowversion`, así que dos movimientos simultáneos sobre la misma billetera
no pueden pisarse: el más lento falla y la API responde **409**. La unidad de trabajo traduce el
`DbUpdateConcurrencyException` de EF Core a una excepción del dominio, de modo que la capa de
aplicación reacciona al conflicto sin conocer el ORM.

Verificado con 24 depósitos disparados en el mismo instante sobre una misma billetera: 7
aceptados, 17 rechazados con 409, y el saldo final exactamente igual a la suma de los aceptados.
Sin el bloqueo optimista habría actualizaciones perdidas.

```bash
TOKEN=$(curl -s -X POST http://localhost:5080/api/v1/sessions   -H "Content-Type: application/json"   -d '{"email":"comprador1@test.com","password":"Password123!"}'   | python -c "import sys,json;print(json.load(sys.stdin)['token'])")

curl -s http://localhost:5080/api/v1/wallets/me -H "Authorization: Bearer $TOKEN"
curl -s -X POST http://localhost:5080/api/v1/wallets/me/deposits   -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json"   -d '{"amount":50000}'
curl -s "http://localhost:5080/api/v1/wallets/me/transactions?count=20"   -H "Authorization: Bearer $TOKEN"
```

---

## Registro de pujas

Es el caso de uso central. Una oferta aceptada tiene que producir, **todo o nada**, cinco efectos:

1. congelar la garantía del nuevo líder,
2. liberar la del postor desplazado,
3. registrar la puja y actualizar el importe líder,
4. correr el cierre si la oferta entró en la ventana anti-sniping,
5. dejar constancia en el libro mayor y en la auditoría.

Si cualquiera falla, se revierte el bloque completo. No puede quedar dinero congelado respaldando
una oferta que nunca se registró, ni una puja sin su garantía detrás.

### Orden de validación

El agregado valida **antes** de que se toque una sola billetera: estado de la subasta, luego
elegibilidad del postor, luego monto contra el incremento mínimo, y recién entonces el saldo. Una
oferta inadmisible no llega a mover dinero ni siquiera dentro de una transacción que después se
revertiría.

| Situación | Respuesta |
|---|:---:|
| Oferta aceptada | `201` |
| Monto por debajo del incremento mínimo, o menor o igual a cero | `400` |
| Sin sesión | `401` |
| El vendedor puja en su propia subasta | `403` |
| La subasta no existe | `404` |
| Subasta programada, vencida, o el líder puja contra sí mismo | `409` |
| Conflicto de concurrencia | `409` |
| Saldo disponible insuficiente | `422` |

### Auditoría

`AuditRecord` complementa al libro mayor: éste sólo conoce movimientos de dinero, mientras que la
auditoría también guarda los intentos **rechazados**, que son los que explican por qué una puja no
prosperó. El detalle viaja como JSON sin esquema fijo porque cada acción necesita datos distintos,
y forzar columnas dejaría la tabla llena de nulos.

El registro del intento fallido se escribe **después** de la reversión y en una transacción propia:
tiene que sobrevivir justamente a la transacción que se revirtió. Si esa escritura falla, se deja
traza en el log y se propaga el error de negocio original, porque una falla al auditar no debe
reemplazar al mensaje que el usuario necesita ver.

La auditoría cubre rechazos **de negocio**. Un monto menor o igual a cero se descarta antes de
llegar al dominio y no se audita: es una petición mal formada, no una decisión del negocio.

> A diferencia de las dos funcionalidades anteriores, ésta **no exige regenerar la base**: la
> migración sólo agrega la tabla `AuditLog` y los datos semilla no cambiaron. Verificado revirtiendo
> la migración sobre una base ya poblada y volviéndola a aplicar: las 5 subastas, los 4 usuarios y
> sus asientos quedaron intactos y la auditoría arrancó vacía, que es lo correcto.

### Difusión en tiempo real

`IAuctionNotifier` es el puerto hacia la sala en vivo. La capa de aplicación publica el evento sin
saber qué hay por debajo; el transporte concreto se describe en su propia sección más abajo.

La difusión ocurre **fuera** de la transacción: anunciar una puja antes de confirmarla podría
publicar una oferta que después se revierte.

### Concurrencia verificada

Con 48 ofertas simultáneas repartidas en 8 rondas, dos compradores ofertando el mismo monto en el
mismo instante:

```
por ronda:  1 aceptada   ·   3 rechazos por estado   ·   2 conflictos de concurrencia
```

Y al terminar, las invariantes se sostienen exactamente:

* el libro mayor de cada billetera reconstruye su saldo al centavo;
* por subasta, la garantía viva (`HOLD` menos `RELEASE`) es **igual al importe líder**: sólo el
  líder tiene fondos congelados, nunca dos postores a la vez;
* la cantidad de ofertas nuevas coincide con la cantidad de respuestas `201`.

```bash
TOKEN=$(curl -s -X POST http://localhost:5080/api/v1/sessions   -H "Content-Type: application/json"   -d '{"email":"comprador2@test.com","password":"Password123!"}'   | python -c "import sys,json;print(json.load(sys.stdin)['token'])")

curl -s -X POST http://localhost:5080/api/v1/auctions/1/bids   -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json"   -d '{"amount":50000}'

curl -s "http://localhost:5080/api/v1/audit-logs?count=10" -H "Authorization: Bearer $TOKEN"
```

---

## Publicación y cierre automático

### Publicar

`POST /auctions` crea una subasta a nombre del usuario autenticado. Si la fecha de inicio ya pasó
nace **activa**; si es futura queda **programada** y la activa el proceso en segundo plano cuando
llega el momento.

El servicio sólo orquesta: la coherencia económica y temporal la valida la propia entidad, de modo
que no haya una segunda definición de las reglas conviviendo con la del dominio. La única
comprobación que queda en el servicio es la existencia de la categoría, porque una subasta no puede
conocer el catálogo entero y dejar que falle la clave foránea daría un `500` en lugar de un `400`.

Las fechas se normalizan a UTC antes de llegar al dominio: un `DateTime` sin zona se interpretaría
como hora local y correría la subasta varias horas.

### El proceso en segundo plano

Cada pasada hace dos cosas: activa las subastas programadas que ya comenzaron y resuelve las
vencidas, adjudicándolas o declarándolas desiertas.

| Situación | Resultado |
|---|---|
| Programada cuyo inicio llegó | pasa a `Active` |
| Vencida con ofertas | `Completed` + liquidación final |
| Vencida sin ofertas | `Unsold`, sin mover dinero |
| Vencida pero extendida por anti-sniping | se deja correr, se reintenta después |

**La liquidación** saca la garantía de la billetera del ganador y la acredita al vendedor, dejando
los dos asientos (`Payment` y `Payout`) y el registro de auditoría en la misma transacción. El
comprador usa `SettleWithHold` y no un débito común: el dinero ya estaba congelado desde que pasó a
liderar, así que liberarlo y descontarlo tiene que ser una sola operación.

### Decisiones

* **Una transacción por subasta.** Un conflicto puntual no bloquea el lote: la subasta afectada se
  reintenta en la pasada siguiente y el resto avanza igual.
* **Se relee la subasta dentro de la transacción.** Las candidatas se eligen con una lectura previa
  sin seguimiento, y entre esa lectura y la transacción el estado pudo cambiar. Si al releerla ya
  no está vencida —porque una puja de último segundo la extendió— se la deja correr.
* **Un conflicto de concurrencia no es un error.** Se registra como advertencia y se reintenta; el
  resumen de la pasada lo cuenta aparte de los fallos.
* **La lógica vive en la capa de aplicación, no en el `BackgroundService`.** Éste sólo resuelve las
  preocupaciones del alojamiento: intervalo, alcance de las dependencias y que un error puntual no
  mate al proceso. El disparador podría ser igual de bien una tarea programada externa.
* **`userId` nulo en la auditoría del cierre.** No lo hizo una persona: lo hizo el sistema, y el
  registro lo dice explícitamente.
* **Sólo se informa cuando hubo trabajo.** Con intervalo de 10 segundos, registrar cada pasada
  vacía ahogaría el log.

```json
"AuctionWorker": { "IntervalSeconds": 10 }
```

### Verificado de punta a punta

Publicar → pujar → cerrar → liquidar, sobre subastas creadas al momento:

```
A  cierra en 100 s, puja con 70 s restantes  → sin extensión → Completed y liquidada
B  cierra en  70 s, puja con 40 s restantes  → extendida 2 min → el proceso la deja correr

vendedor     30.000,00 →  50.000,00   (+20.000,00)
comprador1  150.000,00 → 130.000,00   (−20.000,00)
```

Y sobre los datos semilla, en la primera pasada tras el arranque:

```
subasta #4 (vencida con puja)   → COMPLETED, liquidada
subasta #5 (vencida sin pujas)  → UNSOLD
comprador2: $230.000 / $30.000 retenidos  →  $200.000 totales y disponibles
```

Ese último renglón es el que la consigna exige y el que la semilla venía prometiendo: la garantía
congelada era exactamente lo que faltaba adjudicar.

En todo momento el dinero se conserva: la suma de los saldos del sistema es igual a la suma de lo
depositado. Una liquidación mueve dinero entre billeteras, nunca lo crea ni lo destruye.

---

## Tiempo real con SignalR

El canal vive en `/hubs/auctions` y **cada subasta tiene su propio grupo**. Una puja se difunde
sólo a quienes están mirando esa sala: sin esa separación, abrir el catálogo implicaría recibir el
tráfico de todas las subastas activas a la vez.

| Método del hub | Efecto |
|---|---|
| `JoinRoom(auctionId)` | El cliente empieza a recibir los eventos de esa subasta |
| `LeaveRoom(auctionId)` | Deja de recibirlos |

| Evento | Cuándo llega | Contenido |
|---|---|---|
| `BidPlaced` | Se aceptó una oferta | Importe líder, próximo mínimo, cantidad de ofertas, seudónimo, nuevo cierre y si hubo extensión |
| `AuctionClosed` | El proceso en segundo plano cerró la subasta | Estado final, importe, ganador (nulos si quedó desierta) |

El evento lleva todo lo que necesita un cliente para repintar la sala **sin volver a consultar la
API**. En particular, `wasExtended` y `endsAtUtc` permiten recalcular el contador regresivo en el
momento exacto en que la regla anti-sniping corre el cierre.

### El reemplazo del notificador provisional

La funcionalidad anterior dejó declarado `IAuctionNotifier` y lo satisfizo con una implementación
que sólo escribía en el log. Sustituirla por el transporte real consistió en **registrar otra
implementación del mismo puerto**: el servicio de pujas, el de cierre y el dominio no cambiaron una
línea. El notificador provisional se eliminó junto con su carpeta, igual que en su momento se
eliminó el repositorio en memoria al llegar EF Core.

### Decisiones

* **El hub no exige autenticación**, en coherencia con el resto de la lectura: el catálogo y el
  historial de ofertas son públicos, y mirar una sala no debería pedir más permisos que verla por
  HTTP. Para **pujar** sigue haciendo falta el token, porque eso pasa por la API REST.
* **Un fallo al difundir no llega al usuario.** Cuando el evento se emite, la operación ya está
  confirmada en la base. Se registra una advertencia y nada más: el cliente se resincroniza en su
  próxima consulta. La difusión es una mejora de experiencia, no la fuente de verdad.
* **Notificador singleton.** El `IHubContext` es seguro para uso concurrente, así que no hace falta
  una instancia por petición aunque lo consuman servicios con alcance de petición.
* **CORS con `AllowCredentials`.** Es obligatorio para que SignalR negocie la conexión, y obliga a
  enumerar los orígenes: con credenciales habilitadas el navegador rechaza un comodín.

```json
"Cors": { "AllowedOrigins": ["http://localhost:5173", "http://127.0.0.1:5173"] }
```

### Verificado con un cliente real

Con una página conectada al hub desde el navegador, mientras se operaba la API por separado:

```
unirse a la sala 1 · pujar en #1        → llega BidPlaced con el nuevo líder y el próximo mínimo
pujar en otra subasta no observada      → no llega nada  (los grupos aíslan de verdad)
pujar dentro de la ventana crítica      → llega BidPlaced con wasExtended=true y el cierre corrido
el proceso cierra la subasta observada  → llega AuctionClosed con estado, importe y ganador
abandonar la sala y volver a pujar      → no llega nada
```

El evento de cierre es el que más confianza da: lo emite el proceso en segundo plano, no una
petición HTTP, y aun así llega al navegador por el mismo canal.

---

## Frontend

Sin framework ni proceso de compilación: HTML, CSS y **JavaScript con módulos nativos**, servido
como contenido estático desde la propia API. Así no hay un segundo servidor que levantar, y el
proyecto se sigue ejecutando con un único `dotnet run`.

Bootstrap 5 aporta la grilla y los componentes básicos; la hoja propia define sólo lo que el
framework no cubre: la identidad visual, los distintivos de estado y el contador crítico.

### Organización

| Módulo | Responsabilidad |
|---|---|
| `session.js` | Guarda y recupera la sesión; una sesión vencida se descarta antes de usarla |
| `api.js` | Único punto que conoce `fetch`, la ruta base y los códigos de estado |
| `ui.js` | Formato, avisos flotantes, estados de carga, contador y barra de navegación |
| `catalog.js` · `login.js` · `auction-room.js` | Una vista por pantalla, sin lógica compartida duplicada |

Las vistas nunca tocan `fetch` ni interpretan un código HTTP: reciben un `ApiError` ya traducido.
Es la misma separación que en el backend, sólo que del otro lado del cable.

### Decisiones

* **El catálogo es público.** Funciona sin sesión, igual que el endpoint que consume. Iniciar
  sesión sólo cambia la barra de navegación; el contenido es el mismo.
* **Un único temporizador para todos los contadores.** Se refrescan juntos los elementos con
  `[data-ends-at]` en lugar de crear un intervalo por tarjeta.
* **Una subasta cerrada no muestra cuenta regresiva**, sino su desenlace. Mezclar ambas cosas
  llevaba a leer "cierra en finalizada".
* **Al llegar a cero se recarga el listado**, con unos segundos de margen: para entonces el
  proceso en segundo plano ya resolvió el estado final y la tarjeta pasa a mostrarlo.
* **Todo texto se escapa antes de entrar al DOM.** Los títulos de las subastas los escribe un
  usuario, así que se tratan como datos y no como HTML.
* **Los botones se deshabilitan mientras dura la operación**, para impedir envíos duplicados.
* **Las cuentas de prueba se listan en la pantalla de acceso.** No hay registro público: el
  conjunto de usuarios es fijo, y quien corrige la práctica necesita entrar sin leer el código.
* **La contraseña se limpia al fallar, el email no.** Casi siempre el error está en la primera.

### Verificado en el navegador

```
catálogo sin sesión            → 5 subastas, 4 estados distintos, contadores corriendo
filtro por estado              → 1 resultado, sólo "Activa"
filtro por categoría           → 2 resultados, ambos de Coleccionables
búsqueda sin coincidencias     → estado vacío, sin errores
contraseña incorrecta          → aviso rojo, contraseña limpiada, sigue en la pantalla
credenciales correctas         → redirige al catálogo, la barra pasa a mostrar el seudónimo
cerrar sesión                  → vuelve al catálogo anónimo y borra el token
sesión vencida o corrupta      → se descarta sola, sin romper la página
ancho de 375 px                → filtros apilados, menú plegado, sin desborde horizontal
```

Sin errores en la consola, y sin una sola petición fallida salvo el 401 del intento deliberado
con contraseña incorrecta.

### La sala en vivo

Es la pantalla donde se cruza todo lo construido antes: el estado de la subasta, el saldo, la
regla anti-sniping, el proceso de cierre y el canal de tiempo real.

**Indicador de situación.** Lo primero que se ve al entrar es en qué posición está uno: liderando
(verde), superado (rojo) o sin haber participado. Haber ofertado y no liderar significa exactamente
una cosa, y el indicador lo dice: *"te superaron, tu garantía fue liberada"*.

**La consola se bloquea antes de dejar fallar.** Si la operación es imposible, el formulario no se
muestra y se explica por qué: sin sesión, siendo el vendedor, con la subasta programada o cerrada,
o cuando uno ya es el postor líder. Es más barato y más claro que dejar que el backend responda un
error por algo que la pantalla ya sabía.

| Situación | Mensaje |
|---|---|
| Sin sesión | Iniciá sesión para poder ofertar |
| Es el vendedor | Un vendedor no puede ofertar en su propia subasta |
| Subasta programada | Las ofertas se habilitan el *(fecha de inicio)* |
| Subasta cerrada | La subasta ya está cerrada |
| Ya lidera | Ya sos el postor líder: esperá a que alguien te supere |

**El monto se sugiere solo**, en el mínimo admitido, y el campo no se pisa mientras el usuario
escribe. El texto de ayuda explica de dónde sale ese número: líder más incremento mínimo.

### Sincronización

La sala se conecta al hub y se une al grupo de **esa** subasta. Al recibir un evento **recarga el
estado desde la API** en lugar de confiar sólo en el payload: así el historial, el saldo y los
indicadores quedan siempre consistentes con la base de datos, y el evento funciona como una señal
de "algo cambió" y no como fuente de verdad.

Por encima hay un **sondeo de respaldo cada 5 segundos**, que sólo consulta con la pestaña visible.
Si el WebSocket no está disponible, la sala se degrada a sondeo en lugar de quedarse congelada; el
indicador junto al historial dice en cuál de los dos modos está.

Al reconectar hay que **volver a unirse al grupo**: la pertenencia vive en la conexión, y además
pudo haber pasado cualquier cosa mientras el canal estuvo caído, así que también se recarga.

### Verificado en el navegador

Con la sala abierta y las ofertas enviadas **desde fuera del navegador**, para que el único camino
posible fuera el canal en tiempo real:

```
otro postor supera tu oferta   → aviso dirigido "Postor_A1 te superó con $ 55.000"
                                  el indicador pasa de verde a rojo
                                  retenido $ 50.000 → $ 0 y disponible $ 150.000 → $ 200.000
                                  la consola se rehabilita con el nuevo mínimo
puja dentro de la ventana      → el reloj salta de 00:00:45 a 00:02:41 y baja de rojo a ámbar
el proceso cierra la subasta   → "Subasta finalizada. Ganador: Postor_A1 con $ 8.000"
                                  el estado pasa a Finalizada y la consola se bloquea
```

Ver la garantía liberarse en vivo, sin recargar, es la prueba de que el escrow del backend y la
sala del frontend están mirando el mismo estado.

El resto de los caminos: vendedor bloqueado en su propia subasta, subasta programada con cuenta
regresiva al inicio, subasta desierta con el reloj en "Cerrada", saldo insuficiente resuelto como
advertencia ámbar y no como error, monto por debajo del mínimo rechazado en pantalla sin llamar al
backend, identificador inválido con estado vacío, y a 375 px de ancho las columnas se apilan sin
desborde horizontal.

---

## Persistencia

Entity Framework Core con enfoque **Code-First**: el esquema relacional se deriva de las
entidades del dominio y se materializa mediante migraciones.

El cambio desde la implementación en memoria de la funcionalidad anterior tocó **sólo la capa de
infraestructura**: dos líneas de `InfrastructureServiceRegistration`. Ni el dominio, ni los casos
de uso, ni los controladores se enteraron, que es exactamente lo que buscaba la inversión de
dependencias.

### Esquema

| Tabla | Contenido |
|---|---|
| `Users` | Participantes. `Email` y `Pseudonym` únicos. |
| `Wallets` | Saldos. Relación 1:1 con `Users`, con índice único sobre `UserId`. |
| `Categories` | Clasificación del catálogo, con `Name` único. |
| `Auctions` | Subastas. `Status` se guarda como texto legible. |
| `Bids` | Historial de ofertas, con índice `(AuctionId, Amount)`. |
| `LedgerEntries` | Libro mayor de las billeteras, con índice `(WalletId, OccurredAt)`. |
| `AuditLog` | Traza de auditoría. Clave `bigint`: es la tabla que más crece, porque registra también los intentos rechazados. |

**Integridad garantizada por el motor**, no sólo por el código:

| Restricción | Tabla | Regla |
|---|---|---|
| `CK_Wallets_CoherentBalances` | Wallets | `TotalBalance >= 0 AND HeldBalance >= 0 AND TotalBalance >= HeldBalance` |
| `CK_Auctions_PositiveAmounts` | Auctions | `StartingPrice > 0 AND MinimumIncrement > 0` |
| `CK_Auctions_Schedule` | Auctions | `EndsAt > StartsAt` |
| `CK_Auctions_BidCount` | Auctions | `BidCount >= 0` |
| `CK_Bids_PositiveAmount` | Bids | `Amount > 0` |
| `CK_LedgerEntries_PositiveAmount` | LedgerEntries | `Amount > 0` |

`Auctions.Version` y `Wallets.Version` se mapean como **`rowversion`**: es el soporte del bloqueo
optimista, y ambos ya están en uso: `Wallets.Version` protege los movimientos de saldo y
`Auctions.Version` impide que dos pujas simultáneas se pisen sobre la misma subasta.

`AvailableBalance` **no se persiste**: es un dato derivado (`TotalBalance - HeldBalance`) que
calcula el dominio. Almacenarlo introduciría un tercer valor que podría quedar desincronizado.

### Fechas en UTC

Un convertidor global fuerza `DateTimeKind.Utc` al leer, porque `datetime2` no almacena zona
horaria. Sin eso el JSON viajaría sin `Z` y el navegador interpretaría los cierres como hora
local, desfasando todos los contadores regresivos.

---

## Datos semilla

Se cargan al arrancar si la base está vacía, y **no** con `HasData` de una migración: los casos de
prueba se definen en relación al instante actual ("cierra en 25 minutos", "venció hace 2
minutos") y una semilla estática quedaría obsoleta apenas se genera.

### Usuarios y billeteras

| Email | Rol en la demo | Total | Retenido | Disponible |
|---|---|---:|---:|---:|
| `vendedor@test.com` | Publica las 5 subastas | $0 | $0 | $0 |
| `comprador1@test.com` | Postor líder de la subasta activa | $150.000 | $45.000 | $105.000 |
| `comprador2@test.com` | Ganador de la subasta vencida | $230.000 | $30.000 | $200.000 |
| `sinfondos@test.com` | Servirá para probar el rechazo por saldo | $500 | $0 | $500 |

> Los saldos de la tabla son los del **instante de la siembra**. Los $30.000 retenidos de
> `comprador2` respaldan la subasta vencida que todavía nadie adjudicó, y el proceso en segundo
> plano los liquida en su primera pasada: unos segundos después del arranque, `comprador2` queda
> en **$200.000 totales y disponibles** y el vendedor en **$30.000**, que es el estado que
> describe la consigna.

Todas las cuentas comparten la contraseña `Password123!`, ya hasheada con PBKDF2 por el
sembrador.

La semilla escribe además los **asientos** que justifican cada saldo: el depósito inicial y las
retenciones y liberaciones de las pujas históricas. Por eso el historial de movimientos tiene
contenido desde el primer arranque y reconstruye exactamente los saldos de la tabla de arriba.

### Subastas (casos de prueba de la consigna)

| # | Caso | Estado |
|---|---|---|
| 1 | Notebook gamer — cierra en 25 min, 2 pujas previas, líder $45.000 | `Active` |
| 2 | Figura coleccionable — cierra en 90 s | `Active` |
| 3 | Campera vintage — inicio a +24 h | `Scheduled` |
| 4 | Moto 150cc — cierre vencido con puja ganadora | nace `Active`, el proceso la deja `Completed` |
| 5 | Álbum de figuritas — cierre vencido sin pujas | nace `Active`, el proceso la deja `Unsold` |

Los casos 4 y 5 nacen **vencidos pero abiertos** a propósito: son la prueba de que el proceso en
segundo plano resuelve lo que encuentra pendiente al arrancar, incluidas las subastas que
vencieron mientras la aplicación no estaba corriendo.

---

## Puesta en marcha

### Requisitos

* [.NET SDK 9.0](https://dotnet.microsoft.com/download) o superior
* SQL Server en cualquiera de sus variantes: **LocalDB** (viene con Visual Studio y con SQL
  Server Express), SQL Server Developer/Express, o un contenedor Docker.

La cadena de conexión está en `src/SubastaYa.Api/appsettings.json` y por defecto apunta a LocalDB,
que no requiere ninguna instalación adicional en Windows:

```json
"ConnectionStrings": {
  "SubastaYa": "Server=(localdb)\MSSQLLocalDB;Database=SubastaYaDb;Trusted_Connection=True;TrustServerCertificate=True"
}
```

### Compilar y ejecutar

```bash
dotnet restore
dotnet build
dotnet run --project src/SubastaYa.Api
```

**Las migraciones se aplican solas al arrancar.** Para ejecutarlas a mano:

```bash
dotnet tool restore
dotnet ef database update --project src/SubastaYa.Infrastructure --startup-project src/SubastaYa.Api
```

Para reiniciar el escenario de prueba alcanza con borrar la base; el próximo arranque la recrea y
la vuelve a sembrar:

```bash
dotnet ef database drop --force --project src/SubastaYa.Infrastructure --startup-project src/SubastaYa.Api
```

### Si LocalDB deja de responder

LocalDB se apaga solo tras un rato de inactividad. Si al arrancar aparece un error de conexión:

```bash
sqllocaldb start MSSQLLocalDB
```

Y si después de eso el arranque falla con **«Cannot create file … SubastaYaDb.mdf because it
already exists»** (error 5170), quedaron archivos huérfanos: la base ya no figura en el catálogo de
LocalDB pero sus archivos siguen en disco. Como sólo contienen datos semilla, que se regeneran en
el siguiente arranque, alcanza con borrarlos:

```bash
rm -f "$USERPROFILE/SubastaYaDb.mdf" "$USERPROFILE/SubastaYaDb_log.ldf"
```

| Recurso | URL |
|---|---|
| **Aplicación** | <http://localhost:5080/> |
| Iniciar sesión | <http://localhost:5080/login.html> |
| Sala en vivo | <http://localhost:5080/auction.html?id=1> |
| Swagger UI | <http://localhost:5080/swagger> |
| Health check | <http://localhost:5080/api/v1/health> |

No hace falta levantar nada aparte: el frontend se sirve como contenido estático desde la misma
API, así que `dotnet run` alcanza para tener la aplicación completa.

---

## Estado del proyecto

El desarrollo avanza por funcionalidad, una por *pull request*.

- [x] Estructura de la solución en capas y health check
- [x] Modelo de dominio y reglas de subasta
- [x] Catálogo de subastas (API de lectura)
- [x] Persistencia con EF Core, migraciones y datos semilla
- [x] Autenticación con JWT
- [x] Billetera virtual y libro mayor
- [x] Registro de pujas con garantías atómicas y bloqueo optimista
- [x] Publicación de subastas
- [x] Proceso en segundo plano de adjudicación
- [x] Sincronización en tiempo real con SignalR
- [x] Frontend: catálogo e inicio de sesión
- [x] Frontend: sala en vivo
- [ ] Frontend: billetera y actividad
- [ ] Prueba de concurrencia

---

## Estructura del repositorio

```
SubastasYaProyectoSoftware/
├── SubastaYa.sln
├── Directory.Build.props            # TargetFramework y opciones comunes
├── .config/dotnet-tools.json        # dotnet-ef fijado por versión
├── tests/
│   └── SubastaYa.Domain.Tests/      # Pruebas unitarias del dominio (xUnit)
└── src/
    ├── SubastaYa.Domain/
    │   ├── Entities/                # Auction, Wallet, LedgerEntry, AuditRecord, Bid, User, Category
    │   ├── Enums/                   # AuctionStatus, LedgerEntryType, AuditAction, AuditedEntity
    │   ├── Exceptions/              # Jerarquía de errores de negocio
    │   ├── Rules/                   # Parámetros de anti-sniping
    │   └── Results/                 # BidPlacementResult
    ├── SubastaYa.Application/
    │   ├── Abstractions/            # Puertos: persistencia, seguridad, reloj y tiempo real
    │   ├── Dtos/                    # Contratos de entrada y salida
    │   ├── Mapping/                 # Entidad -> DTO
    │   ├── Common/                  # PagedResult
    │   └── Services/                # Casos de uso
    ├── SubastaYa.Infrastructure/
    │   ├── Persistence/
    │   │   ├── Configurations/      # Fluent API por entidad
    │   │   ├── Migrations/          # Code-First
    │   │   ├── Repositories/
    │   │   └── Seeding/
    │   ├── Security/                # PBKDF2 y emisión de JWT
    │   └── Time/                    # Reloj del sistema
    └── SubastaYa.Api/
        ├── BackgroundJobs/          # Proceso de cierre y su configuración
        ├── Configuration/           # Registro de servicios web
        ├── Controllers/
        ├── Hubs/                    # Canal SignalR de la sala en vivo
        ├── Middleware/              # Manejo global de excepciones
        ├── Security/                # Usuario actual desde el token
        └── wwwroot/                 # Frontend servido como contenido estático
            ├── css/styles.css       # Identidad visual sobre Bootstrap
            ├── js/                  # session, api, ui y una vista por pantalla
            ├── index.html           # Catálogo
            ├── auction.html         # Sala de subasta en vivo
            └── login.html           # Inicio de sesión
```
