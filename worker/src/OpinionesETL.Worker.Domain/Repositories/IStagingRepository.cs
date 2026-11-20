using OpinionesETL.Worker.Domain.Entities;

namespace OpinionesETL.Worker.Domain.Repositories;

/// <summary>Tablas staging (esquema stg) de la base analítica donde aterriza lo extraído.</summary>
public interface IStagingRepository
{
    /// <summary>Reemplaza las opiniones de un origen en una sola transacción y devuelve cuántas guardó.
    /// Recibe un flujo asíncrono para no cargar todo en memoria.</summary>
    Task<int> ReemplazarOpinionesAsync(string origen, IAsyncEnumerable<OpinionStaging> filas, CancellationToken ct = default);

    Task<int> ReemplazarClientesAsync(IReadOnlyList<ClienteStaging> clientes, CancellationToken ct = default);

    Task<int> ReemplazarProductosAsync(IReadOnlyList<ProductoStaging> productos, CancellationToken ct = default);

    /// <summary>Reemplaza los rechazos de la etapa de extracción de un origen.</summary>
    Task ReemplazarRechazosAsync(string origen, IReadOnlyList<RegistroRechazado> rechazos, CancellationToken ct = default);
}
