namespace OpinionesETL.Application.Models;

public sealed record ResumenArchivo(
    string Archivo,
    int Procesados,
    int Insertados,
    int Rechazados,
    IReadOnlyDictionary<string, int> MotivosRechazo);

public sealed class ResumenEtl
{
    public DateTime Inicio { get; init; }
    public DateTime Fin { get; set; }
    public List<ResumenArchivo> Archivos { get; } = [];
    public List<string> Errores { get; } = [];

    public int TotalProcesados => Archivos.Sum(a => a.Procesados);
    public int TotalInsertados => Archivos.Sum(a => a.Insertados);
    public int TotalRechazados => Archivos.Sum(a => a.Rechazados);
    public TimeSpan Duracion => Fin - Inicio;
}
