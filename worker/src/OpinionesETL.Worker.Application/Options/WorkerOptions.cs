namespace OpinionesETL.Worker.Application.Options;

public sealed class WorkerOptions
{
    public const string Seccion = "Worker";

    /// <summary>"UnaVez" ejecuta el ETL y termina; "Periodico" lo repite cada IntervaloMinutos.</summary>
    public string Modo { get; set; } = "UnaVez";

    public int IntervaloMinutos { get; set; } = 60;

    public bool EsPeriodico => string.Equals(Modo, "Periodico", StringComparison.OrdinalIgnoreCase);
}
