using Npgsql;
using NpgsqlTypes;
using OpinionesETL.Domain.Entities;
using OpinionesETL.Domain.Repositories;
using static OpinionesETL.Infrastructure.Persistence.Sql;

namespace OpinionesETL.Infrastructure.Persistence;

public sealed class ClienteRepository(NpgsqlDataSource db) : IClienteRepository
{
    private const string Insertar = """
        INSERT INTO opiniones.cliente (id_cliente, nombre, email)
        VALUES (@id_cliente, @nombre, @email)
        ON CONFLICT DO NOTHING
        """;

    public Task<ResultadoInsercion> InsertarAsync(IReadOnlyList<Cliente> registros, CancellationToken ct = default) =>
        Sql.InsertarAsync(db, Insertar, registros,
            p =>
            {
                p.Add(P("id_cliente", NpgsqlDbType.Integer));
                p.Add(P("nombre", NpgsqlDbType.Varchar));
                p.Add(P("email", NpgsqlDbType.Varchar));
            },
            (p, c) =>
            {
                p["id_cliente"].Value = c.IdCliente;
                p["nombre"].Value = c.Nombre;
                p["email"].Value = c.Email;
            }, ct);

    public Task<HashSet<int>> ObtenerIdsAsync(CancellationToken ct = default) =>
        LeerIdsAsync(db, "SELECT id_cliente FROM opiniones.cliente", ct);
}

public sealed class ProductoRepository(NpgsqlDataSource db) : IProductoRepository
{
    private const string Insertar = """
        INSERT INTO opiniones.producto (id_producto, nombre, id_categoria)
        VALUES (@id_producto, @nombre, @id_categoria)
        ON CONFLICT DO NOTHING
        """;

    public Task<ResultadoInsercion> InsertarAsync(IReadOnlyList<Producto> registros, CancellationToken ct = default) =>
        Sql.InsertarAsync(db, Insertar, registros,
            p =>
            {
                p.Add(P("id_producto", NpgsqlDbType.Integer));
                p.Add(P("nombre", NpgsqlDbType.Varchar));
                p.Add(P("id_categoria", NpgsqlDbType.Smallint));
            },
            (p, x) =>
            {
                p["id_producto"].Value = x.IdProducto;
                p["nombre"].Value = x.Nombre;
                p["id_categoria"].Value = x.IdCategoria;
            }, ct);

    public Task<HashSet<int>> ObtenerIdsAsync(CancellationToken ct = default) =>
        LeerIdsAsync(db, "SELECT id_producto FROM opiniones.producto", ct);
}

public sealed class FuenteDatosRepository(NpgsqlDataSource db) : IFuenteDatosRepository
{
    private const string Insertar = """
        INSERT INTO opiniones.fuente_datos (id_fuente, id_tipo_fuente, fecha_carga)
        VALUES (@id_fuente, @id_tipo_fuente, @fecha_carga)
        ON CONFLICT DO NOTHING
        """;

    public Task<ResultadoInsercion> InsertarAsync(IReadOnlyList<FuenteDatos> registros, CancellationToken ct = default) =>
        Sql.InsertarAsync(db, Insertar, registros,
            p =>
            {
                p.Add(P("id_fuente", NpgsqlDbType.Varchar));
                p.Add(P("id_tipo_fuente", NpgsqlDbType.Smallint));
                p.Add(P("fecha_carga", NpgsqlDbType.Date));
            },
            (p, x) =>
            {
                p["id_fuente"].Value = x.IdFuente;
                p["id_tipo_fuente"].Value = x.IdTipoFuente;
                p["fecha_carga"].Value = x.FechaCarga;
            }, ct);
}

public sealed class EncuestaRepository(NpgsqlDataSource db) : IEncuestaRepository
{
    private const string Insertar = """
        INSERT INTO opiniones.encuesta
            (id_opinion, id_cliente, id_producto, fecha, comentario, id_clasificacion, puntaje_satisfaccion, fuente)
        VALUES
            (@id_opinion, @id_cliente, @id_producto, @fecha, @comentario, @id_clasificacion, @puntaje, @fuente)
        ON CONFLICT DO NOTHING
        """;

