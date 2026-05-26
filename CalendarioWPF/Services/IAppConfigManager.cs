using CalendarioWPF.Models;

namespace CalendarioWPF.Services
{
    /// <summary>
    /// Interfaz del gestor de configuración local de la aplicación (<see cref="AppConfigManager"/>).
    /// Permite abstraer la carga y guardado de <see cref="AppConfig"/> para facilitar
    /// pruebas y sustitución de implementaciones de persistencia.
    /// </summary>
    public interface IAppConfigManager
    {
        /// <summary>
        /// Carga la configuración de la aplicación desde el almacenamiento local.
        /// Si el archivo no existe o está corrupto, devuelve valores por defecto.
        /// </summary>
        /// <returns>Instancia de <see cref="AppConfig"/> con la configuración cargada.</returns>
        AppConfig Cargar();

        /// <summary>
        /// Persiste la configuración de la aplicación en el almacenamiento local.
        /// El guardado es silencioso: los errores no se propagan.
        /// </summary>
        /// <param name="config">La instancia de <see cref="AppConfig"/> a guardar.</param>
        void Guardar(AppConfig config);
    }
}
