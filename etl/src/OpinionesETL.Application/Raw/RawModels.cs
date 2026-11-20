namespace OpinionesETL.Application.Raw;

// Una fila del CSV tal como viene: todo texto. Propiedades con set para que CsvHelper las llene.

public sealed class ClienteRaw
{
    public string IdCliente { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Email { get; set; } = "";
}

public sealed class ProductoRaw
{
    public string IdProducto { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Categoria { get; set; } = "";
}

public sealed class FuenteDatosRaw
{
    public string IdFuente { get; set; } = "";
    public string TipoFuente { get; set; } = "";
    public string FechaCarga { get; set; } = "";
}

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

public sealed class ResenaWebRaw
{
    public string IdReview { get; set; } = "";
    public string IdCliente { get; set; } = "";
    public string IdProducto { get; set; } = "";
    public string Fecha { get; set; } = "";
    public string Comentario { get; set; } = "";
    public string Rating { get; set; } = "";
}

public sealed class ComentarioSocialRaw
{
    public string IdComment { get; set; } = "";
    public string IdCliente { get; set; } = "";
    public string IdProducto { get; set; } = "";
    public string Fuente { get; set; } = "";
    public string Fecha { get; set; } = "";
    public string Comentario { get; set; } = "";
}
