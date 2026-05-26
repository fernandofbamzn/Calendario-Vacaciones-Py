using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CalendarioWPF.Services;
using CalendarioWPF.Models;
using CalendarioWPF.Data;

namespace CalendarioWPF
{
    /// <summary>
    /// Ventana principal de la aplicación Gestor de Vacaciones Pro.
    /// Esta es la clase parcial base; el resto de la lógica está dividida en:
    /// <list type="bullet">
    ///   <item><description><see cref="Views/MainWindow.Panel.cs"/> — Toolbar de trabajador y panel de cupo.</description></item>
    ///   <item><description><see cref="Views/MainWindow.Calendario.cs"/> — Renderizado del calendario mensual y drag-to-select.</description></item>
    ///   <item><description><see cref="Views/MainWindow.Gantt.cs"/> — Renderizado de la tabla Gantt.</description></item>
    ///   <item><description><see cref="Views/MainWindow.Exports.cs"/> — Importación, exportación y visor de logs.</description></item>
    /// </list>
    /// </summary>
    public partial class MainWindow : Window
    {
        // ── Estado Global ─────────────────────────────────────────────────────────

        /// <summary>Plan de vacaciones cargado en memoria (todos los trabajadores, festivos y cupos).</summary>
        private PlanVacaciones _datos = new();

        /// <summary>Configuración de presentación y exportación de la aplicación.</summary>
        private AppConfig _config = new();

        /// <summary>Nombre del trabajador seleccionado actualmente en el ComboBox.</summary>
        private string _activeWorker = "";

        /// <summary>Modo de edición activo: "vacaciones" o "festivos".</summary>
        private string _editMode = "vacaciones";

        /// <summary>Año del calendario que se está visualizando actualmente (puede diferir del año de cupo activo).</summary>
        private int _visualizedYear;

        /// <summary>Logger centralizado de la aplicación. Sustituye la lista _logMessages manual.</summary>
        private readonly IAppLogger _logger = AppLogger.Instance;

        // ── Variables de arrastre (drag-to-select) ────────────────────────────────

        /// <summary>Indica si el usuario está arrastrando el ratón sobre el calendario.</summary>
        private bool _isDragging = false;

        /// <summary>Acción del arrastre actual: "select" o "deselect".</summary>
        private string _dragAction = "";

        /// <summary>Tipo de edición durante el arrastre: "vacaciones" o "festivos".</summary>
        private string _dragSelectionType = "";

        // ── Timers ────────────────────────────────────────────────────────────────

        /// <summary>Timer para auto-reset de la barra de estado tras 4 segundos de inactividad.</summary>
        private DispatcherTimer? _statusTimer;

        // ── Override de FindResource para valores de respaldo ─────────────────────

        /// <summary>
        /// Override de FindResource que devuelve colores de respaldo premium cuando los recursos
        /// de WPF no están aún inicializados (p.ej. durante la construcción de controles dinámicos).
        /// </summary>
        public new object FindResource(object resourceKey)
        {
            string key = resourceKey?.ToString() ?? string.Empty;
            if (this.Resources.Contains(key))
                return this.Resources[key];

            var res = this.TryFindResource(key);
            if (res != null) return res;

            return key switch
            {
                "ColorPrimary"           => new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                "ColorPrimaryHover"      => new SolidColorBrush(Color.FromRgb(79, 70, 229)),
                "ColorAccent"            => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                "ColorAccentHover"       => new SolidColorBrush(Color.FromRgb(5, 150, 105)),
                "ColorDanger"            => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                "ColorDangerHover"       => new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                "ColorBorder"            => new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                "ColorTextMain"          => new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                "ColorTextMuted"         => new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                "ColorBgCard"            => Brushes.White,
                "ColorBgApp"             => new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                "ColorWeekend"           => new SolidColorBrush(Color.FromRgb(243, 244, 246)),
                "ColorWeekendText"       => new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                "ColorFestivo"           => new SolidColorBrush(Color.FromRgb(254, 226, 226)),
                "ColorFestivoText"       => new SolidColorBrush(Color.FromRgb(153, 27, 27)),
                "ColorVacacionBase"      => new SolidColorBrush(Color.FromRgb(219, 234, 254)),
                "ColorVacacionText"      => new SolidColorBrush(Color.FromRgb(30, 64, 175)),
                _                        => Brushes.Transparent
            };
        }

