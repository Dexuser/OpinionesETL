using OpinionesETL.Application.Models;
using OpinionesETL.Domain.Repositories;

namespace OpinionesETL.Application.Services;

/// <summary>Ejecuta el ETL completo respetando el orden de las claves foráneas.</summary>
public sealed class EtlOrquestador(
    FuenteDatosService fuentes,
    ClienteService clientes,
    ProductoService productos,
    ResenaWebService resenas,
    ComentarioSocialService sociales,
    EncuestaService encuestas,
    IMantenimientoRepository mantenimiento)
{
    public async Task<ResumenEtl> EjecutarAsync(string carpeta, bool reiniciar, CancellationToken ct = default)
    {
        var resumen = new ResumenEtl { Inicio = DateTime.Now };

        if (reiniciar)
        {
            try
            {
                await mantenimiento.ReiniciarCargaAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                resumen.Errores.Add($"No se pudo reiniciar la carga: {ex.Message}");
                resumen.Fin = DateTime.Now;
                return resumen;
            }
        }

        // Primero los padres (catálogos de datos), luego las tablas que los referencian.
        IServicioImportacion[] pasos = [fuentes, clientes, productos, resenas, sociales, encuestas];
        foreach (var paso in pasos)
        {
            var resultado = await paso.ImportarAsync(carpeta, ct);
            if (resultado.IsSuccess) resumen.Archivos.Add(resultado.Data!);
            else resumen.Errores.Add($"{paso.NombreArchivo}: {resultado.Error!.Message}");
        }

        try
        {
            await mantenimiento.ClasificarPorPuntajeAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            resumen.Errores.Add($"No se pudo ejecutar sp_clasificar_por_puntaje: {ex.Message}");
        }

        resumen.Fin = DateTime.Now;
        return resumen;
    }
}
