using CalendarioWPF.Services;

namespace CalendarioWPF.Data
{
    /// <summary>
    /// Provee la instancia adecuada de IVacacionesRepository basada en la configuración activa.
    /// </summary>
    public static class RepositoryFactory
    {
        public static IVacacionesRepository GetRepository()
        {
            var config = AppConfigManager.Cargar();
            if (config.TipoPersistencia == "SQLite")
            {
                return new SqliteVacacionesRepository();
            }
            return new JsonVacacionesRepository();
        }
    }
}
