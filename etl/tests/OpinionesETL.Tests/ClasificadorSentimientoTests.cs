using OpinionesETL.Application.Transform;
using Xunit;

namespace OpinionesETL.Tests;

public class ClasificadorSentimientoTests
{
    [Theory]
    [InlineData(1, Sentimiento.Negativa)]
    [InlineData(2, Sentimiento.Negativa)]
    [InlineData(3, Sentimiento.Neutra)]
    [InlineData(4, Sentimiento.Positiva)]
    [InlineData(5, Sentimiento.Positiva)]
    public void PorPuntaje(short puntaje, Sentimiento esperado) =>
        Assert.Equal(esperado, ClasificadorSentimiento.PorPuntaje(puntaje));

    // Las 15 frases distintas que aparecen en social_comments.csv
    [Theory]
    [InlineData("Gran relación calidad-precio", Sentimiento.Positiva)]
    [InlineData("Calidad superior, muy contento", Sentimiento.Positiva)]
    [InlineData("Me encanta este producto, excelente calidad", Sentimiento.Positiva)]
    [InlineData("Producto llegó rápido y funciona perfecto", Sentimiento.Positiva)]
    [InlineData("Muy satisfecho con la compra, lo recomiendo", Sentimiento.Positiva)]
    [InlineData("Muy mala calidad, se rompió rápido", Sentimiento.Negativa)]
    [InlineData("Pésima atención al cliente", Sentimiento.Negativa)]
    [InlineData("Envío tardío y producto dañado", Sentimiento.Negativa)]
    [InlineData("No cumple con lo anunciado, insatisfecho", Sentimiento.Negativa)]
    [InlineData("No funciona como esperaba, decepcionado", Sentimiento.Negativa)]
    [InlineData("Entrega correcta, sin comentarios adicionales", Sentimiento.Neutra)]
    [InlineData("Es lo que esperaba, nada excepcional", Sentimiento.Neutra)]
    [InlineData("Información suficiente, sin mayor novedad", Sentimiento.Neutra)]
    [InlineData("Satisface lo básico", Sentimiento.Neutra)]
    [InlineData("Producto recibido, cumple su función", Sentimiento.Neutra)]
    public void PorTexto(string comentario, Sentimiento esperado) =>
        Assert.Equal(esperado, ClasificadorSentimiento.PorTexto(comentario));

    [Fact]
    public void Insatisfecho_no_se_confunde_con_satisfecho() =>
        Assert.Equal(Sentimiento.Negativa, ClasificadorSentimiento.PorTexto("Estoy insatisfecho"));
}
