using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpinionesETL.Application.Models;
using OpinionesETL.Application.Services;
using OpinionesETL.ConsoleApp;

// Uso:  dotnet run --project etl/src/OpinionesETL.Console [-- --reiniciar] [--carpeta <ruta>]
//   --reiniciar   vacía las tablas de datos (CALL sp_reiniciar_carga) antes de cargar
//   --carpeta     carpeta con los CSV (por defecto la indicada en appsettings.json)

Console.OutputEncoding = Encoding.UTF8;

try
{
    var reiniciar = args.Contains("--reiniciar");
    var indiceCarpeta = Array.IndexOf(args, "--carpeta");

    var configuracion = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false)
        .AddEnvironmentVariables() // permite ConnectionStrings__Opiniones con contraseña fuera del repo
        .Build();

    var cadenaConexion = configuracion.GetConnectionString("Opiniones")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Opiniones en appsettings.json");
    var carpetaConfigurada = indiceCarpeta >= 0 && indiceCarpeta + 1 < args.Length
        ? args[indiceCarpeta + 1]
        : configuracion["Etl:CarpetaDatos"] ?? "data";
    var carpeta = LocalizarCarpetaDatos(carpetaConfigurada);

    await using var proveedor = new ServiceCollection().RegistrarEtl(cadenaConexion).BuildServiceProvider();

    // Verificación previa: fallar rápido si no hay conexión
    await using (var prueba = await proveedor.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync())
    {
        Console.WriteLine($"Conectado a PostgreSQL ({prueba.Host}:{prueba.Port}, base '{prueba.Database}')");
    }

    Console.WriteLine($"Carpeta de datos: {carpeta}");
    if (reiniciar) Console.WriteLine("Modo --reiniciar: se vaciarán las tablas de datos antes de cargar.");
    Console.WriteLine("Ejecutando ETL...");

    var resumen = await proveedor.GetRequiredService<EtlOrquestador>().EjecutarAsync(carpeta, reiniciar);
    ImprimirResumen(resumen);
    return resumen.Errores.Count == 0 ? 0 : 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error fatal: {ex.Message}");
    return 2;
}

static string LocalizarCarpetaDatos(string configurada)
{
    if (Path.IsPathRooted(configurada)) return configurada;
    if (Directory.Exists(configurada)) return Path.GetFullPath(configurada);

    // Si se ejecuta desde bin/Debug/..., busca la carpeta subiendo por los directorios padre.
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        var candidato = Path.Combine(dir.FullName, configurada);
        if (Directory.Exists(candidato)) return candidato;
        dir = dir.Parent;
    }

    throw new DirectoryNotFoundException($"No se encontró la carpeta de datos '{configurada}'. Usa --carpeta <ruta>.");
}

static void ImprimirResumen(ResumenEtl r)
{
    Console.WriteLine();
    Console.WriteLine("==================== RESUMEN DEL PROCESO ETL ====================");
    Console.WriteLine($"{"Archivo",-22}{"Procesados",12}{"Insertados",12}{"Rechazados",12}");
    Console.WriteLine(new string('-', 58));
    foreach (var a in r.Archivos)
        Console.WriteLine($"{a.Archivo,-22}{a.Procesados,12}{a.Insertados,12}{a.Rechazados,12}");
    Console.WriteLine(new string('-', 58));
    Console.WriteLine($"{"TOTAL",-22}{r.TotalProcesados,12}{r.TotalInsertados,12}{r.TotalRechazados,12}");

    var conRechazos = r.Archivos.Where(a => a.Rechazados > 0).ToList();
    if (conRechazos.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Motivos de rechazo (detalle completo en la tabla etl_registro_rechazado):");
        foreach (var a in conRechazos)
        {
            Console.WriteLine($"  {a.Archivo}");
            foreach (var (motivo, cantidad) in a.MotivosRechazo.OrderByDescending(m => m.Value))
                Console.WriteLine($"    {cantidad,5} x {motivo}");
        }
    }

    if (r.Errores.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("ERRORES:");
        foreach (var e in r.Errores) Console.WriteLine($"  - {e}");
    }

    Console.WriteLine();
    Console.WriteLine($"Duración: {r.Duracion.TotalSeconds:F1} s | Estado: {(r.Errores.Count == 0 ? "OK" : "CON ERRORES")}");
}
