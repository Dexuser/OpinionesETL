using OpinionesETL.Application.Abstractions;
using OpinionesETL.Application.Common;
using OpinionesETL.Application.Raw;
using OpinionesETL.Application.Transform;
using OpinionesETL.Domain.Entities;
using OpinionesETL.Domain.Repositories;

namespace OpinionesETL.Application.Services;

public sealed class ClienteService(
    IArchivoLector<ClienteRaw> lector,
    IClienteRepository repositorio,
    IRechazoRepository rechazos)
    : ServicioImportacion<ClienteRaw, Cliente>(lector, repositorio, rechazos)
{
    public override string NombreArchivo => "clients.csv";
    protected override string IdRaw(ClienteRaw raw) => raw.IdCliente;
    protected override string Clave(Cliente e) => e.IdCliente.ToString();

    protected override Result<Cliente> Transformar(ClienteRaw raw)
    {
        var errores = new Errores();
        var id = Limpieza.Id(raw.IdCliente, "IdCliente", "C");
        var nombre = Limpieza.TextoRequerido(raw.Nombre, "Nombre", 100);
        var email = Limpieza.Email(raw.Email);
        errores.Agregar(id, nombre, email);

        if (errores.Hay) return errores.ComoError();
        return Result.Success(new Cliente(id.Data, nombre.Data!, email.Data!));
    }
}
