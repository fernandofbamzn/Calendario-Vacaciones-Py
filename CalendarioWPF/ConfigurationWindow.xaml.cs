using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CalendarioWPF.Services;

namespace CalendarioWPF
{
    /// <summary>
    /// Modelo de vista para el DataGrid de trabajadores en la ventana de configuración.
    /// </summary>
    public class TrabajadorRow : INotifyPropertyChanged
    {
        private string _nombre = "";
        private int _diasBase = 22;
        private int _diasExtras = 0;
        private int _vacacionesUsadas = 0;

        public string Nombre
        {
            get => _nombre;
            set { _nombre = value; OnPropertyChanged(nameof(Nombre)); }
        }

        public int DiasBase
        {
            get => _diasBase;
            set { _diasBase = value; OnPropertyChanged(nameof(DiasBase)); }
        }

        public int DiasExtras
        {
            get => _diasExtras;
            set { _diasExtras = value; OnPropertyChanged(nameof(DiasExtras)); }
        }

        public int VacacionesUsadas
        {
            get => _vacacionesUsadas;
            set { _vacacionesUsadas = value; OnPropertyChanged(nameof(VacacionesUsadas)); }
        }

        /// <summary>
        /// Nombre original antes de la edición, para poder rastrear renombrados.
        /// </summary>
        public string NombreOriginal { get; set; } = "";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Modelo de vista para el DataGrid de festivos en la ventana de configuración.
    /// </summary>
    public class FestivoRow : INotifyPropertyChanged
    {
        private string _fecha = "";

