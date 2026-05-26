using System;

namespace CalendarioWPF.Services
{
    /// <summary>
    /// Interfaz mínima de registro de eventos (logging) para la aplicación.
    /// Permite desacoplar los componentes de infraestructura (DataManager, AppConfigManager)
    /// de la presentación (MainWindow), eliminando dependencias directas sobre _logMessages.
    /// </summary>
    public interface IAppLogger
    {
        /// <summary>
        /// Registra un mensaje informativo. No implica error ni advertencia.
        /// </summary>
        /// <param name="mensaje">Texto descriptivo del evento.</param>
        void Info(string mensaje);

        /// <summary>
        /// Registra un mensaje de advertencia. La operación se completó pero con condiciones no ideales.
        /// </summary>
        /// <param name="mensaje">Texto descriptivo de la advertencia.</param>
        void Advertencia(string mensaje);

        /// <summary>
        /// Registra un error. La operación no pudo completarse correctamente.
        /// </summary>
        /// <param name="mensaje">Texto descriptivo del error.</param>
        /// <param name="ex">Excepción asociada (opcional).</param>
        void Error(string mensaje, Exception? ex = null);

        /// <summary>
        /// Devuelve todos los mensajes registrados hasta el momento en la sesión actual,
        /// ordenados cronológicamente de más antiguo a más reciente.
        /// </summary>
        /// <returns>Array de cadenas con el historial de mensajes formateados.</returns>
        string[] ObtenerHistorial();
    }
}
