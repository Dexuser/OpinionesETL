using OpinionesETL.Application.Abstractions;
using OpinionesETL.Application.Common;
using OpinionesETL.Application.Raw;
using OpinionesETL.Application.Transform;
using OpinionesETL.Domain.Entities;
using OpinionesETL.Domain.Repositories;

namespace OpinionesETL.Application.Services;

/// <summary>
/// Las reseñas web no traen clasificación: se insertan con id_clasificacion NULL y el procedimiento
/// almacenado sp_clasificar_por_puntaje la completa a partir del rating al final de la carga.
/// </summary>
public sealed class ResenaWebService(
    IArchivoLector<ResenaWebRaw> lector,
    IResenaWebRepository repositorio,
    IRechazoRepository rechazos,
    IClienteRepository clientes,
    IProductoRepository productos)
    : ServicioImportacion<ResenaWebRaw, ResenaWeb>(lector, repositorio, rechazos)
{
    private HashSet<int> _clientes = [];
    private HashSet<int> _productos = [];

    public override string NombreArchivo => "web_reviews.csv";
    protected override string IdRaw(ResenaWebRaw raw) => raw.IdReview;
    protected override string Clave(ResenaWeb e) => e.IdReview;

    protected override async Task PrepararAsync(CancellationToken ct)
    {
        _clientes = await clientes.ObtenerIdsAsync(ct);
        _productos = await productos.ObtenerIdsAsync(ct);
    }

    protected override Result<ResenaWeb> Transformar(ResenaWebRaw raw)
    {
        var errores = new Errores();
        var id = Limpieza.CodigoFijo(raw.IdReview, "IdReview", 5);
        var cliente = Limpieza.Id(raw.IdCliente, "IdCliente", "C");
        var producto = Limpieza.Id(raw.IdProducto, "IdProducto", "P");
        var fecha = Limpieza.Fecha(raw.Fecha, "Fecha");
        var comentario = Limpieza.TextoRequerido(raw.Comentario, "Comentario", 500);
        var rating = Limpieza.Puntaje(raw.Rating, "Rating");
        errores.Agregar(id, cliente, producto, fecha, comentario, rating);

        if (cliente.IsSuccess && !_clientes.Contains(cliente.Data))
            errores.Agregar($"IdCliente {cliente.Data} no existe en cliente");
        if (producto.IsSuccess && !_productos.Contains(producto.Data))
            errores.Agregar($"IdProducto {producto.Data} no existe en producto");

        if (errores.Hay) return errores.ComoError();
        return Result.Success(new ResenaWeb(
            id.Data!, cliente.Data, producto.Data, fecha.Data, comentario.Data!, rating.Data));
    }
}
