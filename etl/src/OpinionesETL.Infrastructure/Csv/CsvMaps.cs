using CsvHelper.Configuration;
using OpinionesETL.Application.Raw;

namespace OpinionesETL.Infrastructure.Csv;

// Relacionan el nombre de cada columna del CSV (con tildes incluidas) con la propiedad del objeto raw.

public sealed class ClienteRawMap : ClassMap<ClienteRaw>
{
    public ClienteRawMap()
    {
        Map(m => m.IdCliente).Name("IdCliente");
        Map(m => m.Nombre).Name("Nombre");
        Map(m => m.Email).Name("Email");
    }
}

public sealed class ProductoRawMap : ClassMap<ProductoRaw>
{
    public ProductoRawMap()
    {
        Map(m => m.IdProducto).Name("IdProducto");
        Map(m => m.Nombre).Name("Nombre");
        Map(m => m.Categoria).Name("Categoría");
    }
}

public sealed class FuenteDatosRawMap : ClassMap<FuenteDatosRaw>
{
    public FuenteDatosRawMap()
    {
        Map(m => m.IdFuente).Name("IdFuente");
        Map(m => m.TipoFuente).Name("TipoFuente");
        Map(m => m.FechaCarga).Name("FechaCarga");
    }
}

public sealed class EncuestaRawMap : ClassMap<EncuestaRaw>
{
    public EncuestaRawMap()
    {
        Map(m => m.IdOpinion).Name("IdOpinion");
        Map(m => m.IdCliente).Name("IdCliente");
        Map(m => m.IdProducto).Name("IdProducto");
        Map(m => m.Fecha).Name("Fecha");
        Map(m => m.Comentario).Name("Comentario");
        Map(m => m.Clasificacion).Name("Clasificación");
        Map(m => m.Puntaje).Name("PuntajeSatisfacción");
        Map(m => m.Fuente).Name("Fuente");
    }
}

public sealed class ResenaWebRawMap : ClassMap<ResenaWebRaw>
{
    public ResenaWebRawMap()
    {
        Map(m => m.IdReview).Name("IdReview");
        Map(m => m.IdCliente).Name("IdCliente");
        Map(m => m.IdProducto).Name("IdProducto");
        Map(m => m.Fecha).Name("Fecha");
        Map(m => m.Comentario).Name("Comentario");
        Map(m => m.Rating).Name("Rating");
    }
}

public sealed class ComentarioSocialRawMap : ClassMap<ComentarioSocialRaw>
{
    public ComentarioSocialRawMap()
    {
        Map(m => m.IdComment).Name("IdComment");
        Map(m => m.IdCliente).Name("IdCliente");
        Map(m => m.IdProducto).Name("IdProducto");
        Map(m => m.Fuente).Name("Fuente");
        Map(m => m.Fecha).Name("Fecha");
        Map(m => m.Comentario).Name("Comentario");
    }
}
