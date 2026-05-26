using System;
using System.IO;
using System.Text;
using System.Text.Json;
using CalendarioWPF.Models;

namespace CalendarioWPF.Services
{
    /// <summary>
    /// Gestor estático de persistencia para la configuración local de la aplicación.
    /// Carga y guarda <see cref="AppConfig"/> desde/hacia el archivo 'app_config.json'
    /// ubicado en el directorio de trabajo de la aplicación.
    /// </summary>
    public static class AppConfigManager
    {
        /// <summary>
        /// Logger para registrar errores silenciosos de configuración.
        /// Por defecto apunta al singleton global <see cref="AppLogger.Instance"/>.
        /// </summary>
        public static IAppLogger Logger { get; set; } = AppLogger.Instance;

        private const string ConfigFilename = "app_config.json";

        /// <summary>
        /// Carga la configuración desde el archivo local. Si el archivo no existe
        /// o está corrupto, devuelve una instancia con valores por defecto sin interrumpir la aplicación.
        /// </summary>
        /// <returns>La instancia de <see cref="AppConfig"/> cargada o por defecto.</returns>
        public static AppConfig Cargar()
        {
            try
            {
                if (File.Exists(ConfigFilename))
                {
                    string json = File.ReadAllText(ConfigFilename, Encoding.UTF8);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch (Exception ex)
            {
                // Error silencioso: si falla la lectura, devolver configuración por defecto
                Logger.Advertencia($"No se pudo cargar '{ConfigFilename}' (se usarán valores por defecto). Detalle: {ex.Message}");
            }
            return new AppConfig();
        }

        /// <summary>
        /// Serializa y guarda la configuración actual en el archivo local con formato indentado.
        /// El guardado es silencioso: los errores no se propagan al nivel de UI.
        /// </summary>
        /// <param name="config">La instancia de <see cref="AppConfig"/> a persistir.</param>
        public static void Guardar(AppConfig config)
        {
            try
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFilename, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // Error silencioso al guardar configuración
                Logger.Error($"Error silencioso al guardar '{ConfigFilename}'", ex);
            }
        }
    }
}
