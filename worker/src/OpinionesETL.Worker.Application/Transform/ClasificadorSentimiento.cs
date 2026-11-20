using System.Globalization;
using System.Text;

namespace OpinionesETL.Worker.Application.Transform;

/// <summary>Los nombres coinciden con los de dw.dim_sentimiento.</summary>
public enum Sentimiento { Positiva, Neutra, Negativa }

public static class ClasificadorSentimiento
{
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

    /// <summary>1-2 Negativa, 3 Neutra, 4-5 Positiva.</summary>
    public static Sentimiento PorPuntaje(short puntaje) => puntaje switch
    {
        <= 2 => Sentimiento.Negativa,
        3 => Sentimiento.Neutra,
        _ => Sentimiento.Positiva
    };

    /// <summary>Palabras clave: gana la negativa si hay más señales negativas que positivas (o empate).</summary>
    public static Sentimiento PorTexto(string? comentario)
    {
        var texto = SinTildes(comentario ?? "");
        var negativas = Negativas.Count(k => texto.Contains(k));
        var positivas = Positivas.Count(k => texto.Contains(k));

        if (negativas > 0 && negativas >= positivas) return Sentimiento.Negativa;
        if (positivas > 0) return Sentimiento.Positiva;
        return Sentimiento.Neutra;
    }

    /// <summary>Acepta solo nombres ("Positiva", "neutra"); un número como "1" NO se interpreta como valor del enum.</summary>
    public static bool TryParsear(string? texto, out Sentimiento sentimiento)
    {
        var limpio = Limpieza.Texto(texto);
        if (limpio.Length == 0 || char.IsDigit(limpio[0]) || limpio[0] == '-')
        {
            sentimiento = default;
            return false;
        }
        return Enum.TryParse(limpio, ignoreCase: true, out sentimiento) && Enum.IsDefined(sentimiento);
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
