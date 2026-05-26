using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CalendarioWPF.Models;

namespace CalendarioWPF.Services
{
    /// <summary>
    /// Helper estático para la agrupación textual de rangos de vacaciones y el cómputo de días consumidos.
    /// Implementa la lógica de continuidad de rango teniendo en cuenta festivos y fines de semana.
    /// Ver <see cref="IRangoVacacionesHelper"/> para la documentación del contrato.
    /// </summary>
    public static class RangoVacacionesHelper
    {
        /// <summary>
        /// Agrupa fechas sueltas en rangos de texto legibles (sobrecarga sin imputaciones).
        /// </summary>
        public static string AgruparVacacionesEnTexto(List<string> fechasStr, List<string> festivosStr, int year)
        {
            return AgruparVacacionesEnTexto(fechasStr, null, festivosStr, year);
        }

        /// <summary>
        /// Agrupa las vacaciones de un trabajador en rangos de texto legibles, filtrando
        /// por el <paramref name="year"/> de cupo. Los festivos y fines de semana se consideran
        /// continuidad dentro de un rango pero no se cuentan como días consumidos.
        /// </summary>
        /// <param name="fechasStr">Lista de fechas en formato "dd/MM/yyyy".</param>
        /// <param name="imputaciones">Diccionario fecha→año de cupo. Null implica usar el año natural.</param>
        /// <param name="festivosStr">Lista de festivos que actúan como puentes en los rangos.</param>
        /// <param name="year">Año de cupo de referencia para filtrar.</param>
        /// <returns>Texto con los rangos o "Sin vacaciones disfrutadas".</returns>
        public static string AgruparVacacionesEnTexto(List<string> fechasStr, Dictionary<string, int>? imputaciones, List<string> festivosStr, int year)
        {
            if (fechasStr == null || fechasStr.Count == 0)
                return "Sin vacaciones disfrutadas";

            // Parsear a DateTime, filtrar por año de cupo y ordenar
            var fechas = fechasStr
                .Select(f => {
                    if (DateTime.TryParseExact(f, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                        return (DateTime?)d;
                    return null;
                })
                .Where(d => {
                    if (!d.HasValue) return false;
                    string dateStr = d.Value.ToString("dd/MM/yyyy");
                    int quotaYear = (imputaciones != null && imputaciones.TryGetValue(dateStr, out var val)) ? val : d.Value.Year;
                    return quotaYear == year;
                })
                .Select(d => d!.Value)
                .OrderBy(d => d)
                .ToList();

            if (fechas.Count == 0)
                return "Sin vacaciones disfrutadas";

            var festivos = (festivosStr ?? new List<string>())
                .Select(f => {
                    if (DateTime.TryParseExact(f, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                        return (DateTime?)d;
                    return null;
                })
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .ToHashSet();

            return FormatearRangos(fechas, festivos, null);
        }

        /// <summary>
        /// Agrupa en rangos con sufijo de año cuando la fecha pertenece a un año natural diferente
        /// al año de referencia. Sobrecarga sin imputaciones.
        /// </summary>
        public static string AgruparVacacionesEnTextoMultiano(List<string> fechasStr, List<string> festivosStr, int anoReferencia)
        {
            return AgruparVacacionesEnTextoMultiano(fechasStr, null, festivosStr, anoReferencia);
        }

        /// <summary>
        /// Agrupa en rangos textuales añadiendo sufijo "(AAAA)" cuando el rango pertenece a un año
        /// natural diferente al <paramref name="anoReferencia"/>. Útil para el resumen del detalle lateral.
        /// </summary>
        /// <param name="fechasStr">Lista de fechas en formato "dd/MM/yyyy".</param>
        /// <param name="imputaciones">Diccionario fecha→año de cupo. Null implica usar el año natural.</param>
        /// <param name="festivosStr">Lista de festivos que actúan como puentes.</param>
        /// <param name="anoReferencia">Año de cupo de referencia para filtrar y anotar.</param>
        /// <returns>Texto con los rangos anotados con el año cuando difieren, o "Sin vacaciones disfrutadas".</returns>
        public static string AgruparVacacionesEnTextoMultiano(List<string> fechasStr, Dictionary<string, int>? imputaciones, List<string> festivosStr, int anoReferencia)
        {
            if (fechasStr == null || fechasStr.Count == 0)
                return "Sin vacaciones disfrutadas";

            var fechas = fechasStr
                .Select(f => {
                    if (DateTime.TryParseExact(f, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                        return (DateTime?)d;
                    return null;
                })
                .Where(d => {
                    if (!d.HasValue) return false;
                    string dateStr = d.Value.ToString("dd/MM/yyyy");
                    int quotaYear = (imputaciones != null && imputaciones.TryGetValue(dateStr, out var val)) ? val : d.Value.Year;
                    return quotaYear == anoReferencia;
                })
                .Select(d => d!.Value)
                .OrderBy(d => d)
                .ToList();

            if (fechas.Count == 0)
                return "Sin vacaciones disfrutadas";

            var festivos = (festivosStr ?? new List<string>())
                .Select(f => {
                    if (DateTime.TryParseExact(f, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                        return (DateTime?)d;
                    return null;
                })
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .ToHashSet();

            return FormatearRangos(fechas, festivos, anoReferencia);
        }

        /// <summary>
        /// Cuenta los días laborables de vacaciones imputados al <paramref name="year"/> indicado.
        /// Excluye fines de semana y festivos del cómputo. Sobrecarga sin imputaciones.
        /// </summary>
        public static int ContarDiasConsumidos(List<string> vacaciones, List<string> festivos, int year)
        {
            return ContarDiasConsumidos(vacaciones, null, festivos, year);
        }

        /// <summary>
        /// Cuenta los días laborables de vacaciones imputados al año de cupo <paramref name="year"/>.
        /// Excluye fines de semana y festivos.
        /// </summary>
        /// <param name="vacaciones">Lista de fechas de vacaciones en formato "dd/MM/yyyy".</param>
        /// <param name="imputaciones">Diccionario fecha→año de cupo. Null implica usar el año natural.</param>
        /// <param name="festivos">Lista de festivos en formato "dd/MM/yyyy".</param>
        /// <param name="year">Año de cupo del que se quieren contar los días consumidos.</param>
        /// <returns>Número de días laborables consumidos.</returns>
        public static int ContarDiasConsumidos(List<string> vacaciones, Dictionary<string, int>? imputaciones, List<string> festivos, int year)
        {
            if (vacaciones == null) return 0;

            int dias = 0;
            var festivosSet = festivos != null ? new HashSet<string>(festivos) : new HashSet<string>();

            foreach (var dateStr in vacaciones)
            {
                if (DateTime.TryParseExact(dateStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    int quotaYear = (imputaciones != null && imputaciones.TryGetValue(dateStr, out var val)) ? val : date.Year;
                    if (quotaYear == year)
                    {
                        bool esWeekend = (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday);
                        bool esFestivo = festivosSet.Contains(dateStr);

                        if (!esWeekend && !esFestivo)
                        {
                            dias++;
                        }
                    }
                }
            }

            return dias;
        }

        // ── Métodos privados ───────────────────────────────────────────────────────

        /// <summary>
        /// Construye los rangos de texto a partir de una lista de fechas ordenadas,
        /// considerando festivos y fines de semana como "puentes" de continuidad.
        /// Si <paramref name="anoReferencia"/> tiene valor, añade el sufijo "(AAAA)" cuando el año difiere.
        /// </summary>
        private static string FormatearRangos(List<DateTime> fechas, HashSet<DateTime> festivos, int? anoReferencia)
        {
            var ranges = new List<List<DateTime>>();
            var currentRange = new List<DateTime> { fechas[0] };

            for (int i = 1; i < fechas.Count; i++)
            {
                var prev = currentRange[^1];
                var curr = fechas[i];

                bool esContinuo = true;
                var tempDate = prev.AddDays(1);

                while (tempDate < curr)
                {
                    bool esFinSemana = (tempDate.DayOfWeek == DayOfWeek.Saturday || tempDate.DayOfWeek == DayOfWeek.Sunday);
                    bool esFestivo = festivos.Contains(tempDate);

                    if (!esFinSemana && !esFestivo)
                    {
                        esContinuo = false;
                        break;
                    }
                    tempDate = tempDate.AddDays(1);
                }

                // Si hay referencia de año, se rompe el rango al cambiar de año natural
                bool mismosAno = anoReferencia == null || prev.Year == curr.Year;

                if (esContinuo && mismosAno)
                {
                    currentRange.Add(curr);
                }
                else
                {
                    ranges.Add(currentRange);
                    currentRange = new List<DateTime> { curr };
                }
            }
            ranges.Add(currentRange);

            string[] nombresMeses = { "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };

            var rangesText = ranges.Select(range => {
                var start = range[0];
                var end = range[^1];

                string startMonth = nombresMeses[start.Month - 1];
                string endMonth = nombresMeses[end.Month - 1];
                string anoSufijo = (anoReferencia.HasValue && start.Year != anoReferencia.Value) ? $" ({start.Year})" : "";

                if (start == end)
                    return $"el {start.Day} de {startMonth}{anoSufijo}";
                else if (start.Month == end.Month)
                    return $"del {start.Day} al {end.Day} de {startMonth}{anoSufijo}";
                else
                    return $"del {start.Day} de {startMonth} al {end.Day} de {endMonth}{anoSufijo}";
            }).ToList();

            if (rangesText.Count == 1)
                return rangesText[0];

            var lastText = rangesText[^1];
            rangesText.RemoveAt(rangesText.Count - 1);
            return string.Join(", ", rangesText) + " y " + lastText;
        }
    }
}
