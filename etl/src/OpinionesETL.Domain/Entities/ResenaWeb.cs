namespace OpinionesETL.Domain.Entities;

public sealed record ResenaWeb(string IdReview, int IdCliente, int IdProducto, DateOnly Fecha, string Comentario, short Rating);