    public Task<ResultadoInsercion> InsertarAsync(IReadOnlyList<Encuesta> registros, CancellationToken ct = default) =>
        Sql.InsertarAsync(db, Insertar, registros,
            p =>
            {
                p.Add(P("id_opinion", NpgsqlDbType.Integer));
                p.Add(P("id_cliente", NpgsqlDbType.Integer));
                p.Add(P("id_producto", NpgsqlDbType.Integer));
                p.Add(P("fecha", NpgsqlDbType.Date));
                p.Add(P("comentario", NpgsqlDbType.Varchar));
                p.Add(P("id_clasificacion", NpgsqlDbType.Smallint));
                p.Add(P("puntaje", NpgsqlDbType.Smallint));
                p.Add(P("fuente", NpgsqlDbType.Varchar));
            },
            (p, x) =>
            {
                p["id_opinion"].Value = x.IdOpinion;
                p["id_cliente"].Value = x.IdCliente;
                p["id_producto"].Value = x.IdProducto;
                p["fecha"].Value = x.Fecha;
                p["comentario"].Value = x.Comentario;
                p["id_clasificacion"].Value = x.IdClasificacion;
                p["puntaje"].Value = x.PuntajeSatisfaccion;
                p["fuente"].Value = x.Fuente;
            }, ct);
}

public sealed class ResenaWebRepository(NpgsqlDataSource db) : IResenaWebRepository
{
    // id_clasificacion queda NULL: lo completa el procedimiento sp_clasificar_por_puntaje.
    private const string Insertar = """
        INSERT INTO opiniones.resena_web
            (id_review, id_cliente, id_producto, fecha, comentario, rating)
        VALUES
            (@id_review, @id_cliente, @id_producto, @fecha, @comentario, @rating)
        ON CONFLICT DO NOTHING
        """;

    public Task<ResultadoInsercion> InsertarAsync(IReadOnlyList<ResenaWeb> registros, CancellationToken ct = default) =>
        Sql.InsertarAsync(db, Insertar, registros,
            p =>
            {
                p.Add(P("id_review", NpgsqlDbType.Varchar));
                p.Add(P("id_cliente", NpgsqlDbType.Integer));
                p.Add(P("id_producto", NpgsqlDbType.Integer));
                p.Add(P("fecha", NpgsqlDbType.Date));
                p.Add(P("comentario", NpgsqlDbType.Varchar));
                p.Add(P("rating", NpgsqlDbType.Smallint));
            },
            (p, x) =>
            {
                p["id_review"].Value = x.IdReview;
                p["id_cliente"].Value = x.IdCliente;
                p["id_producto"].Value = x.IdProducto;
                p["fecha"].Value = x.Fecha;
                p["comentario"].Value = x.Comentario;
                p["rating"].Value = x.Rating;
            }, ct);
}

public sealed class ComentarioSocialRepository(NpgsqlDataSource db) : IComentarioSocialRepository
{
    private const string Insertar = """
        INSERT INTO opiniones.comentario_social
            (id_comment, id_cliente, id_producto, id_red_social, fecha, comentario, id_clasificacion)
        VALUES
            (@id_comment, @id_cliente, @id_producto, @id_red_social, @fecha, @comentario, @id_clasificacion)
        ON CONFLICT DO NOTHING
        """;

