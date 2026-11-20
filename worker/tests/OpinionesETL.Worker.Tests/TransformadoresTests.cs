using OpinionesETL.Worker.Application.Raw;
using OpinionesETL.Worker.Application.Transform;
using OpinionesETL.Worker.Domain.Entities;
using Xunit;

namespace OpinionesETL.Worker.Tests;

public class TransformadoresTests
{
    private static EncuestaRaw Encuesta() => new()
    {
        IdOpinion = "7", IdCliente = "12", IdProducto = "34", Fecha = "2025-03-01",
        Comentario = "  Excelente   producto ", Clasificacion = "Positiva", Puntaje = "5", Fuente = "EncuestaInterna"
    };

    [Fact]
    public void Encuesta_Valida()
    {
        var r = EncuestaTransformador.Transformar(Encuesta());
        Assert.True(r.IsSuccess);
        Assert.Equal(Origenes.Encuesta, r.Data!.Origen);
        Assert.Equal("7", r.Data.IdOrigen);
        Assert.Equal(12, r.Data.IdCliente);
        Assert.Equal("Excelente producto", r.Data.Comentario);
        Assert.Equal((short)5, r.Data.Puntaje);
        Assert.Equal("Positiva", r.Data.Sentimiento);
    }

    [Fact]
    public void Encuesta_ClasificacionInconsistenteConPuntajeSeRechaza()
    {
        var raw = Encuesta(); raw.Clasificacion = "Positiva"; raw.Puntaje = "1";
        var r = EncuestaTransformador.Transformar(raw);
        Assert.True(r.IsFailure);
        Assert.Contains("inconsistente", r.Error!.Message);
    }

    [Fact]
    public void Encuesta_AcumulaTodosLosErrores()
    {
        var raw = Encuesta(); raw.Fecha = "mala"; raw.Comentario = ""; raw.IdCliente = "0";
        var msg = EncuestaTransformador.Transformar(raw).Error!.Message;
        Assert.Contains("Fecha", msg);
        Assert.Contains("Comentario", msg);
        Assert.Contains("IdCliente", msg);
    }

    [Fact]
    public void Encuesta_ClasificacionNumericaSeRechaza()
    {
        var raw = Encuesta(); raw.Clasificacion = "1";
        Assert.True(EncuestaTransformador.Transformar(raw).IsFailure);
    }

    [Fact]
    public void Web_SinSentimientoLoDerivaDelRating()
    {
        var r = ResenaWebTransformador.Transformar(new ResenaWebRaw
        { IdReview = "W0001", IdCliente = 7, IdProducto = 16, Fecha = new DateOnly(2024, 10, 23), Comentario = "Llegó rápido", Rating = 2 });
        Assert.Equal("Negativa", r.Data!.Sentimiento);
        Assert.Equal("Web", r.Data.Canal);
        Assert.Equal((short)2, r.Data.Puntaje);
    }

    [Fact]
    public void Web_ConSentimientoExistenteLoRespeta()
    {
        var r = ResenaWebTransformador.Transformar(new ResenaWebRaw
        { IdReview = "W0002", IdCliente = 7, IdProducto = 16, Fecha = new DateOnly(2024, 10, 23), Comentario = "Ok", Rating = 2, Sentimiento = "Positiva" });
        Assert.Equal("Positiva", r.Data!.Sentimiento);
    }

    [Fact]
    public void Social_AnonimoEsValidoYNoTienePuntaje()
    {
        var r = ComentarioSocialTransformador.Transformar(new ComentarioSocialRaw
        { IdComment = "T0002", IdCliente = null, IdProducto = 6, RedSocial = "Twitter", Fecha = "2025-08-27", Comentario = "Muy mala calidad, se rompió rápido" });
        Assert.True(r.IsSuccess);
        Assert.Null(r.Data!.IdCliente);
        Assert.Null(r.Data.Puntaje);
        Assert.Equal("Negativa", r.Data.Sentimiento);
        Assert.Equal("Twitter", r.Data.Canal);
    }

    [Fact]
    public void Social_ClienteInformadoPeroInvalidoSeRechaza()
    {
        var r = ComentarioSocialTransformador.Transformar(new ComentarioSocialRaw
        { IdComment = "T1", IdCliente = 0, IdProducto = 6, RedSocial = "Twitter", Fecha = "2025-08-27", Comentario = "ok" });
        Assert.True(r.IsFailure);
    }
}
