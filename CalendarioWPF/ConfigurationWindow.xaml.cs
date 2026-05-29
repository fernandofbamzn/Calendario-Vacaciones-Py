using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CalendarioWPF.Services;

namespace CalendarioWPF
{
    /// <summary>
    /// Modelo de vista para el DataGrid de trabajadores en la ventana de configuración.
    /// </summary>
    public class TrabajadorRow : INotifyPropertyChanged
    {
        private string _nombre = "";
        private string _departamento = "General";
        private int _diasBase = 22;
        private int _diasExtras = 0;
        private int _vacacionesUsadas = 0;

        public string Nombre
        {
            get => _nombre;
            set { _nombre = value; OnPropertyChanged(nameof(Nombre)); }
        }

        public string Departamento
        {
            get => _departamento;
            set { _departamento = value; OnPropertyChanged(nameof(Departamento)); }
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

        private string _departamento = "Global";
        public string Departamento
        {
            get => _departamento;
            set { _departamento = value; OnPropertyChanged(nameof(Departamento)); }
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
            this.Loaded += ConfigurationWindow_Loaded;

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
        /// Evento disparado al cargar la ventana. Inicializa la lista de regiones para la importación de festivos.
        /// </summary>
        private async void ConfigurationWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var api = new OpenHolidaysApiService();
            var regiones = await api.ObtenerRegionesAsync("ES");
            
            // Si falla la API y no trae regiones, añadimos un par por defecto
            if (regiones.Count == 0)
            {
                regiones.Add("ES-MD", "Madrid");
                regiones.Add("ES-CT", "Cataluña");
                regiones.Add("ES-AN", "Andalucía");
            }
            
            CbRegiones.ItemsSource = regiones;
            if (regiones.Count > 0)
            {
                CbRegiones.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Carga los datos actuales del plan y la configuración en los controles de la ventana.
        /// </summary>
        private List<string> GetDepartamentosConGeneral()
        {
            var list = new List<string> { "__todos__" };
            if (_datos.Departamentos != null)
            {
                list.AddRange(_datos.Departamentos);
            }
            return list;
        }

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
                    Departamento = kvp.Value.Departamento,
                    DiasBase = kvp.Value.DiasBase,
                    DiasExtras = kvp.Value.DiasExtras,
                    VacacionesUsadas = usados
                });
            }
            DgTrabajadores.ItemsSource = Trabajadores;
            
            if (_datos.Departamentos == null) _datos.Departamentos = new List<string> { "General" };
            CmbLoteDepartamento.ItemsSource = _datos.Departamentos;
            if (_datos.Departamentos.Count > 0) CmbLoteDepartamento.SelectedIndex = 0;

            // --- Pestaña Festivos ---
            Festivos.Clear();
            var todosFestivos = new List<FestivoRow>();
            
            // Globales
            foreach (var f in _datos.Festivos)
            {
                todosFestivos.Add(new FestivoRow { Fecha = f, Departamento = "Global" });
            }

            // Por Departamento
            if (_datos.FestivosDepartamento != null)
            {
                foreach (var kvp in _datos.FestivosDepartamento)
                {
                    foreach (var f in kvp.Value)
                    {
                        todosFestivos.Add(new FestivoRow { Fecha = f, Departamento = kvp.Key });
                    }
                }
            }

            // Ordenar por fecha
            var festivosOrdenados = todosFestivos
                .Select(fr =>
                {
                    DateTime.TryParseExact(fr.Fecha, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d);
                    return new { Row = fr, Date = d };
                })
                .OrderBy(x => x.Date)
                .Select(x => x.Row)
                .ToList();

            foreach (var fr in festivosOrdenados)
            {
                Festivos.Add(fr);
            }
            DgFestivos.ItemsSource = Festivos;
            
