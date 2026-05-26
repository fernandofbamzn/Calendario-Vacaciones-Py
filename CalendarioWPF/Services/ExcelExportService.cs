using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;

namespace CalendarioWPF.Services
{
    /// <summary>
    /// Servicio encargado de exportar el calendario vacacional en formato de tabla Gantt de Excel usando ClosedXML.
    /// Soporta múltiples pestañas de Excel, una por cada año con datos.
    /// </summary>
    public class ExcelExportService : IExcelExportService
    {
        /// <summary>
        /// Instancia única (Singleton) para acceso dinámico a través de la interfaz IExcelExportService.
        /// </summary>
        public static IExcelExportService Instance { get; } = new ExcelExportService();

        private static (List<string> mesesSecuencia, List<DateTime> fechasEjeX) ObtenerSecuenciaGanttPorAno(PlanVacaciones datos, int year)
        {
            var todasFechas = new List<DateTime>();
            foreach (var kvp in datos.Trabajadores)
            {
                foreach (var fStr in kvp.Value.Vacaciones)
                {
                    if (DateTime.TryParseExact(fStr, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d))
                    {
                        int qYear = (kvp.Value.Imputaciones != null && kvp.Value.Imputaciones.TryGetValue(fStr, out int yVal)) ? yVal : d.Year;
                        if (qYear == year)
                        {
                            todasFechas.Add(d);
                        }
                    }
                }
            }

            DateTime minDate, maxDate;
            if (todasFechas.Count > 0)
            {
                minDate = todasFechas.Min();
                maxDate = todasFechas.Max();
            }
            else
            {
                minDate = new DateTime(year, 6, 1);
                maxDate = new DateTime(year, 9, 30);
            }

            var mesesRango = new List<string>();
            DateTime current = new DateTime(minDate.Year, minDate.Month, 1);
            DateTime limit = new DateTime(maxDate.Year, maxDate.Month, 1);

            while (current <= limit)
            {
                mesesRango.Add($"{current.Year}-{current.Month}");
                current = current.AddMonths(1);
            }

            var fechasEjeX = new List<DateTime>();
            foreach (var mStr in mesesRango)
            {
                var parts = mStr.Split('-');
                int y = int.Parse(parts[0]);
                int m = int.Parse(parts[1]);
                int totalDias = DateTime.DaysInMonth(y, m);
                for (int d = 1; d <= totalDias; d++)
                {
                    fechasEjeX.Add(new DateTime(y, m, d));
                }
            }

            return (mesesRango, fechasEjeX);
        }

