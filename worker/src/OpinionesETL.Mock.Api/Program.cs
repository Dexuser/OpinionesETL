using System.Globalization;
using System.Text;

// API de comentarios sociales de PRUEBA. Implementa el contrato docs/contrato-api-comentarios-sociales.yaml
// leyendo social_comments.csv. Sirve para ejecutar el Worker de punta a punta sin depender de un servicio real.
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://localhost:5080");
var app = builder.Build();

var apiKey = app.Configuration["ApiKey"] ?? "clave-de-prueba";
var rutaCsv = app.Configuration["ComentariosCsv"] ?? Path.Combine("data", "social_comments.csv");
var fallosPendientes = int.Parse(app.Configuration["SimularFallos"] ?? "0"); // primeras N peticiones responden 503

var comentarios = Cargar(rutaCsv);
app.Logger.LogInformation("Mock API: {N} comentarios cargados desde {Ruta}", comentarios.Count, Path.GetFullPath(rutaCsv));

app.MapGet("/api/comentarios-sociales", (HttpRequest req, int? pagina, int? tamanio) =>
{
    if (!req.Headers.TryGetValue("X-API-Key", out var k) || k != apiKey)
        return Results.Json(new { error = "API key inválida o ausente" }, statusCode: 401);

    if (Interlocked.Decrement(ref fallosPendientes) >= 0)
        return Results.Json(new { error = "Servicio no disponible (simulado)" }, statusCode: 503);

    var p = Math.Max(1, pagina ?? 1);
    var t = Math.Clamp(tamanio ?? 100, 1, 500);
    var total = comentarios.Count;
    var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)t));

    return Results.Ok(new
    {
        pagina = p,
        tamanio = t,
        total,
        totalPaginas,
        datos = comentarios.Skip((p - 1) * t).Take(t)
    });
});

app.Run();

static List<ComentarioDto> Cargar(string ruta)
{
    var filas = LeerCsv(File.ReadAllText(ruta, Encoding.UTF8));
    var encabezado = filas[0].Select(h => h.Trim().TrimStart('﻿')).ToList();
    int Col(string n) => encabezado.IndexOf(n);
    var lista = new List<ComentarioDto>();
    foreach (var f in filas.Skip(1))
    {
        if (f.Count < encabezado.Count) continue;
        var cli = f[Col("IdCliente")].Trim();
        lista.Add(new ComentarioDto(
            f[Col("IdComment")].Trim(),
            cli.Length == 0 ? null : int.Parse(cli.TrimStart('C'), CultureInfo.InvariantCulture),
            int.Parse(f[Col("IdProducto")].Trim().TrimStart('P'), CultureInfo.InvariantCulture),
            f[Col("Fuente")].Trim(),
            f[Col("Fecha")].Trim(),
            f[Col("Comentario")].Trim(),
            null));   // sin sentimiento: el Worker lo clasifica por palabras clave
    }
    return lista;
}

// Lector CSV mínimo (RFC 4180: comillas dobles y comas dentro de campos).
static List<List<string>> LeerCsv(string texto)
{
    var filas = new List<List<string>>();
    var fila = new List<string>();
    var campo = new StringBuilder();
    var enComillas = false;
    for (var i = 0; i < texto.Length; i++)
    {
        var c = texto[i];
        if (enComillas)
        {
            if (c == '"' && i + 1 < texto.Length && texto[i + 1] == '"') { campo.Append('"'); i++; }
            else if (c == '"') enComillas = false;
            else campo.Append(c);
        }
        else if (c == '"') enComillas = true;
        else if (c == ',') { fila.Add(campo.ToString()); campo.Clear(); }
        else if (c == '\n' || c == '\r')
        {
            if (c == '\r' && i + 1 < texto.Length && texto[i + 1] == '\n') i++;
            fila.Add(campo.ToString()); campo.Clear();
            if (fila.Any(x => x.Length > 0) || fila.Count > 1) filas.Add(fila);
            fila = [];
        }
        else campo.Append(c);
    }
    if (campo.Length > 0 || fila.Count > 0) { fila.Add(campo.ToString()); filas.Add(fila); }
    return filas;
}

record ComentarioDto(string IdComment, int? IdCliente, int IdProducto, string RedSocial, string Fecha, string Comentario, string? Sentimiento);