            // Llenar combo de nuevo festivo
            CmbNuevoFestivoDpto.Items.Clear();
            CmbNuevoFestivoDpto.Items.Add("Global");
            if (_datos.Departamentos != null)
            {
                foreach(var d in _datos.Departamentos)
                {
                    CmbNuevoFestivoDpto.Items.Add(d);
                }
            }
            CmbNuevoFestivoDpto.SelectedIndex = 0;
            
            // --- Pestaña Departamentos e Incompatibilidades ---
            LstDepartamentos.ItemsSource = GetDepartamentosConGeneral();
            LstTrabajadoresIncomp.ItemsSource = _datos.Trabajadores.Keys.ToList();

            // --- Pestaña Exportación ---
            CbPersistencia.SelectedIndex = _config.TipoPersistencia == "SQLite" ? 0 : 1;
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

        /// <summary>
        /// Añade un nuevo trabajador a la lista si el nombre es válido y no existe.
        /// </summary>
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
                Departamento = "General",
                DiasBase = 22,
                DiasExtras = 0,
                VacacionesUsadas = 0
            });
            TxtNuevoTrabajador.Text = "";
        }

        /// <summary>
        /// Elimina los trabajadores seleccionados en el DataGrid.
        /// </summary>
        private void BtnRemoveTrabajador_Click(object sender, RoutedEventArgs e)
        {
            var seleccionados = DgTrabajadores.SelectedItems.Cast<TrabajadorRow>().ToList();
            foreach (var selected in seleccionados)
            {
                Trabajadores.Remove(selected);
            }
        }

        /// <summary>
        /// Asigna el valor especificado de días base a los trabajadores seleccionados.
        /// </summary>
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

        /// <summary>
        /// Suma el valor especificado a los días base de los trabajadores seleccionados.
        /// </summary>
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

        /// <summary>
        /// Resta el valor especificado de los días base de los trabajadores seleccionados.
        /// </summary>
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

        /// <summary>
        /// Asigna el valor especificado de días extra a los trabajadores seleccionados.
        /// </summary>
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

        /// <summary>
        /// Suma el valor especificado a los días extra de los trabajadores seleccionados.
        /// </summary>
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

        /// <summary>
        /// Resta el valor especificado de los días extra de los trabajadores seleccionados.
        /// </summary>
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

        /// <summary>
        /// Asigna el departamento especificado a los trabajadores seleccionados.
        /// </summary>
        private void BtnLoteAsignarDepartamento_Click(object sender, RoutedEventArgs e)
        {
            if (CmbLoteDepartamento.SelectedItem is string dept && !string.IsNullOrEmpty(dept))
            {
                foreach (var row in DgTrabajadores.SelectedItems.Cast<TrabajadorRow>())
                {
                    if (row.Departamento != dept)
                    {
                        row.Departamento = dept;
                        
                        // Lógica recursiva de heredar cierres e incompatibilidades al cambiar departamento
                        if (_datos.DepartamentosIncompatibles != null && _datos.DepartamentosIncompatibles.Contains(dept))
                        {
                            var miembros = Trabajadores.Where(t => t.Departamento == dept && t.Nombre != row.Nombre).Select(t => t.Nombre).ToList();
                            if (!_datos.Incompatibilidades.ContainsKey(row.Nombre)) _datos.Incompatibilidades[row.Nombre] = new List<string>();
                            
                            foreach(var m in miembros)
                            {
                                if (!_datos.Incompatibilidades[row.Nombre].Contains(m)) _datos.Incompatibilidades[row.Nombre].Add(m);
                                if (!_datos.Incompatibilidades.ContainsKey(m)) _datos.Incompatibilidades[m] = new List<string>();
                                if (!_datos.Incompatibilidades[m].Contains(row.Nombre)) _datos.Incompatibilidades[m].Add(row.Nombre);
                            }
                        }

                        if (_datos.Cierres != null && _datos.Cierres.ContainsKey(dept))
                        {
                            var workerOriginal = _datos.Trabajadores.ContainsKey(row.NombreOriginal) ? _datos.Trabajadores[row.NombreOriginal] : null;
                            List<string> vacs = workerOriginal != null ? workerOriginal.Vacaciones.ToList() : new List<string>();
                            foreach(var f in _datos.Cierres[dept])
                            {
                                if (!vacs.Contains(f)) vacs.Add(f);
                            }
                            if (workerOriginal != null) workerOriginal.Vacaciones = vacs;
                            // Esto no se refleja directamente en la UI de días usados hasta guardar
                        }
                    }
                }
            }
        }

        #endregion

        #region Pestaña Festivos

        /// <summary>
        /// Añade la fecha seleccionada en el DatePicker a la lista de festivos.
        /// </summary>
        private void BtnAddFestivo_Click(object sender, RoutedEventArgs e)
        {
            if (DpNuevoFestivo.SelectedDate.HasValue)
            {
                string fechaStr = DpNuevoFestivo.SelectedDate.Value.ToString("dd/MM/yyyy");
                string dpto = CmbNuevoFestivoDpto.SelectedItem?.ToString() ?? "Global";
                
                if (!Festivos.Any(f => f.Fecha == fechaStr && f.Departamento == dpto))
                {
                    Festivos.Add(new FestivoRow { Fecha = fechaStr, Departamento = dpto });
                    DpNuevoFestivo.SelectedDate = null;
                }
            }
        }

        /// <summary>
        /// Elimina los festivos seleccionados en el DataGrid.
        /// </summary>
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

        /// <summary>
        /// Marca todas las casillas de meses para incluirlos en la exportación.
        /// </summary>
        private void BtnSelectAllMeses_Click(object sender, RoutedEventArgs e)
        {
            foreach (var chk in _chkMeses) chk.IsChecked = true;
        }

        /// <summary>
        /// Desmarca todas las casillas de meses en la configuración de exportación.
        /// </summary>
        private void BtnDeselectAllMeses_Click(object sender, RoutedEventArgs e)
        {
            foreach (var chk in _chkMeses) chk.IsChecked = false;
        }

        #endregion

        #region Acciones principales

        /// <summary>
        /// Procesa los cambios realizados en la ventana y los aplica a la configuración y plan de vacaciones.
        /// </summary>
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
                        info.Departamento = row.Departamento;
                        info.DiasBase = row.DiasBase;
                        info.DiasExtras = row.DiasExtras;
                        _datos.Trabajadores[row.Nombre] = info;
                    }
                    else
                    {
                        // Solo actualizar datos
                        _datos.Trabajadores[row.Nombre].Departamento = row.Departamento;
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
                            Departamento = row.Departamento,
                            DiasBase = row.DiasBase,
                            DiasExtras = row.DiasExtras
                        };
                    }
                }
            }

            // Aplicar recursivamente Cierres e Incompatibilidades de Departamento a todos los trabajadores
            if (_datos.DepartamentosIncompatibles != null)
            {
                foreach (var dept in _datos.DepartamentosIncompatibles)
                {
                    var miembros = _datos.Trabajadores.Where(kvp => (kvp.Value.Departamento ?? "General") == dept).Select(kvp => kvp.Key).ToList();
                    if (miembros.Count >= 2)
                    {
                        if (_datos.Incompatibilidades == null) _datos.Incompatibilidades = new Dictionary<string, List<string>>();
                        foreach (var m1 in miembros)
                        {
                            if (!_datos.Incompatibilidades.ContainsKey(m1)) _datos.Incompatibilidades[m1] = new List<string>();
                            foreach (var m2 in miembros)
                            {
                                if (m1 != m2 && !_datos.Incompatibilidades[m1].Contains(m2)) _datos.Incompatibilidades[m1].Add(m2);
                            }
                        }
                    }
                }
            }

            if (_datos.Cierres != null)
            {
                foreach (var kvpCierre in _datos.Cierres)
                {
                    string dept = kvpCierre.Key;
                    var cierresDept = kvpCierre.Value;
                    if (cierresDept != null && cierresDept.Count > 0)
                    {
                        var miembros = _datos.Trabajadores.Where(kvp => dept == "__todos__" || (kvp.Value.Departamento ?? "General") == dept).Select(kvp => kvp.Value).ToList();
                        foreach (var trabajador in miembros)
                        {
                            foreach (var cierreFecha in cierresDept)
                            {
                                if (!trabajador.Vacaciones.Contains(cierreFecha)) trabajador.Vacaciones.Add(cierreFecha);
                            }
                        }
                    }
                }
            }

            // --- Aplicar cambios de Festivos ---
            _datos.Festivos.Clear();
            if (_datos.FestivosDepartamento == null) _datos.FestivosDepartamento = new Dictionary<string, List<string>>();
            _datos.FestivosDepartamento.Clear();

            foreach (var row in Festivos)
            {
                string f = row.Fecha.Trim();
                if (!string.IsNullOrEmpty(f))
                {
                    if (row.Departamento == "Global")
                    {
                        if (!_datos.Festivos.Contains(f)) _datos.Festivos.Add(f);
                    }
                    else
                    {
                        if (!_datos.FestivosDepartamento.ContainsKey(row.Departamento))
                        {
                            _datos.FestivosDepartamento[row.Departamento] = new List<string>();
                        }
                        if (!_datos.FestivosDepartamento[row.Departamento].Contains(f))
                        {
                            _datos.FestivosDepartamento[row.Departamento].Add(f);
                        }
                    }
                }
            }

            // Limpiar vacaciones que caigan en festivos nuevos (globales y de departamento)
            foreach (var kvp in _datos.Trabajadores)
            {
                var w = kvp.Key;
                var info = kvp.Value;
                var festivosTrabajador = RangoVacacionesHelper.ObtenerFestivosTrabajador(w, _datos);
                info.Vacaciones.RemoveAll(v => festivosTrabajador.Contains(v));
            }

            // --- Aplicar cambios de Configuración ---
            _config.TipoPersistencia = CbPersistencia.SelectedIndex == 0 ? "SQLite" : "JSON";
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

        /// <summary>
        /// Cierra la ventana sin aplicar los cambios.
        /// </summary>
        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            Aceptado = false;
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Disminuye en uno el año activo del plan de vacaciones.
        /// </summary>
        private void BtnDecActiveYear_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtAnoActivo.Text, out int y) && y > 1900)
            {
                TxtAnoActivo.Text = (y - 1).ToString();
            }
        }

        /// <summary>
        /// Incrementa en uno el año activo del plan de vacaciones.
        /// </summary>
        private void BtnIncActiveYear_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtAnoActivo.Text, out int y) && y < 2100)
            {
                TxtAnoActivo.Text = (y + 1).ToString();
            }
        }

        /// <summary>
        /// Obtiene una lista consolidada y ordenada de todos los años que contienen datos (vacaciones o festivos).
        /// </summary>
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

        /// <summary>
        /// Importa los festivos oficiales de la región seleccionada para el año activo desde OpenHolidays API.
        /// </summary>
        private async void BtnImportarFestivosCCAA_Click(object sender, RoutedEventArgs e)
        {
            if (CbRegiones.SelectedValue == null) return;
            
            string isoCode = CbRegiones.SelectedValue.ToString();
            string nombreCCAA = ((KeyValuePair<string, string>)CbRegiones.SelectedItem).Value;

            BtnImportarFestivosCCAA_Click_UIStatus(false, "Descargando...");

            var api = new OpenHolidaysApiService();
            var nuevosFestivos = await api.ObtenerFestivosAsync(isoCode, _datos.Year);

            BtnImportarFestivosCCAA_Click_UIStatus(true, "📥 Importar Festivos");

            if (nuevosFestivos.Count > 0)
            {
                int agregados = 0;
                foreach (var f in nuevosFestivos)
                {
                    if (!Festivos.Any(existing => existing.Fecha == f))
                    {
                        Festivos.Add(new FestivoRow { Fecha = f });
                        agregados++;
                    }
                }

                MessageBox.Show($"Se han importado {agregados} nuevos festivos para {nombreCCAA} ({_datos.Year}).", 
                    "Importación Completada", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"No se pudieron obtener los festivos para {nombreCCAA} o la lista está vacía.", 
                    "Información", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Actualiza el estado visual de la interfaz durante la importación de festivos.
        /// </summary>
        private void BtnImportarFestivosCCAA_Click_UIStatus(bool enabled, string content)
        {
            CbRegiones.IsEnabled = enabled;
        }

        #endregion

        #region Departamentos e Incompatibilidades

        private void LstDepartamentos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstDepartamentos.SelectedItem is string dpt)
            {
                PanelDetalleDepartamento.IsEnabled = true;
                LstCierresDpto.ItemsSource = null;
                if (_datos.Cierres != null && _datos.Cierres.ContainsKey(dpt))
                {
                    LstCierresDpto.ItemsSource = _datos.Cierres[dpt].ToList();
                }
                
                ChkDptoIncompatible.IsChecked = _datos.DepartamentosIncompatibles != null && _datos.DepartamentosIncompatibles.Contains(dpt);
                
                // Cargar color
                CmbDptoColor.SelectionChanged -= CmbDptoColor_SelectionChanged; // Desuscribir temporalmente
                CmbDptoColor.SelectedIndex = 0; // Por defecto Gris
                if (_datos.DepartamentosColores != null && _datos.DepartamentosColores.ContainsKey(dpt))
                {
                    string colorHex = _datos.DepartamentosColores[dpt];
                    foreach (ComboBoxItem item in CmbDptoColor.Items)
                    {
                        if (item.Tag?.ToString() == colorHex)
                        {
                            CmbDptoColor.SelectedItem = item;
                            break;
                        }
                    }
                }
                CmbDptoColor.SelectionChanged += CmbDptoColor_SelectionChanged; // Suscribir de nuevo
            }
            else
            {
                PanelDetalleDepartamento.IsEnabled = false;
                LstCierresDpto.ItemsSource = null;
                ChkDptoIncompatible.IsChecked = false;
            }
        }

        private void BtnAddCierreDpto_Click(object sender, RoutedEventArgs e)
        {
            if (LstDepartamentos.SelectedItem is string dpt && CalCierreDpto.SelectedDates.Count > 0)
            {
                if (_datos.Cierres == null) _datos.Cierres = new Dictionary<string, List<string>>();
                if (!_datos.Cierres.ContainsKey(dpt)) _datos.Cierres[dpt] = new List<string>();
                
                var list = _datos.Cierres[dpt];
                bool changed = false;
                
                foreach (DateTime date in CalCierreDpto.SelectedDates)
                {
                    string fecha = date.ToString("dd/MM/yyyy");
                    if (!list.Contains(fecha))
                    {
                        list.Add(fecha);
                        changed = true;
                    }
                }
                
                if (changed)
                {
                    list.Sort((a, b) => System.DateTime.ParseExact(a, "dd/MM/yyyy", null).CompareTo(System.DateTime.ParseExact(b, "dd/MM/yyyy", null)));
                    LstCierresDpto.ItemsSource = null;
                    LstCierresDpto.ItemsSource = list;
                }
            }
        }

        private void BtnRemoveCierreDpto_Click(object sender, RoutedEventArgs e)
        {
            if (LstDepartamentos.SelectedItem is string dpt && LstCierresDpto.SelectedItem is string fecha)
            {
                if (_datos.Cierres != null && _datos.Cierres.ContainsKey(dpt))
                {
                    var list = _datos.Cierres[dpt];
                    list.Remove(fecha);
                    LstCierresDpto.ItemsSource = null;
                    LstCierresDpto.ItemsSource = list;
                }
            }
        }

        private void ChkDptoIncompatible_Checked(object sender, RoutedEventArgs e)
        {
            if (LstDepartamentos.SelectedItem is string dpt)
            {
                if (_datos.DepartamentosIncompatibles == null) _datos.DepartamentosIncompatibles = new List<string>();
                var list = _datos.DepartamentosIncompatibles;
                
                bool isChecked = ChkDptoIncompatible.IsChecked == true;
                if (isChecked && !list.Contains(dpt))
                {
                    list.Add(dpt);
                    
                    // Aplicar incompatibilidad mutua a todos los miembros actuales del departamento
                    var miembros = _datos.Trabajadores.Where(kvp => (kvp.Value.Departamento ?? "General") == dpt).Select(kvp => kvp.Key).ToList();
                    if (miembros.Count >= 2)
                    {
                        if (_datos.Incompatibilidades == null) _datos.Incompatibilidades = new Dictionary<string, List<string>>();
                        foreach (var m1 in miembros)
                        {
                            if (!_datos.Incompatibilidades.ContainsKey(m1)) _datos.Incompatibilidades[m1] = new List<string>();
                            foreach (var m2 in miembros)
                            {
                                if (m1 != m2 && !_datos.Incompatibilidades[m1].Contains(m2))
                                {
                                    _datos.Incompatibilidades[m1].Add(m2);
                                }
                            }
                        }
                        MessageBox.Show($"Se han generado las incompatibilidades cruzadas para los {miembros.Count} miembros de {dpt}.", "Incompatibilidades generadas", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else if (!isChecked && list.Contains(dpt)) 
                {
                    list.Remove(dpt);
                }
            }
        }

        private void CmbDptoColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstDepartamentos.SelectedItem is string dpt && CmbDptoColor.SelectedItem is ComboBoxItem item)
            {
                if (_datos.DepartamentosColores == null) _datos.DepartamentosColores = new Dictionary<string, string>();
                
                string hex = item.Tag?.ToString();
                if (!string.IsNullOrEmpty(hex))
                {
                    _datos.DepartamentosColores[dpt] = hex;
                }
                else
                {
                    if (_datos.DepartamentosColores.ContainsKey(dpt))
                        _datos.DepartamentosColores.Remove(dpt);
                }
            }
        }

        private void BtnAddDpto_Click(object sender, RoutedEventArgs e)
        {
            string dpt = TxtNuevoDpto.Text.Trim();
            if (!string.IsNullOrEmpty(dpt))
            {
                if (_datos.Departamentos == null) _datos.Departamentos = new List<string>();
                if (!_datos.Departamentos.Contains(dpt))
                {
                    _datos.Departamentos.Add(dpt);
                    LstDepartamentos.ItemsSource = null;
                    LstDepartamentos.ItemsSource = GetDepartamentosConGeneral();
                    CmbLoteDepartamento.ItemsSource = null;
                    CmbLoteDepartamento.ItemsSource = _datos.Departamentos;
                    TxtNuevoDpto.Text = "";
                }
            }
        }

        private void BtnRemoveDpto_Click(object sender, RoutedEventArgs e)
        {
            if (LstDepartamentos.SelectedItem is string dpt)
            {
                if (_datos.Departamentos != null)
                {
                    _datos.Departamentos.Remove(dpt);
                    LstDepartamentos.ItemsSource = null;
                    LstDepartamentos.ItemsSource = GetDepartamentosConGeneral();
                    CmbLoteDepartamento.ItemsSource = null;
                    CmbLoteDepartamento.ItemsSource = _datos.Departamentos;
                }
            }
        }

        private void LstTrabajadoresIncomp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstTrabajadoresIncomp.SelectedItem is string w)
            {
                PanelDetalleIncomp.IsEnabled = true;
                LstIncompatibles.ItemsSource = null;
                if (_datos.Incompatibilidades != null && _datos.Incompatibilidades.ContainsKey(w))
                {
                    LstIncompatibles.ItemsSource = _datos.Incompatibilidades[w].ToList();
                }
                
                var otros = _datos.Trabajadores.Keys.Where(k => k != w).ToList();
                CmbTrabajadorIncompatible.ItemsSource = otros;
            }
            else
            {
                PanelDetalleIncomp.IsEnabled = false;
                LstIncompatibles.ItemsSource = null;
            }
        }

        private void BtnAddIncomp_Click(object sender, RoutedEventArgs e)
        {
            if (LstTrabajadoresIncomp.SelectedItem is string w && CmbTrabajadorIncompatible.SelectedItem is string incomp)
            {
                if (_datos.Incompatibilidades == null) _datos.Incompatibilidades = new Dictionary<string, List<string>>();
                
                if (!_datos.Incompatibilidades.ContainsKey(w)) _datos.Incompatibilidades[w] = new List<string>();
                if (!_datos.Incompatibilidades[w].Contains(incomp)) _datos.Incompatibilidades[w].Add(incomp);
                
                if (!_datos.Incompatibilidades.ContainsKey(incomp)) _datos.Incompatibilidades[incomp] = new List<string>();
                if (!_datos.Incompatibilidades[incomp].Contains(w)) _datos.Incompatibilidades[incomp].Add(w);
                
                LstIncompatibles.ItemsSource = null;
                LstIncompatibles.ItemsSource = _datos.Incompatibilidades[w].ToList();
            }
        }

        private void BtnRemoveIncomp_Click(object sender, RoutedEventArgs e)
        {
            if (LstTrabajadoresIncomp.SelectedItem is string w && LstIncompatibles.SelectedItem is string incomp)
            {
                if (_datos.Incompatibilidades != null)
                {
                    if (_datos.Incompatibilidades.ContainsKey(w)) _datos.Incompatibilidades[w].Remove(incomp);
                    if (_datos.Incompatibilidades.ContainsKey(incomp)) _datos.Incompatibilidades[incomp].Remove(w);
                    
                    LstIncompatibles.ItemsSource = null;
                    LstIncompatibles.ItemsSource = _datos.Incompatibilidades[w].ToList();
                }
            }
        }

        #endregion
        private void List_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (sender is ListBox listBox)
                {
                    int index = listBox.SelectedIndex;
                    if (index == -1) return;
                    
                    if (listBox == LstDepartamentos) BtnRemoveDpto_Click(null, null);
                    else if (listBox == LstCierresDpto) BtnRemoveCierreDpto_Click(null, null);
                    else if (listBox == LstIncompatibles) BtnRemoveIncomp_Click(null, null);
                    
                    if (listBox.Items.Count > 0)
                    {
                        listBox.SelectedIndex = Math.Min(index, listBox.Items.Count - 1);
                        var item = listBox.SelectedItem;
                        if (item != null)
                        {
                            listBox.ScrollIntoView(item);
                        }
                    }
                }
                else if (sender is DataGrid dataGrid)
                {
                    int index = dataGrid.SelectedIndex;
                    if (index == -1) return;
                    
                    if (dataGrid == DgTrabajadores) BtnRemoveTrabajador_Click(null, null);
                    else if (dataGrid == DgFestivos) BtnRemoveFestivo_Click(null, null);
                    
                    if (dataGrid.Items.Count > 0)
                    {
                        dataGrid.SelectedIndex = Math.Min(index, dataGrid.Items.Count - 1);
                        var item = dataGrid.SelectedItem;
                        if (item != null)
                        {
                            dataGrid.ScrollIntoView(item);
                        }
                    }
                }
            }
        }
    }
}
