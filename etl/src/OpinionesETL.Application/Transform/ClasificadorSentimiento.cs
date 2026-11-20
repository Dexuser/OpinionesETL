using System.Globalization;
using System.Text;

namespace OpinionesETL.Application.Transform;

/// <summary>Los nombres coinciden con los del catálogo clasificacion_sentimiento.</summary>
public enum Sentimiento { Positiva, Neutra, Negativa }

public static class ClasificadorSentimiento
{
    // Palabras clave sin tildes y en minúsculas (el texto se normaliza antes de buscar).
    private static readonly string[] Negativas =
    [
        "mala", "pesima", "pesimo", "tardio", "danado", "no cumple", "insatisfecho",
        "decepcionado", "no funciona", "no volveria", "se rompio", "defectuoso", "terrible", "horrible"
    ];

    private static readonly string[] Positivas =
    [
        "encanta", "excelente", "superior", "contento", "satisfecho", "recomiendo",
        "recomendable", "perfecto", "gran relacion", "genial", "fantastico", "increible"
    ];

    /// <summary>1-2 Negativa, 3 Neutra, 4-5 Positiva (la regla que ya siguen las encuestas).</summary>
    public static Sentimiento PorPuntaje(short puntaje) => puntaje switch
    {
        <= 2 => Sentimiento.Negativa,
        3 => Sentimiento.Neutra,
        _ => Sentimiento.Positiva
    };

    /// <summary>Clasifica por palabras clave. Si hay más señales negativas que positivas (o empate), gana la negativa.</summary>
    public static Sentimiento PorTexto(string? comentario)
    {
        var texto = SinTildes(comentario ?? "");
        var negativas = Negativas.Count(k => texto.Contains(k));
        var positivas = Positivas.Count(k => texto.Contains(k));

        if (negativas > 0 && negativas >= positivas) return Sentimiento.Negativa;
        if (positivas > 0) return Sentimiento.Positiva;
        return Sentimiento.Neutra;
    }

    private static string SinTildes(string texto)
    {
        var descompuesto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString();
    }
}
