namespace OpinionesETL.Application.Abstractions;

public sealed record FilaCsv<T>(int Numero, T Datos);

public sealed record FilaInvalida(int Numero, string Motivo);

public sealed class LecturaCsv<T>
{
    public List<FilaCsv<T>> Filas { get; } = [];
    public List<FilaInvalida> Invalidas { get; } = [];
}

public interface IArchivoLector<T>
{
    /// <exception cref="EstructuraArchivoInvalidaException">Si el archivo no tiene los encabezados esperados.</exception>
    LecturaCsv<T> Leer(string ruta);
}

public sealed class EstructuraArchivoInvalidaException(string archivo, string detalle)
    : Exception($"Estructura inválida en '{archivo}': {detalle}");
