# Sistema de Análisis de Opiniones de Clientes

Proyecto final de Big Data (ITLA). Proceso ETL en **C# / .NET 8** sobre **PostgreSQL**, en dos etapas:

1. **ETL operacional** (`etl/`): carga los CSV de clientes, productos, reseñas, comentarios y encuestas en el esquema `opiniones` (base operacional).
2. **Worker ETL** (`worker/`): extrae de tres fuentes (CSV, base operacional y API REST), pasa por staging (`stg`) y carga el modelo en estrella `dw`.

## Estructura

```
opiniones-clientes/
├── run.ps1                         lanza la API de prueba o el Worker (PowerShell)
├── data/                           CSV de origen
│   └── pruebas/                    surveys_con_errores.csv (defectos deliberados)
├── sql/
│   ├── 00_crear_base_datos.sql     crea la base opiniones_clientes (conectado a "postgres")
│   ├── 01_crear_base_datos.sql     esquema opiniones: tablas, claves, índices, catálogos
│   ├── 02_procedimientos_vistas.sql  función, procedimientos y vistas del esquema opiniones
│   ├── 03_consultas_validacion.sql   validación de la base operacional
│   ├── 04_modelo_analitico_dw.sql    esquema dw en estrella (Actividad 3.1); BORRA dw si existe
│   ├── 05_consultas_analiticas.sql   consultas analíticas del enunciado
│   ├── 06_staging_worker.sql         esquema stg y procedimiento stg.sp_cargar_dw (Worker)
│   └── 07_consultas_validacion_worker.sql  validación del Worker (huérfanos, duplicados, rechazos)
├── docs/                           diagramas, contrato OpenAPI de la API, documentos Word
├── etl/                            ETL operacional (.NET 8)
│   ├── OpinionesETL.sln
│   ├── src/OpinionesETL.Domain
│   ├── src/OpinionesETL.Application
│   ├── src/OpinionesETL.Infrastructure
│   ├── src/OpinionesETL.Console    punto de entrada (appsettings.json)
│   └── tests/OpinionesETL.Tests
└── worker/                         Worker Service (.NET 8, arquitectura Onion)
    ├── OpinionesETL.Worker.sln
    ├── src/OpinionesETL.Worker.Domain
    ├── src/OpinionesETL.Worker.Application
    ├── src/OpinionesETL.Worker.Infrastructure
    ├── src/OpinionesETL.Worker.Host     punto de entrada (appsettings.json, Serilog)
    ├── src/OpinionesETL.Mock.Api        API de prueba del contrato de comentarios sociales
    └── tests/OpinionesETL.Worker.Tests
```

## 1. Requisitos

- **.NET 8 SDK** (`dotnet --version` debe empezar por 8).
- **PostgreSQL 14 o superior** en `localhost:5432`, con un usuario `postgres`.
- **PowerShell** para `run.ps1`.
- Opcional: `psql` o DBeaver para ejecutar los scripts SQL. Si `psql` no está en el PATH, use la ruta completa, por ejemplo `"C:\Program Files\PostgreSQL\18\bin\psql.exe"`.

## 2. Credenciales

El repositorio **no contiene contraseñas ni claves reales**. Los valores se entregan por variables de entorno o por user-secrets:

| Variable | Para qué | Valor de ejemplo |
|---|---|---|
| `ConnectionStrings__Opiniones` | ETL operacional (`etl/`) | `Host=localhost;Database=opiniones_clientes;Username=postgres;Password=<clave>` |
| `ConnectionStrings__Operacional` | Worker: lee la base operacional | `Host=localhost;Database=opiniones_clientes;Username=postgres;Password=<clave>` |
| `ConnectionStrings__Analitica` | Worker: escribe en `stg` y `dw` | igual que la anterior |
| `Fuentes__Api__ApiKey` | Worker: clave de la API | `clave-de-prueba` para la API de prueba |

Si el servidor local acepta conexiones sin contraseña, no hace falta definir ninguna de las cadenas de conexión. Ejemplo en PowerShell (solo para la sesión actual):

```powershell
$env:ConnectionStrings__Opiniones = "Host=localhost;Database=opiniones_clientes;Username=postgres;Password=<clave>"
```

También puede guardarlas con user-secrets, sin que queden en archivos del repo:

```powershell
dotnet user-secrets set "ConnectionStrings:Operacional" "Host=localhost;Database=opiniones_clientes;Username=postgres;Password=<clave>" --project worker/src/OpinionesETL.Worker.Host
```

