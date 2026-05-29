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
    public class PdfMensualService : IPdfMensualService
    {
        public static IPdfMensualService Instance { get; } = new PdfMensualService();

        private const double Mm = 72.0 / 25.4;


        /// <summary>
        /// Exporta la planilla mensual a un archivo PDF con soporte multiaño.
        /// </summary>
        public void ExportarMensual(string path, PlanVacaciones datos, AppConfig config, List<int> anos, string filtroDpto = "")
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var filteredWorkers = datos.Trabajadores
                .Where(w => string.IsNullOrEmpty(filtroDpto) || w.Value.Departamento == filtroDpto)
                .ToDictionary(k => k.Key, v => v.Value);

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
                    GenerarUnicoPdfMensual(yearPath, datos, config, new List<int> { year }, filtroDpto);
                }
            }
            else
            {
                GenerarUnicoPdfMensual(path, datos, config, anosAProcesar, filtroDpto);
            }
        }


        private static void GenerarUnicoPdfMensual(string path, PlanVacaciones datos, AppConfig config, List<int> añosAProcesar, string filtroDpto)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            var filteredWorkers = datos.Trabajadores
                .Where(w => string.IsNullOrEmpty(filtroDpto) || w.Value.Departamento == filtroDpto)
                .ToDictionary(k => k.Key, v => v.Value);

            PdfDocument document = new PdfDocument();
            document.Info.Title = datos.TituloPagina;

            var mesesBase = config.MesesAMostrar.OrderBy(m => m).ToList();
            if (mesesBase.Count == 0) mesesBase = new List<int> { 6, 7, 8, 9 };

            bool esLandscape = config.OrientacionPdf == "Landscape";
            int colsPorPagina = esLandscape ? 3 : 2;
            int filasPorPagina = esLandscape ? 2 : 3;
            int mesesPorPagina = colsPorPagina * filasPorPagina;

            string[] nombresMeses = { "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };
            string[] daysHeader = { "L", "M", "X", "J", "V", "S", "D" };

            // 1. Agrupar y filtrar los meses por año de cupo
            var mesesPorAñoCupo = new Dictionary<int, List<(int mes, int yearNatural)>>();

            foreach (int quotaYear in añosAProcesar)
            {
                var listaMeses = new List<(int mes, int yearNatural)>();
                foreach (int m in mesesBase)
                {
                    if (!config.OcultarMesesSinDias || PdfExportHelper.CupoMesTieneDiasMarcados(datos, m, quotaYear, quotaYear, filtroDpto))
                    {
                        listaMeses.Add((m, quotaYear));
                    }
                }

                if (listaMeses.Count == 0 && quotaYear == datos.Year)
                {
                    foreach (int m in mesesBase)
                    {
                        listaMeses.Add((m, quotaYear));
                    }
                }

                var mesesAdicionales = new List<(int mes, int yearNatural)>();
                foreach (var w in filteredWorkers.Values)
                {
                    foreach (var v in w.Vacaciones)
                    {
                        if (DateTime.TryParseExact(v, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d))
                        {
                            int val = 0;
                            int qYear = (w.Imputaciones != null && w.Imputaciones.TryGetValue(v, out val)) ? val : d.Year;
                            if (qYear == quotaYear)
                            {
                                var item = (d.Month, d.Year);
                                if (!listaMeses.Contains(item) && !mesesAdicionales.Contains(item))
                                {
                                    mesesAdicionales.Add(item);
                                }
                            }
                        }
                    }
                }
                mesesAdicionales = mesesAdicionales.OrderBy(x => x.yearNatural).ThenBy(x => x.mes).ToList();
                listaMeses.AddRange(mesesAdicionales);

                if (listaMeses.Count > 0)
                {
                    mesesPorAñoCupo[quotaYear] = listaMeses;
                }
            }

            XFont fontTitle = new XFont("Arial", 12, XFontStyleEx.Bold);
            XFont fontDays = new XFont("Arial", 9, XFontStyleEx.Bold);
            XFont fontCells = new XFont("Arial", 9, XFontStyleEx.Regular);
            XFont fontCellsBold = new XFont("Arial", 7.5, XFontStyleEx.Bold);
            XFont fontInitials = new XFont("Arial", 6.5, XFontStyleEx.Bold);
            XPen penGray = new XPen(XColor.FromArgb(200, 200, 200), 0.4);

            int pagNumGlobal = 1;

            // 3. Renderizado de informes por año de cupo
            foreach (int quotaYear in añosAProcesar)
            {
                if (!mesesPorAñoCupo.TryGetValue(quotaYear, out var meses) || meses.Count == 0)
                    continue;

                // --- DIBUJAR CALENDARIOS DE ESTE CUPO ---
                int paginasCalendario = (int)Math.Ceiling((double)meses.Count / mesesPorPagina);
                int mesIndex = 0;

                for (int pag = 0; pag < paginasCalendario; pag++)
                {
                    PdfPage page = document.AddPage();
                    page.Size = PageSize.A4;
                    page.Orientation = esLandscape ? PageOrientation.Landscape : PageOrientation.Portrait;

                    using (XGraphics gfx = XGraphics.FromPdfPage(page))
                    {
                        PdfExportHelper.DrawHeaderPdf(gfx, page, datos.TituloPagina, quotaYear);

                        DibujarCalendariosEnPagina(gfx, page, meses, mesIndex, mesesPorPagina, colsPorPagina, filasPorPagina, nombresMeses, daysHeader, fontTitle, fontDays, fontCells, fontCellsBold, fontInitials, penGray, quotaYear, datos, filtroDpto);
                    }
                    mesIndex += mesesPorPagina;
                }

                // --- DIBUJAR RESUMEN DE ESTE CUPO ---
                double textY = 0;
                PdfPage pageResumen = null!;
                XGraphics gfx2 = null!;
                bool usarNuevaPaginaParaResumen = config.ForzarSaltoPagina;

                if (!usarNuevaPaginaParaResumen && document.Pages.Count > 0)
                {
                    pageResumen = document.Pages[document.Pages.Count - 1];
                    double pageH = pageResumen.Height.Value;
                    double marginTop = 22 * Mm;
                    double gapY = 5 * Mm;
                    double areaH = pageH - marginTop - 18 * Mm;
                    double rowHeight = (areaH - (filasPorPagina - 1) * gapY) / filasPorPagina;

                    int mesesEnUltimaPagina = meses.Count % mesesPorPagina;
                    if (mesesEnUltimaPagina == 0) mesesEnUltimaPagina = mesesPorPagina;
                    int filasOcupadasUltima = (int)Math.Ceiling((double)mesesEnUltimaPagina / colsPorPagina);

                    double startY = marginTop + filasOcupadasUltima * (rowHeight + gapY) + 5 * Mm;
                    double estimatedNeededHeight = 35 * Mm;

                    if (filasOcupadasUltima < filasPorPagina && startY + estimatedNeededHeight <= pageResumen.Height.Value - 18 * Mm)
                    {
                        textY = startY;
                        gfx2 = XGraphics.FromPdfPage(pageResumen);
                    }
                    else
                    {
                        usarNuevaPaginaParaResumen = true;
                    }
                }

                if (usarNuevaPaginaParaResumen || document.Pages.Count == 0)
                {
                    pageResumen = document.AddPage();
                    pageResumen.Size = PageSize.A4;
                    pageResumen.Orientation = esLandscape ? PageOrientation.Landscape : PageOrientation.Portrait;
                    gfx2 = XGraphics.FromPdfPage(pageResumen);
                    PdfExportHelper.DrawHeaderPdf(gfx2, pageResumen, datos.TituloPagina, quotaYear);
                    textY = 28 * Mm;
                }

                XFont fontH2 = new XFont("Arial", 12, XFontStyleEx.Bold);
                XFont fontLabel = new XFont("Arial", 9.5, XFontStyleEx.Regular);
                XFont fontLabelBold = new XFont("Arial", 9.5, XFontStyleEx.Bold);
                XFont fontItalic = new XFont("Arial", 8.5, XFontStyleEx.Italic);

                gfx2.DrawString("Resumen de Vacaciones y Leyenda", fontH2, XBrushes.DarkSlateGray, new XPoint(15 * Mm, textY), XStringFormats.TopLeft);
                textY += 6 * Mm;

                // Caja Leyenda de Vacación (Azul)
                gfx2.DrawRectangle(new XSolidBrush(XColor.FromArgb(174, 214, 241)), 15 * Mm, textY, 20 * Mm, 7 * Mm);
                gfx2.DrawRectangle(penGray, 15 * Mm, textY, 20 * Mm, 7 * Mm);
                gfx2.DrawString("Día(XX)", new XFont("Arial", 7.5, XFontStyleEx.Bold), new XSolidBrush(XColor.FromArgb(27, 79, 114)), new XRect(15 * Mm, textY, 20 * Mm, 7 * Mm), XStringFormats.Center);
                gfx2.DrawString("Vacaciones (Color Dpto. Oscuro/Distinto para año anterior)", fontLabel, XBrushes.DarkSlateGray, new XPoint(38 * Mm, textY + 2 * Mm), XStringFormats.TopLeft);
                textY += 8.5 * Mm;

                // Caja Leyenda Cierre
                gfx2.DrawRectangle(new XSolidBrush(XColor.FromArgb(250, 215, 161)), 15 * Mm, textY, 20 * Mm, 7 * Mm);
                gfx2.DrawRectangle(penGray, 15 * Mm, textY, 20 * Mm, 7 * Mm);
                gfx2.DrawString("CDía", new XFont("Arial", 7.5, XFontStyleEx.Bold), new XSolidBrush(XColor.FromArgb(27, 79, 114)), new XRect(15 * Mm, textY, 20 * Mm, 7 * Mm), XStringFormats.Center);
                gfx2.DrawString("Cierre Patronal (Color claro del dpto.)", fontLabel, XBrushes.DarkSlateGray, new XPoint(38 * Mm, textY + 2 * Mm), XStringFormats.TopLeft);
                textY += 8.5 * Mm;

                // Caja Leyenda de Incompatibilidad
                gfx2.DrawRectangle(new XSolidBrush(XColors.White), 15 * Mm, textY, 20 * Mm, 7 * Mm);
                gfx2.DrawRectangle(penGray, 15 * Mm, textY, 20 * Mm, 7 * Mm);
                gfx2.DrawString("!Día(XX)", new XFont("Arial", 7.5, XFontStyleEx.Bold), XBrushes.Red, new XRect(15 * Mm, textY, 20 * Mm, 7 * Mm), XStringFormats.Center);
                gfx2.DrawString("Incompatibilidad detectada (¡)", fontLabel, XBrushes.DarkSlateGray, new XPoint(38 * Mm, textY + 2 * Mm), XStringFormats.TopLeft);
                textY += 8.5 * Mm;

                // Caja Leyenda Finde/Festivo
                gfx2.DrawRectangle(new XSolidBrush(XColor.FromArgb(244, 246, 247)), 15 * Mm, textY, 20 * Mm, 7 * Mm);
                gfx2.DrawRectangle(penGray, 15 * Mm, textY, 20 * Mm, 7 * Mm);
                gfx2.DrawString("14", new XFont("Arial", 7.5, XFontStyleEx.Bold), new XSolidBrush(XColor.FromArgb(231, 76, 60)), new XRect(15 * Mm, textY, 20 * Mm, 7 * Mm), XStringFormats.Center);
                gfx2.DrawString("Fines de semana o días festivos oficiales", fontLabel, XBrushes.DarkSlateGray, new XPoint(38 * Mm, textY + 2 * Mm), XStringFormats.TopLeft);
                textY += 10 * Mm;

                if (datos.DepartamentosColores != null && datos.DepartamentosColores.Count > 0)
                {
                    gfx2.DrawString("Colores por Departamento:", fontH2, XBrushes.DarkSlateGray, new XPoint(15 * Mm, textY), XStringFormats.TopLeft);
                    textY += 6 * Mm;
                    
                    double curX = 15 * Mm;
                    foreach (var kvp in datos.DepartamentosColores)
                    {
                        if (curX > pageResumen.Width.Value - 40 * Mm)
                        {
                            curX = 15 * Mm;
                            textY += 6 * Mm;
                        }
                        
                        try {
                            ColorConverter colorConverter = new ColorConverter();
                            var color = (Color)colorConverter.ConvertFromString(kvp.Value);
                            gfx2.DrawRectangle(new XSolidBrush(XColor.FromArgb(color.R, color.G, color.B)), curX, textY, 5 * Mm, 5 * Mm);
                            gfx2.DrawRectangle(penGray, curX, textY, 5 * Mm, 5 * Mm);
                            gfx2.DrawString(kvp.Key, fontLabel, XBrushes.DarkSlateGray, new XPoint(curX + 6 * Mm, textY + 4 * Mm), XStringFormats.BottomLeft);
                            curX += 45 * Mm;
                        } catch {}
                    }
                    textY += 8 * Mm;
                }

                XPen penLight = new XPen(XColor.FromArgb(220, 220, 220), 0.4);
                gfx2.DrawLine(penLight, 15 * Mm, textY, pageResumen.Width.Value - 15 * Mm, textY);
                textY += 4 * Mm;

                gfx2.DrawString("Disfrute de Vacaciones (Días laborables netos consumidos y detalle):", fontH2, XBrushes.DarkSlateGray, new XPoint(15 * Mm, textY), XStringFormats.TopLeft);
                textY += 7 * Mm;

                double limitY = pageResumen.Height.Value - 25 * Mm;

                Action CheckPageBreak = () =>
                {
                    if (textY > limitY)
                    {
                        if (gfx2 != null) gfx2.Dispose();
                        pageResumen = document.AddPage();
                        pageResumen.Size = PageSize.A4;
                        pageResumen.Orientation = esLandscape ? PageOrientation.Landscape : PageOrientation.Portrait;
                        gfx2 = XGraphics.FromPdfPage(pageResumen);
                        PdfExportHelper.DrawHeaderPdf(gfx2, pageResumen, datos.TituloPagina, quotaYear);
                        textY = 28 * Mm;
                    }
                };

                foreach (var kvpWorker in filteredWorkers.OrderBy(n => n.Key))
                {
                    CheckPageBreak();

                    string w = kvpWorker.Key;
                    var info = kvpWorker.Value;

                    var festivosTrabajador = RangoVacacionesHelper.ObtenerFestivosTrabajador(w, datos);
                    int netos = RangoVacacionesHelper.ContarDiasConsumidos(info.Vacaciones, info.Imputaciones, festivosTrabajador, quotaYear);
                    int limite = info.DiasBase + info.DiasExtras;
                    string consumosStr = $"{netos} de {limite} (en {quotaYear})";
                    string excede = netos > limite ? " (¡Cupo superado!)" : "";

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

                    string rangosPropias = vPropias.Count > 0 ? RangoVacacionesHelper.AgruparVacacionesEnTextoMultiano(vPropias, info.Imputaciones, festivosTrabajador, quotaYear) : "Ninguna";
                    string rangosCierres = vCierres.Count > 0 ? RangoVacacionesHelper.AgruparVacacionesEnTextoMultiano(vCierres, info.Imputaciones, festivosTrabajador, quotaYear) : "";

                    gfx2.DrawString($"- {w}: {consumosStr} días consumidos{excede}.", fontLabelBold, XBrushes.DarkSlateGray, new XPoint(18 * Mm, textY), XStringFormats.TopLeft);
                    textY += 4.5 * Mm;
                    CheckPageBreak();

                    gfx2.DrawString($"Vacaciones libres: {rangosPropias}", fontItalic, XBrushes.Gray, new XPoint(25 * Mm, textY), XStringFormats.TopLeft);
                    textY += 4.5 * Mm;
                    CheckPageBreak();

                    if (!string.IsNullOrEmpty(rangosCierres))
                    {
                        gfx2.DrawString($"Cierres patronales: {rangosCierres}", fontItalic, XBrushes.Gray, new XPoint(25 * Mm, textY), XStringFormats.TopLeft);
                        textY += 4.5 * Mm;
                        CheckPageBreak();
                    }

                    // Buscar conflictos de este trabajador en el año de cupo
                    var conflictosWorker = new List<string>();
                    foreach (var vac in info.Vacaciones)
                    {
                        int qYear = (info.Imputaciones != null && info.Imputaciones.TryGetValue(vac, out int yVal)) ? yVal : int.Parse(vac.Substring(6, 4));
                        if (qYear == quotaYear && RangoVacacionesHelper.EsIncompatible(w, vac, datos))
                        {
                            conflictosWorker.Add(vac.Substring(0, 5));
                        }
                    }
                    if (conflictosWorker.Count > 0)
                    {
                        var confSorted = conflictosWorker.OrderBy(c => DateTime.ParseExact(c + "/" + quotaYear, "dd/MM/yyyy", null)).ToList();
                        gfx2.DrawString($"! Incompatibilidades detectadas en: {string.Join(", ", confSorted)}", fontLabelBold, XBrushes.Red, new XPoint(25 * Mm, textY), XStringFormats.TopLeft);
                        textY += 6 * Mm;
                        CheckPageBreak();
                    }
                    else
                    {
                        textY += 2 * Mm; // Separación extra si no hay conflicto
                        CheckPageBreak();
                    }
                }

                gfx2.Dispose(); // Liberar el recurso gráfico de este cupo
            }

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


        private static void DibujarCalendariosEnPagina(XGraphics gfx, PdfPage page, List<(int mes, int yearNatural)> meses, int mesIndex, int mesesPorPagina, int colsPorPagina, int filasPorPagina, string[] nombresMeses, string[] daysHeader, XFont fontTitle, XFont fontDays, XFont fontCells, XFont fontCellsBold, XFont fontInitials, XPen penGray, int quotaYear, PlanVacaciones datos, string filtroDpto)
        {
            double pageW = page.Width.Value;
            double pageH = page.Height.Value;

            double marginL = 12 * Mm;
            double marginR = 12 * Mm;
            double marginTop = 22 * Mm;
            double gapX = 5 * Mm;
            double gapY = 5 * Mm;

            double areaW = pageW - marginL - marginR;
            double areaH = pageH - marginTop - 18 * Mm;

            double colWidth = (areaW - (colsPorPagina - 1) * gapX) / colsPorPagina;
            double rowHeight = (areaH - (filasPorPagina - 1) * gapY) / filasPorPagina;

            for (int slot = 0; slot < mesesPorPagina && mesIndex < meses.Count; slot++, mesIndex++)
            {
                var (mes, yearNatural) = meses[mesIndex];
                int col = slot % colsPorPagina;
                int row = slot / colsPorPagina;

                double xStart = marginL + col * (colWidth + gapX);
                double yStart = marginTop + row * (rowHeight + gapY);

                DibujarMesCalendario(gfx, xStart, yStart, colWidth, rowHeight, mes, yearNatural, quotaYear, nombresMeses, daysHeader, fontTitle, fontDays, fontCells, fontCellsBold, fontInitials, penGray, datos, filtroDpto);
            }
        }


        private static void DibujarMesCalendario(XGraphics gfx, double xStart, double yStart, double colWidth, double rowHeight, int mes, int year, int quotaYear, string[] nombresMeses, string[] daysHeader, XFont fontTitle, XFont fontDays, XFont fontCells, XFont fontCellsBold, XFont fontInitials, XPen penGray, PlanVacaciones datos, string filtroDpto)
        {
            gfx.DrawString($"{nombresMeses[mes].ToUpper()} {year}", fontTitle, XBrushes.DarkSlateGray, new XPoint(xStart + colWidth / 2, yStart + 3 * Mm), XStringFormats.TopCenter);

            double cellW = colWidth / 7;
            double cellHHeader = 5 * Mm;
            double cellH = (rowHeight - 10 * Mm - cellHHeader) / 6;

            double curX = xStart;
            double curY = yStart + 8 * Mm;

            foreach (var h in daysHeader)
            {
                gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(242, 244, 244)), curX, curY, cellW, cellHHeader);
                gfx.DrawRectangle(penGray, curX, curY, cellW, cellHHeader);
                gfx.DrawString(h, fontDays, XBrushes.DarkGray, new XRect(curX, curY, cellW, cellHHeader), XStringFormats.Center);
                curX += cellW;
            }

            curY += cellHHeader;

            DateTime firstDay = new DateTime(year, mes, 1);
            int startOffset = ((int)firstDay.DayOfWeek == 0) ? 6 : (int)firstDay.DayOfWeek - 1;
            int totalDays = DateTime.DaysInMonth(year, mes);

            int dayCounter = 1;
            int currentOffset = 0;

            while (dayCounter <= totalDays)
            {
                curX = xStart;
                for (int c = 0; c < 7; c++)
                {
                    if (currentOffset < startOffset || dayCounter > totalDays)
                    {
                        gfx.DrawRectangle(penGray, curX, curY, cellW, cellH);
                    }
                    else
                    {
                        string dateStr = $"{dayCounter:00}/{mes:00}/{year}";
                        bool esWeekend = (c >= 5);
                        bool esFestivo = datos.Festivos.Contains(dateStr) ||
                                         (!string.IsNullOrEmpty(filtroDpto) &&
                                          datos.FestivosDepartamento != null &&
                                          datos.FestivosDepartamento.ContainsKey(filtroDpto) &&
                                          datos.FestivosDepartamento[filtroDpto].Contains(dateStr));

                        XColor fillC = XColors.White;
                        XColor textC = XColor.FromArgb(44, 62, 80);
                        bool isFilled = false;

                        if (esWeekend || esFestivo)
                        {
                            fillC = XColor.FromArgb(244, 246, 247);
                            textC = XColor.FromArgb(231, 76, 60);
                            isFilled = true;
                        }

                        var trabsVac = datos.Trabajadores
                            .Where(k => k.Value.Vacaciones.Contains(dateStr) && (string.IsNullOrEmpty(filtroDpto) || k.Value.Departamento == filtroDpto))
                            .Select(k => k.Key)
                            .ToList();

                        bool tieneCierre = false;
                        bool tieneConflicto = false;
                        if (datos.Cierres != null)
                        {
                            if (datos.Cierres.ContainsKey("__todos__") && datos.Cierres["__todos__"].Contains(dateStr)) tieneCierre = true;
                            
                            if (!tieneCierre && !string.IsNullOrEmpty(filtroDpto) && datos.Cierres.ContainsKey(filtroDpto) && datos.Cierres[filtroDpto].Contains(dateStr)) tieneCierre = true;
                            
                            if (!tieneCierre && trabsVac.Count > 0)
                            {
                                foreach(var w in trabsVac)
                                {
                                    string dpt = datos.Trabajadores.TryGetValue(w, out var wInfo) ? (wInfo.Departamento ?? "General") : "General";
                                    if (datos.Cierres.ContainsKey(dpt) && datos.Cierres[dpt].Contains(dateStr))
                                    {
                                        tieneCierre = true; break;
                                    }
                                }
                            }
                            
                            // Si no hay trabajadores de vacaciones ni filtro, comprobar si cualquier departamento tiene cierre
                            if (!tieneCierre && trabsVac.Count == 0 && string.IsNullOrEmpty(filtroDpto))
                            {
                                foreach (var cierreKvp in datos.Cierres)
                                {
                                    if (cierreKvp.Value.Contains(dateStr))
                                    {
                                        tieneCierre = true; break;
                                    }
                                }
                            }
                        }

                        if (trabsVac.Count > 0)
                        {
                            foreach (var w in trabsVac)
                            {
                                if (RangoVacacionesHelper.EsIncompatible(w, dateStr, datos))
                                {
                                    tieneConflicto = true;
                                    break;
                                }
                            }
                        }

                        if (trabsVac.Count > 0 || tieneCierre)
                        {
                            bool todosOtroCupo = trabsVac.Count > 0;
                            foreach (var t in trabsVac)
                            {
                                if (datos.Trabajadores.TryGetValue(t, out var tInfo))
                                {
                                    int qYear = (tInfo.Imputaciones != null && tInfo.Imputaciones.TryGetValue(dateStr, out int yVal)) ? yVal : year;
                                    if (qYear == quotaYear)
                                    {
                                        todosOtroCupo = false;
                                        break;
                                    }
                                }
                            }

                            string dptColorHex = null;
                            if (datos.DepartamentosColores != null)
                            {
                                if (!string.IsNullOrEmpty(filtroDpto) && datos.DepartamentosColores.ContainsKey(filtroDpto))
                                {
                                    dptColorHex = datos.DepartamentosColores[filtroDpto];
                                }
                                else if (trabsVac.Count > 0)
                                {
                                    string wSample = trabsVac.First();
                                    string wDpt = datos.Trabajadores.TryGetValue(wSample, out var wI) ? wI.Departamento : null;
                                    if (!string.IsNullOrEmpty(wDpt) && datos.DepartamentosColores.ContainsKey(wDpt))
                                    {
                                        dptColorHex = datos.DepartamentosColores[wDpt];
                                    }
                                }
                            }

                            if (dptColorHex != null && dptColorHex.StartsWith("#") && dptColorHex.Length >= 7)
                            {
                                try {
                                    byte r = Convert.ToByte(dptColorHex.Substring(1, 2), 16);
                                    byte g = Convert.ToByte(dptColorHex.Substring(3, 2), 16);
                                    byte b = Convert.ToByte(dptColorHex.Substring(5, 2), 16);
                                    fillC = XColor.FromArgb(r, g, b);
                                    textC = XColor.FromArgb(27, 79, 114); // Azul oscuro
                                    
                                    if (todosOtroCupo && trabsVac.Count > 0)
                                    {
                                        // Año anterior: color más oscuro o distinto en la paleta
                                        fillC = XColor.FromArgb(200, (byte)Math.Max(0, r - 40), (byte)Math.Max(0, g - 40), (byte)Math.Max(0, b - 40));
                                        textC = XColor.FromArgb(255, 255, 255); // Texto blanco
                                    }
                                    else if (tieneCierre && trabsVac.Count == 0)
                                    {
                                        // Cierre: color más claro
                                        fillC = XColor.FromArgb(120, r, g, b);
                                    }
                                } catch { 
                                    fillC = XColor.FromArgb(174, 214, 241); 
                                    textC = XColor.FromArgb(27, 79, 114);
                                }
                            }
                            else
                            {
                                if (todosOtroCupo && trabsVac.Count > 0)
                                {
                                    fillC = XColor.FromArgb(243, 232, 255); // Lavanda (#F3E8FF)
                                    textC = XColor.FromArgb(107, 33, 168);   // Lavanda oscuro (#6B21A8)
                                }
                                else
                                {
                                    fillC = tieneCierre && trabsVac.Count == 0 ? XColor.FromArgb(250, 215, 161) : XColor.FromArgb(174, 214, 241);
                                    textC = XColor.FromArgb(27, 79, 114);
                                }
                            }
                            
                            if (tieneConflicto)
                            {
                                textC = XColor.FromArgb(192, 57, 43); // Rojo para conflictos
                            }
                            isFilled = true;
                        }

                        if (isFilled)
                        {
                            gfx.DrawRectangle(new XSolidBrush(fillC), curX, curY, cellW, cellH);
                        }
                        gfx.DrawRectangle(penGray, curX, curY, cellW, cellH);

                        if (trabsVac.Count > 0 || tieneCierre)
                        {
                            string ObtenerChipTexto(string trabajador)
                            {
                                string ini = PdfExportHelper.ObtenerIniciales(trabajador);
                                if (datos.Trabajadores.TryGetValue(trabajador, out var tInfo))
                                {
                                    int qYear = (tInfo.Imputaciones != null && tInfo.Imputaciones.TryGetValue(dateStr, out int yVal)) ? yVal : year;
                                    if (qYear != quotaYear)
                                    {
                                        ini = $"{ini}-{qYear}";
                                    }
                                }
                                return ini;
                            }

                            string prefix = tieneCierre ? "C" : "";
                            if (tieneConflicto) prefix += "!";
                            
                            string initialsText = "";
                            if (trabsVac.Count == 0 && tieneCierre)
                            {
                                initialsText = prefix;
                            }
                            else if (trabsVac.Count == 1)
                            {
                                initialsText = $"{prefix}({ObtenerChipTexto(trabsVac[0])})";
                            }
                            else if (trabsVac.Count == 2)
                            {
                                initialsText = $"{prefix}({ObtenerChipTexto(trabsVac[0])},{ObtenerChipTexto(trabsVac[1])})";
                            }
                            else if (trabsVac.Count > 2)
                            {
                                initialsText = $"{prefix}({ObtenerChipTexto(trabsVac[0])}+{trabsVac.Count - 1})";
                            }

                            var rectDay = new XRect(curX, curY + 1 * Mm, cellW, cellH * 0.45);
                            gfx.DrawString(dayCounter.ToString(), fontCellsBold, new XSolidBrush(textC), rectDay, XStringFormats.TopCenter);

                            if (!string.IsNullOrEmpty(initialsText))
                            {
                                var rectInitials = new XRect(curX, curY + cellH * 0.5, cellW, cellH * 0.4);
                                gfx.DrawString(initialsText, fontInitials, new XSolidBrush(textC), rectInitials, XStringFormats.TopCenter);
                            }
                        }
                        else
                        {
                            gfx.DrawString(dayCounter.ToString(), fontCells, new XSolidBrush(textC), new XRect(curX, curY, cellW, cellH), XStringFormats.Center);
                        }

                        dayCounter++;
                    }
                    currentOffset++;
                    curX += cellW;
                }
                curY += cellH;
            }
        }
    }
}
