using OpinionesETL.Domain.Entities;

namespace OpinionesETL.Domain.Repositories;

/// <summary>Resultado de una carga: cuántas filas se insertaron y cuáles ya existían (por posición en la lista enviada).</summary>
public sealed record ResultadoInsercion(int Insertados, IReadOnlyList<int> IndicesDuplicados);

public interface IRepositorioCarga<T>
{
    /// <summary>Inserta en una sola transacción. Si la clave primaria o un campo único ya existe, la fila no se inserta y se reporta como duplicada.</summary>
    Task<ResultadoInsercion> InsertarAsync(IReadOnlyList<T> registros, CancellationToken ct = default);
}

public interface IClienteRepository : IRepositorioCarga<Cliente>
{
    Task<HashSet<int>> ObtenerIdsAsync(CancellationToken ct = default);
}

public interface IProductoRepository : IRepositorioCarga<Producto>
{
    Task<HashSet<int>> ObtenerIdsAsync(CancellationToken ct = default);
}

public interface IFuenteDatosRepository : IRepositorioCarga<FuenteDatos> { }
public interface IEncuestaRepository : IRepositorioCarga<Encuesta> { }
public interface IResenaWebRepository : IRepositorioCarga<ResenaWeb> { }
public interface IComentarioSocialRepository : IRepositorioCarga<ComentarioSocial> { }

/// <summary>Catálogos de solo lectura: nombre -> id.</summary>
public interface ICatalogoRepository
{
    Task<Dictionary<string, short>> ObtenerCategoriasAsync(CancellationToken ct = default);
    Task<Dictionary<string, short>> ObtenerTiposFuenteAsync(CancellationToken ct = default);
    Task<Dictionary<string, short>> ObtenerRedesSocialesAsync(CancellationToken ct = default);
    Task<Dictionary<string, short>> ObtenerClasificacionesAsync(CancellationToken ct = default);
}

public interface IRechazoRepository
{
    Task InsertarAsync(IReadOnlyList<RegistroRechazado> rechazos, CancellationToken ct = default);
}

public interface IMantenimientoRepository
{
    /// <summary>CALL opiniones.sp_reiniciar_carga()</summary>
    Task ReiniciarCargaAsync(CancellationToken ct = default);

    /// <summary>CALL opiniones.sp_clasificar_por_puntaje()</summary>
    Task ClasificarPorPuntajeAsync(CancellationToken ct = default);
}
