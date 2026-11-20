using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using OpinionesETL.Worker.Application.Abstractions;
using OpinionesETL.Worker.Application.Models;
using OpinionesETL.Worker.Application.Options;

namespace OpinionesETL.Worker.Infrastructure.Persistence;

/// <summary>Staging → dw. Toda la lógica de dimensiones y hechos vive en stg.sp_cargar_dw (06_staging_worker.sql).</summary>
public sealed class DataLoader(IOptions<ConexionesOptions> conexiones, ILogger<DataLoader> logger) : IDataLoader
{
    private readonly string _cs = conexiones.Value.Analitica;

    public async Task<ResultadoCargaDw> CargarAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct);

        // Una sola transacción: o entra todo el lote al almacén, o no entra nada.
        await using var tx = await conn.BeginTransactionAsync(ct);
        await using var cmd = new NpgsqlCommand("CALL stg.sp_cargar_dw(0, 0, 0, 0, 0, 0, 0)", conn, tx)
        {
            CommandTimeout = 300
        };

        ResultadoCargaDw resultado;
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct))
                throw new InvalidOperationException("stg.sp_cargar_dw no devolvió resultados.");

            resultado = new ResultadoCargaDw(
                OpinionesEnStaging: reader.GetInt32(0),
                DiasNuevos: reader.GetInt32(1),
                ProductosAfectados: reader.GetInt32(2),
                ClientesAfectados: reader.GetInt32(3),
                FuentesNuevas: reader.GetInt32(4),
                RechazadosIntegridad: reader.GetInt32(5),
                HechosNuevos: reader.GetInt32(6),
                Duracion: TimeSpan.Zero);
        }

        await tx.CommitAsync(ct);
        sw.Stop();

        logger.LogInformation(
            "Carga a dw: {EnStaging} en staging, {Hechos} hechos nuevos, {Rechazados} rechazados por integridad, {Existentes} ya existentes ({Ms} ms)",
            resultado.OpinionesEnStaging, resultado.HechosNuevos, resultado.RechazadosIntegridad,
            resultado.YaExistentes, sw.ElapsedMilliseconds);

        return resultado with { Duracion = sw.Elapsed };
    }
}
