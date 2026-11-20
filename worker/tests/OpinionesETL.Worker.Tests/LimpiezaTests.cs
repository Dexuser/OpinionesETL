using OpinionesETL.Worker.Application.Transform;
using Xunit;

namespace OpinionesETL.Worker.Tests;

public class LimpiezaTests
{
    [Fact]
    public void Texto_ColapsaEspaciosYRecorta() =>
        Assert.Equal("Muy bueno", Limpieza.Texto("  Muy   bueno \t"));

    [Fact]
    public void Texto_NuloDevuelveVacio() =>
        Assert.Equal("", Limpieza.Texto(null));

    [Fact]
    public void TextoRequerido_VacioFalla()
    {
        var r = Limpieza.TextoRequerido("   ", "Comentario", 10);
        Assert.True(r.IsFailure);
        Assert.Contains("Comentario es obligatorio", r.Error!.Message);
    }

    [Fact]
    public void TextoRequerido_ExcedeLongitudFalla() =>
        Assert.True(Limpieza.TextoRequerido(new string('a', 11), "Campo", 10).IsFailure);

    [Theory]
    [InlineData("C007", "C", 7)]
    [InlineData("c007", "C", 7)]
    [InlineData("42", "", 42)]
    [InlineData(" P016 ", "P", 16)]
    public void Id_ValidosSeNormalizan(string entrada, string prefijo, int esperado)
    {
        var r = Limpieza.Id(entrada, "Id", prefijo);
        Assert.True(r.IsSuccess);
        Assert.Equal(esperado, r.Data);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    public void Id_InvalidosFallan(string entrada) =>
        Assert.True(Limpieza.Id(entrada, "Id").IsFailure);

    [Fact]
    public void Fecha_Valida() =>
        Assert.Equal(new DateOnly(2025, 3, 1), Limpieza.Fecha("2025-03-01", "Fecha").Data);

    [Theory]
    [InlineData("fecha-mala")]
    [InlineData("01/03/2025")]
    [InlineData("")]
    [InlineData("1999-12-31")]
    [InlineData("2999-01-01")]
    public void Fecha_InvalidaOFueraDeRangoFalla(string entrada) =>
        Assert.True(Limpieza.Fecha(entrada, "Fecha").IsFailure);

    [Theory]
    [InlineData("1", 1)]
    [InlineData("5", 5)]
    public void Puntaje_Valido(string entrada, int esperado) =>
        Assert.Equal((short)esperado, Limpieza.Puntaje(entrada, "P").Data);

    [Theory]
    [InlineData("0")]
    [InlineData("6")]
    [InlineData("x")]
    [InlineData("")]
    public void Puntaje_FueraDeRangoFalla(string entrada) =>
        Assert.True(Limpieza.Puntaje(entrada, "P").IsFailure);
}
