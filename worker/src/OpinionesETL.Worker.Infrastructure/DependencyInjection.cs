using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpinionesETL.Worker.Application.Abstractions;
using OpinionesETL.Worker.Application.Options;
using OpinionesETL.Worker.Application.Services;
using OpinionesETL.Worker.Domain.Repositories;
using OpinionesETL.Worker.Infrastructure.Extractors;
using OpinionesETL.Worker.Infrastructure.Persistence;

namespace OpinionesETL.Worker.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registra todo lo necesario. Solo las fuentes habilitadas en appsettings se agregan al pipeline.</summary>
    public static IServiceCollection AddWorkerEtl(this IServiceCollection servicios, IConfiguration config)
    {
        servicios.Configure<WorkerOptions>(config.GetSection(WorkerOptions.Seccion));
        servicios.Configure<FuentesOptions>(config.GetSection(FuentesOptions.Seccion));
        servicios.Configure<ConexionesOptions>(config.GetSection(ConexionesOptions.Seccion));

        var fuentes = config.GetSection(FuentesOptions.Seccion).Get<FuentesOptions>() ?? new FuentesOptions();

        servicios.AddSingleton<IStagingRepository, StagingRepository>();
        servicios.AddSingleton<IDataLoader, DataLoader>();

        if (fuentes.Csv.Habilitado) servicios.AddSingleton<IExtractor, CsvExtractor>();
        if (fuentes.BaseOperacional.Habilitado) servicios.AddSingleton<IExtractor, DatabaseExtractor>();
        if (fuentes.Api.Habilitado)
        {
            servicios.AddSingleton<IExtractor, ApiExtractor>();
            servicios.AddHttpClient(ApiExtractor.NombreCliente, c =>
            {
                // La BaseUrl debe terminar en "/" para que la ruta relativa se agregue y no reemplace.
                c.BaseAddress = new Uri(fuentes.Api.BaseUrl.TrimEnd('/') + "/");
                c.Timeout = TimeSpan.FromSeconds(fuentes.Api.TimeoutSegundos);
                c.DefaultRequestHeaders.Add(fuentes.Api.NombreHeaderApiKey, fuentes.Api.ApiKey);
                c.DefaultRequestHeaders.Add("Accept", "application/json");
            });
        }

        servicios.AddSingleton<PipelineEtl>();
        return servicios;
    }
}
