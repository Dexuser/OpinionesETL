using OpinionesETL.Application.Abstractions;
using OpinionesETL.Application.Common;
using OpinionesETL.Application.Raw;
using OpinionesETL.Application.Transform;
using OpinionesETL.Domain.Entities;
using OpinionesETL.Domain.Repositories;

namespace OpinionesETL.Application.Services;

public sealed class EncuestaService(
    IArchivoLector<EncuestaRaw> lector,
    IEncuestaRepository repositorio,
    IRechazoRepository rechazos,
    IClienteRepository clientes,
    IProductoRepository productos,
    ICatalogoRepository catalogos)
    : ServicioImportacion<EncuestaRaw, Encuesta>(lector, repositorio, rechazos)
{
    private HashSet<int> _clientes = [];
    private HashSet<int> _productos = [];
    private Dictionary<string, short> _clasificaciones = [];

    public override string NombreArchivo => "surveys_part1.csv";
    protected override string IdRaw(EncuestaRaw raw) => raw.IdOpinion;
    protected override string Clave(Encuesta e) => e.IdOpinion.ToString();

    protected override async Task PrepararAsync(CancellationToken ct)
    {
        _clientes = await clientes.ObtenerIdsAsync(ct);
        _productos = await productos.ObtenerIdsAsync(ct);
        _clasificaciones = await catalogos.ObtenerClasificacionesAsync(ct);
    }

    protected override Result<Encuesta> Transformar(EncuestaRaw raw)
    {
        var errores = new Errores();
        var id = Limpieza.Id(raw.IdOpinion, "IdOpinion");
        var cliente = Limpieza.Id(raw.IdCliente, "IdCliente", "C");
        var producto = Limpieza.Id(raw.IdProducto, "IdProducto", "P");
        var fecha = Limpieza.Fecha(raw.Fecha, "Fecha");
        var comentario = Limpieza.TextoRequerido(raw.Comentario, "Comentario", 500);
        var puntaje = Limpieza.Puntaje(raw.Puntaje, "PuntajeSatisfacción");
        var fuente = Limpieza.TextoRequerido(raw.Fuente, "Fuente", 30);
        errores.Agregar(id, cliente, producto, fecha, comentario, puntaje, fuente);

        if (cliente.IsSuccess && !_clientes.Contains(cliente.Data))
            errores.Agregar($"IdCliente {cliente.Data} no existe en cliente");
        if (producto.IsSuccess && !_productos.Contains(producto.Data))
            errores.Agregar($"IdProducto {producto.Data} no existe en producto");

        // La clasificación del CSV debe existir en el catálogo y ser coherente con el puntaje.
        short idClasificacion = 0;
        if (!Enum.TryParse<Sentimiento>(Limpieza.Texto(raw.Clasificacion), ignoreCase: true, out var sentimiento)
            || !Enum.IsDefined(sentimiento))
        {
            errores.Agregar($"Clasificación inválida: '{raw.Clasificacion}'");
        }
        else if (puntaje.IsSuccess && sentimiento != ClasificadorSentimiento.PorPuntaje(puntaje.Data))
        {
            errores.Agregar($"Clasificación '{sentimiento}' inconsistente con el puntaje {puntaje.Data}");
        }
        else if (!_clasificaciones.TryGetValue(sentimiento.ToString(), out idClasificacion))
        {
            errores.Agregar($"Clasificación '{sentimiento}' no existe en el catálogo");
        }

        if (errores.Hay) return errores.ComoError();
        return Result.Success(new Encuesta(
            id.Data, cliente.Data, producto.Data, fecha.Data, comentario.Data!,
            idClasificacion, puntaje.Data, fuente.Data!));
    }
}
