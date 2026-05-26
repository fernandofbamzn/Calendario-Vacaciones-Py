using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CalendarioWPF
{
    public static class RangoVacacionesHelper
    {
        public static string AgruparVacacionesEnTexto(List<string> fechasStr, List<string> festivosStr, int year)
        {
            return AgruparVacacionesEnTexto(fechasStr, null, festivosStr, year);
        }

        public static string AgruparVacacionesEnTexto(List<string> fechasStr, Dictionary<string, int>? imputaciones, List<string> festivosStr, int year)
        {
            if (fechasStr == null || fechasStr.Count == 0)
                return "Sin vacaciones disfrutadas";

            // Parsear a DateTime, filtrar por la imputación correspondiente y ordenar
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

                if (esContinuo)
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

            var nombresMeses = new[] { "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };

            var rangesText = ranges.Select(range => {
                var start = range[0];
                var end = range[^1];

                string startMonth = nombresMeses[start.Month - 1];
                string endMonth = nombresMeses[end.Month - 1];

                if (start == end)
                {
                    return $"el {start.Day} de {startMonth}";
                }
                else
                {
                    if (start.Month == end.Month)
                    {
                        return $"del {start.Day} al {end.Day} de {startMonth}";
                    }
                    else
                    {
                        return $"del {start.Day} de {startMonth} al {end.Day} de {endMonth}";
                    }
                }
            }).ToList();

            if (rangesText.Count == 1)
                return rangesText[0];

            var lastText = rangesText[^1];
            rangesText.RemoveAt(rangesText.Count - 1);
            return string.Join(", ", rangesText) + " y " + lastText;
        }

        public static string AgruparVacacionesEnTextoMultiaño(List<string> fechasStr, List<string> festivosStr, int añoReferencia)
        {
            return AgruparVacacionesEnTextoMultiaño(fechasStr, null, festivosStr, añoReferencia);
        }

        public static string AgruparVacacionesEnTextoMultiaño(List<string> fechasStr, Dictionary<string, int>? imputaciones, List<string> festivosStr, int añoReferencia)
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
                    return quotaYear == añoReferencia;
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

                if (esContinuo && prev.Year == curr.Year)
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

            var nombresMeses = new[] { "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };

            var rangesText = ranges.Select(range => {
                var start = range[0];
                var end = range[^1];

                string startMonth = nombresMeses[start.Month - 1];
                string endMonth = nombresMeses[end.Month - 1];
                string añoSufijo = start.Year != añoReferencia ? $" ({start.Year})" : "";

                if (start == end)
                {
                    return $"el {start.Day} de {startMonth}{añoSufijo}";
                }
                else
                {
                    if (start.Month == end.Month)
                    {
                        return $"del {start.Day} al {end.Day} de {startMonth}{añoSufijo}";
                    }
                    else
                    {
                        return $"del {start.Day} de {startMonth} al {end.Day} de {endMonth}{añoSufijo}";
                    }
                }
            }).ToList();

            if (rangesText.Count == 1)
                return rangesText[0];

            var lastText = rangesText[^1];
            rangesText.RemoveAt(rangesText.Count - 1);
            return string.Join(", ", rangesText) + " y " + lastText;
        }

        public static int ContarDiasConsumidos(List<string> vacaciones, List<string> festivos, int year)
        {
            return ContarDiasConsumidos(vacaciones, null, festivos, year);
        }

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
    }
}
