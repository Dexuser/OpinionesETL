using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
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
/// Fuente 2: la base operacional (esquema opiniones). Trae las reseñas web y además los clientes y
/// productos, que son las dimensiones de referencia que necesita el almacén.
/// Solo lee: la conexión operacional nunca escribe.
/// </summary>
public sealed class DatabaseExtractor(
    IOptions<ConexionesOptions> conexiones,
    IStagingRepository staging,
    ILogger<DatabaseExtractor> logger) : IExtractor
{
    public string Nombre => "BaseOperacional";

    private readonly string _cs = conexiones.Value.Operacional;

    public async Task<Result<ResumenExtraccion>> ExtraerAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(_cs))
            return new Error("No está configurada la cadena de conexión 'Operacional'.");

        var contador = new ContadorExtraccion();
        var rechazos = new List<RegistroRechazado>();

        var clientes = await LeerClientesAsync(ct);
        var productos = await LeerProductosAsync(ct);
        await staging.ReemplazarClientesAsync(clientes, ct);
        await staging.ReemplazarProductosAsync(productos, ct);

        var extraidos = await staging.ReemplazarOpinionesAsync(Origenes.Web, LeerResenasAsync(contador, rechazos, ct), ct);
        await staging.ReemplazarRechazosAsync(Origenes.Web, rechazos, ct);

        sw.Stop();
        var resumen = new ResumenExtraccion(Nombre, contador.Leidos, extraidos, rechazos.Count, sw.Elapsed);
        logger.LogInformation(
            "BaseOperacional: {Clientes} clientes, {Productos} productos, {Leidas} reseñas leídas, {Extraidas} extraídas, {Rechazadas} rechazadas, {Fps:F0} filas/s",
            clientes.Count, productos.Count, resumen.Leidos, resumen.Extraidos, resumen.Rechazados, resumen.FilasPorSegundo);
        return Result.Success(resumen);
    }

    private async Task<List<ClienteStaging>> LeerClientesAsync(CancellationToken ct)
    {
        var lista = new List<ClienteStaging>();
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id_cliente, nombre, email FROM opiniones.cliente ORDER BY id_cliente", conn);
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            lista.Add(new ClienteStaging(rd.GetInt32(0), rd.GetString(1), rd.GetString(2)));
        return lista;
    }

    private async Task<List<ProductoStaging>> LeerProductosAsync(CancellationToken ct)
    {
        var lista = new List<ProductoStaging>();
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT p.id_producto, p.nombre, c.nombre
              FROM opiniones.producto p
              JOIN opiniones.categoria_producto c ON c.id_categoria = p.id_categoria
             ORDER BY p.id_producto
            """, conn);
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            lista.Add(new ProductoStaging(rd.GetInt32(0), rd.GetString(1), rd.GetString(2)));
        return lista;
    }

    private async IAsyncEnumerable<OpinionStaging> LeerResenasAsync(
        ContadorExtraccion contador,
        List<RegistroRechazado> rechazos,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT TRIM(r.id_review), r.id_cliente, r.id_producto, r.fecha, r.comentario, r.rating, cl.nombre
              FROM opiniones.resena_web r
              LEFT JOIN opiniones.clasificacion_sentimiento cl ON cl.id_clasificacion = r.id_clasificacion
             ORDER BY r.id_review
            """, conn);
        await using var rd = await cmd.ExecuteReaderAsync(ct);

        while (await rd.ReadAsync(ct))
        {
            contador.Leidos++;
            var raw = new ResenaWebRaw
            {
                IdReview = rd.GetString(0),
                IdCliente = rd.GetInt32(1),
                IdProducto = rd.GetInt32(2),
                Fecha = rd.GetFieldValue<DateOnly>(3),
                Comentario = rd.GetString(4),
                Rating = rd.GetInt16(5),
                Sentimiento = rd.IsDBNull(6) ? null : rd.GetString(6)
            };

            var r = ResenaWebTransformador.Transformar(raw);
            if (r.IsFailure)
            {
                rechazos.Add(new RegistroRechazado(Origenes.Web, raw.IdReview, r.Error!.Message,
                    $"{raw.IdReview}|{raw.IdCliente}|{raw.IdProducto}|{raw.Fecha:yyyy-MM-dd}|{raw.Rating}|{raw.Comentario}"));
                continue;
            }
            yield return r.Data!;
        }
    }
}