    public Task<ResultadoInsercion> InsertarAsync(IReadOnlyList<ComentarioSocial> registros, CancellationToken ct = default) =>
        Sql.InsertarAsync(db, Insertar, registros,
            p =>
            {
                p.Add(P("id_comment", NpgsqlDbType.Varchar));
                p.Add(P("id_cliente", NpgsqlDbType.Integer));
                p.Add(P("id_producto", NpgsqlDbType.Integer));
                p.Add(P("id_red_social", NpgsqlDbType.Smallint));
                p.Add(P("fecha", NpgsqlDbType.Date));
                p.Add(P("comentario", NpgsqlDbType.Varchar));
                p.Add(P("id_clasificacion", NpgsqlDbType.Smallint));
            },
            (p, x) =>
            {
                p["id_comment"].Value = x.IdComment;
                p["id_cliente"].Value = (object?)x.IdCliente ?? DBNull.Value;   // comentario anónimo -> NULL
                p["id_producto"].Value = x.IdProducto;
                p["id_red_social"].Value = x.IdRedSocial;
                p["fecha"].Value = x.Fecha;
                p["comentario"].Value = x.Comentario;
                p["id_clasificacion"].Value = x.IdClasificacion;
            }, ct);
}

public sealed class CatalogoRepository(NpgsqlDataSource db) : ICatalogoRepository
{
    public Task<Dictionary<string, short>> ObtenerCategoriasAsync(CancellationToken ct = default) =>
        LeerCatalogoAsync(db, "SELECT id_categoria, nombre FROM opiniones.categoria_producto", ct);

    public Task<Dictionary<string, short>> ObtenerTiposFuenteAsync(CancellationToken ct = default) =>
        LeerCatalogoAsync(db, "SELECT id_tipo_fuente, nombre FROM opiniones.tipo_fuente", ct);

    public Task<Dictionary<string, short>> ObtenerRedesSocialesAsync(CancellationToken ct = default) =>
        LeerCatalogoAsync(db, "SELECT id_red_social, nombre FROM opiniones.red_social", ct);

    public Task<Dictionary<string, short>> ObtenerClasificacionesAsync(CancellationToken ct = default) =>
        LeerCatalogoAsync(db, "SELECT id_clasificacion, nombre FROM opiniones.clasificacion_sentimiento", ct);
}

public sealed class RechazoRepository(NpgsqlDataSource db) : IRechazoRepository
{
    private const string Insertar = """
        INSERT INTO opiniones.etl_registro_rechazado (archivo_origen, id_registro, motivo, datos_originales)
        VALUES (@archivo, @id_registro, @motivo, @datos)
        """;

    public async Task InsertarAsync(IReadOnlyList<RegistroRechazado> rechazos, CancellationToken ct = default)
    {
        if (rechazos.Count == 0) return;

        await using var conexion = await db.OpenConnectionAsync(ct);
        await using var transaccion = await conexion.BeginTransactionAsync(ct);
        await using var comando = new NpgsqlCommand(Insertar, conexion, transaccion);
        comando.Parameters.Add(P("archivo", NpgsqlDbType.Varchar));
        comando.Parameters.Add(P("id_registro", NpgsqlDbType.Varchar));
        comando.Parameters.Add(P("motivo", NpgsqlDbType.Varchar));
        comando.Parameters.Add(P("datos", NpgsqlDbType.Jsonb));

        foreach (var r in rechazos)
        {
            comando.Parameters["archivo"].Value = Recortar(r.ArchivoOrigen, 60);
            comando.Parameters["id_registro"].Value = Recortar(r.IdRegistro, 20);
            comando.Parameters["motivo"].Value = Recortar(r.Motivo, 200);
            comando.Parameters["datos"].Value = r.DatosOriginales;
            await comando.ExecuteNonQueryAsync(ct);
        }

        await transaccion.CommitAsync(ct);
    }

    private static string Recortar(string texto, int max) =>
        texto.Length <= max ? texto : texto[..max];
}

public sealed class MantenimientoRepository(NpgsqlDataSource db) : IMantenimientoRepository
{
    public async Task ReiniciarCargaAsync(CancellationToken ct = default)
    {
        await using var comando = db.CreateCommand("CALL opiniones.sp_reiniciar_carga()");
        await comando.ExecuteNonQueryAsync(ct);
    }

    public async Task ClasificarPorPuntajeAsync(CancellationToken ct = default)
    {
        await using var comando = db.CreateCommand("CALL opiniones.sp_clasificar_por_puntaje()");
        await comando.ExecuteNonQueryAsync(ct);
    }
}
