# SubastaYa

Plataforma web de subastas en tiempo real con billetera virtual, garantías (*escrow*),
regla anti-sniping y adjudicación automática por proceso en segundo plano.

Trabajo práctico de la cátedra **Proyecto de Software** — Ingeniería en Informática.

| | |
|---|---|
| **Backend** | C# / .NET 9 · ASP.NET Core Web API |
| **Base de datos** | SQL Server (Entity Framework Core, Code-First) |
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

## Puesta en marcha

### Requisitos

* [.NET SDK 9.0](https://dotnet.microsoft.com/download) o superior

### Compilar y ejecutar

```bash
dotnet restore
dotnet build
dotnet run --project src/SubastaYa.Api
```

| Recurso | URL |
|---|---|
| Swagger UI | <http://localhost:5080/swagger> |
| Health check | <http://localhost:5080/api/v1/health> |

```bash
curl -s http://localhost:5080/api/v1/health
# {"status":"ok","checkedAtUtc":"2026-09-20T12:00:00.0000000Z"}
```

---

## Estado del proyecto

El desarrollo avanza por funcionalidad, una por *pull request*.

- [x] Estructura de la solución en capas y health check
- [x] Modelo de dominio y reglas de subasta
- [ ] Catálogo de subastas (API de lectura)
- [ ] Persistencia con EF Core, migraciones y datos semilla
- [ ] Autenticación con JWT
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
├── tests/
│   └── SubastaYa.Domain.Tests/      # Pruebas unitarias del dominio (xUnit)
└── src/
    ├── SubastaYa.Domain/
    │   ├── Entities/                # Auction, Wallet, Bid, User, Category
    │   ├── Enums/                   # AuctionStatus
    │   ├── Exceptions/              # Jerarquía de errores de negocio
    │   ├── Rules/                   # Parámetros de anti-sniping
    │   └── Results/                 # BidPlacementResult
    ├── SubastaYa.Application/       # Casos de uso y puertos
    ├── SubastaYa.Infrastructure/    # Implementación de los puertos
    └── SubastaYa.Api/               # Controladores REST y hosting
```
