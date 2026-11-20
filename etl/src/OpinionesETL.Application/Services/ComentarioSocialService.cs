using OpinionesETL.Application.Abstractions;
using OpinionesETL.Application.Common;
using OpinionesETL.Application.Raw;
using OpinionesETL.Application.Transform;
using OpinionesETL.Domain.Entities;
using OpinionesETL.Domain.Repositories;

namespace OpinionesETL.Application.Services;

public sealed class ComentarioSocialService(
    IArchivoLector<ComentarioSocialRaw> lector,
    IComentarioSocialRepository repositorio,
    IRechazoRepository rechazos,
    IClienteRepository clientes,
    IProductoRepository productos,
    ICatalogoRepository catalogos)
    : ServicioImportacion<ComentarioSocialRaw, ComentarioSocial>(lector, repositorio, rechazos)
{
    private HashSet<int> _clientes = [];
    private HashSet<int> _productos = [];
    private Dictionary<string, short> _redes = [];
    private Dictionary<string, short> _clasificaciones = [];

    public override string NombreArchivo => "social_comments.csv";
    protected override string IdRaw(ComentarioSocialRaw raw) => raw.IdComment;
    protected override string Clave(ComentarioSocial e) => e.IdComment;

    protected override async Task PrepararAsync(CancellationToken ct)
    {
        _clientes = await clientes.ObtenerIdsAsync(ct);
        _productos = await productos.ObtenerIdsAsync(ct);
        _redes = await catalogos.ObtenerRedesSocialesAsync(ct);
        _clasificaciones = await catalogos.ObtenerClasificacionesAsync(ct);
    }

    protected override Result<ComentarioSocial> Transformar(ComentarioSocialRaw raw)
    {
        var errores = new Errores();
        var id = Limpieza.CodigoFijo(raw.IdComment, "IdComment", 5);
        var producto = Limpieza.Id(raw.IdProducto, "IdProducto", "P");
        var red = Limpieza.TextoRequerido(raw.Fuente, "Fuente", 30);
        var fecha = Limpieza.Fecha(raw.Fecha, "Fecha");
        var comentario = Limpieza.TextoRequerido(raw.Comentario, "Comentario", 500);
        errores.Agregar(id, producto, red, fecha, comentario);

        // El cliente es opcional: un comentario anónimo se guarda con id_cliente NULL.
        int? idCliente = null;
        if (!string.IsNullOrWhiteSpace(raw.IdCliente))
        {
            var cliente = Limpieza.Id(raw.IdCliente, "IdCliente", "C");
            errores.Agregar(cliente);
            if (cliente.IsSuccess)
            {
                if (_clientes.Contains(cliente.Data)) idCliente = cliente.Data;
                else errores.Agregar($"IdCliente {cliente.Data} no existe en cliente");
            }
        }

        if (producto.IsSuccess && !_productos.Contains(producto.Data))
            errores.Agregar($"IdProducto {producto.Data} no existe en producto");

        short idRed = 0;
        if (red.IsSuccess && !_redes.TryGetValue(red.Data!, out idRed))
            errores.Agregar($"Red social '{red.Data}' no existe en el catálogo");

        short idClasificacion = 0;
        if (comentario.IsSuccess)
        {
            var sentimiento = ClasificadorSentimiento.PorTexto(comentario.Data);
            if (!_clasificaciones.TryGetValue(sentimiento.ToString(), out idClasificacion))
                errores.Agregar($"Clasificación '{sentimiento}' no existe en el catálogo");
        }

        if (errores.Hay) return errores.ComoError();
        return Result.Success(new ComentarioSocial(
            id.Data!, idCliente, producto.Data, idRed, fecha.Data, comentario.Data!, idClasificacion));
    }
}
