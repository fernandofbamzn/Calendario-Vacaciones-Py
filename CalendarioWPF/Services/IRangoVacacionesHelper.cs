using System.Collections.Generic;
using CalendarioWPF.Models;

namespace CalendarioWPF.Services
{
    /// <summary>
    /// Interfaz del helper de cálculo y presentación de rangos de vacaciones (<see cref="RangoVacacionesHelper"/>).
    /// Proporciona métodos para agrupar fechas sueltas en texto legible y contar días laborables consumidos.
    /// Documentado para permitir a futuros agentes entender el contrato sin leer la implementación.
    /// </summary>
    public interface IRangoVacacionesHelper
    {
        /// <summary>
        /// Agrupa las vacaciones de un trabajador en rangos de texto legibles (ej. "del 1 al 15 de Agosto"),
        /// filtrando únicamente las fechas imputadas al <paramref name="year"/> indicado.
        /// Los festivos y fines de semana se consideran continuidad de rango pero no se cuentan.
        /// </summary>
        /// <param name="fechasStr">Lista de fechas en formato "dd/MM/yyyy".</param>
        /// <param name="imputaciones">Diccionario fecha→año de cupo. Puede ser null (se asume el año natural).</param>
        /// <param name="festivosStr">Lista de festivos a excluir del cómputo de ruptura de rango.</param>
        /// <param name="year">Año de cupo de referencia para filtrar las fechas.</param>
        /// <returns>Cadena de texto con el resumen de rangos, o "Sin vacaciones disfrutadas".</returns>
        string AgruparVacacionesEnTexto(List<string> fechasStr, Dictionary<string, int>? imputaciones, List<string> festivosStr, int year);

        /// <summary>
        /// Versión multiaño de <see cref="AgruparVacacionesEnTexto"/>: agrupa en rangos pero
        /// añade el sufijo "(AAAA)" cuando el rango pertenece a un año natural diferente al de referencia.
        /// Útil para mostrar en el detalle vacaciones disfrutadas en otro año natural que se imputan al cupo actual.
        /// </summary>
        /// <param name="fechasStr">Lista de fechas en formato "dd/MM/yyyy".</param>
        /// <param name="imputaciones">Diccionario fecha→año de cupo. Puede ser null.</param>
        /// <param name="festivosStr">Lista de festivos.</param>
        /// <param name="anoReferencia">Año de cupo de referencia.</param>
        /// <returns>Cadena de texto con el resumen de rangos con año anotado cuando es diferente.</returns>
        string AgruparVacacionesEnTextoMultiano(List<string> fechasStr, Dictionary<string, int>? imputaciones, List<string> festivosStr, int anoReferencia);

        /// <summary>
        /// Cuenta los días laborables de vacaciones consumidos de un cupo específico.
        /// Excluye fines de semana y festivos del cómputo.
        /// </summary>
        /// <param name="vacaciones">Lista de fechas de vacaciones en formato "dd/MM/yyyy".</param>
        /// <param name="imputaciones">Diccionario fecha→año de cupo. Puede ser null.</param>
        /// <param name="festivos">Lista de festivos en formato "dd/MM/yyyy".</param>
        /// <param name="year">Año de cupo del que se quieren contar los días.</param>
        /// <returns>Número entero de días laborables de vacaciones consumidos en ese cupo.</returns>
        int ContarDiasConsumidos(List<string> vacaciones, Dictionary<string, int>? imputaciones, List<string> festivos, int year);
    }
}
