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

Los estados de la subasta son `Scheduled`, `Active`, `Completed` y `Unsold`. El dominio **no mueve
dinero por su cuenta**: `PlaceBid` devuelve un `BidPlacementResult` con el postor superado y el
monto a liberar, para que el caso de uso lo resuelva dentro de una transacción atómica.

### Preparación para la concurrencia

`Auction` y `Wallet` exponen una propiedad `Version` que la capa de persistencia mapeará como
`rowversion`. Es el mecanismo de **bloqueo optimista** exigido por la consigna: se modela desde el
dominio aunque todavía no haya base de datos.

---

## Pruebas

```bash
dotnet test
```

34 pruebas unitarias cubren las invariantes del negocio sin tocar infraestructura: incremento
mínimo, ventana anti-sniping (45 s extiende, 60 s extiende, 61 s no), puja del vendedor en su
propia subasta, puja del postor que ya lidera, subasta programada o vencida, transiciones a
`Completed` / `Unsold`, y las reglas de la billetera (retención, liberación, liquidación y
traspaso de liderazgo entre postores).

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
| `GET` | `/auctions/{id}` | — | Detalle de la subasta con su historial de ofertas |

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

> Si ya tenías la base creada de antes, hay que **regenerarla** para que los usuarios queden con
> su hash de contraseña; hasta esta funcionalidad se sembraban sin credenciales:
> ```bash
> dotnet ef database drop --force --project src/SubastaYa.Infrastructure --startup-project src/SubastaYa.Api
> ```

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

**Integridad garantizada por el motor**, no sólo por el código:

| Restricción | Tabla | Regla |
|---|---|---|
| `CK_Wallets_CoherentBalances` | Wallets | `TotalBalance >= 0 AND HeldBalance >= 0 AND TotalBalance >= HeldBalance` |
| `CK_Auctions_PositiveAmounts` | Auctions | `StartingPrice > 0 AND MinimumIncrement > 0` |
| `CK_Auctions_Schedule` | Auctions | `EndsAt > StartsAt` |
| `CK_Auctions_BidCount` | Auctions | `BidCount >= 0` |
| `CK_Bids_PositiveAmount` | Bids | `Amount > 0` |

`Auctions.Version` y `Wallets.Version` se mapean como **`rowversion`**: es el soporte del bloqueo
optimista que usará el registro de pujas.

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
| `comprador2@test.com` | Ganador pendiente de liquidación | $230.000 | $30.000 | $200.000 |
| `sinfondos@test.com` | Servirá para probar el rechazo por saldo | $500 | $0 | $500 |

> Los $30.000 retenidos de `comprador2` respaldan la subasta vencida que todavía **nadie
> adjudicó**. Cuando exista el proceso en segundo plano, esa garantía pasará al vendedor y la
> cuenta quedará en $200.000 totales y disponibles.

Todas las cuentas comparten la contraseña `Password123!`, ya hasheada con PBKDF2 por el
sembrador.

### Subastas (casos de prueba de la consigna)

| # | Caso | Estado |
|---|---|---|
| 1 | Notebook gamer — cierra en 25 min, 2 pujas previas, líder $45.000 | `Active` |
| 2 | Figura coleccionable — cierra en 90 s | `Active` |
| 3 | Campera vintage — inicio a +24 h | `Scheduled` |
| 4 | Moto 150cc — cierre vencido con puja ganadora | `Active`, pendiente de adjudicación |
| 5 | Álbum de figuritas — cierre vencido sin pujas | `Active`, pendiente de cierre |

Los casos 4 y 5 quedan **vencidos pero abiertos**: es el estado que deberá resolver el proceso en
segundo plano cuando se implemente.

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

| Recurso | URL |
|---|---|
| Swagger UI | <http://localhost:5080/swagger> |
| Health check | <http://localhost:5080/api/v1/health> |
| Catálogo | <http://localhost:5080/api/v1/auctions> |

---

## Estado del proyecto

El desarrollo avanza por funcionalidad, una por *pull request*.

- [x] Estructura de la solución en capas y health check
- [x] Modelo de dominio y reglas de subasta
- [x] Catálogo de subastas (API de lectura)
- [x] Persistencia con EF Core, migraciones y datos semilla
- [x] Autenticación con JWT
- [ ] Billetera virtual y libro mayor
- [ ] Registro de pujas con garantías atómicas y bloqueo optimista
- [ ] Proceso en segundo plano de adjudicación
- [ ] Sincronización en tiempo real
- [ ] Frontend
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
    │   ├── Entities/                # Auction, Wallet, Bid, User, Category
    │   ├── Enums/                   # AuctionStatus
    │   ├── Exceptions/              # Jerarquía de errores de negocio
    │   ├── Rules/                   # Parámetros de anti-sniping
    │   └── Results/                 # BidPlacementResult
    ├── SubastaYa.Application/
    │   ├── Abstractions/            # Puertos: persistencia, seguridad y reloj
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
        ├── Configuration/           # Registro de servicios web
        ├── Controllers/
        ├── Middleware/              # Manejo global de excepciones
        └── Security/                # Usuario actual desde el token
```
