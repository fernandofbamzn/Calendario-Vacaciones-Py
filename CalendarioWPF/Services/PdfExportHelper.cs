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
    public static class PdfExportHelper
    {
        public const double Mm = 72.0 / 25.4;

        /// <summary>
        /// Determina si un mes tiene días marcados (festivos del año natural o vacaciones imputadas a este cupo).
        /// </summary>
        public static bool CupoMesTieneDiasMarcados(PlanVacaciones datos, int mes, int yearNatural, int quotaYear, string filtroDpto = "")
        {
            // Verificar si hay vacaciones imputadas a este cupo en este mes/año natural
            foreach (var wKV in datos.Trabajadores)
            {
                var w = wKV.Value;
                if (!string.IsNullOrEmpty(filtroDpto) && (w.Departamento ?? "General") != filtroDpto) continue;
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
        /// Método estático privado para dibujar el encabezado de las hojas PDF de forma consistente.
        /// </summary>
        public static void DrawHeaderPdf(XGraphics gfx, PdfPage page, string titulo, int year)
        {
            double width = page.Width.Value;

            XFont fontHeader = new XFont("Arial", 13, XFontStyleEx.Bold);
            XFont fontHeaderSub = new XFont("Arial", 9.5, XFontStyleEx.Italic);

            // Título a la izquierda
            gfx.DrawString($"{titulo} - {year}", fontHeader, XBrushes.DarkSlateGray, new XPoint(15 * Mm, 12 * Mm), XStringFormats.TopLeft);

            // Fecha de generación a la derecha
            string dateStr = DateTime.Now.ToString("dd/MM/yyyy");
            gfx.DrawString($"Generado: {dateStr}", fontHeaderSub, XBrushes.Gray, new XPoint(width - 15 * Mm, 12 * Mm), XStringFormats.TopRight);

            // Línea divisoria superior
            XPen pen = new XPen(XColor.FromArgb(200, 200, 200), 0.4);
            gfx.DrawLine(pen, 15 * Mm, 18 * Mm, width - 15 * Mm, 18 * Mm);
        }

        /// <summary>
        /// Método estático privado para dibujar el pie de página.
        /// </summary>
        public static void DrawFooterPdf(XGraphics gfx, PdfPage page, int pagNum, int totalPags, string piePagina)
        {
            double width = page.Width.Value;
            double height = page.Height.Value;

            XFont fontFooter = new XFont("Arial", 8.5, XFontStyleEx.Regular);
            XPen pen = new XPen(XColor.FromArgb(200, 200, 200), 0.4);

            // Línea y textos del pie de página (footer)
            gfx.DrawLine(pen, 15 * Mm, height - 15 * Mm, width - 15 * Mm, height - 15 * Mm);
            gfx.DrawString(piePagina, fontFooter, XBrushes.Gray, new XPoint(15 * Mm, height - 11 * Mm), XStringFormats.TopLeft);
            gfx.DrawString($"Página {pagNum} de {totalPags}", fontFooter, XBrushes.Gray, new XPoint(width - 15 * Mm, height - 11 * Mm), XStringFormats.TopRight);
        }


        /// <summary>
        /// Devuelve las dos primeras letras de cada parte del nombre en mayúsculas como iniciales descriptivas.
        /// </summary>
        public static string ObtenerIniciales(string nombre)
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
