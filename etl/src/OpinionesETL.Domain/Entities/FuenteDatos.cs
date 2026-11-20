namespace OpinionesETL.Domain.Entities;

public sealed record FuenteDatos(string IdFuente, short IdTipoFuente, DateOnly FechaCarga);
