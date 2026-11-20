# Ejecuta la API de prueba o el Worker desde cualquier directorio.
# Uso (desde la raíz del repo):
#   .\run.ps1 -Componente api      # levanta OpinionesETL.Mock.Api en http://localhost:5080
#   .\run.ps1 -Componente worker   # ejecuta el Worker una vez (Worker:Modo=UnaVez en appsettings.json)
#
# Secretos: NO se guardan aquí. Para la clave real de la API use:
#   $env:Fuentes__Api__ApiKey = "<clave>"        (si no se define, usa la clave de prueba del contrato)
# Contraseña de PostgreSQL (si el servidor la exige), sin tocar el repo:
#   $env:ConnectionStrings__Operacional = "Host=localhost;Database=opiniones_clientes;Username=postgres;Password=<clave>"
#   $env:ConnectionStrings__Analitica   = "Host=localhost;Database=opiniones_clientes;Username=postgres;Password=<clave>"

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('api', 'worker')]
    [string]$Componente
)

$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot
$datos = Join-Path $raiz 'data'

if ($Componente -eq 'api') {
    $env:ComentariosCsv = Join-Path $datos 'social_comments.csv'
    dotnet run --project (Join-Path $raiz 'worker/src/OpinionesETL.Mock.Api') --no-launch-profile
    exit $LASTEXITCODE
}

# Worker
if (-not $env:Fuentes__Api__ApiKey) { $env:Fuentes__Api__ApiKey = 'clave-de-prueba' }
$env:Fuentes__Csv__Carpeta = $datos   # ruta absoluta: dotnet run cambia el directorio de trabajo al del proyecto
dotnet run --project (Join-Path $raiz 'worker/src/OpinionesETL.Worker.Host')
exit $LASTEXITCODE
