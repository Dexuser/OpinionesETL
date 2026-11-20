namespace OpinionesETL.Worker.Domain.Entities;

/// <summary>Valores de la columna "origen" (coinciden con dw.fact_opinion.origen).</summary>
public static class Origenes
{
    public const string Encuesta = "Encuesta";
    public const string Web = "Web";
    public const string Social = "Social";
    public const string Cliente = "Cliente";
    public const string Producto = "Producto";
}

/// <summary>Una opinión ya limpia y lista para guardarse en stg.opinion, venga del canal que venga.</summary>
public sealed record OpinionStaging(
    string Origen,
    string IdOrigen,
    int? IdCliente,
    int IdProducto,
    DateOnly Fecha,
    string Comentario,
    short? Puntaje,
    string Canal,
    string? Sentimiento);

public sealed record ClienteStaging(int IdCliente, string Nombre, string Email);

public sealed record ProductoStaging(int IdProducto, string Nombre, string Categoria);

public sealed record RegistroRechazado(string Origen, string IdRegistro, string Motivo, string DatosOriginales);
