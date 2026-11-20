using OpinionesETL.Worker.Host;
using OpinionesETL.Worker.Infrastructure;
using Serilog;

// Raíz de composición: aquí (y solo aquí) se conectan las capas.
var builder = Host.CreateApplicationBuilder(args);

// Serilog reemplaza al logging por defecto; sinks y niveles se configuran en appsettings.json (sección Serilog).
builder.Services.AddSerilog((_, cfg) => cfg.ReadFrom.Configuration(builder.Configuration));

builder.Services.AddWorkerEtl(builder.Configuration);
builder.Services.AddHostedService<Worker>();

await builder.Build().RunAsync();
