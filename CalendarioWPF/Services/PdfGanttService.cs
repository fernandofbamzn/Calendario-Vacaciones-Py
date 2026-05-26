using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PdfSharp;
using PdfSharp.Pdf;
using PdfSharp.Drawing;

namespace CalendarioWPF.Services
{
    public class PdfGanttService : IPdfGanttService
    {
        public static IPdfGanttService Instance { get; } = new PdfGanttService();

        private const double Mm = 72.0 / 25.4;

        public void ExportarGantt(string path, PlanVacaciones datos, AppConfig config, List<int> anos)
        {
            var anosAProcesar = (config.AnosAExportar != null && config.AnosAExportar.Count > 0)
                ? config.AnosAExportar.OrderBy(y => y).ToList()
                : anos.OrderBy(y => y).ToList();

            if (config.ExportarMultiplesPdfs && anosAProcesar.Count > 1)
            {
                foreach (int year in anosAProcesar)
                {
                    string yearPath = path.Contains(".")
                        ? path.Insert(path.LastIndexOf('.'), $"_{year}")
                        : $"{path}_{year}";
                    GenerarUnicoPdfGantt(yearPath, datos, config, new List<int> { year });
                }
            }
            else
            {
                GenerarUnicoPdfGantt(path, datos, config, anosAProcesar);
            }
        }

        private static void GenerarUnicoPdfGantt(string path, PlanVacaciones datos, AppConfig config, List<int> añosAProcesar)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            PdfDocument document = new PdfDocument();
            document.Info.Title = datos.TituloPagina + " - Tabla Gantt";

            var sortedWorkers = datos.Trabajadores.Keys.OrderBy(n => n).ToList();
            string[] nombresMeses = { "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };
            XPen penGray = new XPen(XColor.FromArgb(200, 200, 200), 0.4);

            // 1. Precalcular las páginas totales
            int totalPaginas = 0;
            var secuenciasPorAño = new Dictionary<int, (List<string> meses, List<DateTime> fechas)>();

            foreach (int year in añosAProcesar)
            {
                var (mSeq, fEje) = ObtenerSecuenciaGanttPorAno(datos, year);
                secuenciasPorAño[year] = (mSeq, fEje);
                totalPaginas += mSeq.Count;
            }

            // Precalcular páginas de resumen Gantt
            int pagsResumen = 0;
            double pageHeightLimit = 210 * Mm; // Landscape
            double maxSimY = pageHeightLimit - 20 * Mm;
            double simY = 40 * Mm;

            foreach (var w in sortedWorkers)
            {
                simY += 4.5 * Mm;
                simY += 8 * Mm;

                if (simY > 180 * Mm)
                {
                    pagsResumen++;
                    simY = 30 * Mm;
                }
            }
            pagsResumen++;

            double finalTableY = 30 * Mm + 17 * Mm + (sortedWorkers.Count * 9 * Mm) + 16 * Mm;
            if (!config.ForzarSaltoPagina && finalTableY + 35 * Mm <= 190 * Mm && totalPaginas > 0)
            {
                // No sumamos pagsResumen
            }
            else
            {
                totalPaginas += pagsResumen;
            }

            if (totalPaginas == 0) totalPaginas = 1;

            int pagNumGlobal = 1;

            // --- FASE 1: RENDER DE TABLAS GANTT ---
            foreach (var kvp in secuenciasPorAño.OrderBy(x => x.Key))
            {
                int year = kvp.Key;
                var mesesSecuencia = kvp.Value.meses;
                var fechasEjeX = kvp.Value.fechas;

                foreach (var mStr in mesesSecuencia)
                {
                    var parts = mStr.Split('-');
                    int y = int.Parse(parts[0]);
                    int m = int.Parse(parts[1]);
                    int diasMes = DateTime.DaysInMonth(y, m);

                    PdfPage page = document.AddPage();
                    page.Orientation = PageOrientation.Landscape;
                    page.Size = PageSize.A4;

                    using (XGraphics gfx = XGraphics.FromPdfPage(page))
                    {
                        PdfExportHelper.DrawHeaderFooterPdf(gfx, page, datos.TituloPagina, year, pagNumGlobal++, totalPaginas, config.PiePaginaPdf);

                        double colNameWidth = 42 * Mm;
                        double tableWidth = page.Width.Value - 30 * Mm;
                        double colDayWidth = (tableWidth - colNameWidth) / diasMes;
                        double anchoDias = tableWidth - colNameWidth;

                        double curY = 30 * Mm;

                        XFont fontTitle = new XFont("Arial", 10.5, XFontStyleEx.Bold);
                        XFont fontLabel = new XFont("Arial", 8.5, XFontStyleEx.Bold);
                        XFont fontName = new XFont("Arial", 9, XFontStyleEx.Regular);

                        // 1. Cabecera del Mes
                        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(71, 85, 105)), 15 * Mm, curY, colNameWidth, 9 * Mm);
                        gfx.DrawRectangle(penGray, 15 * Mm, curY, colNameWidth, 9 * Mm);
                        gfx.DrawString("MES", fontTitle, XBrushes.White, new XRect(15 * Mm, curY, colNameWidth, 9 * Mm), XStringFormats.Center);

                        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(71, 85, 105)), 15 * Mm + colNameWidth, curY, anchoDias, 9 * Mm);
                        gfx.DrawRectangle(penGray, 15 * Mm + colNameWidth, curY, anchoDias, 9 * Mm);
                        gfx.DrawString($"{nombresMeses[m].ToUpper()} {y}", fontTitle, XBrushes.White, new XRect(15 * Mm + colNameWidth, curY, anchoDias, 9 * Mm), XStringFormats.Center);

