using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using OpinionesETL.Worker.Application.Options;
using OpinionesETL.Worker.Domain.Entities;
using OpinionesETL.Worker.Domain.Repositories;

namespace OpinionesETL.Worker.Infrastructure.Persistence;

/// <summary>
/// Escribe en el esquema stg de la base analítica con SQL a mano.
/// Cada método reemplaza lo de SU origen dentro de una transacción: si algo falla, no queda nada a medias,
/// y volver a ejecutar el Worker deja el mismo resultado.
/// </summary>
public sealed class StagingRepository(IOptions<ConexionesOptions> conexiones) : IStagingRepository
{
    private readonly string _cs = conexiones.Value.Analitica;

    public async Task<int> ReemplazarOpinionesAsync(string origen, IAsyncEnumerable<OpinionStaging> filas, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var del = new NpgsqlCommand("DELETE FROM stg.opinion WHERE origen = @origen", conn, tx))
        {
            del.Parameters.AddWithValue("origen", origen);
            await del.ExecuteNonQueryAsync(ct);
        }

        // COPY binario: la forma más rápida de insertar muchas filas en PostgreSQL (sin un INSERT por fila).
        var total = 0;
        await using (var copy = await conn.BeginBinaryImportAsync(
            "COPY stg.opinion (origen, id_origen, id_cliente, id_producto, fecha, comentario, puntaje, canal, sentimiento) " +
            "FROM STDIN (FORMAT BINARY)", ct))
        {
            await foreach (var f in filas.WithCancellation(ct))
            {
                await copy.StartRowAsync(ct);
                await copy.WriteAsync(f.Origen, NpgsqlDbType.Varchar, ct);
                await copy.WriteAsync(f.IdOrigen, NpgsqlDbType.Varchar, ct);
                if (f.IdCliente.HasValue) await copy.WriteAsync(f.IdCliente.Value, NpgsqlDbType.Integer, ct);
                else await copy.WriteNullAsync(ct);
                await copy.WriteAsync(f.IdProducto, NpgsqlDbType.Integer, ct);
                await copy.WriteAsync(f.Fecha, NpgsqlDbType.Date, ct);
                await copy.WriteAsync(f.Comentario, NpgsqlDbType.Varchar, ct);
                if (f.Puntaje.HasValue) await copy.WriteAsync(f.Puntaje.Value, NpgsqlDbType.Smallint, ct);
                else await copy.WriteNullAsync(ct);
                await copy.WriteAsync(f.Canal, NpgsqlDbType.Varchar, ct);
                if (f.Sentimiento is not null) await copy.WriteAsync(f.Sentimiento, NpgsqlDbType.Varchar, ct);
                else await copy.WriteNullAsync(ct);
                total++;
            }
            await copy.CompleteAsync(ct);
        }

        await tx.CommitAsync(ct);
        return total;
    }

    public async Task<int> ReemplazarClientesAsync(IReadOnlyList<ClienteStaging> clientes, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var del = new NpgsqlCommand("DELETE FROM stg.cliente", conn, tx))
            await del.ExecuteNonQueryAsync(ct);

        await using (var copy = await conn.BeginBinaryImportAsync(
            "COPY stg.cliente (id_cliente, nombre, email) FROM STDIN (FORMAT BINARY)", ct))
        {
            foreach (var c in clientes)
            {
                await copy.StartRowAsync(ct);
                await copy.WriteAsync(c.IdCliente, NpgsqlDbType.Integer, ct);
                await copy.WriteAsync(c.Nombre, NpgsqlDbType.Varchar, ct);
                await copy.WriteAsync(c.Email, NpgsqlDbType.Varchar, ct);
            }
            await copy.CompleteAsync(ct);
        }

        await tx.CommitAsync(ct);
        return clientes.Count;
    }

    public async Task<int> ReemplazarProductosAsync(IReadOnlyList<ProductoStaging> productos, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var del = new NpgsqlCommand("DELETE FROM stg.producto", conn, tx))
            await del.ExecuteNonQueryAsync(ct);

        await using (var copy = await conn.BeginBinaryImportAsync(
            "COPY stg.producto (id_producto, nombre, categoria) FROM STDIN (FORMAT BINARY)", ct))
        {
            foreach (var p in productos)
            {
                await copy.StartRowAsync(ct);
                await copy.WriteAsync(p.IdProducto, NpgsqlDbType.Integer, ct);
                await copy.WriteAsync(p.Nombre, NpgsqlDbType.Varchar, ct);
                await copy.WriteAsync(p.Categoria, NpgsqlDbType.Varchar, ct);
            }
            await copy.CompleteAsync(ct);
        }

        await tx.CommitAsync(ct);
        return productos.Count;
    }

    public async Task ReemplazarRechazosAsync(string origen, IReadOnlyList<RegistroRechazado> rechazos, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var del = new NpgsqlCommand(
            "DELETE FROM stg.rechazo WHERE etapa = 'Extraccion' AND origen = @origen", conn, tx))
        {
            del.Parameters.AddWithValue("origen", origen);
            await del.ExecuteNonQueryAsync(ct);
        }

        const string sql = """
            INSERT INTO stg.rechazo (etapa, origen, id_registro, motivo, datos_originales)
            VALUES ('Extraccion', @origen, @id, @motivo, @datos)
            """;
        foreach (var r in rechazos)
        {
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("origen", r.Origen);
            cmd.Parameters.AddWithValue("id", Recortar(r.IdRegistro, 30));
            cmd.Parameters.AddWithValue("motivo", Recortar(r.Motivo, 500));
            cmd.Parameters.AddWithValue("datos", Recortar(r.DatosOriginales, 1000));
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    private static string Recortar(string texto, int max) => texto.Length <= max ? texto : texto[..max];
}
