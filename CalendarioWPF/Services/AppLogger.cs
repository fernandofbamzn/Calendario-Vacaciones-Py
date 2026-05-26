using System;
using System.Collections.Generic;
using System.Linq;

namespace CalendarioWPF.Services
{
    /// <summary>
    /// Implementación en memoria del <see cref="IAppLogger"/>.
    /// Almacena los mensajes con marca de tiempo en una cola de tamaño máximo configurable.
    /// Thread-safe mediante lock para uso desde múltiples hilos (p.ej. callbacks de async).
    /// </summary>
    public class AppLogger : IAppLogger
    {
        /// <summary>
        /// Instancia singleton de uso global cuando no se inyecta una implementación específica.
        /// </summary>
        public static readonly IAppLogger Instance = new AppLogger();

        private readonly Queue<string> _mensajes = new();
        private readonly int _maxEntradas;
        private readonly object _lock = new();

        /// <summary>
        /// Inicializa un nuevo logger con el límite de entradas especificado.
        /// </summary>
        /// <param name="maxEntradas">Número máximo de entradas a conservar (FIFO). Por defecto 500.</param>
        public AppLogger(int maxEntradas = 500)
        {
            _maxEntradas = maxEntradas;
            Registrar("INFO", $"Logger iniciado. Máximo de {maxEntradas} entradas.");
        }

        /// <inheritdoc/>
        public void Info(string mensaje) => Registrar("INFO", mensaje);

        /// <inheritdoc/>
        public void Advertencia(string mensaje) => Registrar("WARN", mensaje);

        /// <inheritdoc/>
        public void Error(string mensaje, Exception? ex = null)
        {
            string texto = ex != null ? $"{mensaje} → {ex.GetType().Name}: {ex.Message}" : mensaje;
            Registrar("ERROR", texto);
        }

        /// <inheritdoc/>
        public string[] ObtenerHistorial()
        {
            lock (_lock)
            {
                return _mensajes.ToArray();
            }
        }

        // ── Privado ───────────────────────────────────────────────────────────────

        private void Registrar(string nivel, string mensaje)
        {
            string entrada = $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] [{nivel}] {mensaje}";
            lock (_lock)
            {
                // Mantener el límite FIFO eliminando la entrada más antigua
                while (_mensajes.Count >= _maxEntradas)
                    _mensajes.Dequeue();

                _mensajes.Enqueue(entrada);
            }
        }
    }
}
