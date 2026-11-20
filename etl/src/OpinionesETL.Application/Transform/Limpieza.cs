using System.Text.RegularExpressions;
using OpinionesETL.Application.Common;

namespace OpinionesETL.Application.Transform;

public static class Limpieza
{
    private static readonly Regex Espacios = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex FormatoEmail = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private static readonly DateOnly FechaMinima = new(2000, 1, 1);

    /// <summary>"  Muy   bueno " -> "Muy bueno"</summary>
    public static string Texto(string? valor) =>
        Espacios.Replace((valor ?? "").Trim(), " ");

    public static Result<string> TextoRequerido(string? valor, string campo, int maxLongitud)
    {
        var s = Texto(valor);
        if (s.Length == 0)
            return new Error($"{campo} es obligatorio");
        if (s.Length > maxLongitud)
            return new Error($"{campo} excede {maxLongitud} caracteres");

        return Result.Success(s);
    }

    /// <summary>Código de texto con longitud fija (por ejemplo F001 o W0001), como las columnas CHAR(n).</summary>
    public static Result<string> CodigoFijo(string? valor, string campo, int longitud)
    {
        var s = Texto(valor);
        if (s.Length == 0)
            return new Error($"{campo} es obligatorio");
        if (s.Length != longitud)
            return new Error($"{campo} debe tener {longitud} caracteres: '{s}'");

        return Result.Success(s);
    }

    /// <summary>"C007" -> 7, "42" -> 42. Falla si está vacío, no es numérico o no es positivo.</summary>
    public static Result<int> Id(string? valor, string campo, string prefijo = "")
    {
        var s = Texto(valor);
        if (prefijo != "" && s.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
            s = s[prefijo.Length..];

        if (!int.TryParse(s, out var id) || id <= 0)
            return new Error($"{campo} inválido: '{valor}'");

        return Result.Success(id);
    }

    public static Result<DateOnly> Fecha(string? valor, string campo)
    {
        var s = Texto(valor);
        if (!DateOnly.TryParseExact(s, "yyyy-MM-dd", out var fecha))
            return new Error($"{campo} inválida: '{valor}'");
        if (fecha < FechaMinima || fecha > DateOnly.FromDateTime(DateTime.Today))
            return new Error($"{campo} fuera de rango: '{s}'");

        return Result.Success(fecha);
    }

    public static Result<short> Puntaje(string? valor, string campo)
    {
        if (!short.TryParse(Texto(valor), out var p) || p < 1 || p > 5)
            return new Error($"{campo} fuera de rango 1-5: '{valor}'");

        return Result.Success(p);
    }

    /// <summary>Minúsculas, sin espacios sobrantes y con formato usuario@dominio.ext</summary>
    public static Result<string> Email(string? valor)
    {
        var s = Texto(valor).ToLowerInvariant();
        if (s.Length == 0)
            return new Error("Email es obligatorio");
        if (s.Length > 150 || !FormatoEmail.IsMatch(s))
            return new Error($"Email inválido: '{valor}'");

        return Result.Success(s);
    }
}
