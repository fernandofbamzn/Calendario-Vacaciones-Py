using System.Collections.Generic;

namespace CalendarioWPF.Services
{
    /// <summary>
    /// Interfaz para el servicio de exportación a PDF (PdfExportService).
    /// Actúa como facade opcional para quienes necesiten inyectar ambos servicios a la vez.
    /// </summary>
    public interface IPdfExportService : IPdfMensualService, IPdfGanttService
    {
    }

    /// <summary>
    /// Implementación de la interfaz IPdfExportService como un facade que redirige a los servicios divididos.
    /// </summary>
    public class PdfExportFacade : IPdfExportService
    {
        public static IPdfExportService Instance { get; } = new PdfExportFacade();

        public void ExportarMensual(string path, PlanVacaciones datos, AppConfig config, List<int> años)
        {
            PdfMensualService.Instance.ExportarMensual(path, datos, config, años);
        }

        public void ExportarGantt(string path, PlanVacaciones datos, AppConfig config, List<int> años)
        {
            PdfGanttService.Instance.ExportarGantt(path, datos, config, años);
        }
    }
}
