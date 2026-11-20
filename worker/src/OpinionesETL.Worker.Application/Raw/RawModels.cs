namespace OpinionesETL.Worker.Application.Raw;

/// <summary>Fila de surveys_*.csv tal como viene (todo texto).</summary>
public sealed class EncuestaRaw
{
    public string IdOpinion { get; set; } = "";
    public string IdCliente { get; set; } = "";
    public string IdProducto { get; set; } = "";
    public string Fecha { get; set; } = "";
    public string Comentario { get; set; } = "";
    public string Clasificacion { get; set; } = "";
    public string Puntaje { get; set; } = "";
    public string Fuente { get; set; } = "";
}

/// <summary>Fila leída de opiniones.resena_web en la base operacional.</summary>
public sealed class ResenaWebRaw
{
    public string IdReview { get; set; } = "";
    public int IdCliente { get; set; }
    public int IdProducto { get; set; }
    public DateOnly Fecha { get; set; }
    public string Comentario { get; set; } = "";
    public short Rating { get; set; }
    public string? Sentimiento { get; set; }
}

// ---- Contrato JSON de la API de comentarios sociales (ver docs/contrato-api-comentarios-sociales.yaml) ----

public sealed class ComentarioSocialRaw
{
    public string IdComment { get; set; } = "";
    public int? IdCliente { get; set; }
    public int IdProducto { get; set; }
    public string RedSocial { get; set; } = "";
    public string Fecha { get; set; } = "";
    public string Comentario { get; set; } = "";
    public string? Sentimiento { get; set; }
}

public sealed class PaginaComentariosRaw
{
    public int Pagina { get; set; }
    public int Tamanio { get; set; }
    public int Total { get; set; }
    public int TotalPaginas { get; set; }
    public List<ComentarioSocialRaw> Datos { get; set; } = [];
}
