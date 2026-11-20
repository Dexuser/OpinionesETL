using OpinionesETL.Application.Abstractions;
using OpinionesETL.Application.Common;
using OpinionesETL.Application.Raw;
using OpinionesETL.Application.Transform;
using OpinionesETL.Domain.Entities;
using OpinionesETL.Domain.Repositories;

namespace OpinionesETL.Application.Services;

public sealed class FuenteDatosService(
    IArchivoLector<FuenteDatosRaw> lector,
    IFuenteDatosRepository repositorio,
    IRechazoRepository rechazos,
    ICatalogoRepository catalogos)
    : ServicioImportacion<FuenteDatosRaw, FuenteDatos>(lector, repositorio, rechazos)
{
    private Dictionary<string, short> _tipos = [];

    public override string NombreArchivo => "fuente_datos.csv";
    protected override string IdRaw(FuenteDatosRaw raw) => raw.IdFuente;
    protected override string Clave(FuenteDatos e) => e.IdFuente;

    protected override async Task PrepararAsync(CancellationToken ct) =>
        _tipos = await catalogos.ObtenerTiposFuenteAsync(ct);

    protected override Result<FuenteDatos> Transformar(FuenteDatosRaw raw)
    {
        var errores = new Errores();
        var id = Limpieza.CodigoFijo(raw.IdFuente, "IdFuente", 4);
        var tipo = Limpieza.TextoRequerido(raw.TipoFuente, "TipoFuente", 30);
        var fecha = Limpieza.Fecha(raw.FechaCarga, "FechaCarga");
        errores.Agregar(id, tipo, fecha);

        short idTipo = 0;
        if (tipo.IsSuccess && !_tipos.TryGetValue(tipo.Data!, out idTipo))
            errores.Agregar($"TipoFuente '{tipo.Data}' no existe en el catálogo");

        if (errores.Hay) return errores.ComoError();
        return Result.Success(new FuenteDatos(id.Data!, idTipo, fecha.Data));
    }
}
