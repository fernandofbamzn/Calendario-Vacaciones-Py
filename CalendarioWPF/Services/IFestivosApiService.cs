using System.Collections.Generic;
using System.Threading.Tasks;

namespace CalendarioWPF.Services
{
    /// <summary>
    /// Interfaz para el servicio que obtiene festivos de proveedores externos.
    /// Documentado exhaustivamente para reducir el consumo de tokens en futuras interacciones con agentes.
    /// </summary>
    public interface IFestivosApiService
    {
        /// <summary>
        /// Obtiene una lista de fechas (en formato dd/MM/yyyy) que corresponden a los festivos de una región específica para un año dado.
        /// </summary>
        /// <param name="isoCode">Código ISO de la región (ej. ES-MD para Madrid).</param>
        /// <param name="year">Año para el cual se consultarán los festivos.</param>
        /// <returns>Lista de fechas en formato dd/MM/yyyy.</returns>
        Task<List<string>> ObtenerFestivosAsync(string isoCode, int year);

        /// <summary>
        /// Obtiene un diccionario con las regiones disponibles y sus códigos ISO.
        /// </summary>
        /// <param name="countryCode">Código ISO del país (por defecto ES).</param>
        /// <returns>Diccionario donde la clave es el código (ej. ES-MD) y el valor es el nombre de la región.</returns>
        Task<Dictionary<string, string>> ObtenerRegionesAsync(string countryCode = "ES");
    }
}
