namespace OpinionesETL.Worker.Application.Models;

public sealed class ContadorExtraccion
{
    public int Leidos { get; set; }
}

public sealed record ResumenExtraccion(string Fuente, int Leidos, int Extraidos, int Rechazados, TimeSpan Duracion)
{
    public double FilasPorSegundo => Duracion.TotalSeconds > 0 ? Leidos / Duracion.TotalSeconds : 0;
}

public sealed record ResultadoCargaDw(
    int OpinionesEnStaging,
    int DiasNuevos,
    int ProductosAfectados,
    int ClientesAfectados,
    int FuentesNuevas,
    int RechazadosIntegridad,
    int HechosNuevos,
    TimeSpan Duracion)
{
    /// <summary>Opiniones válidas que ya estaban en el almacén (la carga es idempotente).</summary>
    public int YaExistentes => Math.Max(0, OpinionesEnStaging - RechazadosIntegridad - HechosNuevos);
}

public sealed class ResumenEjecucion
{
    public DateTime Inicio { get; init; }
    public DateTime Fin { get; set; }
    public List<ResumenExtraccion> Extracciones { get; } = [];
    public List<string> Errores { get; } = [];
    public ResultadoCargaDw? Carga { get; set; }

    public TimeSpan Duracion => Fin - Inicio;
    public bool Exitoso => Errores.Count == 0;
}