> `appsettings.Development.json` está en `.gitignore`. Puede copiar `worker/src/OpinionesETL.Worker.Host/appsettings.Development.json.example` a ese nombre y poner ahí sus valores.

## 3. Etapa A: preparar la base de datos

Ejecute los scripts en este orden. Cada uno se puede repetir: borra y recrea su esquema.

```powershell
psql -h localhost -U postgres -d postgres -f sql/00_crear_base_datos.sql
```

```powershell
psql -h localhost -U postgres -d opiniones_clientes -v ON_ERROR_STOP=1 -f sql/01_crear_base_datos.sql
```

```powershell
psql -h localhost -U postgres -d opiniones_clientes -v ON_ERROR_STOP=1 -f sql/02_procedimientos_vistas.sql
```

Si va a usar el Worker, ejecute también los scripts de su parte:

```powershell
psql -h localhost -U postgres -d opiniones_clientes -v ON_ERROR_STOP=1 -f sql/04_modelo_analitico_dw.sql
```

```powershell
psql -h localhost -U postgres -d opiniones_clientes -v ON_ERROR_STOP=1 -f sql/06_staging_worker.sql
```

> **Atención:** `04_modelo_analitico_dw.sql` borra el esquema `dw` completo. Si ya cargó datos con el Worker, vuelva a ejecutarlo solo si también va a volver a correr el Worker.

## 4. Etapa B: ETL operacional (`etl/`)

Carga los CSV de `data/` en el esquema `opiniones`. Se ejecuta **desde la raíz del repo**:

```powershell
dotnet run --project etl/src/OpinionesETL.Console -- --reiniciar
```

- `--reiniciar` vacía las tablas de datos antes de cargar. Úselo para repetir la corrida sin duplicados.
- `--carpeta <ruta>` indica otra carpeta de CSV.
- Códigos de salida: `0` si no hubo errores de archivo (los registros rechazados no cuentan como error), `1` si hubo errores de archivo, `2` error fatal (por ejemplo, sin conexión).

Resultado esperado con los CSV entregados:

| Archivo | Procesados | Insertados | Rechazados |
|---|---|---|---|
| fuente_datos.csv | 100 | 100 | 0 |
| clients.csv | 500 | 500 | 0 |
| products.csv | 200 | 200 | 0 |
| web_reviews.csv | 200 | 200 | 0 |
| social_comments.csv | 200 | 200 | 0 |
| surveys_part1.csv | 500 | 6 | 494 |
| **Total** | **1700** | **1206** | **494** |

Las 494 encuestas rechazadas tienen `IdCliente` o `IdProducto` inexistentes en `clients.csv` o `products.csv`.

Validación: ejecute `sql/03_consultas_validacion.sql` en `opiniones_clientes`.

## 5. Etapa C: Worker ETL (`worker/`)

El Worker necesita **dos procesos**: la API de comentarios sociales (en este proyecto, una API de prueba) y el propio Worker. Ambos se lanzan con `run.ps1` desde la raíz del repo.

### C.1 Levantar la API de prueba

Abra una terminal y déjela abierta:

```powershell
.\run.ps1 -Componente api
```

Sirve `data/social_comments.csv` en `http://localhost:5080/api/comentarios-sociales`. Exige el header `X-API-Key` con el valor `clave-de-prueba`.

Para probar reintentos ante fallos transitorios, reinicie la API con dos respuestas 503 simuladas:

```powershell
$env:SimularFallos = "2"; .\run.ps1 -Componente api
```

### C.2 Ejecutar el Worker

En otra terminal, desde la raíz del repo:

```powershell
.\run.ps1 -Componente worker
```

`run.ps1` pasa la ruta absoluta de `data/` y, si `Fuentes__Api__ApiKey` no está definida, usa `clave-de-prueba`. Para correrlo sin el script, defina antes las variables necesarias; no use `dotnet run` desde la raíz sin `run.ps1`, porque `dotnet run` cambia el directorio de trabajo al del proyecto y la ruta `data` no se resuelve.

El Worker corre en modo `UnaVez` (por defecto) y termina con código `0` o `1`. Para ejecución periódica:

```powershell
$env:Worker__Modo = "Periodico"; $env:Worker__IntervaloMinutos = "60"; .\run.ps1 -Componente worker
```

Al terminar imprime un resumen con las fuentes, los rechazos y la carga al almacén. Resultado esperado con la base operacional cargada y la API en marcha:

| Fuente | Leídos | Extraídos | Rechazados |
|---|---|---|---|
| CSV (surveys_part1 + part2) | 800 | 800 | 0 |
| Base operacional (reseñas web) | 200 | 200 | 0 |
| API (comentarios sociales) | 200 | 200 | 0 |

