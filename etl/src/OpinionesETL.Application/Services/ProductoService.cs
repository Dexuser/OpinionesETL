using OpinionesETL.Application.Abstractions;
using OpinionesETL.Application.Common;
using OpinionesETL.Application.Raw;
using OpinionesETL.Application.Transform;
using OpinionesETL.Domain.Entities;
using OpinionesETL.Domain.Repositories;

namespace OpinionesETL.Application.Services;

public sealed class ProductoService(
    IArchivoLector<ProductoRaw> lector,
    IProductoRepository repositorio,
    IRechazoRepository rechazos,
    ICatalogoRepository catalogos)
    : ServicioImportacion<ProductoRaw, Producto>(lector, repositorio, rechazos)
{
    private Dictionary<string, short> _categorias = [];

    public override string NombreArchivo => "products.csv";
    protected override string IdRaw(ProductoRaw raw) => raw.IdProducto;
    protected override string Clave(Producto e) => e.IdProducto.ToString();

    protected override async Task PrepararAsync(CancellationToken ct) =>
        _categorias = await catalogos.ObtenerCategoriasAsync(ct);

    protected override Result<Producto> Transformar(ProductoRaw raw)
    {
        var errores = new Errores();
        var id = Limpieza.Id(raw.IdProducto, "IdProducto", "P");
        var nombre = Limpieza.TextoRequerido(raw.Nombre, "Nombre", 100);
        var categoria = Limpieza.TextoRequerido(raw.Categoria, "Categoría", 50);
        errores.Agregar(id, nombre, categoria);

        short idCategoria = 0;
        if (categoria.IsSuccess && !_categorias.TryGetValue(categoria.Data!, out idCategoria))
            errores.Agregar($"Categoría '{categoria.Data}' no existe en el catálogo");

        if (errores.Hay) return errores.ComoError();
        return Result.Success(new Producto(id.Data, nombre.Data!, idCategoria));
    }
}