                        curY += 9 * Mm;

                        // 2. Cabecera de días
                        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(148, 163, 184)), 15 * Mm, curY, colNameWidth, 8 * Mm);
                        gfx.DrawRectangle(penGray, 15 * Mm, curY, colNameWidth, 8 * Mm);
                        gfx.DrawString("TRABAJADOR", fontLabel, XBrushes.White, new XRect(15 * Mm + 2 * Mm, curY, colNameWidth, 8 * Mm), XStringFormats.CenterLeft);

                        for (int d = 1; d <= diasMes; d++)
                        {
                            double x = 15 * Mm + colNameWidth + (d - 1) * colDayWidth;
                            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(148, 163, 184)), x, curY, colDayWidth, 8 * Mm);
                            gfx.DrawRectangle(penGray, x, curY, colDayWidth, 8 * Mm);
                            gfx.DrawString(d.ToString(), fontLabel, XBrushes.White, new XRect(x, curY, colDayWidth, 8 * Mm), XStringFormats.Center);
                        }

                        curY += 8 * Mm;

                        // 3. Filas por cada Trabajador
                        foreach (var w in sortedWorkers)
                        {
                            var info = datos.Trabajadores[w];

                            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(248, 250, 252)), 15 * Mm, curY, colNameWidth, 9 * Mm);
                            gfx.DrawRectangle(penGray, 15 * Mm, curY, colNameWidth, 9 * Mm);
                            gfx.DrawString(w, fontName, XBrushes.DarkSlateGray, new XRect(15 * Mm + 2 * Mm, curY, colNameWidth - 2 * Mm, 9 * Mm), XStringFormats.CenterLeft);

                            for (int d = 1; d <= diasMes; d++)
                            {
                                double x = 15 * Mm + colNameWidth + (d - 1) * colDayWidth;
                                string dateStr = $"{d:00}/{m:00}/{y}";

                                DateTime date = new DateTime(y, m, d);
                                bool esWeekend = (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday);
                                bool esFestivo = datos.Festivos.Contains(dateStr);
                                bool esVacacion = info.Vacaciones.Contains(dateStr);

                                XColor cellFill = XColors.White;
                                bool isFilled = false;

                                if (esVacacion)
                                {
                                    int quotaYear = (info.Imputaciones != null && info.Imputaciones.TryGetValue(dateStr, out int yVal)) ? yVal : y;
                                    if (quotaYear != year)
                                    {
                                        cellFill = XColor.FromArgb(243, 232, 255); // Lavanda
                                    }
                                    else
                                    {
                                        cellFill = XColor.FromArgb(174, 214, 241); // Azul
                                    }
                                    isFilled = true;
                                }
                                else if (esFestivo || esWeekend)
                                {
                                    cellFill = XColor.FromArgb(241, 243, 245);
                                    isFilled = true;
                                }

                                if (isFilled)
                                {
                                    gfx.DrawRectangle(new XSolidBrush(cellFill), x, curY, colDayWidth, 9 * Mm);
                                }
                                gfx.DrawRectangle(penGray, x, curY, colDayWidth, 9 * Mm);
                            }

                            curY += 9 * Mm;
                        }

                        // 4. Leyendas de la página
                        curY += 6 * Mm;
                        XFont fontLeyenda = new XFont("Arial", 9, XFontStyleEx.Regular);

                        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(174, 214, 241)), 15 * Mm, curY, 8 * Mm, 5 * Mm);
                        gfx.DrawRectangle(penGray, 15 * Mm, curY, 8 * Mm, 5 * Mm);
                        gfx.DrawString("Vacaciones del cupo actual", fontLeyenda, XBrushes.SlateGray, new XPoint(25 * Mm, curY + 1 * Mm), XStringFormats.TopLeft);

                        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(243, 232, 255)), 85 * Mm, curY, 8 * Mm, 5 * Mm);
                        gfx.DrawRectangle(penGray, 85 * Mm, curY, 8 * Mm, 5 * Mm);
                        gfx.DrawString("Vacaciones de otro cupo", fontLeyenda, XBrushes.SlateGray, new XPoint(95 * Mm, curY + 1 * Mm), XStringFormats.TopLeft);

                        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(241, 243, 245)), 170 * Mm, curY, 8 * Mm, 5 * Mm);
                        gfx.DrawRectangle(penGray, 170 * Mm, curY, 8 * Mm, 5 * Mm);
                        gfx.DrawString("Fin de semana / Festivos oficiales", fontLeyenda, XBrushes.SlateGray, new XPoint(180 * Mm, curY + 1 * Mm), XStringFormats.TopLeft);
                    }
                }
            }

            // --- FASE 2: RENDER DE RESUMEN CONSOLIDADO AL FINAL ---
            double textY = 0;
            PdfPage pageFinal = null!;
            XGraphics gfxFinal = null!;
            bool usarNuevaPaginaParaResumenGantt = config.ForzarSaltoPagina;

            if (!usarNuevaPaginaParaResumenGantt && document.Pages.Count > 0)
            {
                pageFinal = document.Pages[document.Pages.Count - 1];
                if (finalTableY + 30 * Mm <= 190 * Mm)
                {
                    textY = finalTableY;
                    gfxFinal = XGraphics.FromPdfPage(pageFinal);
                }
                else
                {
                    usarNuevaPaginaParaResumenGantt = true;
                }
            }

            if (usarNuevaPaginaParaResumenGantt || document.Pages.Count == 0)
            {
                pageFinal = document.AddPage();
                pageFinal.Orientation = PageOrientation.Landscape;
                pageFinal.Size = PageSize.A4;
                gfxFinal = XGraphics.FromPdfPage(pageFinal);
                PdfExportHelper.DrawHeaderFooterPdf(gfxFinal, pageFinal, datos.TituloPagina, datos.Year, pagNumGlobal++, totalPaginas, config.PiePaginaPdf);
                textY = 30 * Mm;
            }

            XFont fontFinalTitle = new XFont("Arial", 12.5, XFontStyleEx.Bold);
            XFont fontFinalLabelBold = new XFont("Arial", 10, XFontStyleEx.Bold);
            XFont fontFinalItalic = new XFont("Arial", 9, XFontStyleEx.Italic);

            gfxFinal.DrawString("Cómputo Anual de Vacaciones (Días laborables netos y detalle):", fontFinalTitle, XBrushes.DarkSlateGray, new XPoint(15 * Mm, textY), XStringFormats.TopLeft);
            textY += 10 * Mm;

            foreach (var w in sortedWorkers)
            {
                var info = datos.Trabajadores[w];

                List<string> consumosList = new List<string>();
                bool cupoSuperado = false;
                foreach (int y in añosAProcesar)
                {
                    int netos = RangoVacacionesHelper.ContarDiasConsumidos(info.Vacaciones, info.Imputaciones, datos.Festivos, y);
                    int limite = info.DiasBase + info.DiasExtras;
                    if (netos > limite) cupoSuperado = true;
                    consumosList.Add($"{netos} de {limite} (en {y})");
                }
                string consumosStr = string.Join(", ", consumosList);
                string excede = cupoSuperado ? " (¡Cupo superado en algún año!)" : "";

                string rangosTexto = RangoVacacionesHelper.AgruparVacacionesEnTextoMultiano(info.Vacaciones, info.Imputaciones, datos.Festivos, datos.Year);

                gfxFinal.DrawString($"- {w}: {consumosStr} días disfrutados{excede}.", fontFinalLabelBold, XBrushes.DarkSlateGray, new XPoint(18 * Mm, textY), XStringFormats.TopLeft);
                textY += 4.5 * Mm;

                gfxFinal.DrawString($"Detalle: {rangosTexto}", fontFinalItalic, XBrushes.Gray, new XPoint(25 * Mm, textY), XStringFormats.TopLeft);
                textY += 8 * Mm;

                if (textY > 180 * Mm)
                {
                    gfxFinal.Dispose(); // Liberar el recurso activo
                    PdfPage extraPage = document.AddPage();
                    extraPage.Orientation = PageOrientation.Landscape;
                    extraPage.Size = PageSize.A4;
                    gfxFinal = XGraphics.FromPdfPage(extraPage);
                    PdfExportHelper.DrawHeaderFooterPdf(gfxFinal, extraPage, datos.TituloPagina, datos.Year, pagNumGlobal++, totalPaginas, config.PiePaginaPdf);
                    textY = 30 * Mm;
                }
            }

            gfxFinal.Dispose(); // Liberar el recurso final
            document.Save(path);
        }

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

    }
}