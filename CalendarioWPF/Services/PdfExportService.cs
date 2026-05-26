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
    /// <summary>
    /// Servicio de exportación a PDF para el planificador de vacaciones.
    /// Utiliza PdfSharp y aplica una escala precisa en milímetros para maquetar el calendario mensual y la tabla Gantt.
    /// </summary>
    public class PdfExportService : IPdfExportService
    {
        /// <summary>
        /// Instancia única (Singleton) para acceso dinámico a través de la interfaz IPdfExportService.
        /// </summary>
        public static IPdfExportService Instance { get; } = new PdfExportService();

        // Factor de escala: en PdfSharp las coordenadas se expresan en puntos tipográficos (points).
        // 1 pulgada = 72 puntos. 1 pulgada = 25.4 mm. Por tanto, 1 mm = 72 / 25.4 ≈ 2.8346 puntos.
        private const double Mm = 72.0 / 25.4;

        /// <summary>
        /// Determina si un mes tiene días marcados (festivos del año natural o vacaciones imputadas a este cupo).
        /// </summary>
        private static bool CupoMesTieneDiasMarcados(PlanVacaciones datos, int mes, int yearNatural, int quotaYear)
        {
            // Verificar si hay festivos en este mes/año natural
            foreach (var f in datos.Festivos)
            {
                if (DateTime.TryParseExact(f, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d))
                {
                    if (d.Year == yearNatural && d.Month == mes)
                    {
                        return true;
                    }
                }
            }
            // Verificar si hay vacaciones imputadas a este cupo en este mes/año natural
            foreach (var w in datos.Trabajadores.Values)
            {
                foreach (var v in w.Vacaciones)
                {
                    if (DateTime.TryParseExact(v, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d))
                    {
                        if (d.Year == yearNatural && d.Month == mes)
                        {
                            int qYear = (w.Imputaciones != null && w.Imputaciones.TryGetValue(v, out int val)) ? val : d.Year;
                            if (qYear == quotaYear)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Exporta la planilla mensual a un archivo PDF con soporte multiaño.
        /// </summary>
        public static void ExportarMensual(string path, PlanVacaciones datos, AppConfig config, List<int> anos)
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
                    GenerarUnicoPdfMensual(yearPath, datos, config, new List<int> { year });
                }
            }
            else
            {
                GenerarUnicoPdfMensual(path, datos, config, anosAProcesar);
            }
        }

        /// <summary>
        /// Exporta la vista de tabla Gantt a un archivo PDF con soporte multiaño.
        /// </summary>
        public static void ExportarGantt(string path, PlanVacaciones datos, AppConfig config, List<int> anos)
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

        private static void GenerarUnicoPdfMensual(string path, PlanVacaciones datos, AppConfig config, List<int> añosAProcesar)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

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
                    if (!config.OcultarMesesSinDias || CupoMesTieneDiasMarcados(datos, m, quotaYear, quotaYear))
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
                foreach (var w in datos.Trabajadores.Values)
                {
                    foreach (var v in w.Vacaciones)
                    {
                        if (DateTime.TryParseExact(v, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d))
                        {
                            int qYear = (w.Imputaciones != null && w.Imputaciones.TryGetValue(v, out int val)) ? val : d.Year;
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

            int totalPaginas = 0;
            double pageHeightLimit = (esLandscape ? 210 : 297) * Mm;
            double maxSimY = pageHeightLimit - 20 * Mm;

            // 2. Simulación precisa para obtener el total de páginas del reporte
            int pagSimulada = 0;
            foreach (int quotaYear in añosAProcesar)
            {
                if (!mesesPorAñoCupo.TryGetValue(quotaYear, out var meses) || meses.Count == 0)
                    continue;

                int pagsCalCupo = (int)Math.Ceiling((double)meses.Count / mesesPorPagina);
                pagSimulada += pagsCalCupo;

                double simY = 28 * Mm;
                bool usarNuevaPaginaParaResumen = config.ForzarSaltoPagina;

                if (!usarNuevaPaginaParaResumen)
                {
                    int mesesEnUltima = meses.Count % mesesPorPagina;
                    if (mesesEnUltima == 0) mesesEnUltima = mesesPorPagina;
                    int filasOcupadas = (int)Math.Ceiling((double)mesesEnUltima / colsPorPagina);
                    double marginTop = 22 * Mm;
                    double gapY = 5 * Mm;
                    double areaH = pageHeightLimit - marginTop - 18 * Mm;
                    double rowHeight = (areaH - (filasPorPagina - 1) * gapY) / filasPorPagina;
                    double startY = marginTop + filasOcupadas * (rowHeight + gapY) + 5 * Mm;

                    double estimatedNeededHeight = 35 * Mm; // leyenda + espacio básico
                    if (filasOcupadas < filasPorPagina && startY + estimatedNeededHeight <= maxSimY)
                    {
                        simY = startY;
                    }
                    else
                    {
                        usarNuevaPaginaParaResumen = true;
                    }
                }

                if (usarNuevaPaginaParaResumen)
                {
                    pagSimulada++;
                    simY = 28 * Mm;
                }

                // Simular listado de trabajadores
                simY += 6 * Mm; // Título
                simY += 8.5 * Mm; // Leyenda 1
                simY += 8.5 * Mm; // Leyenda 2
                simY += 10 * Mm; // Leyenda 3
                simY += 4 * Mm; // Línea
                simY += 7 * Mm; // Título listado

                foreach (var kvpWorker in datos.Trabajadores.OrderBy(n => n.Key))
                {
                    simY += 4.5 * Mm;
                    simY += 7.5 * Mm;

                    if (simY > maxSimY)
                    {
                        pagSimulada++;
                        simY = 28 * Mm;
                    }
                }
            }
            totalPaginas = pagSimulada == 0 ? 1 : pagSimulada;

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
                        DrawHeaderFooterPdf(gfx, page, datos.TituloPagina, quotaYear, pagNumGlobal++, totalPaginas, config.PiePaginaPdf);

                        DibujarCalendariosEnPagina(gfx, page, meses, mesIndex, mesesPorPagina, colsPorPagina, filasPorPagina, nombresMeses, daysHeader, fontTitle, fontDays, fontCells, fontCellsBold, fontInitials, penGray, quotaYear, datos);
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

                    if (filasOcupadasUltima < filasPorPagina && startY + estimatedNeededHeight <= maxSimY)
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
                    DrawHeaderFooterPdf(gfx2, pageResumen, datos.TituloPagina, quotaYear, pagNumGlobal++, totalPaginas, config.PiePaginaPdf);
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
                gfx2.DrawString("Vacaciones disfrutadas (imputadas al año en curso)", fontLabel, XBrushes.DarkSlateGray, new XPoint(38 * Mm, textY + 2 * Mm), XStringFormats.TopLeft);
                textY += 8.5 * Mm;

                // Caja Leyenda de Vacación Otro Año (Lavanda)
                gfx2.DrawRectangle(new XSolidBrush(XColor.FromArgb(243, 232, 255)), 15 * Mm, textY, 20 * Mm, 7 * Mm);
                gfx2.DrawRectangle(penGray, 15 * Mm, textY, 20 * Mm, 7 * Mm);
                gfx2.DrawString("Día(XX-Año)", new XFont("Arial", 7.5, XFontStyleEx.Bold), new XSolidBrush(XColor.FromArgb(107, 33, 168)), new XRect(15 * Mm, textY, 20 * Mm, 7 * Mm), XStringFormats.Center);
                gfx2.DrawString("Vacaciones imputadas a otro año (lavanda)", fontLabel, XBrushes.DarkSlateGray, new XPoint(38 * Mm, textY + 2 * Mm), XStringFormats.TopLeft);
                textY += 8.5 * Mm;

                // Caja Leyenda Finde/Festivo
                gfx2.DrawRectangle(new XSolidBrush(XColor.FromArgb(244, 246, 247)), 15 * Mm, textY, 20 * Mm, 7 * Mm);
                gfx2.DrawRectangle(penGray, 15 * Mm, textY, 20 * Mm, 7 * Mm);
                gfx2.DrawString("14", new XFont("Arial", 7.5, XFontStyleEx.Bold), new XSolidBrush(XColor.FromArgb(231, 76, 60)), new XRect(15 * Mm, textY, 20 * Mm, 7 * Mm), XStringFormats.Center);
                gfx2.DrawString("Fines de semana o días festivos oficiales", fontLabel, XBrushes.DarkSlateGray, new XPoint(38 * Mm, textY + 2 * Mm), XStringFormats.TopLeft);
                textY += 10 * Mm;

                XPen penLight = new XPen(XColor.FromArgb(220, 220, 220), 0.4);
                gfx2.DrawLine(penLight, 15 * Mm, textY, pageResumen.Width.Value - 15 * Mm, textY);
                textY += 4 * Mm;

                gfx2.DrawString("Disfrute de Vacaciones (Días laborables netos consumidos y detalle):", fontH2, XBrushes.DarkSlateGray, new XPoint(15 * Mm, textY), XStringFormats.TopLeft);
                textY += 7 * Mm;

                foreach (var kvpWorker in datos.Trabajadores.OrderBy(n => n.Key))
                {
                    string w = kvpWorker.Key;
                    var info = kvpWorker.Value;

                    int netos = RangoVacacionesHelper.ContarDiasConsumidos(info.Vacaciones, info.Imputaciones, datos.Festivos, quotaYear);
                    int limite = info.DiasBase + info.DiasExtras;
                    string consumosStr = $"{netos} de {limite} (en {quotaYear})";
                    string excede = netos > limite ? " (¡Cupo superado!)" : "";

                    string rangosTexto = RangoVacacionesHelper.AgruparVacacionesEnTextoMultiano(info.Vacaciones, info.Imputaciones, datos.Festivos, quotaYear);

                    gfx2.DrawString($"- {w}: {consumosStr} días consumidos{excede}.", fontLabelBold, XBrushes.DarkSlateGray, new XPoint(18 * Mm, textY), XStringFormats.TopLeft);
                    textY += 4.5 * Mm;

                    gfx2.DrawString($"Detalle: {rangosTexto}", fontItalic, XBrushes.Gray, new XPoint(25 * Mm, textY), XStringFormats.TopLeft);
                    textY += 7.5 * Mm;

                    if (textY > maxSimY)
                    {
                        gfx2.Dispose(); // Liberar el recurso gráfico activo antes de abrir uno nuevo
                        PdfPage extraPage = document.AddPage();
                        extraPage.Size = PageSize.A4;
                        extraPage.Orientation = esLandscape ? PageOrientation.Landscape : PageOrientation.Portrait;
                        gfx2 = XGraphics.FromPdfPage(extraPage);
                        DrawHeaderFooterPdf(gfx2, extraPage, datos.TituloPagina, quotaYear, pagNumGlobal++, totalPaginas, config.PiePaginaPdf);
                        textY = 28 * Mm;
                        maxSimY = extraPage.Height.Value - 20 * Mm;
                    }
                }

                gfx2.Dispose(); // Liberar el recurso gráfico de este cupo
            }

            document.Save(path);
        }

        private static void DibujarCalendariosEnPagina(XGraphics gfx, PdfPage page, List<(int mes, int yearNatural)> meses, int mesIndex, int mesesPorPagina, int colsPorPagina, int filasPorPagina, string[] nombresMeses, string[] daysHeader, XFont fontTitle, XFont fontDays, XFont fontCells, XFont fontCellsBold, XFont fontInitials, XPen penGray, int quotaYear, PlanVacaciones datos)
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

                DibujarMesCalendario(gfx, xStart, yStart, colWidth, rowHeight, mes, yearNatural, quotaYear, nombresMeses, daysHeader, fontTitle, fontDays, fontCells, fontCellsBold, fontInitials, penGray, datos);
            }
        }

        private static void DibujarMesCalendario(XGraphics gfx, double xStart, double yStart, double colWidth, double rowHeight, int mes, int year, int quotaYear, string[] nombresMeses, string[] daysHeader, XFont fontTitle, XFont fontDays, XFont fontCells, XFont fontCellsBold, XFont fontInitials, XPen penGray, PlanVacaciones datos)
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
                        bool esFestivo = datos.Festivos.Contains(dateStr);

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
                            .Where(k => k.Value.Vacaciones.Contains(dateStr))
                            .Select(k => k.Key)
                            .ToList();

                        if (trabsVac.Count > 0)
                        {
                            bool todosOtroCupo = true;
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

                            if (todosOtroCupo)
                            {
                                fillC = XColor.FromArgb(243, 232, 255); // Lavanda (#F3E8FF)
                                textC = XColor.FromArgb(107, 33, 168);   // Lavanda oscuro (#6B21A8)
                            }
                            else
                            {
                                fillC = XColor.FromArgb(174, 214, 241); // Azul claro (#AED6F1)
                                textC = XColor.FromArgb(27, 79, 114);   // Azul oscuro
                            }
                            isFilled = true;
                        }

                        if (isFilled)
                        {
                            gfx.DrawRectangle(new XSolidBrush(fillC), curX, curY, cellW, cellH);
                        }
                        gfx.DrawRectangle(penGray, curX, curY, cellW, cellH);

                        if (trabsVac.Count > 0)
                        {
                            string ObtenerChipTexto(string trabajador)
                            {
                                string ini = ObtenerIniciales(trabajador);
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

                            string initialsText = "";
                            if (trabsVac.Count == 1)
                            {
                                initialsText = $"({ObtenerChipTexto(trabsVac[0])})";
                            }
                            else if (trabsVac.Count == 2)
                            {
                                initialsText = $"({ObtenerChipTexto(trabsVac[0])},{ObtenerChipTexto(trabsVac[1])})";
                            }
                            else
                            {
                                initialsText = $"({ObtenerChipTexto(trabsVac[0])}+{trabsVac.Count - 1})";
                            }

                            var rectDay = new XRect(curX, curY + 1 * Mm, cellW, cellH * 0.45);
                            gfx.DrawString(dayCounter.ToString(), fontCellsBold, new XSolidBrush(textC), rectDay, XStringFormats.TopCenter);

                            var rectInitials = new XRect(curX, curY + cellH * 0.5, cellW, cellH * 0.4);
                            gfx.DrawString(initialsText, fontInitials, new XSolidBrush(textC), rectInitials, XStringFormats.TopCenter);
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
                        DrawHeaderFooterPdf(gfx, page, datos.TituloPagina, year, pagNumGlobal++, totalPaginas, config.PiePaginaPdf);

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
                DrawHeaderFooterPdf(gfxFinal, pageFinal, datos.TituloPagina, datos.Year, pagNumGlobal++, totalPaginas, config.PiePaginaPdf);
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
                    DrawHeaderFooterPdf(gfxFinal, extraPage, datos.TituloPagina, datos.Year, pagNumGlobal++, totalPaginas, config.PiePaginaPdf);
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

        #region Implementación de IPdfExportService

        void IPdfExportService.ExportarMensual(string path, PlanVacaciones datos, AppConfig config, List<int> años) => ExportarMensual(path, datos, config, años);
        void IPdfExportService.ExportarGantt(string path, PlanVacaciones datos, AppConfig config, List<int> años) => ExportarGantt(path, datos, config, años);

        #endregion

        /// <summary>
        /// Método estático privado para dibujar el encabezado y pie de página de las hojas PDF de forma consistente.
        /// </summary>
        private static void DrawHeaderFooterPdf(XGraphics gfx, PdfPage page, string titulo, int year, int pagNum, int totalPags, string piePagina)
        {
            double width = page.Width.Value;
            double height = page.Height.Value;

            XFont fontHeader = new XFont("Arial", 13, XFontStyleEx.Bold);
            XFont fontHeaderSub = new XFont("Arial", 9.5, XFontStyleEx.Italic);
            XFont fontFooter = new XFont("Arial", 8.5, XFontStyleEx.Regular);

            // Título a la izquierda
            gfx.DrawString($"{titulo} - {year}", fontHeader, XBrushes.DarkSlateGray, new XPoint(15 * Mm, 12 * Mm), XStringFormats.TopLeft);

            // Fecha de generación a la derecha
            string dateStr = DateTime.Now.ToString("dd/MM/yyyy");
            gfx.DrawString($"Generado: {dateStr}", fontHeaderSub, XBrushes.Gray, new XPoint(width - 15 * Mm, 12 * Mm), XStringFormats.TopRight);

            // Línea divisoria superior
            XPen pen = new XPen(XColor.FromArgb(200, 200, 200), 0.4);
            gfx.DrawLine(pen, 15 * Mm, 18 * Mm, width - 15 * Mm, 18 * Mm);

            // Línea y textos del pie de página (footer)
            gfx.DrawLine(pen, 15 * Mm, height - 15 * Mm, width - 15 * Mm, height - 15 * Mm);
            gfx.DrawString(piePagina, fontFooter, XBrushes.Gray, new XPoint(15 * Mm, height - 11 * Mm), XStringFormats.TopLeft);
            gfx.DrawString($"Página {pagNum} de {totalPags}", fontFooter, XBrushes.Gray, new XPoint(width - 15 * Mm, height - 11 * Mm), XStringFormats.TopRight);
        }

        /// <summary>
        /// Devuelve las dos primeras letras de cada parte del nombre en mayúsculas como iniciales descriptivas.
        /// </summary>
        private static string ObtenerIniciales(string nombre)
        {
            var partes = nombre.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length >= 2)
            {
                return (partes[0][0].ToString() + partes[1][0].ToString()).ToUpper();
            }
            else if (partes.Length == 1 && partes[0].Length > 0)
            {
                return partes[0].Substring(0, Math.Min(2, partes[0].Length)).ToUpper();
            }
            return "";
        }
    }
}
