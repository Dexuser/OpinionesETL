namespace OpinionesETL.Worker.Application.Options;

/// <summary>Configuración modular de las tres fuentes: cada una se puede apagar sin tocar código.</summary>
public sealed class FuentesOptions
{
    public const string Seccion = "Fuentes";

    public CsvOptions Csv { get; set; } = new();
    public BaseOperacionalOptions BaseOperacional { get; set; } = new();
    public ApiOptions Api { get; set; } = new();
}

public sealed class CsvOptions
{
    public bool Habilitado { get; set; } = true;

    /// <summary>Carpeta donde viven los archivos (ruta absoluta o relativa al directorio de trabajo).</summary>
    public string Carpeta { get; set; } = "data";

    /// <summary>Patrón de archivos de encuestas; permite surveys_part1.csv, surveys_part2.csv, ...</summary>
    public string Patron { get; set; } = "surveys_*.csv";
}

public sealed class BaseOperacionalOptions
{
    public bool Habilitado { get; set; } = true;
}

public sealed class ApiOptions
{
    public bool Habilitado { get; set; } = true;
    public string BaseUrl { get; set; } = "";
    public string Ruta { get; set; } = "api/comentarios-sociales";

    /// <summary>Nombre del header de autenticación.</summary>
    public string NombreHeaderApiKey { get; set; } = "X-API-Key";

    /// <summary>La clave NO va en appsettings.json: se entrega por variable de entorno Fuentes__Api__ApiKey o user-secrets.</summary>
    public string ApiKey { get; set; } = "";

    public int TamanioPagina { get; set; } = 100;
    public int TimeoutSegundos { get; set; } = 30;
    public int Reintentos { get; set; } = 3;
    public int EsperaInicialMs { get; set; } = 500;
}

/// <summary>Cadenas de conexión (sección ConnectionStrings). Las contraseñas se inyectan desde el entorno.</summary>
public sealed class ConexionesOptions
{
    public const string Seccion = "ConnectionStrings";

    /// <summary>Base operacional (esquema opiniones): fuente de reseñas web, clientes y productos.</summary>
    public string Operacional { get; set; } = "";

    /// <summary>Base analítica (esquemas stg y dw): destino.</summary>
    public string Analitica { get; set; } = "";
}
