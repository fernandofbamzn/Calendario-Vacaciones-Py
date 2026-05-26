using System.Collections.Generic;

namespace CalendarioWPF.Services
{
    /// <summary>
    /// Interfaz para el servicio de exportación a Excel (ExcelExportService).
    /// Define la firma para generar informes de tipo Gantt en un libro de cálculo Excel.
    /// Documentado exhaustivamente para reducir el consumo de tokens en futuras interacciones con agentes.
    /// </summary>
    public interface IExcelExportService
    {
        /// <summary>
        /// Genera un reporte tipo Gantt en un archivo Excel (.xlsx) usando ClosedXML.
        /// Crea una pestaña independiente por cada año con datos presente en la lista <paramref name="años"/>.
        /// </summary>
        /// <param name="path">La ruta física de disco donde se guardará el archivo Excel resultante.</param>
        /// <param name="datos">El plan de vacaciones que contiene a los trabajadores y sus días asignados.</param>
        /// <param name="config">Configuración general de visualización de la aplicación.</param>
        /// <param name="años">Lista de años con datos registrados que se desea exportar.</param>
        void Exportar(string path, PlanVacaciones datos, AppConfig config, List<int> años);
    }
}
