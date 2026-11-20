using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpinionesETL.Application.Abstractions;
using OpinionesETL.Application.Common;
using OpinionesETL.Application.Models;
using OpinionesETL.Domain.Entities;
using OpinionesETL.Domain.Repositories;

namespace OpinionesETL.Application.Services;

public interface IServicioImportacion
{
    string NombreArchivo { get; }
    Task<Result<ResumenArchivo>> ImportarAsync(string carpeta, CancellationToken ct = default);
}

/// <summary>
/// Flujo común de cada archivo: leer -> validar estructura -> limpiar/transformar -> detectar duplicados
/// -> insertar -> registrar rechazos. Cada servicio concreto solo define cómo transformar una fila.
/// </summary>
public abstract class ServicioImportacion<TRaw, TEntidad>(
    IArchivoLector<TRaw> lector,
    IRepositorioCarga<TEntidad> repositorio,
    IRechazoRepository repositorioRechazos) : IServicioImportacion
    where TRaw : class
{
    private static readonly JsonSerializerOptions JsonOpciones = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly Regex Variables = new(@"'[^']*'|\d+", RegexOptions.Compiled);

    public abstract string NombreArchivo { get; }

    /// <summary>Id tal como viene en el CSV (para identificar la fila en los rechazos).</summary>
    protected abstract string IdRaw(TRaw raw);

    /// <summary>Clave de negocio de la entidad ya limpia (para detectar duplicados dentro del archivo).</summary>
    protected abstract string Clave(TEntidad entidad);

    protected abstract Result<TEntidad> Transformar(TRaw raw);

    /// <summary>Carga catálogos y conjuntos de ids existentes antes de transformar.</summary>
    protected virtual Task PrepararAsync(CancellationToken ct) => Task.CompletedTask;

    public async Task<Result<ResumenArchivo>> ImportarAsync(string carpeta, CancellationToken ct = default)
    {
        var ruta = Path.Combine(carpeta, NombreArchivo);
        try
        {
            var lectura = lector.Leer(ruta);
            await PrepararAsync(ct);

            var rechazados = new List<RegistroRechazado>();
            foreach (var invalida in lectura.Invalidas)
                rechazados.Add(Rechazo(null, invalida.Numero, invalida.Motivo, new { fila = invalida.Numero }));

            var validos = new List<Valido>();
            var claves = new HashSet<string>();
            foreach (var fila in lectura.Filas)
            {
                var resultado = Transformar(fila.Datos);
                if (resultado.IsFailure)
                {
                    rechazados.Add(Rechazo(IdRaw(fila.Datos), fila.Numero, resultado.Error!.Message, fila.Datos));
                    continue;
                }

                var entidad = resultado.Data!;
                var clave = Clave(entidad);
                if (!claves.Add(clave))
                {
                    rechazados.Add(Rechazo(IdRaw(fila.Datos), fila.Numero, $"Duplicado en el archivo: clave '{clave}' repetida", fila.Datos));
                    continue;
                }

                validos.Add(new Valido(entidad, fila.Datos, fila.Numero));
            }

            var insercion = await repositorio.InsertarAsync(validos.Select(v => v.Entidad).ToList(), ct);
            foreach (var indice in insercion.IndicesDuplicados)
            {
                var v = validos[indice];
                rechazados.Add(Rechazo(IdRaw(v.Raw), v.Fila,
                    "Duplicado en la base de datos: ya existe la clave o un campo único", v.Raw));
            }

            await repositorioRechazos.InsertarAsync(rechazados, ct);

            var motivos = rechazados
                .GroupBy(r => Variables.Replace(r.Motivo, "#"))
                .ToDictionary(g => g.Key, g => g.Count());

            return Result.Success(new ResumenArchivo(
                NombreArchivo,
                lectura.Filas.Count + lectura.Invalidas.Count,
                insercion.Insertados,
                rechazados.Count,
                motivos));
        }
        catch (EstructuraArchivoInvalidaException ex)
        {
            return new Error(ex.Message);
        }
        catch (FileNotFoundException ex)
        {
            return new Error(ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new Error($"Error inesperado procesando '{NombreArchivo}': {ex.Message}");
        }
    }

    private RegistroRechazado Rechazo(string? id, int fila, string motivo, object datos) =>
        new(NombreArchivo,
            string.IsNullOrWhiteSpace(id) ? $"fila {fila}" : id,
            motivo,
            JsonSerializer.Serialize(datos, datos.GetType(), JsonOpciones));

    private sealed record Valido(TEntidad Entidad, TRaw Raw, int Fila);
}
