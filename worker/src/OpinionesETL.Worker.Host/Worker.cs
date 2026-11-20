using Microsoft.Extensions.Options;
using OpinionesETL.Worker.Application.Options;
using OpinionesETL.Worker.Application.Services;

namespace OpinionesETL.Worker.Host;

/// <summary>
/// Servicio en segundo plano. En modo "UnaVez" ejecuta el ETL y cierra la aplicación con código de salida
/// (0 = éxito, 1 = hubo errores), útil para tareas programadas. En modo "Periodico" se repite cada IntervaloMinutos.
/// </summary>
public sealed class Worker(
    PipelineEtl pipeline,
    IOptions<WorkerOptions> opciones,
    IHostApplicationLifetime vida,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opt = opciones.Value;
        logger.LogInformation("Worker iniciado en modo {Modo}", opt.Modo);

        try
        {
            do
            {
                var resumen = await pipeline.EjecutarAsync(stoppingToken);
                ResumenConsola.Imprimir(resumen);
                Environment.ExitCode = resumen.Exitoso ? 0 : 1;

                if (!opt.EsPeriodico) break;

                logger.LogInformation("Próxima ejecución en {Min} minuto(s)", opt.IntervaloMinutos);
                await Task.Delay(TimeSpan.FromMinutes(opt.IntervaloMinutos), stoppingToken);
            }
            while (!stoppingToken.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Worker detenido");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Error no controlado en el Worker");
            Environment.ExitCode = 1;
        }

        if (!opt.EsPeriodico) vida.StopApplication();
    }
}
