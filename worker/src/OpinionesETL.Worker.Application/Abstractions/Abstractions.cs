using OpinionesETL.Worker.Application.Common;
using OpinionesETL.Worker.Application.Models;

namespace OpinionesETL.Worker.Application.Abstractions;

/// <summary>Una fuente de datos. Extrae, valida y deja lo extraído en las tablas staging.</summary>
public interface IExtractor
{
    string Nombre { get; }
    Task<Result<ResumenExtraccion>> ExtraerAsync(CancellationToken ct = default);
}

/// <summary>Mueve lo que hay en staging hacia el esquema dw (dimensiones y hechos).</summary>
public interface IDataLoader
{
    Task<ResultadoCargaDw> CargarAsync(CancellationToken ct = default);
}

public sealed class EstructuraArchivoInvalidaException(string archivo, string detalle)
    : Exception($"Estructura inválida en '{archivo}': {detalle}");

public sealed class ApiAutenticacionException(string detalle) : Exception(detalle);
