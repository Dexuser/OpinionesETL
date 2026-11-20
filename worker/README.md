# Worker ETL — Sistema de Análisis de Opiniones de Clientes

Worker Service (.NET 8) que llena la base analítica (`opiniones_clientes`, esquema `dw`) desde tres fuentes:
CSV de encuestas, API REST de comentarios sociales y reseñas web de la base operacional.
Documento técnico: `docs/Documento_Arquitectura_Extraccion_Worker.docx`.

## Estructura
- `src/OpinionesETL.Worker.Domain` — entidades de staging e interfaz del repositorio.
- `src/OpinionesETL.Worker.Application` — contratos, pipeline, limpieza, transformadores.
- `src/OpinionesETL.Worker.Infrastructure` — extractores (CSV, base de datos, API), staging, carga.
- `src/OpinionesETL.Worker.Host` — Worker, configuración, Serilog.
- `src/OpinionesETL.Mock.Api` — API de prueba del contrato `docs/contrato-api-comentarios-sociales.yaml`.
- `tests/OpinionesETL.Worker.Tests` — pruebas xUnit.

## Cómo correrlo
1. En la base `opiniones_clientes` ejecute (Alt+X en DBeaver): `sql/01`, `sql/02`, `sql/04` y `sql/06_staging_worker.sql`,
   y cargue la base operacional con el ETL de `etl/`.
2. Copie la carpeta `data/` junto al ejecutable o ajuste `Fuentes:Csv:Carpeta`.
3. Secretos (no van en el repositorio):
   ```
   export ConnectionStrings__Analitica="Host=localhost;Database=opiniones_clientes;Username=postgres;Password=..."
   export ConnectionStrings__Operacional="Host=localhost;Database=opiniones_clientes;Username=postgres;Password=..."
   export Fuentes__Api__ApiKey="clave-de-prueba"
   ```
4. API de prueba: `.\run.ps1 -Componente api` (desde la raíz del repositorio; sirve `data/social_comments.csv` en http://localhost:5080).
5. Worker: `.\run.ps1 -Componente worker` (desde la raíz). El script pasa la ruta absoluta de `data/`, porque `dotnet run` cambia el directorio de trabajo al del proyecto.
6. Validar con `sql/07_consultas_validacion_worker.sql`. Pruebas: `dotnet test worker/OpinionesETL.Worker.sln`.

Modo periódico: `Worker__Modo=Periodico` y `Worker__IntervaloMinutos=60`.
Datos de muestra: `data/surveys_part2.csv` (300 encuestas válidas) y `data/pruebas/surveys_con_errores.csv` (9 filas con defectos deliberados).