| Carga a dw | Primera ejecución | Segunda ejecución |
|---|---|---|
| Opiniones en staging | 1200 | 1200 |
| Rechazadas por integridad | 494 | 494 |
| Hechos nuevos | 706 | 0 |
| Ya existentes (idempotencia) | 0 | 706 |
| Días nuevos en dim_tiempo | 395 | 0 |

La segunda ejecución no agrega hechos: es la comprobación de idempotencia.

### C.3 Validar la carga

```powershell
psql -h localhost -U postgres -d opiniones_clientes -f sql/07_consultas_validacion_worker.sql
```

Lo esperado: 0 hechos huérfanos, 0 duplicados y 494 rechazos de integridad.

### C.4 Escenarios de error

| Escenario | Cómo reproducirlo | Resultado esperado |
|---|---|---|
| CSV con defectos | `$env:Fuentes__Csv__Carpeta = "$PWD\data\pruebas"` y luego ejecutar el Worker | 9 leídas, 2 extraídas, 7 rechazadas |
| API key inválida | `$env:Fuentes__Api__ApiKey = "otra"` | Falla solo la fuente API (HTTP 401, sin reintentos), código `1` |
| API con fallos transitorios | API con `SimularFallos=2` | Reintenta y termina bien |
| API apagada | Cerrar la API y ejecutar el Worker | 4 intentos, falla solo la API, código `1` |

Limpie las variables después de cada prueba con `Remove-Item Env:<nombre>`.

## 6. Pruebas unitarias

```powershell
dotnet test etl/OpinionesETL.sln
```

```powershell
dotnet test worker/OpinionesETL.Worker.sln
```

Resultado esperado: `etl/` 47 de 47 y `worker/` 58 de 58.

## 7. Flujo del ETL operacional

Orden de carga (respeta las claves foráneas): `fuente_datos` → `clients` → `products` → `web_reviews` → `social_comments` → `surveys_part1`.

Por cada archivo: leer y validar encabezados → limpiar y transformar → detectar duplicados (en el archivo y en la base) → insertar en una transacción → guardar rechazos en `etl_registro_rechazado` → resumen. Al final se llama `CALL sp_clasificar_por_puntaje()` para clasificar las reseñas web por rating.

## 8. Flujo del Worker

1. **Extracción en paralelo** (`Task.WhenAll`): CSV, base operacional y API. Cada fuente es independiente: si una falla, las demás siguen.
2. **Staging**: cada extractor reemplaza sus filas en `stg.opinion` con una transacción y COPY binario.
3. **Carga**: `stg.sp_cargar_dw` mueve staging al modelo en estrella en una sola transacción. Los registros con producto o cliente inexistente van a `stg.rechazo`.

Detalles, diagramas y justificación de decisiones: `docs/Documento_Arquitectura_Extraccion_Worker.docx`.

## 9. Modelo analítico (Actividad 3.1)

Esquema `dw` en estrella: `fact_opinion` con `dim_tiempo`, `dim_producto`, `dim_cliente`, `dim_fuente`, `dim_sentimiento` y `dim_evento`.

Hay **dos caminos de carga** al mismo esquema `dw`. Use uno de los dos por base de datos:

- **Worker** (camino completo del proyecto): `sql/06_staging_worker.sql` y `CALL stg.sp_cargar_dw(...)`, invocado por el Worker.
- **Actividad 3.1**: `sql/04_modelo_analitico_dw.sql` y `CALL dw.sp_cargar_dw();`, a partir del esquema `opiniones` ya cargado por el ETL operacional. Luego `sql/05_consultas_analiticas.sql`.

Mezclar los dos sobre la misma base mezcla los hechos de ambos. Si cambia de camino, ejecute `04` de nuevo para recrear `dw`.

Entregables: `docs/diagrama_estrella.png`, `sql/04_modelo_analitico_dw.sql`, `docs/Documento_Diseno_Modelo_Analitico.docx`.

## 10. Notas de seguridad y decisiones

- Sin credenciales en el repo: variables de entorno o user-secrets. La clave `clave-de-prueba` es solo de la API de prueba.
- La API de comentarios sociales **no existe como servicio real**: el proyecto incluye su contrato (`docs/contrato-api-comentarios-sociales.yaml`) y una API de prueba que lee `data/social_comments.csv`.
- Las encuestas con cliente o producto inexistente se rechazan y se registran; no se cargan.
- Los comentarios sociales anónimos (`idCliente` vacío) son válidos y se asignan al cliente 0 del `dw`.
