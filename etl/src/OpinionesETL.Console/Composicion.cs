using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpinionesETL.Application.Abstractions;
using OpinionesETL.Application.Raw;
using OpinionesETL.Application.Services;
using OpinionesETL.Domain.Repositories;
using OpinionesETL.Infrastructure.Csv;
using OpinionesETL.Infrastructure.Persistence;

namespace OpinionesETL.ConsoleApp;

/// <summary>Raíz de composición: aquí se conectan las interfaces con sus implementaciones.</summary>
internal static class Composicion
{
    public static IServiceCollection RegistrarEtl(this IServiceCollection s, string cadenaConexion)
    {
        // Conexión (el contenedor la libera al terminar)
        s.AddSingleton(_ => NpgsqlDataSource.Create(cadenaConexion));

        // Lectores CSV: archivo -> objeto raw, con los encabezados que se esperan
        s.AddSingleton<IArchivoLector<ClienteRaw>>(_ =>
            new LectorCsv<ClienteRaw, ClienteRawMap>(["IdCliente", "Nombre", "Email"]));
        s.AddSingleton<IArchivoLector<ProductoRaw>>(_ =>
            new LectorCsv<ProductoRaw, ProductoRawMap>(["IdProducto", "Nombre", "Categoría"]));
        s.AddSingleton<IArchivoLector<FuenteDatosRaw>>(_ =>
            new LectorCsv<FuenteDatosRaw, FuenteDatosRawMap>(["IdFuente", "TipoFuente", "FechaCarga"]));
        s.AddSingleton<IArchivoLector<EncuestaRaw>>(_ =>
            new LectorCsv<EncuestaRaw, EncuestaRawMap>(
                ["IdOpinion", "IdCliente", "IdProducto", "Fecha", "Comentario", "Clasificación", "PuntajeSatisfacción", "Fuente"]));
        s.AddSingleton<IArchivoLector<ResenaWebRaw>>(_ =>
            new LectorCsv<ResenaWebRaw, ResenaWebRawMap>(
                ["IdReview", "IdCliente", "IdProducto", "Fecha", "Comentario", "Rating"]));
        s.AddSingleton<IArchivoLector<ComentarioSocialRaw>>(_ =>
            new LectorCsv<ComentarioSocialRaw, ComentarioSocialRawMap>(
                ["IdComment", "IdCliente", "IdProducto", "Fuente", "Fecha", "Comentario"]));

        // Repositorios (ADO.NET)
        s.AddTransient<IClienteRepository, ClienteRepository>();
        s.AddTransient<IProductoRepository, ProductoRepository>();
        s.AddTransient<IFuenteDatosRepository, FuenteDatosRepository>();
        s.AddTransient<IEncuestaRepository, EncuestaRepository>();
        s.AddTransient<IResenaWebRepository, ResenaWebRepository>();
        s.AddTransient<IComentarioSocialRepository, ComentarioSocialRepository>();
        s.AddTransient<ICatalogoRepository, CatalogoRepository>();
        s.AddTransient<IRechazoRepository, RechazoRepository>();
        s.AddTransient<IMantenimientoRepository, MantenimientoRepository>();

        // Servicios
        s.AddTransient<FuenteDatosService>();
        s.AddTransient<ClienteService>();
        s.AddTransient<ProductoService>();
        s.AddTransient<ResenaWebService>();
        s.AddTransient<ComentarioSocialService>();
        s.AddTransient<EncuestaService>();
        s.AddTransient<EtlOrquestador>();

        return s;
    }
}
