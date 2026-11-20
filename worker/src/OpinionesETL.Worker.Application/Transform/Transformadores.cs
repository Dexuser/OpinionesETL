using OpinionesETL.Worker.Application.Common;
using OpinionesETL.Worker.Application.Raw;
using OpinionesETL.Worker.Domain.Entities;

namespace OpinionesETL.Worker.Application.Transform;

/// <summary>Convierten la fila cruda de cada fuente en una OpinionStaging limpia, o la rechazan con todos sus motivos.</summary>

public static class EncuestaTransformador
{
    public static Result<OpinionStaging> Transformar(EncuestaRaw raw)
    {
        var errores = new Errores();
        var id = Limpieza.Id(raw.IdOpinion, "IdOpinion");
        var cliente = Limpieza.Id(raw.IdCliente, "IdCliente", "C");
        var producto = Limpieza.Id(raw.IdProducto, "IdProducto", "P");
        var fecha = Limpieza.Fecha(raw.Fecha, "Fecha");
        var comentario = Limpieza.TextoRequerido(raw.Comentario, "Comentario", 500);
        var puntaje = Limpieza.Puntaje(raw.Puntaje, "PuntajeSatisfacción");
        var canal = Limpieza.TextoRequerido(raw.Fuente, "Fuente", 30);
        errores.Agregar(id, cliente, producto, fecha, comentario, puntaje, canal);

        // La clasificación del CSV debe ser válida y coherente con el puntaje.
        Sentimiento? sentimiento = null;
        if (!ClasificadorSentimiento.TryParsear(raw.Clasificacion, out var s))
            errores.Agregar($"Clasificación inválida: '{raw.Clasificacion}'");
        else if (puntaje.IsSuccess && s != ClasificadorSentimiento.PorPuntaje(puntaje.Data))
            errores.Agregar($"Clasificación '{s}' inconsistente con el puntaje {puntaje.Data}");
        else
            sentimiento = s;

        if (errores.Hay) return errores.ComoError();
        return Result.Success(new OpinionStaging(
            Origenes.Encuesta, id.Data.ToString(), cliente.Data, producto.Data, fecha.Data,
            comentario.Data!, puntaje.Data, canal.Data!, sentimiento?.ToString()));
    }
}

public static class ResenaWebTransformador
{
    public static Result<OpinionStaging> Transformar(ResenaWebRaw raw)
    {
        var errores = new Errores();
        var id = Limpieza.TextoRequerido(raw.IdReview, "IdReview", 20);
        var cliente = Limpieza.IdPositivo(raw.IdCliente, "IdCliente");
        var producto = Limpieza.IdPositivo(raw.IdProducto, "IdProducto");
        var fecha = Limpieza.Fecha(raw.Fecha, "Fecha");
        var comentario = Limpieza.TextoRequerido(raw.Comentario, "Comentario", 500);
        var rating = Limpieza.Puntaje(raw.Rating.ToString(), "Rating");
        errores.Agregar(id, cliente, producto, fecha, comentario, rating);

        if (errores.Hay) return errores.ComoError();

        // Si la base operacional aún no clasificó la reseña, se deriva del rating.
        var sentimiento = ClasificadorSentimiento.TryParsear(raw.Sentimiento, out var s)
            ? s
            : ClasificadorSentimiento.PorPuntaje(rating.Data);

        return Result.Success(new OpinionStaging(
            Origenes.Web, id.Data!, cliente.Data, producto.Data, fecha.Data,
            comentario.Data!, rating.Data, "Web", sentimiento.ToString()));
    }
}

public static class ComentarioSocialTransformador
{
    public static Result<OpinionStaging> Transformar(ComentarioSocialRaw raw)
    {
        var errores = new Errores();
        var id = Limpieza.TextoRequerido(raw.IdComment, "IdComment", 20);
        var producto = Limpieza.IdPositivo(raw.IdProducto, "IdProducto");
        var red = Limpieza.TextoRequerido(raw.RedSocial, "RedSocial", 30);
        var fecha = Limpieza.Fecha(raw.Fecha, "Fecha");
        var comentario = Limpieza.TextoRequerido(raw.Comentario, "Comentario", 500);
        errores.Agregar(id, producto, red, fecha, comentario);

        // El cliente es opcional: un comentario anónimo llega con idCliente nulo.
        int? idCliente = null;
        if (raw.IdCliente.HasValue)
        {
            var cliente = Limpieza.IdPositivo(raw.IdCliente.Value, "IdCliente");
            errores.Agregar(cliente);
            if (cliente.IsSuccess) idCliente = cliente.Data;
        }

        if (errores.Hay) return errores.ComoError();

        // Si la API no manda el sentimiento, se clasifica por palabras clave.
        var sentimiento = ClasificadorSentimiento.TryParsear(raw.Sentimiento, out var s)
            ? s
            : ClasificadorSentimiento.PorTexto(comentario.Data);

        return Result.Success(new OpinionStaging(
            Origenes.Social, id.Data!, idCliente, producto.Data, fecha.Data,
            comentario.Data!, null, red.Data!, sentimiento.ToString()));
    }
}
