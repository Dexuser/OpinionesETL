using OpinionesETL.Application.Transform;
using Xunit;

namespace OpinionesETL.Tests;

public class LimpiezaTests
{
    [Theory]
    [InlineData("C007", "C", 7)]
    [InlineData("  c016 ", "C", 16)]
    [InlineData("42", "", 42)]
    [InlineData("P003", "P", 3)]
    public void Id_limpia_prefijo_y_espacios(string entrada, string prefijo, int esperado)
    {
        var r = Limpieza.Id(entrada, "IdX", prefijo);
        Assert.True(r.IsSuccess);
        Assert.Equal(esperado, r.Data);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    public void Id_invalido_falla(string entrada) =>
        Assert.True(Limpieza.Id(entrada, "IdX", "C").IsFailure);

    [Fact]
    public void Fecha_valida() =>
        Assert.Equal(new DateOnly(2025, 3, 14), Limpieza.Fecha("2025-03-14", "Fecha").Data);

    [Theory]
    [InlineData("14/03/2025")]
    [InlineData("2025-13-40")]
    [InlineData("")]
    [InlineData("2999-01-01")]   // futura
    public void Fecha_invalida_falla(string entrada) =>
        Assert.True(Limpieza.Fecha(entrada, "Fecha").IsFailure);

    [Theory]
    [InlineData("1", 1)]
    [InlineData(" 5 ", 5)]
    public void Puntaje_valido(string entrada, short esperado) =>
        Assert.Equal(esperado, Limpieza.Puntaje(entrada, "Puntaje").Data);

    [Theory]
    [InlineData("0")]
    [InlineData("6")]
    [InlineData("x")]
    [InlineData("")]
    public void Puntaje_fuera_de_rango_falla(string entrada) =>
        Assert.True(Limpieza.Puntaje(entrada, "Puntaje").IsFailure);

    [Fact]
    public void Texto_colapsa_espacios() =>
        Assert.Equal("Muy bueno", Limpieza.Texto("  Muy   bueno "));

    [Fact]
    public void Email_se_normaliza_a_minusculas() =>
        Assert.Equal("ana@correo.com", Limpieza.Email("  Ana@Correo.COM ").Data);

    [Theory]
    [InlineData("sin-arroba")]
    [InlineData("a@b")]
    [InlineData("")]
    public void Email_invalido_falla(string entrada) =>
        Assert.True(Limpieza.Email(entrada).IsFailure);

    [Fact]
    public void CodigoFijo_exige_longitud()
    {
        Assert.True(Limpieza.CodigoFijo("W0001", "IdReview", 5).IsSuccess);
        Assert.True(Limpieza.CodigoFijo("W001", "IdReview", 5).IsFailure);
    }
}