        public string Fecha
        {
            get => _fecha;
            set { _fecha = value; OnPropertyChanged(nameof(Fecha)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Ventana modal de configuración de la aplicación.
    /// Permite gestionar trabajadores, festivos y preferencias de exportación/visualización.
    /// </summary>
    public partial class ConfigurationWindow : Window
    {
        private readonly PlanVacaciones _datos;
        private readonly AppConfig _config;

        /// <summary>
        /// Indica si el usuario pulsó "Aceptar" y los cambios deben aplicarse.
        /// </summary>
        public bool Aceptado { get; private set; } = false;

        /// <summary>
        /// Colección observable de trabajadores para el DataGrid.
        /// </summary>
        public ObservableCollection<TrabajadorRow> Trabajadores { get; set; } = new();

        /// <summary>
        /// Colección observable de festivos para el DataGrid.
        /// </summary>
        public ObservableCollection<FestivoRow> Festivos { get; set; } = new();

        // Referencias a los CheckBoxes de meses para acceder por índice
        private CheckBox[] _chkMeses = null!;

        public ConfigurationWindow(PlanVacaciones datos, AppConfig config)
        {
            InitializeComponent();

            _datos = datos;
            _config = config;

            _chkMeses = new CheckBox[]
            {
                ChkMes1, ChkMes2, ChkMes3, ChkMes4, ChkMes5, ChkMes6,
                ChkMes7, ChkMes8, ChkMes9, ChkMes10, ChkMes11, ChkMes12
            };

            CargarDatosEnUI();
        }

        /// <summary>
        /// Carga los datos actuales del plan y la configuración en los controles de la ventana.
        /// </summary>
        private void CargarDatosEnUI()
        {
            // --- Pestaña Personal ---
            Trabajadores.Clear();
            foreach (var kvp in _datos.Trabajadores.OrderBy(k => k.Key))
            {
                int usados = RangoVacacionesHelper.ContarDiasConsumidos(kvp.Value.Vacaciones, _datos.Festivos, _datos.Year);
                Trabajadores.Add(new TrabajadorRow
                {
                    Nombre = kvp.Key,
                    NombreOriginal = kvp.Key,
                    DiasBase = kvp.Value.DiasBase,
                    DiasExtras = kvp.Value.DiasExtras,
                    VacacionesUsadas = usados
                });
            }
            DgTrabajadores.ItemsSource = Trabajadores;

            // --- Pestaña Festivos ---
            Festivos.Clear();
            var festivosOrdenados = _datos.Festivos
                .Select(f =>
                {
                    DateTime.TryParseExact(f, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d);
                    return new { Str = f, Date = d };
                })
                .OrderBy(x => x.Date)
                .Select(x => x.Str)
                .ToList();

            foreach (var f in festivosOrdenados)
            {
                Festivos.Add(new FestivoRow { Fecha = f });
            }
            DgFestivos.ItemsSource = Festivos;

            // --- Pestaña Exportación ---
            TxtTituloCalendario.Text = _datos.TituloPagina;
            TxtPiePagina.Text = _config.PiePaginaPdf;
            CbOrientacion.SelectedIndex = _config.OrientacionPdf == "Landscape" ? 1 : 0;
            ChkOcultarComputo.IsChecked = _config.OcultarComputoGantt;
            ChkOcultarMesesSinDias.IsChecked = _config.OcultarMesesSinDias;
            ChkForzarSaltoPagina.IsChecked = _config.ForzarSaltoPagina;
            ChkExportarMultiplesPdfs.IsChecked = _config.ExportarMultiplesPdfs;
            TxtAnoActivo.Text = _datos.Year.ToString();

            // Rellenar dinámicamente los CheckBoxes de años a exportar
            PanelAnosAExportar.Children.Clear();
            var todosAnos = ObtenerTodosLosAnosConDatos();
            foreach (var y in todosAnos)
            {
                var chk = new CheckBox
                {
                    Content = y.ToString(),
                    IsChecked = _config.AnosAExportar.Count == 0 || _config.AnosAExportar.Contains(y),
                    Margin = new Thickness(0, 3, 0, 3),
                    FontSize = 13
                };
                PanelAnosAExportar.Children.Add(chk);
            }

            // Checkboxes de meses
            for (int i = 0; i < 12; i++)
            {
                _chkMeses[i].IsChecked = _config.MesesAMostrar.Contains(i + 1);
            }
        }

        #region Pestaña Personal

        private void BtnAddTrabajador_Click(object sender, RoutedEventArgs e)
        {
            string nombre = TxtNuevoTrabajador.Text.Trim();
            if (string.IsNullOrEmpty(nombre)) return;

            string upper = nombre.ToUpper();
            if (upper == "FESTIVO" || upper == "MES" || upper == "TRABAJADOR") return;

            if (Trabajadores.Any(t => t.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase))) return;

            Trabajadores.Add(new TrabajadorRow
            {
                Nombre = nombre,
                NombreOriginal = "", // Vacío indica que es nuevo
                DiasBase = 22,
                DiasExtras = 0,
                VacacionesUsadas = 0
            });
            TxtNuevoTrabajador.Text = "";
        }

        private void BtnRemoveTrabajador_Click(object sender, RoutedEventArgs e)
        {
            var seleccionados = DgTrabajadores.SelectedItems.Cast<TrabajadorRow>().ToList();
            foreach (var selected in seleccionados)
            {
                Trabajadores.Remove(selected);
            }
        }

        private void BtnLoteAsignarBase_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtLoteBase.Text, out int val) && val >= 0)
            {
                foreach (var row in DgTrabajadores.SelectedItems.Cast<TrabajadorRow>())
                {
                    row.DiasBase = val;
                }
            }
        }

