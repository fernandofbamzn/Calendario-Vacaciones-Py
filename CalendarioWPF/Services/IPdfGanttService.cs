using System.Collections.Generic;

namespace CalendarioWPF.Services
{
    /// <summary>
    /// Interfaz para el servicio de exportación de la cuadrícula Gantt a PDF.
    /// </summary>
    public interface IPdfGanttService
    {
        /// <summary>
        /// Exporta el calendario en formato de cuadrícula Gantt a un archivo PDF.
        /// Cada mes se renderiza en una hoja horizontal (Landscape) y al final se incluye una hoja de resumen.
        /// </summary>
        /// <param name="path">Ruta del archivo PDF a crear.</param>
        /// <param name="datos">Datos del plan de vacaciones actual.</param>
        /// <param name="config">Configuración general de visualización y exportación.</param>
        /// <param name="años">Lista de años que se procesarán e incluirán en el reporte.</param>
        void ExportarGantt(string path, PlanVacaciones datos, AppConfig config, List<int> años);
    }
}
