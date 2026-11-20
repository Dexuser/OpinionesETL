using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using OpinionesETL.Application.Abstractions;

namespace OpinionesETL.Infrastructure.Csv;

/// <summary>
/// Lee un CSV a objetos "raw" (todo texto). Valida que existan los encabezados esperados y
/// convierte las filas malformadas en filas inválidas en lugar de abortar todo el archivo.
/// </summary>
public sealed class LectorCsv<TRaw, TMap>(string[] encabezadosEsperados) : IArchivoLector<TRaw>
    where TRaw : class
    where TMap : ClassMap<TRaw>
{
    public LecturaCsv<TRaw> Leer(string ruta)
    {
        if (!File.Exists(ruta))
            throw new FileNotFoundException($"No se encontró el archivo '{ruta}'", ruta);

        var archivo = Path.GetFileName(ruta);
        var configuracion = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = ",",
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,   // los campos faltantes llegan vacíos y los valida la limpieza
            HeaderValidated = null      // los encabezados se validan abajo con un mensaje propio
        };

        using var reader = new StreamReader(ruta, Encoding.UTF8);
        using var csv = new CsvReader(reader, configuracion);
        csv.Context.RegisterClassMap<TMap>();

        if (!csv.Read() || !csv.ReadHeader())
            throw new EstructuraArchivoInvalidaException(archivo, "el archivo está vacío o no tiene encabezado");

        var presentes = new HashSet<string>(csv.HeaderRecord ?? [], StringComparer.OrdinalIgnoreCase);
        var faltantes = encabezadosEsperados.Where(h => !presentes.Contains(h)).ToList();
        if (faltantes.Count > 0)
            throw new EstructuraArchivoInvalidaException(archivo, $"faltan las columnas: {string.Join(", ", faltantes)}");

        var lectura = new LecturaCsv<TRaw>();
        while (true)
        {
            bool hayFila;
            try
            {
                hayFila = csv.Read();
            }
            catch (CsvHelperException ex)
            {
                // Una fila con comillas mal cerradas, etc. No se puede seguir leyendo con seguridad.
                lectura.Invalidas.Add(new FilaInvalida(csv.Parser.Row, $"Fila malformada: {PrimeraLinea(ex.Message)}"));
                break;
            }

            if (!hayFila) break;

            var numero = csv.Parser.Row;
            try
            {
                lectura.Filas.Add(new FilaCsv<TRaw>(numero, csv.GetRecord<TRaw>()!));
            }
            catch (CsvHelperException ex)
            {
                lectura.Invalidas.Add(new FilaInvalida(numero, $"Fila inválida: {PrimeraLinea(ex.Message)}"));
            }
        }

        return lectura;
    }

    private static string PrimeraLinea(string mensaje) =>
        mensaje.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
}
