using System.Collections.Generic;

namespace CalendarioWPF.Services
{
    /// <summary>
    /// Interfaz para el servicio de exportación a PDF (PdfExportService).
    /// Define las firmas para exportar planillas mensuales y diagramas Gantt a PDF.
    /// Documentado exhaustivamente para reducir el consumo de tokens en futuras interacciones con agentes.
    /// </summary>
    public interface IPdfExportService
    {
        /// <summary>
        /// Exporta la planilla mensual a un archivo PDF.
        /// Renderiza de forma consecutiva los meses seleccionados de todos los años con actividad
        /// y al final coloca una o varias hojas con el resumen consolidado de disfrute de vacaciones.
        /// </summary>
        /// <param name="path">Ruta del archivo PDF a crear.</param>
        /// <param name="datos">Datos del plan de vacaciones actual.</param>
        /// <param name="config">Configuración general de visualización y exportación.</param>
        /// <param name="años">Lista de años que se procesarán e incluirán en el reporte.</param>
        void ExportarMensual(string path, PlanVacaciones datos, AppConfig config, List<int> años);

        /// <summary>
        /// Exporta el calendario en formato de cuadrícula Gantt a un archivo PDF.
        /// Cada mes se renderiza en una hoja horizontal (Landscape) y al final se incluye una hoja de resumen consolidado.
        /// </summary>
        /// <param name="path">Ruta del archivo PDF a crear.</param>
        /// <param name="datos">Datos del plan de vacaciones actual.</param>
        /// <param name="config">Configuración general de visualización y exportación.</param>
        /// <param name="años">Lista de años que se procesarán e incluirán en el reporte.</param>
        void ExportarGantt(string path, PlanVacaciones datos, AppConfig config, List<int> años);
    }
}
