using Microsoft.Extensions.Logging;
using OpinionesETL.Worker.Application.Abstractions;
using OpinionesETL.Worker.Application.Common;
using OpinionesETL.Worker.Application.Models;

namespace OpinionesETL.Worker.Application.Services;

/// <summary>
/// Orquesta una ejecución completa: extractores en paralelo (cada uno deja su resultado en staging)
/// y luego una sola carga hacia el esquema dw.
/// </summary>
public sealed class PipelineEtl(
    IEnumerable<IExtractor> extractores,
    IDataLoader cargador,
    ILogger<PipelineEtl> logger)
{
    public async Task<ResumenEjecucion> EjecutarAsync(CancellationToken ct = default)
    {
        var resumen = new ResumenEjecucion { Inicio = DateTime.Now };
        var lista = extractores.ToList();

        logger.LogInformation("Inicio del ETL con {Cantidad} fuente(s) habilitada(s): {Fuentes}",
            lista.Count, string.Join(", ", lista.Select(e => e.Nombre)));

        if (lista.Count == 0)
        {
            resumen.Errores.Add("No hay fuentes habilitadas en la configuración (sección Fuentes).");
            resumen.Fin = DateTime.Now;
            return resumen;
        }

        // 1) Extracción en paralelo: las fuentes son independientes entre sí.
        var resultados = await Task.WhenAll(lista.Select(e => ExtraerSeguroAsync(e, ct)));
        foreach (var (nombre, resultado) in resultados)
        {
            if (resultado.IsSuccess)
            {
                resumen.Extracciones.Add(resultado.Data!);
            }
            else
            {
                resumen.Errores.Add($"{nombre}: {resultado.Error!.Message}");
                logger.LogError("[{Fuente}] La extracción falló: {Motivo}", nombre, resultado.Error.Message);
            }
        }

        if (resumen.Extracciones.Count == 0)
        {
            logger.LogError("Ninguna fuente se extrajo correctamente; no se ejecuta la carga al esquema dw.");
            resumen.Fin = DateTime.Now;
            return resumen;
        }

        // 2) Carga: staging -> dw (dimensiones y hechos).
        try
        {
            resumen.Carga = await cargador.CargarAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            resumen.Errores.Add($"Carga a dw: {ex.Message}");
            logger.LogError(ex, "La carga al esquema dw falló");
        }

        resumen.Fin = DateTime.Now;
        return resumen;
    }

    private async Task<(string Nombre, Result<ResumenExtraccion> Resultado)> ExtraerSeguroAsync(
        IExtractor extractor, CancellationToken ct)
    {
        try
        {
            return (extractor.Nombre, await extractor.ExtraerAsync(ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[{Fuente}] Fallo inesperado", extractor.Nombre);
            return (extractor.Nombre, new Error($"Fallo inesperado: {ex.Message}"));
        }
    }
}
