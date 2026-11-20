namespace OpinionesETL.Application.Common;

/// <summary>Acumula todos los errores de una fila para registrar un motivo de rechazo completo.</summary>
public sealed class Errores
{
    private readonly List<string> _mensajes = [];

    public bool Hay => _mensajes.Count > 0;

    public void Agregar(string mensaje) => _mensajes.Add(mensaje);

    public void Agregar(params Result[] resultados)
    {
        foreach (var r in resultados)
            if (r.IsFailure) _mensajes.Add(r.Error!.Message);
    }

    public Error ComoError() => new(string.Join("; ", _mensajes));
}
