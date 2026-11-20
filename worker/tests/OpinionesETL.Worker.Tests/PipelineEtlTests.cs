using Microsoft.Extensions.Logging.Abstractions;
using OpinionesETL.Worker.Application.Abstractions;
using OpinionesETL.Worker.Application.Common;
using OpinionesETL.Worker.Application.Models;
using OpinionesETL.Worker.Application.Services;
using Xunit;

namespace OpinionesETL.Worker.Tests;

public class PipelineEtlTests
{
    private sealed class ExtractorFalso(string nombre, Func<Result<ResumenExtraccion>> accion) : IExtractor
    {
        public string Nombre => nombre;
        public Task<Result<ResumenExtraccion>> ExtraerAsync(CancellationToken ct = default) => Task.FromResult(accion());
    }

    private sealed class CargadorFalso(Func<ResultadoCargaDw>? accion = null) : IDataLoader
    {
        public int Llamadas { get; private set; }
        public Task<ResultadoCargaDw> CargarAsync(CancellationToken ct = default)
        {
            Llamadas++;
            return Task.FromResult(accion?.Invoke() ?? new ResultadoCargaDw(10, 1, 0, 0, 0, 0, 10, TimeSpan.Zero));
        }
    }

    private static Result<ResumenExtraccion> Ok(string f) => Result.Success(new ResumenExtraccion(f, 10, 10, 0, TimeSpan.FromSeconds(1)));

    private static PipelineEtl Crear(IEnumerable<IExtractor> ex, IDataLoader l) => new(ex, l, NullLogger<PipelineEtl>.Instance);

    [Fact]
    public async Task TodasLasFuentesOk_CargaUnaVezYEsExitoso()
    {
        var cargador = new CargadorFalso();
        var r = await Crear([new ExtractorFalso("A", () => Ok("A")), new ExtractorFalso("B", () => Ok("B"))], cargador).EjecutarAsync();
        Assert.True(r.Exitoso);
        Assert.Equal(2, r.Extracciones.Count);
        Assert.Equal(1, cargador.Llamadas);
        Assert.NotNull(r.Carga);
    }

    [Fact]
    public async Task UnaFuenteFalla_LasDemasSiguenYSeCarga()
    {
        var cargador = new CargadorFalso();
        var r = await Crear([new ExtractorFalso("A", () => Ok("A")), new ExtractorFalso("B", () => new Error("sin red"))], cargador).EjecutarAsync();
        Assert.False(r.Exitoso);
        Assert.Single(r.Extracciones);
        Assert.Contains("B: sin red", r.Errores[0]);
        Assert.Equal(1, cargador.Llamadas);
    }

    [Fact]
    public async Task ExtractorQueLanzaExcepcion_NoDetieneElPipeline()
    {
        var cargador = new CargadorFalso();
        var r = await Crear([new ExtractorFalso("A", () => Ok("A")), new ExtractorFalso("B", () => throw new InvalidOperationException("boom"))], cargador).EjecutarAsync();
        Assert.False(r.Exitoso);
        Assert.Contains("boom", r.Errores[0]);
        Assert.Equal(1, cargador.Llamadas);
    }

    [Fact]
    public async Task NingunaFuenteOk_NoSeCarga()
    {
        var cargador = new CargadorFalso();
        var r = await Crear([new ExtractorFalso("A", () => new Error("x"))], cargador).EjecutarAsync();
        Assert.False(r.Exitoso);
        Assert.Equal(0, cargador.Llamadas);
        Assert.Null(r.Carga);
    }

    [Fact]
    public async Task SinFuentesHabilitadas_ReportaError()
    {
        var r = await Crear([], new CargadorFalso()).EjecutarAsync();
        Assert.False(r.Exitoso);
        Assert.Contains("No hay fuentes", r.Errores[0]);
    }

    [Fact]
    public async Task FallaLaCarga_QuedaRegistradaComoError()
    {
        var cargador = new CargadorFalso(() => throw new InvalidOperationException("dw caído"));
        var r = await Crear([new ExtractorFalso("A", () => Ok("A"))], cargador).EjecutarAsync();
        Assert.False(r.Exitoso);
        Assert.Contains("dw caído", r.Errores[0]);
    }

    [Fact]
    public void ResultadoCarga_CalculaYaExistentes() =>
        Assert.Equal(706, new ResultadoCargaDw(1200, 0, 0, 0, 0, 494, 0, TimeSpan.Zero).YaExistentes);
}
