using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PdfSharp;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using System.Drawing;

namespace CalendarioWPF.Services
{
    public class PdfGanttService : IPdfGanttService
    {
        public static IPdfGanttService Instance { get; } = new PdfGanttService();

        private const double Mm = 72.0 / 25.4;

        public void ExportarGantt(string path, PlanVacaciones datos, AppConfig config, List<int> anos, string filtroDpto = "")
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
                    GenerarUnicoPdfGantt(yearPath, datos, config, new List<int> { year }, filtroDpto);
                }
            }
            else
            {
                GenerarUnicoPdfGantt(path, datos, config, anosAProcesar, filtroDpto);
            }
        }

        private static void GenerarUnicoPdfGantt(string path, PlanVacaciones datos, AppConfig config, List<int> añosAProcesar, string filtroDpto)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            PdfDocument document = new PdfDocument();
            document.Info.Title = datos.TituloPagina + " - Tabla Gantt";

            var sortedWorkers = datos.Trabajadores
                .Where(w => string.IsNullOrEmpty(filtroDpto) || w.Value.Departamento == filtroDpto)
                .Select(w => w.Key)
                .OrderBy(n => n).ToList();

            string[] nombresMeses = { "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };
            XPen penGray = new XPen(XColor.FromArgb(200, 200, 200), 0.4);

            // 1. Agrupar meses por año (sin simulación manual de páginas)
            var secuenciasPorAño = new Dictionary<int, (List<string> meses, List<DateTime> fechas)>();

            foreach (int year in añosAProcesar)
            {
                var (mSeq, fEje) = ObtenerSecuenciaGanttPorAno(datos, year, filtroDpto);
                secuenciasPorAño[year] = (mSeq, fEje);
            }

            // --- FASE 1: RENDER DE TABLAS GANTT ---
            double finalTableY = 0;
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

                    XGraphics gfx = XGraphics.FromPdfPage(page);
                    double curY = 0;
                    try
                    {
                        PdfExportHelper.DrawHeaderPdf(gfx, page, datos.TituloPagina, year);

                        double colNameWidth = 42 * Mm;
                        double tableWidth = page.Width.Value - 30 * Mm;
                        double colDayWidth = (tableWidth - colNameWidth) / diasMes;
                        double anchoDias = tableWidth - colNameWidth;

                        curY = 30 * Mm;

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
                        double pageLimit = page.Height.Value - 20 * Mm; // Margen inferior de seguridad

                        foreach (var w in sortedWorkers)
                        {
                            // Si la fila no cabe, crear nueva página con cabeceras
                            if (curY + 9 * Mm > pageLimit)
                            {
                                // Dibujar leyenda antes de cerrar la página
                                curY += 3 * Mm;
                                XFont fontLeyMin = new XFont("Arial", 8, XFontStyleEx.Regular);
                                gfx.DrawString("(Continúa en la siguiente página...)", fontLeyMin, XBrushes.Gray, new XPoint(15 * Mm, curY), XStringFormats.TopLeft);
                                
                                gfx.Dispose();
                                
                                page = document.AddPage();
                                page.Orientation = PageOrientation.Landscape;
                                page.Size = PageSize.A4;
                                gfx = XGraphics.FromPdfPage(page);
                                PdfExportHelper.DrawHeaderPdf(gfx, page, datos.TituloPagina, year);
                                
                                curY = 30 * Mm;
                                
                                // Re-dibujar cabeceras del mes y días
                                gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(71, 85, 105)), 15 * Mm, curY, colNameWidth, 9 * Mm);
                                gfx.DrawRectangle(penGray, 15 * Mm, curY, colNameWidth, 9 * Mm);
                                gfx.DrawString("MES", fontTitle, XBrushes.White, new XRect(15 * Mm, curY, colNameWidth, 9 * Mm), XStringFormats.Center);
                                gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(71, 85, 105)), 15 * Mm + colNameWidth, curY, anchoDias, 9 * Mm);
                                gfx.DrawRectangle(penGray, 15 * Mm + colNameWidth, curY, anchoDias, 9 * Mm);
                                gfx.DrawString($"{nombresMeses[m].ToUpper()} {y} (cont.)", fontTitle, XBrushes.White, new XRect(15 * Mm + colNameWidth, curY, anchoDias, 9 * Mm), XStringFormats.Center);
                                curY += 9 * Mm;
                                
                                gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(148, 163, 184)), 15 * Mm, curY, colNameWidth, 8 * Mm);
                                gfx.DrawRectangle(penGray, 15 * Mm, curY, colNameWidth, 8 * Mm);
                                gfx.DrawString("TRABAJADOR", fontLabel, XBrushes.White, new XRect(15 * Mm + 2 * Mm, curY, colNameWidth, 8 * Mm), XStringFormats.CenterLeft);
                                for (int d2 = 1; d2 <= diasMes; d2++)
                                {
                                    double x2 = 15 * Mm + colNameWidth + (d2 - 1) * colDayWidth;
                                    gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(148, 163, 184)), x2, curY, colDayWidth, 8 * Mm);
                                    gfx.DrawRectangle(penGray, x2, curY, colDayWidth, 8 * Mm);
                                    gfx.DrawString(d2.ToString(), fontLabel, XBrushes.White, new XRect(x2, curY, colDayWidth, 8 * Mm), XStringFormats.Center);
                                }
                                curY += 8 * Mm;
                            }

                            var info = datos.Trabajadores[w];

                            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(248, 250, 252)), 15 * Mm, curY, colNameWidth, 9 * Mm);
                            gfx.DrawRectangle(penGray, 15 * Mm, curY, colNameWidth, 9 * Mm);
                            gfx.DrawString(w, fontName, XBrushes.DarkSlateGray, new XRect(15 * Mm + 2 * Mm, curY, colNameWidth - 2 * Mm, 9 * Mm), XStringFormats.CenterLeft);

                            var festivosTrabajador = RangoVacacionesHelper.ObtenerFestivosTrabajador(w, datos);

                            for (int d = 1; d <= diasMes; d++)
                            {
                                double x = 15 * Mm + colNameWidth + (d - 1) * colDayWidth;
                                string dateStr = $"{d:00}/{m:00}/{y}";

                                DateTime date = new DateTime(y, m, d);
                                bool esWeekend = (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday);
                                bool esFestivo = festivosTrabajador.Contains(dateStr);
                                bool esVacacion = info.Vacaciones.Contains(dateStr);

                                XColor cellFill = XColors.White;
                                bool isFilled = false;
                                
                                bool tieneCierre = datos.Cierres != null && (
                                    (datos.Cierres.ContainsKey(info.Departamento) && datos.Cierres[info.Departamento].Contains(dateStr)) ||
                                    (datos.Cierres.ContainsKey("__todos__") && datos.Cierres["__todos__"].Contains(dateStr))
                                );
                                bool tieneConflicto = esVacacion && RangoVacacionesHelper.EsIncompatible(w, dateStr, datos);
                                
                                // Solo considerar cierre si el trabajador realmente lo tiene como vacación
                                tieneCierre = tieneCierre && esVacacion;
                                
                                if (esVacacion)
                                {
                                    string hexColor = (datos.DepartamentosColores != null && datos.DepartamentosColores.ContainsKey(info.Departamento)) 
                                        ? datos.DepartamentosColores[info.Departamento] : null;

                                    if (hexColor != null && hexColor.StartsWith("#") && hexColor.Length >= 7)
                                    {
                                        try {
                                            byte r = Convert.ToByte(hexColor.Substring(1, 2), 16);
                                            byte g = Convert.ToByte(hexColor.Substring(3, 2), 16);
                                            byte b = Convert.ToByte(hexColor.Substring(5, 2), 16);
                                            cellFill = XColor.FromArgb(r, g, b);
                                        } catch { cellFill = XColor.FromArgb(174, 214, 241); }
                                    }
                                    isFilled = true;
                                    
                                    int qYear = (info.Imputaciones != null && info.Imputaciones.TryGetValue(dateStr, out int yVal)) ? yVal : date.Year;
                                    if (qYear != date.Year)
                                    {
                                        // Otro año (opacidad o color fijo claro)
                                        cellFill = XColor.FromArgb(100, cellFill.R, cellFill.G, cellFill.B); // Alpha 100
                                    }
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

                                string cMark = "";
                                if (tieneConflicto) cMark += "!";
                                if (tieneCierre) cMark += "C";

                                if (!string.IsNullOrEmpty(cMark))
                                {
                                    XFont fontIncomp = new XFont("Arial", 8.5, XFontStyleEx.Bold);
                                    XBrush textBrush = cMark.Contains("!") ? XBrushes.Red : XBrushes.DarkSlateGray;
                                    gfx.DrawString(cMark, fontIncomp, textBrush, new XRect(x, curY, colDayWidth, 9 * Mm), XStringFormats.Center);
                                }
                            }

                            curY += 9 * Mm;
                        }

                        // 4. Leyendas de la página
                        curY += 6 * Mm;
                        XFont fontLeyenda = new XFont("Arial", 9, XFontStyleEx.Regular);

                        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(174, 214, 241)), 15 * Mm, curY, 8 * Mm, 5 * Mm);
                        gfx.DrawRectangle(penGray, 15 * Mm, curY, 8 * Mm, 5 * Mm);
                        gfx.DrawString("Vacaciones (Opaco año ant.)", fontLeyenda, XBrushes.SlateGray, new XPoint(25 * Mm, curY + 1 * Mm), XStringFormats.TopLeft);

                        XFont fontIncompL = new XFont("Arial", 8.5, XFontStyleEx.Bold);
                        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(250, 215, 161)), 75 * Mm, curY, 8 * Mm, 5 * Mm);
                        gfx.DrawRectangle(penGray, 75 * Mm, curY, 8 * Mm, 5 * Mm);
                        gfx.DrawString("C", fontIncompL, XBrushes.DarkSlateGray, new XRect(75 * Mm, curY, 8 * Mm, 5 * Mm), XStringFormats.Center);
                        gfx.DrawString("Cierre Patronal", fontLeyenda, XBrushes.SlateGray, new XPoint(85 * Mm, curY + 1 * Mm), XStringFormats.TopLeft);

                        gfx.DrawRectangle(new XSolidBrush(XColors.White), 130 * Mm, curY, 8 * Mm, 5 * Mm);
                        gfx.DrawRectangle(penGray, 130 * Mm, curY, 8 * Mm, 5 * Mm);
                        gfx.DrawString("!", fontIncompL, XBrushes.Red, new XRect(130 * Mm, curY, 8 * Mm, 5 * Mm), XStringFormats.Center);
                        gfx.DrawString("Incompatibilidad", fontLeyenda, XBrushes.SlateGray, new XPoint(140 * Mm, curY + 1 * Mm), XStringFormats.TopLeft);

                        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(241, 243, 245)), 185 * Mm, curY, 8 * Mm, 5 * Mm);
                        gfx.DrawRectangle(penGray, 185 * Mm, curY, 8 * Mm, 5 * Mm);
                        gfx.DrawString("Fin de semana / Festivos", fontLeyenda, XBrushes.SlateGray, new XPoint(195 * Mm, curY + 1 * Mm), XStringFormats.TopLeft);

                        if (datos.DepartamentosColores != null && datos.DepartamentosColores.Count > 0)
                        {
                            curY += 7 * Mm;
                            gfx.DrawString("Colores por Departamento:", fontLeyenda, XBrushes.DarkSlateGray, new XPoint(15 * Mm, curY + 1 * Mm), XStringFormats.TopLeft);
                            
                            double curX = 65 * Mm;
                            foreach (var kvpDC in datos.DepartamentosColores)
                            {
                                double textWidth = gfx.MeasureString(kvpDC.Key, fontLeyenda).Width;
                                double itemWidth = 5 * Mm + 1 * Mm + textWidth + 5 * Mm; // rect + inner margin + text + outer margin

                                if (curX + itemWidth > page.Width.Value - 15 * Mm)
                                {
                                    curX = 65 * Mm;
                                    curY += 6 * Mm;
                                }
                                
                                try {
                                    ColorConverter colorConverter = new ColorConverter();
                                    var color = (Color)colorConverter.ConvertFromString(kvpDC.Value);
                                    gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(color.R, color.G, color.B)), curX, curY, 5 * Mm, 5 * Mm);
                                    gfx.DrawRectangle(penGray, curX, curY, 5 * Mm, 5 * Mm);
                                    gfx.DrawString(kvpDC.Key, fontLeyenda, XBrushes.DarkSlateGray, new XPoint(curX + 6 * Mm, curY + 1 * Mm), XStringFormats.TopLeft);
                                    curX += itemWidth;
                                } catch {}
                            }
                        }
                    }
                    finally
                    {
                        finalTableY = curY;
                        gfx.Dispose();
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
                    textY = finalTableY + 8 * Mm;
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
                PdfExportHelper.DrawHeaderPdf(gfxFinal, pageFinal, datos.TituloPagina, datos.Year);
                textY = 30 * Mm;
            }

            XFont fontFinalTitle = new XFont("Arial", 12.5, XFontStyleEx.Bold);
            XFont fontFinalLabelBold = new XFont("Arial", 10, XFontStyleEx.Bold);
            XFont fontFinalItalic = new XFont("Arial", 9, XFontStyleEx.Italic);

            gfxFinal.DrawString("Cómputo Anual de Vacaciones (Días laborables netos y detalle):", fontFinalTitle, XBrushes.DarkSlateGray, new XPoint(15 * Mm, textY), XStringFormats.TopLeft);
            textY += 10 * Mm;

            double limitY = 175 * Mm;

            foreach (var w in sortedWorkers)
            {
                if (textY > limitY)
                {
                    gfxFinal.Dispose(); // Liberar el recurso activo
                    PdfPage extraPage = document.AddPage();
                    extraPage.Orientation = PageOrientation.Landscape;
                    extraPage.Size = PageSize.A4;
                    gfxFinal = XGraphics.FromPdfPage(extraPage);
                    PdfExportHelper.DrawHeaderPdf(gfxFinal, extraPage, datos.TituloPagina, datos.Year);
                    textY = 30 * Mm;
                }

                var info = datos.Trabajadores[w];

                List<string> consumosList = new List<string>();
                bool cupoSuperado = false;
                foreach (int y in añosAProcesar)
                {
                    var festivosTrabajador = RangoVacacionesHelper.ObtenerFestivosTrabajador(w, datos);
                    int netos = RangoVacacionesHelper.ContarDiasConsumidos(info.Vacaciones, info.Imputaciones, festivosTrabajador, y);
                    int limite = info.DiasBase + info.DiasExtras;
                    if (netos > limite) cupoSuperado = true;
                    consumosList.Add($"{netos} de {limite} (en {y})");
                }
                string consumosStr = string.Join(", ", consumosList);
                string excede = cupoSuperado ? " (¡Cupo superado en algún año!)" : "";

                var vPropias = new List<string>();
                var vCierres = new List<string>();
                string wDept = info.Departamento ?? "General";
                
                foreach (var v in info.Vacaciones)
                {
                    bool isClosure = datos.Cierres != null && (
                        (datos.Cierres.ContainsKey(wDept) && datos.Cierres[wDept].Contains(v)) ||
                        (datos.Cierres.ContainsKey("__todos__") && datos.Cierres["__todos__"].Contains(v))
                    );
                    if (isClosure) vCierres.Add(v);
                    else vPropias.Add(v);
                }

                string rangosPropias = vPropias.Count > 0 ? RangoVacacionesHelper.AgruparVacacionesEnTextoMultiano(vPropias, info.Imputaciones, datos.Festivos, datos.Year) : "Ninguna";
                string rangosCierres = vCierres.Count > 0 ? RangoVacacionesHelper.AgruparVacacionesEnTextoMultiano(vCierres, info.Imputaciones, datos.Festivos, datos.Year) : "";

                gfxFinal.DrawString($"- {w}: {consumosStr} días disfrutados{excede}.", fontFinalLabelBold, XBrushes.DarkSlateGray, new XPoint(18 * Mm, textY), XStringFormats.TopLeft);
                textY += 4.5 * Mm;

                gfxFinal.DrawString($"Vacaciones libres: {rangosPropias}", fontFinalItalic, XBrushes.Gray, new XPoint(25 * Mm, textY), XStringFormats.TopLeft);
                textY += 4.5 * Mm;

                if (!string.IsNullOrEmpty(rangosCierres))
                {
                    gfxFinal.DrawString($"Cierres patronales: {rangosCierres}", fontFinalItalic, XBrushes.Gray, new XPoint(25 * Mm, textY), XStringFormats.TopLeft);
                    textY += 4.5 * Mm;
                }

                var conflictosWorker = new List<string>();
                foreach (var vac in info.Vacaciones)
                {
                    int qYear = (info.Imputaciones != null && info.Imputaciones.TryGetValue(vac, out int yVal)) ? yVal : int.Parse(vac.Substring(6, 4));
                    if (añosAProcesar.Contains(qYear) && RangoVacacionesHelper.EsIncompatible(w, vac, datos))
                    {
                        conflictosWorker.Add(vac.Substring(0, 5));
                    }
                }
                if (conflictosWorker.Count > 0)
                {
                    var confSorted = conflictosWorker.OrderBy(c => DateTime.ParseExact(c + "/" + datos.Year, "dd/MM/yyyy", null)).ToList();
                    gfxFinal.DrawString($"! Incompatibilidades detectadas en: {string.Join(", ", confSorted)}", fontFinalLabelBold, XBrushes.Red, new XPoint(25 * Mm, textY), XStringFormats.TopLeft);
                }
                textY += 8 * Mm;
            }

            gfxFinal.Dispose(); // Liberar el recurso final

            // Agregar footers con paginación correcta a todas las páginas
            int totalPaginasDefinitivo = document.PageCount;
            for (int i = 0; i < totalPaginasDefinitivo; i++)
            {
                PdfPage p = document.Pages[i];
                using (XGraphics gfxPage = XGraphics.FromPdfPage(p))
                {
                    PdfExportHelper.DrawFooterPdf(gfxPage, p, i + 1, totalPaginasDefinitivo, config.PiePaginaPdf);
                }
            }

            document.Save(path);
        }

        private static (List<string> mesesSecuencia, List<DateTime> fechasEjeX) ObtenerSecuenciaGanttPorAno(PlanVacaciones datos, int year, string filtroDpto)
        {
            var todasFechas = new List<DateTime>();
            foreach (var kvp in datos.Trabajadores)
            {
                if (!string.IsNullOrEmpty(filtroDpto) && kvp.Value.Departamento != filtroDpto) continue;

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