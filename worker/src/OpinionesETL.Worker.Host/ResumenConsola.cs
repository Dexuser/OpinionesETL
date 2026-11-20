using OpinionesETL.Worker.Application.Models;

namespace OpinionesETL.Worker.Host;

/// <summary>Resumen legible de una ejecución (lo que se muestra en consola y queda como evidencia).</summary>
public static class ResumenConsola
{
    public static void Imprimir(ResumenEjecucion r)
    {
        var o = Console.Out;
        o.WriteLine();
        o.WriteLine("============== RESUMEN DE EJECUCIÓN ETL ==============");
        o.WriteLine($"Inicio: {r.Inicio:yyyy-MM-dd HH:mm:ss}   Duración total: {r.Duracion.TotalSeconds:F2} s");
        o.WriteLine();
        o.WriteLine($"{"Fuente",-16}{"Leídos",8}{"Extraídos",11}{"Rechazados",12}{"Tiempo (ms)",13}{"Filas/s",10}");
        foreach (var e in r.Extracciones)
            o.WriteLine($"{e.Fuente,-16}{e.Leidos,8}{e.Extraidos,11}{e.Rechazados,12}{e.Duracion.TotalMilliseconds,13:F0}{e.FilasPorSegundo,10:F0}");

        if (r.Carga is { } c)
        {
            o.WriteLine();
            o.WriteLine("Carga al almacén (dw):");
            o.WriteLine($"  Opiniones en staging ............ {c.OpinionesEnStaging}");
            o.WriteLine($"  Rechazadas por integridad ....... {c.RechazadosIntegridad}  (ver stg.rechazo, etapa 'Carga')");
            o.WriteLine($"  Hechos nuevos en dw ............. {c.HechosNuevos}");
            o.WriteLine($"  Ya existentes (idempotencia) .... {c.YaExistentes}");
            o.WriteLine($"  Días nuevos en dim_tiempo ....... {c.DiasNuevos}");
            o.WriteLine($"  Fuentes nuevas en dim_fuente .... {c.FuentesNuevas}");
            o.WriteLine($"  Duración de la carga ............ {c.Duracion.TotalMilliseconds:F0} ms");
        }

        o.WriteLine();
        if (r.Errores.Count == 0) o.WriteLine("Estado: EXITOSO");
        else
        {
            o.WriteLine("Estado: CON ERRORES");
            foreach (var e in r.Errores) o.WriteLine($"  - {e}");
        }
        o.WriteLine("======================================================");
    }
}
