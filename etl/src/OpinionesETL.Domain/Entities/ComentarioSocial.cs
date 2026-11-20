namespace OpinionesETL.Domain.Entities;

public sealed record ComentarioSocial(string IdComment, int? IdCliente, int IdProducto, short IdRedSocial, DateOnly Fecha, string Comentario, short IdClasificacion);