        // ── Constructor ───────────────────────────────────────────────────────────

        /// <summary>
        /// Inicializa la ventana, carga la configuración y los datos del plan de vacaciones,
        /// y prepara la interfaz de usuario para su uso.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            _config = AppConfigManager.Cargar();
            // Conectar el logger central a gestores estáticos
            AppConfigManager.Logger = _logger;
            DataManager.Logger = _logger;
            CargarDatos();
            ActualizarSelectTrabajadores();
            ActualizarPanelCupo();
            ActualizarVistas();
        }

        // ── Barra de Estado ───────────────────────────────────────────────────────

        /// <summary>
        /// Muestra un mensaje en la barra de estado inferior con coloreado automático según el tipo:
        /// ⚠️ Amarillo (advertencia), ❌ Rojo (error), ✅ Verde (éxito), gris (informativo).
        /// El mensaje se auto-limpia a "Listo" tras 4 segundos.
        /// Cada mensaje se registra en <see cref="AppLogger"/> para el visor de logs (máx. 500 entradas FIFO).
        /// </summary>
        private void MostrarEstado(string mensaje)
        {
            if (StatusBarText == null || MainStatusBar == null) return;
            StatusBarText.Text = mensaje;

            // Registrar en el logger central (ya gestiona el límite FIFO de 500 entradas)
            if (mensaje.StartsWith("⚠️"))
                _logger.Advertencia(mensaje);
            else if (mensaje.StartsWith("❌"))
                _logger.Error(mensaje);
            else
                _logger.Info(mensaje);

            if (mensaje.StartsWith("⚠️"))
            {
                MainStatusBar.Background = new SolidColorBrush(Color.FromRgb(254, 249, 195));
                StatusBarText.Foreground = new SolidColorBrush(Color.FromRgb(161, 98, 7));
            }
            else if (mensaje.StartsWith("❌"))
            {
                MainStatusBar.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226));
                StatusBarText.Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28));
            }
            else if (mensaje.StartsWith("✅"))
            {
                MainStatusBar.Background = new SolidColorBrush(Color.FromRgb(220, 252, 231));
                StatusBarText.Foreground = new SolidColorBrush(Color.FromRgb(21, 128, 61));
            }
            else
            {
                MainStatusBar.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
                StatusBarText.Foreground = (SolidColorBrush)FindResource("ColorTextMain");
            }

            _statusTimer?.Stop();
            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _statusTimer.Tick += (s, e) =>
            {
                _statusTimer.Stop();
                StatusBarText.Text = "Listo";
                StatusBarText.Foreground = (SolidColorBrush)FindResource("ColorTextMuted");
                MainStatusBar.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
            };
            _statusTimer.Start();
        }

        // ── Persistencia ──────────────────────────────────────────────────────────

        /// <summary>
        /// Carga el plan de vacaciones desde el archivo JSON local y sincroniza la interfaz
        /// con el año de cupo y el título de página almacenados.
        /// </summary>
        private void CargarDatos()
        {
            try
            {
                var repo = RepositoryFactory.GetRepository();
                int currentYear = DateTime.Today.Year;
                _datos = repo.CargarPlan(currentYear);

                if (_datos.Trabajadores.Count == 0 && string.IsNullOrEmpty(_datos.TituloPagina))
                {
                    _datos = DataManager.InicializarDatosVacios();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar datos locales: {ex.Message}", "Error de Carga", MessageBoxButton.OK, MessageBoxImage.Error);
                _datos = DataManager.InicializarDatosVacios();
            }

            PageTitleInput.Text = _datos.TituloPagina;
            _visualizedYear = _datos.Year;
            LabelYear.Text = _visualizedYear.ToString();
            if (MenuLabelActiveYear != null)
                MenuLabelActiveYear.Text = $"Año de vacaciones: {_datos.Year}";
        }

        /// <summary>
        /// Persiste el estado actual del plan de vacaciones en el archivo JSON local,
        /// actualizando también el título de página desde el TextBox editable.
        /// </summary>
        private void GuardarDatos()
        {
            try
            {
                _datos.TituloPagina = PageTitleInput.Text.Trim();
                var repo = RepositoryFactory.GetRepository();
                repo.GuardarPlan(_datos);
            }
            catch (Exception ex)
            {
                MostrarEstado($"❌ Error al guardar datos: {ex.Message}");
            }
        }

        // ── Eventos del Control Superior ──────────────────────────────────────────

        /// <summary>
        /// Navega al año anterior en la vista del calendario (sin cambiar el año de cupo activo).
        /// </summary>
        private void BtnPrevYear_Click(object sender, RoutedEventArgs e)
        {
            _visualizedYear--;
            LabelYear.Text = _visualizedYear.ToString();
            ActualizarVistas();
        }

        /// <summary>
        /// Navega al año siguiente en la vista del calendario (sin cambiar el año de cupo activo).
        /// </summary>
        private void BtnNextYear_Click(object sender, RoutedEventArgs e)
        {
            _visualizedYear++;
            LabelYear.Text = _visualizedYear.ToString();
            ActualizarVistas();
        }

        /// <summary>
        /// Guarda los datos cuando el usuario sale del campo de título editable.
        /// </summary>
        private void PageTitleInput_LostFocus(object sender, RoutedEventArgs e)
        {
            GuardarDatos();
        }

        // ── Limpiar Datos ─────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el diálogo de limpieza selectiva (<see cref="Dialogs.LimpiarDialog"/>)
        /// y aplica los cambios elegidos por el usuario (festivos, vacaciones o trabajadores).
        /// </summary>
        private void BtnLimpiarTodo_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Dialogs.LimpiarDialog { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                if (dialog.LimpiarTrabajadores)
                {
                    _datos.Trabajadores.Clear();
                    _activeWorker = "";
                }
                else if (dialog.LimpiarVacaciones)
                {
                    foreach (var trabajador in _datos.Trabajadores.Values)
                    {
                        trabajador.Vacaciones.Clear();
                        trabajador.Imputaciones?.Clear();
                    }
                }

                if (dialog.LimpiarFestivos)
                    _datos.Festivos.Clear();

                GuardarDatos();
                ActualizarSelectTrabajadores();
                ActualizarPanelCupo();
                ActualizarVistas();

                MostrarEstado("✅ Datos limpiados correctamente según tu selección.");
            }
        }

        // ── Ayuda y Configuración ─────────────────────────────────────────────────

        /// <summary>
        /// Muestra la guía de uso rápido de la aplicación en un MessageBox.
        /// </summary>
        private void BtnAyuda_Click(object sender, RoutedEventArgs e)
        {
            string ayuda =
                "📖 Guía de Uso - Gestor de Vacaciones Pro\n\n" +
                "1. Gestión de Personal y Festivos:\n" +
                "   Accede a 'Archivo → Configuración' para añadir/eliminar trabajadores y festivos.\n\n" +
                "2. Toolbar del Trabajador:\n" +
                "   Selecciona el trabajador activo y ajusta sus días base y extra con ◀▶.\n\n" +
                "3. Modos de Edición (menú Edición):\n" +
                "   - Marcar Vacaciones: Click/arrastre para asignar días al cupo activo.\n" +
                "   - Marcar Festivos: Click/arrastre para marcar festivos oficiales.\n\n" +
                "4. Selector de Año de Vista:\n" +
                "   Los botones ◀▶ del header cambian el año visualizado (no el cupo activo).\n" +
                "   El cupo activo se cambia en Configuración.\n\n" +
                "5. Exportación:\n" +
                "   Menú 'Datos' → exportar en JSON/CSV, PDF Mensual, PDF Gantt o Excel Gantt.";

            MessageBox.Show(ayuda, "Guía de Uso", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Abre la ventana de configuración y aplica los cambios si el usuario los acepta.
        /// </summary>
        private void MenuConfiguracion_Click(object sender, RoutedEventArgs e)
        {
            var configWindow = new ConfigurationWindow(_datos, _config);
            configWindow.Owner = this;
            configWindow.ShowDialog();

            if (configWindow.Aceptado)
            {
                GuardarDatos();
                AppConfigManager.Guardar(_config);

                ActualizarSelectTrabajadores();
                ActualizarPanelCupo();
                ActualizarVistas();

                if (MenuLabelActiveYear != null)
                    MenuLabelActiveYear.Text = $"Año de vacaciones: {_datos.Year}";

                MostrarEstado("✅ Configuración aplicada correctamente.");
            }
        }

        /// <summary>
        /// Cierra la aplicación.
        /// </summary>
        private void MenuSalir_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}