        private void BtnLoteSumarBase_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtLoteBase.Text, out int val))
            {
                foreach (var row in DgTrabajadores.SelectedItems.Cast<TrabajadorRow>())
                {
                    row.DiasBase = Math.Max(0, row.DiasBase + val);
                }
            }
        }

        private void BtnLoteRestarBase_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtLoteBase.Text, out int val))
            {
                foreach (var row in DgTrabajadores.SelectedItems.Cast<TrabajadorRow>())
                {
                    row.DiasBase = Math.Max(0, row.DiasBase - val);
                }
            }
        }

        private void BtnLoteAsignarExtras_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtLoteExtras.Text, out int val) && val >= 0)
            {
                foreach (var row in DgTrabajadores.SelectedItems.Cast<TrabajadorRow>())
                {
                    row.DiasExtras = val;
                }
            }
        }

        private void BtnLoteSumarExtras_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtLoteExtras.Text, out int val))
            {
                foreach (var row in DgTrabajadores.SelectedItems.Cast<TrabajadorRow>())
                {
                    row.DiasExtras = Math.Max(0, row.DiasExtras + val);
                }
            }
        }

        private void BtnLoteRestarExtras_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtLoteExtras.Text, out int val))
            {
                foreach (var row in DgTrabajadores.SelectedItems.Cast<TrabajadorRow>())
                {
                    row.DiasExtras = Math.Max(0, row.DiasExtras - val);
                }
            }
        }

        #endregion

        #region Pestaña Festivos

        private void BtnAddFestivo_Click(object sender, RoutedEventArgs e)
        {
            if (DpNuevoFestivo.SelectedDate.HasValue)
            {
                string fechaStr = DpNuevoFestivo.SelectedDate.Value.ToString("dd/MM/yyyy");
                if (!Festivos.Any(f => f.Fecha == fechaStr))
                {
                    Festivos.Add(new FestivoRow { Fecha = fechaStr });
                    DpNuevoFestivo.SelectedDate = null;
                }
            }
        }

        private void BtnRemoveFestivo_Click(object sender, RoutedEventArgs e)
        {
            var seleccionados = DgFestivos.SelectedItems.Cast<FestivoRow>().ToList();
            foreach (var selected in seleccionados)
            {
                Festivos.Remove(selected);
            }
        }

        #endregion

        #region Pestaña Exportación

        private void BtnSelectAllMeses_Click(object sender, RoutedEventArgs e)
        {
            foreach (var chk in _chkMeses) chk.IsChecked = true;
        }

        private void BtnDeselectAllMeses_Click(object sender, RoutedEventArgs e)
        {
            foreach (var chk in _chkMeses) chk.IsChecked = false;
        }

        #endregion

        #region Acciones principales

        private void BtnAceptar_Click(object sender, RoutedEventArgs e)
        {
            // --- Aplicar cambios de Trabajadores ---
            // Detectar eliminaciones: trabajadores originales que ya no están en la lista
            var nombresOriginales = _datos.Trabajadores.Keys.ToList();
            var nombresActuales = Trabajadores.Select(t => t.Nombre).ToHashSet();
            var nombresOriginalesEnLista = Trabajadores.Select(t => t.NombreOriginal).Where(n => !string.IsNullOrEmpty(n)).ToHashSet();

            // Eliminar trabajadores que ya no están
            foreach (var original in nombresOriginales)
            {
                if (!nombresOriginalesEnLista.Contains(original))
                {
                    _datos.Trabajadores.Remove(original);
                }
            }

            // Actualizar o crear trabajadores
            foreach (var row in Trabajadores)
            {
                if (!string.IsNullOrEmpty(row.NombreOriginal) && _datos.Trabajadores.ContainsKey(row.NombreOriginal))
                {
                    // Renombramiento: si el nombre cambió
                    if (row.Nombre != row.NombreOriginal)
                    {
                        var info = _datos.Trabajadores[row.NombreOriginal];
                        _datos.Trabajadores.Remove(row.NombreOriginal);
                        info.DiasBase = row.DiasBase;
                        info.DiasExtras = row.DiasExtras;
                        _datos.Trabajadores[row.Nombre] = info;
                    }
                    else
                    {
                        // Solo actualizar días
                        _datos.Trabajadores[row.Nombre].DiasBase = row.DiasBase;
                        _datos.Trabajadores[row.Nombre].DiasExtras = row.DiasExtras;
                    }
                }
                else if (string.IsNullOrEmpty(row.NombreOriginal))
                {
                    // Nuevo trabajador
                    if (!_datos.Trabajadores.ContainsKey(row.Nombre))
                    {
                        _datos.Trabajadores[row.Nombre] = new InfoTrabajador
                        {
                            Vacaciones = new List<string>(),
                            DiasBase = row.DiasBase,
                            DiasExtras = row.DiasExtras
                        };
                    }
                }
            }

            // --- Aplicar cambios de Festivos ---
            _datos.Festivos.Clear();
            foreach (var row in Festivos)
            {
                string f = row.Fecha.Trim();
                if (!string.IsNullOrEmpty(f) && !_datos.Festivos.Contains(f))
                {
                    _datos.Festivos.Add(f);
                }
            }

            // Limpiar vacaciones que caigan en festivos nuevos
            foreach (var festivo in _datos.Festivos)
            {
                foreach (var kvp in _datos.Trabajadores)
                {
                    kvp.Value.Vacaciones.Remove(festivo);
                }
            }

            // --- Aplicar cambios de Configuración ---
            _datos.TituloPagina = TxtTituloCalendario.Text.Trim();
            _config.PiePaginaPdf = TxtPiePagina.Text.Trim();
            _config.OrientacionPdf = CbOrientacion.SelectedIndex == 1 ? "Landscape" : "Portrait";
            _config.OcultarComputoGantt = ChkOcultarComputo.IsChecked == true;
            _config.OcultarMesesSinDias = ChkOcultarMesesSinDias.IsChecked == true;
            _config.ForzarSaltoPagina = ChkForzarSaltoPagina.IsChecked == true;
            _config.ExportarMultiplesPdfs = ChkExportarMultiplesPdfs.IsChecked == true;

            if (int.TryParse(TxtAnoActivo.Text.Trim(), out int activeYear) && activeYear >= 1900 && activeYear <= 2100)
            {
                _datos.Year = activeYear;
            }
            else
            {
                MessageBox.Show("El año de vacaciones activo debe ser un número válido entre 1900 y 2100. Se mantendrá el valor actual.", "Año Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // Aplicar años seleccionados a exportar
            _config.AnosAExportar.Clear();
            foreach (var child in PanelAnosAExportar.Children)
            {
                if (child is CheckBox chk && chk.IsChecked == true && int.TryParse(chk.Content.ToString(), out int y))
                {
                    _config.AnosAExportar.Add(y);
                }
            }

            _config.MesesAMostrar.Clear();
            for (int i = 0; i < 12; i++)
            {
                if (_chkMeses[i].IsChecked == true)
                {
                    _config.MesesAMostrar.Add(i + 1);
                }
            }

            // Si no se seleccionó ningún mes, forzar al menos Junio-Septiembre
            if (_config.MesesAMostrar.Count == 0)
            {
                _config.MesesAMostrar = new List<int> { 6, 7, 8, 9 };
            }

            Aceptado = true;
            DialogResult = true;
            Close();
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            Aceptado = false;
            DialogResult = false;
            Close();
        }

        private void BtnDecActiveYear_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtAnoActivo.Text, out int y) && y > 1900)
            {
                TxtAnoActivo.Text = (y - 1).ToString();
            }
        }

        private void BtnIncActiveYear_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtAnoActivo.Text, out int y) && y < 2100)
            {
                TxtAnoActivo.Text = (y + 1).ToString();
            }
        }

        private List<int> ObtenerTodosLosAnosConDatos()
        {
            var anos = new HashSet<int> { _datos.Year, DateTime.Today.Year };
            foreach (var w in _datos.Trabajadores.Values)
            {
                foreach (var v in w.Vacaciones)
                {
                    if (DateTime.TryParseExact(v, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    {
                        int qYear = (w.Imputaciones != null && w.Imputaciones.TryGetValue(v, out int val)) ? val : d.Year;
                        anos.Add(qYear);
                    }
                }
            }
            foreach (var f in _datos.Festivos)
            {
                if (DateTime.TryParseExact(f, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                {
                    anos.Add(d.Year);
                }
            }
            return anos.OrderBy(y => y).ToList();
        }

        #endregion
    }
}
