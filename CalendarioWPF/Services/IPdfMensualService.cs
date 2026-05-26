using System.Collections.Generic;

namespace CalendarioWPF.Services
{
    /// <summary>
    /// Interfaz para el servicio de exportación de la planilla mensual a PDF.
    /// </summary>
    public interface IPdfMensualService
    {
        /// <summary>
        /// Exporta la planilla mensual a un archivo PDF.
        /// Renderiza de forma consecutiva los meses seleccionados de todos los años con actividad
        /// y al final coloca una hoja de resumen consolidado.
        /// </summary>
        /// <param name="path">Ruta del archivo PDF a crear.</param>
        /// <param name="datos">Datos del plan de vacaciones actual.</param>
        /// <param name="config">Configuración general de visualización y exportación.</param>
        /// <param name="años">Lista de años que se procesarán e incluirán en el reporte.</param>
        void ExportarMensual(string path, PlanVacaciones datos, AppConfig config, List<int> años);
    }
}
