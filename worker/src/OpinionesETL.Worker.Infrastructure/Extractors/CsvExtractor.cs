using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesETL.Worker.Application.Abstractions;
using OpinionesETL.Worker.Application.Common;
using OpinionesETL.Worker.Application.Models;
using OpinionesETL.Worker.Application.Options;
using OpinionesETL.Worker.Application.Raw;
using OpinionesETL.Worker.Application.Transform;
using OpinionesETL.Worker.Domain.Entities;
using OpinionesETL.Worker.Domain.Repositories;

namespace OpinionesETL.Worker.Infrastructure.Extractors;

/// <summary>
/// Fuente 1: encuestas en CSV. Lee todos los archivos que cumplan el patrón, uno tras otro y fila por fila
/// (streaming: nunca se carga el archivo completo en memoria).
/// </summary>
public sealed class CsvExtractor(
    IOptions<FuentesOptions> opciones,
    IStagingRepository staging,
    ILogger<CsvExtractor> logger) : IExtractor
{
    public string Nombre => "CSV";

    private readonly CsvOptions _opt = opciones.Value.Csv;

    public async Task<Result<ResumenExtraccion>> ExtraerAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (!Directory.Exists(_opt.Carpeta))
            return new Error($"La carpeta de CSV no existe: '{Path.GetFullPath(_opt.Carpeta)}'");

        var archivos = Directory.GetFiles(_opt.Carpeta, _opt.Patron).OrderBy(a => a, StringComparer.Ordinal).ToList();
        if (archivos.Count == 0)
            return new Error($"No hay archivos '{_opt.Patron}' en '{Path.GetFullPath(_opt.Carpeta)}'");

        var contador = new ContadorExtraccion();
        var rechazos = new List<RegistroRechazado>();

        var extraidos = await staging.ReemplazarOpinionesAsync(
            Origenes.Encuesta, LeerAsync(archivos, contador, rechazos, ct), ct);
        await staging.ReemplazarRechazosAsync(Origenes.Encuesta, rechazos, ct);

        sw.Stop();
        var resumen = new ResumenExtraccion(Nombre, contador.Leidos, extraidos, rechazos.Count, sw.Elapsed);
        logger.LogInformation(
            "CSV: {Archivos} archivo(s), {Leidos} leídas, {Extraidas} extraídas, {Rechazadas} rechazadas, {Fps:F0} filas/s",
            archivos.Count, resumen.Leidos, resumen.Extraidos, resumen.Rechazados, resumen.FilasPorSegundo);
        return Result.Success(resumen);
    }

    private async IAsyncEnumerable<OpinionStaging> LeerAsync(
        IReadOnlyList<string> archivos,
        ContadorExtraccion contador,
        List<RegistroRechazado> rechazos,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var vistos = new HashSet<string>();

        foreach (var archivo in archivos)
        {
            var nombre = Path.GetFileName(archivo);
            using var stream = File.OpenRead(archivo);
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,   // las columnas faltantes se detectan en ValidateHeader, no fila a fila
                BadDataFound = null,
                PrepareHeaderForMatch = a => a.Header.Trim()
            });
            csv.Context.RegisterClassMap<EncuestaMap>();

            if (!await csv.ReadAsync()) { logger.LogWarning("{Archivo} está vacío", nombre); continue; }
            csv.ReadHeader();
            try { csv.ValidateHeader<EncuestaRaw>(); }
            catch (HeaderValidationException ex)
            {
                // Si el archivo no tiene la estructura esperada, se aborta la fuente: no tiene sentido seguir leyendo.
                throw new EstructuraArchivoInvalidaException(nombre, ex.Message.Split('\n')[0]);
            }

            while (await csv.ReadAsync())
            {
                ct.ThrowIfCancellationRequested();
                contador.Leidos++;

                EncuestaRaw? raw = null;
                string? errorLectura = null;
                try { raw = csv.GetRecord<EncuestaRaw>(); }
                catch (CsvHelperException ex) { errorLectura = $"Fila malformada: {ex.Message.Split('\n')[0]}"; }

                var linea = csv.Parser.Row;
                if (raw is null)
                {
                    rechazos.Add(new RegistroRechazado(Origenes.Encuesta, $"{nombre}:{linea}", errorLectura!, csv.Parser.RawRecord.Trim()));
                    continue;
                }

                var r = EncuestaTransformador.Transformar(raw);
                if (r.IsFailure)
                {
                    rechazos.Add(new RegistroRechazado(Origenes.Encuesta, raw.IdOpinion, r.Error!.Message, csv.Parser.RawRecord.Trim()));
                    continue;
                }

                var fila = r.Data!;
                if (!vistos.Add(fila.IdOrigen))
                {
                    rechazos.Add(new RegistroRechazado(Origenes.Encuesta, fila.IdOrigen, "IdOpinion duplicado entre archivos/filas", csv.Parser.RawRecord.Trim()));
                    continue;
                }

                yield return fila;
            }
        }
    }

    /// <summary>Une los encabezados reales del CSV (con tildes) con las propiedades de EncuestaRaw.</summary>
    private sealed class EncuestaMap : ClassMap<EncuestaRaw>
    {
        public EncuestaMap()
        {
            Map(m => m.IdOpinion).Name("IdOpinion");
            Map(m => m.IdCliente).Name("IdCliente");
            Map(m => m.IdProducto).Name("IdProducto");
            Map(m => m.Fecha).Name("Fecha");
            Map(m => m.Comentario).Name("Comentario");
            Map(m => m.Clasificacion).Name("Clasificación");
            Map(m => m.Puntaje).Name("PuntajeSatisfacción");
            Map(m => m.Fuente).Name("Fuente");
        }
    }
}
