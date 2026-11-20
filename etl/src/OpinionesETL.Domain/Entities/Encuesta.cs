namespace OpinionesETL.Domain.Entities;

public sealed record Encuesta(int IdOpinion, int IdCliente, int IdProducto, DateOnly Fecha, string Comentario, short IdClasificacion, short PuntajeSatisfaccion, string Fuente);
