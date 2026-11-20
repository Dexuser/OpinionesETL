using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesETL.Worker.Application.Abstractions;
using OpinionesETL.Worker.Application.Common;
using OpinionesETL.Worker.Application.Models;
using OpinionesETL.Worker.Application.Options;
using OpinionesETL.Worker.Application.Raw;
using OpinionesETL.Worker.Application.Transform;
using OpinionesETL.Worker.Domain.Entities;
using OpinionesETL.Worker.Domain.Repositories;

namespace OpinionesETL.Worker.Infrastructure.Extractors;

/// <summary>
/// Fuente 3: API REST de comentarios sociales (contrato en docs/contrato-api-comentarios-sociales.yaml).
/// Usa un HttpClient nombrado creado por IHttpClientFactory: reutiliza conexiones y centraliza URL, timeout y API key.
/// Recorre la paginación; reintenta con espera exponencial ante fallos transitorios (red, 5xx, 429).
/// </summary>
public sealed class ApiExtractor(
    IHttpClientFactory fabrica,
    IOptions<FuentesOptions> opciones,
    IStagingRepository staging,
    ILogger<ApiExtractor> logger) : IExtractor
{
    public const string NombreCliente = "ComentariosSociales";
    public string Nombre => "ApiSocial";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ApiOptions _opt = opciones.Value.Api;

    public async Task<Result<ResumenExtraccion>> ExtraerAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(_opt.BaseUrl))
            return new Error("No está configurada la URL base de la API (Fuentes:Api:BaseUrl).");

        var contador = new ContadorExtraccion();
        var rechazos = new List<RegistroRechazado>();

        var extraidos = await staging.ReemplazarOpinionesAsync(Origenes.Social, LeerPaginasAsync(contador, rechazos, ct), ct);
        await staging.ReemplazarRechazosAsync(Origenes.Social, rechazos, ct);

        sw.Stop();
        var resumen = new ResumenExtraccion(Nombre, contador.Leidos, extraidos, rechazos.Count, sw.Elapsed);
        logger.LogInformation(
            "API: {Leidos} comentarios leídos, {Extraidos} extraídos, {Rechazados} rechazados, {Fps:F0} filas/s",
            resumen.Leidos, resumen.Extraidos, resumen.Rechazados, resumen.FilasPorSegundo);
        return Result.Success(resumen);
    }

    private async IAsyncEnumerable<OpinionStaging> LeerPaginasAsync(
        ContadorExtraccion contador,
        List<RegistroRechazado> rechazos,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var cliente = fabrica.CreateClient(NombreCliente);
        var vistos = new HashSet<string>();
        var pagina = 1;
        int totalPaginas;

        do
        {
            var datos = await PedirPaginaAsync(cliente, pagina, ct);
            totalPaginas = datos.TotalPaginas;
            logger.LogDebug("API página {Pagina}/{Total}: {N} comentarios", pagina, totalPaginas, datos.Datos.Count);

            foreach (var raw in datos.Datos)
            {
                contador.Leidos++;
                var r = ComentarioSocialTransformador.Transformar(raw);
                if (r.IsFailure)
                {
                    rechazos.Add(new RegistroRechazado(Origenes.Social, raw.IdComment, r.Error!.Message,
                        $"{raw.IdComment}|{raw.IdCliente}|{raw.IdProducto}|{raw.RedSocial}|{raw.Fecha}|{raw.Comentario}"));
                    continue;
                }
                if (!vistos.Add(r.Data!.IdOrigen))
                {
                    rechazos.Add(new RegistroRechazado(Origenes.Social, r.Data.IdOrigen, "IdComment duplicado en la API", raw.Comentario));
                    continue;
                }
                yield return r.Data;
            }
            pagina++;
        }
        while (pagina <= totalPaginas);
    }

    private async Task<PaginaComentariosRaw> PedirPaginaAsync(HttpClient cliente, int pagina, CancellationToken ct)
    {
        var url = $"{_opt.Ruta.TrimStart('/')}?pagina={pagina}&tamanio={_opt.TamanioPagina}";

        for (var intento = 1; ; intento++)
        {
            try
            {
                using var resp = await cliente.GetAsync(url, ct);

                if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    throw new ApiAutenticacionException($"La API rechazó las credenciales (HTTP {(int)resp.StatusCode}). Revise Fuentes:Api:ApiKey.");

                if ((int)resp.StatusCode >= 500 || resp.StatusCode == HttpStatusCode.TooManyRequests)
                    throw new HttpRequestException($"Error transitorio de la API: HTTP {(int)resp.StatusCode}", null, resp.StatusCode);

                resp.EnsureSuccessStatusCode();
                var cuerpo = await resp.Content.ReadFromJsonAsync<PaginaComentariosRaw>(Json, ct);
                return cuerpo ?? throw new InvalidOperationException("La API devolvió un cuerpo vacío.");
            }
            catch (Exception ex) when (EsTransitorio(ex, ct) && intento <= _opt.Reintentos)
            {
                var espera = TimeSpan.FromMilliseconds(_opt.EsperaInicialMs * Math.Pow(2, intento - 1));
                logger.LogWarning("API página {Pagina}: intento {Intento}/{Max} falló ({Motivo}). Reintento en {Espera} ms",
                    pagina, intento, _opt.Reintentos + 1, ex.Message, (int)espera.TotalMilliseconds);
                await Task.Delay(espera, ct);
            }
        }
    }

    // Reintentamos errores de red y timeouts; NO reintentamos credenciales inválidas ni cancelaciones del usuario.
    private static bool EsTransitorio(Exception ex, CancellationToken ct) =>
        !ct.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException;
}
