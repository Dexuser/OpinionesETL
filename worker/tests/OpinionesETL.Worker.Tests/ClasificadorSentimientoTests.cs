using OpinionesETL.Worker.Application.Transform;
using Xunit;

namespace OpinionesETL.Worker.Tests;

public class ClasificadorSentimientoTests
{
    [Theory]
    [InlineData(1, Sentimiento.Negativa)]
    [InlineData(2, Sentimiento.Negativa)]
    [InlineData(3, Sentimiento.Neutra)]
    [InlineData(4, Sentimiento.Positiva)]
    [InlineData(5, Sentimiento.Positiva)]
    public void PorPuntaje(int puntaje, Sentimiento esperado) =>
        Assert.Equal(esperado, ClasificadorSentimiento.PorPuntaje((short)puntaje));

    [Theory]
    [InlineData("Muy mala calidad, se rompió rápido", Sentimiento.Negativa)]
    [InlineData("Me ENCANTA, excelente producto", Sentimiento.Positiva)]
    [InlineData("Información suficiente, sin mayor novedad", Sentimiento.Neutra)]
    [InlineData("Pésimo, llegó dañado", Sentimiento.Negativa)]   // con tildes
    public void PorTexto(string comentario, Sentimiento esperado) =>
        Assert.Equal(esperado, ClasificadorSentimiento.PorTexto(comentario));

    [Fact]
    public void PorTexto_EmpateGanaLaNegativa() =>
        Assert.Equal(Sentimiento.Negativa, ClasificadorSentimiento.PorTexto("excelente pero muy mala entrega"));

    [Fact]
    public void PorTexto_NuloEsNeutra() =>
        Assert.Equal(Sentimiento.Neutra, ClasificadorSentimiento.PorTexto(null));

    [Theory]
    [InlineData("Positiva")]
    [InlineData("neutra")]
    [InlineData(" NEGATIVA ")]
    public void TryParsear_NombresValidos(string texto) =>
        Assert.True(ClasificadorSentimiento.TryParsear(texto, out _));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Xyz")]
    [InlineData("1")]     // un número no es un nombre de sentimiento
    [InlineData("-1")]
    public void TryParsear_InvalidosNoSeAceptan(string? texto) =>
        Assert.False(ClasificadorSentimiento.TryParsear(texto, out _));
}