        /// <summary>
        /// Genera el reporte Gantt en Excel para todos los años con datos (cada uno en su respectiva pestaña) y lo guarda.
        /// </summary>
        public static void Exportar(string path, PlanVacaciones datos, AppConfig config, List<int> años)
        {
            using (var workbook = new XLWorkbook())
            {
                string[] nombresMeses = { "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };

                foreach (int year in años)
                {
                    var (mesesSecuencia, fechasEjeX) = ObtenerSecuenciaGanttPorAno(datos, year);
                    if (fechasEjeX == null || fechasEjeX.Count == 0)
                    {
                        continue;
                    }

                    var ws = workbook.Worksheets.Add($"Gantt {year}");
                    ws.ShowGridLines = true;

                    // 1. Fila 1: Cabecera del Mes
                    ws.Cell(1, 1).Value = "MES";
                    ws.Cell(1, 1).Style.Font.Bold = true;
                    ws.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#475569"); // Slate-600
                    ws.Cell(1, 1).Style.Font.FontColor = XLColor.White;
                    ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    int currentCol = 2;
                    foreach (var mStr in mesesSecuencia)
                    {
                        var parts = mStr.Split('-');
                        int y = int.Parse(parts[0]);
                        int m = int.Parse(parts[1]);
                        int diasMes = DateTime.DaysInMonth(y, m);

                        var cellMes = ws.Cell(1, currentCol);
                        cellMes.Value = $"{nombresMeses[m].ToUpper()} {y}";
                        cellMes.Style.Font.Bold = true;
                        cellMes.Style.Fill.BackgroundColor = XLColor.FromHtml("#475569");
                        cellMes.Style.Font.FontColor = XLColor.White;
                        cellMes.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        ws.Range(1, currentCol, 1, currentCol + diasMes - 1).Merge();
                        currentCol += diasMes;
                    }

                    // 2. Fila 2: Cabecera del Día de la semana (1 al N)
                    ws.Cell(2, 1).Value = "TRABAJADOR";
                    ws.Cell(2, 1).Style.Font.Bold = true;
                    ws.Cell(2, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#94A3B8"); // Slate-400
                    ws.Cell(2, 1).Style.Font.FontColor = XLColor.White;
                    ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    for (int i = 0; i < fechasEjeX.Count; i++)
                    {
                        var cellDia = ws.Cell(2, i + 2);
                        cellDia.Value = fechasEjeX[i].Day;
                        cellDia.Style.Font.Bold = true;
                        cellDia.Style.Fill.BackgroundColor = XLColor.FromHtml("#94A3B8");
                        cellDia.Style.Font.FontColor = XLColor.White;
                        cellDia.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }

                    // 3. Filas por cada Trabajador en orden alfabético
                    int currentRow = 3;
                    var sortedWorkers = datos.Trabajadores.Keys.OrderBy(n => n).ToList();

                    foreach (var w in sortedWorkers)
                    {
                        var info = datos.Trabajadores[w];
                        
                        var cellWorkerName = ws.Cell(currentRow, 1);
                        cellWorkerName.Value = w;
                        cellWorkerName.Style.Font.Bold = true;
                        cellWorkerName.Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC"); // Slate-50
                        cellWorkerName.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                        for (int i = 0; i < fechasEjeX.Count; i++)
                        {
                            DateTime date = fechasEjeX[i];
                            string dateStr = $"{date.Day:00}/{date.Month:00}/{date.Year}";

                            bool esWeekend = (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday);
                            bool esFestivo = datos.Festivos.Contains(dateStr);
                            bool esVacacion = info.Vacaciones.Contains(dateStr);

                            var cellData = ws.Cell(currentRow, i + 2);
                            if (esVacacion)
                            {
                                int quotaYear = (info.Imputaciones != null && info.Imputaciones.TryGetValue(dateStr, out int yVal)) ? yVal : date.Year;
                                if (quotaYear != year)
                                {
                                    cellData.Value = $"V-{quotaYear}";
                                    cellData.Style.Fill.BackgroundColor = XLColor.FromHtml("#F3E8FF"); // Lavanda
                                    cellData.Style.Font.FontColor = XLColor.FromHtml("#6B21A8"); // Morado oscuro
                                }
                                else
                                {
                                    cellData.Value = "V";
                                    cellData.Style.Fill.BackgroundColor = XLColor.FromHtml("#AED6F1"); // Celeste claro
                                    cellData.Style.Font.FontColor = XLColor.FromHtml("#1B4F72"); // Azul oscuro
                                }
                                cellData.Style.Font.Bold = true;
                            }
                            else if (esFestivo || esWeekend)
                            {
                                cellData.Value = "F";
                                cellData.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0"); // Gris descanso
                                cellData.Style.Font.FontColor = XLColor.FromHtml("#64748B");
                            }
                            cellData.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        }

                        currentRow++;
                    }

                    // 4. Bordes
                    var range = ws.Range(1, 1, currentRow - 1, fechasEjeX.Count + 1);
                    range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CBD5E1");
                    range.Style.Border.InsideBorderColor = XLColor.FromHtml("#CBD5E1");

                    // 5. Dimensiones
                    ws.Column(1).Width = 22;
                    for (int i = 2; i <= fechasEjeX.Count + 1; i++)
                    {
                        ws.Column(i).Width = 3.5;
                    }
                }

                if (workbook.Worksheets.Count == 0)
                {
                    workbook.Worksheets.Add("Sin Datos");
                }

                workbook.SaveAs(path);
            }
        }

        #region Implementación de IExcelExportService

        void IExcelExportService.Exportar(string path, PlanVacaciones datos, AppConfig config, List<int> años) => Exportar(path, datos, config, años);

        #endregion
    }
}
