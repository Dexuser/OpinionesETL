using Npgsql;
using NpgsqlTypes;
using OpinionesETL.Domain.Repositories;

namespace OpinionesETL.Infrastructure.Persistence;

/// <summary>Utilidades ADO.NET compartidas por los repositorios (SQL escrito a mano, siempre con parámetros).</summary>
internal static class Sql
{
    public static NpgsqlParameter P(string nombre, NpgsqlDbType tipo) => new(nombre, tipo);

    /// <summary>
    /// Ejecuta el INSERT una vez por registro dentro de UNA transacción. El SQL debe terminar en
    /// ON CONFLICT DO NOTHING: si afecta 0 filas, el registro ya existía y se devuelve como duplicado.
    /// Si ocurre cualquier otro error, la transacción se revierte y la excepción sube al servicio.
    /// </summary>
    public static async Task<ResultadoInsercion> InsertarAsync<T>(
        NpgsqlDataSource db,
        string sql,
        IReadOnlyList<T> registros,
        Action<NpgsqlParameterCollection> declararParametros,
        Action<NpgsqlParameterCollection, T> asignarParametros,
        CancellationToken ct)
    {
        await using var conexion = await db.OpenConnectionAsync(ct);
        await using var transaccion = await conexion.BeginTransactionAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexion, transaccion);
        declararParametros(comando.Parameters);

        var insertados = 0;
        var duplicados = new List<int>();
        for (var i = 0; i < registros.Count; i++)
        {
            asignarParametros(comando.Parameters, registros[i]);
            var filasAfectadas = await comando.ExecuteNonQueryAsync(ct);
            if (filasAfectadas == 1) insertados++;
            else duplicados.Add(i);
        }

        await transaccion.CommitAsync(ct);
        return new ResultadoInsercion(insertados, duplicados);
    }

    public static async Task<HashSet<int>> LeerIdsAsync(NpgsqlDataSource db, string sql, CancellationToken ct)
    {
        var ids = new HashSet<int>();
        await using var comando = db.CreateCommand(sql);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
            ids.Add(lector.GetInt32(0));
        return ids;
    }

    public static async Task<Dictionary<string, short>> LeerCatalogoAsync(NpgsqlDataSource db, string sql, CancellationToken ct)
    {
        var catalogo = new Dictionary<string, short>(StringComparer.OrdinalIgnoreCase);
        await using var comando = db.CreateCommand(sql);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
            catalogo[lector.GetString(1)] = lector.GetInt16(0);
        return catalogo;
    }
}
