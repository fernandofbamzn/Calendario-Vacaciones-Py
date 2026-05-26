using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using CalendarioWPF.Services;

namespace CalendarioWPF
{
    public partial class MainWindow : Window
    {
        // Estado Global
        private PlanVacaciones _datos = new();
        private AppConfig _config = new();
        private string _activeWorker = "";
        private string _editMode = "vacaciones"; // "vacaciones" o "festivos"
        private const string DatosFilename = "datos_vacaciones.json";
        private int _visualizedYear;
        private List<string> _logMessages = new() { $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] Aplicación iniciada." };

        // Variables para arrastre (drag-to-select)
        private bool _isDragging = false;
        private string _dragAction = ""; // "select" o "deselect"
        private string _dragSelectionType = ""; // "vacaciones" o "festivos"

        // Timer para la StatusBar y confirmación de eliminación
        private DispatcherTimer? _statusTimer;

        public new object FindResource(object resourceKey)
        {
            string key = resourceKey?.ToString() ?? string.Empty;
            if (this.Resources.Contains(key))
            {
                return this.Resources[key];
            }
            var res = this.TryFindResource(key);
            if (res != null)
            {
                return res;
            }
            
            // Colores de respaldo premium en caso de fallos de inicialización o caché de recursos
            switch (key)
            {
                case "ColorPrimary": return new SolidColorBrush(Color.FromRgb(99, 102, 241));
                case "ColorPrimaryHover": return new SolidColorBrush(Color.FromRgb(79, 70, 229));
                case "ColorAccent": return new SolidColorBrush(Color.FromRgb(16, 185, 129));
                case "ColorAccentHover": return new SolidColorBrush(Color.FromRgb(5, 150, 105));
                case "ColorDanger": return new SolidColorBrush(Color.FromRgb(239, 68, 68));
                case "ColorDangerHover": return new SolidColorBrush(Color.FromRgb(220, 38, 38));
                case "ColorBorder": return new SolidColorBrush(Color.FromRgb(226, 232, 240));
                case "ColorTextMain": return new SolidColorBrush(Color.FromRgb(15, 23, 42));
                case "ColorTextMuted": return new SolidColorBrush(Color.FromRgb(100, 116, 139));
                case "ColorBgCard": return Brushes.White;
                case "ColorBgApp": return new SolidColorBrush(Color.FromRgb(248, 250, 252));
                case "ColorWeekend": return new SolidColorBrush(Color.FromRgb(243, 244, 246));
                case "ColorWeekendText": return new SolidColorBrush(Color.FromRgb(107, 114, 128));
                case "ColorFestivo": return new SolidColorBrush(Color.FromRgb(254, 226, 226));
                case "ColorFestivoText": return new SolidColorBrush(Color.FromRgb(153, 27, 27));
                case "ColorVacacionBase": return new SolidColorBrush(Color.FromRgb(219, 234, 254));
                case "ColorVacacionText": return new SolidColorBrush(Color.FromRgb(30, 64, 175));
                default: return Brushes.Transparent;
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            _config = AppConfigManager.Cargar();
            CargarDatos();
            ActualizarSelectTrabajadores();
            ActualizarPanelCupo();
            ActualizarVistas();
        }

        private void MostrarEstado(string mensaje)
        {
            if (StatusBarText == null || MainStatusBar == null) return;
            StatusBarText.Text = mensaje;

            // Registrar en el log en memoria
            _logMessages.Add($"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] {mensaje}");

            // Detección automática de color según el emoji del mensaje
            if (mensaje.StartsWith("⚠️"))
            {
                MainStatusBar.Background = new SolidColorBrush(Color.FromRgb(254, 249, 195)); // Amarillo claro (Warning)
                StatusBarText.Foreground = new SolidColorBrush(Color.FromRgb(161, 98, 7));    // Amarillo oscuro
            }
            else if (mensaje.StartsWith("❌"))
            {
                MainStatusBar.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226)); // Rojo claro (Error)
                StatusBarText.Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28));    // Rojo oscuro
            }
            else if (mensaje.StartsWith("✅"))
            {
                MainStatusBar.Background = new SolidColorBrush(Color.FromRgb(220, 252, 231)); // Verde claro (Info)
                StatusBarText.Foreground = new SolidColorBrush(Color.FromRgb(21, 128, 61));   // Verde oscuro
            }
            else
            {
                MainStatusBar.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)); // Gris por defecto
                StatusBarText.Foreground = (SolidColorBrush)FindResource("ColorTextMain");
            }

            _statusTimer?.Stop();
            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _statusTimer.Tick += (s, e) =>
            {
                _statusTimer.Stop();
                StatusBarText.Text = "Listo";
                StatusBarText.Foreground = (SolidColorBrush)FindResource("ColorTextMuted");
                MainStatusBar.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)); // Gris por defecto
            };
            _statusTimer.Start();
        }

        #region Persistencia JSON

        private void CargarDatos()
        {
            try
            {
                _datos = DataManager.CargarDatos();
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
            {
                MenuLabelActiveYear.Text = $"Año de vacaciones: {_datos.Year}";
            }
        }

        private void GuardarDatos()
        {
            try
            {
                _datos.TituloPagina = PageTitleInput.Text.Trim();
                DataManager.GuardarDatos(_datos);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar datos: {ex.Message}", "Error de Guardado", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Gestión de Personal y Controles

        private void ActualizarSelectTrabajadores()
        {
            SelectWorker.SelectionChanged -= SelectWorker_SelectionChanged;
            SelectWorker.Items.Clear();

            var nombres = _datos.Trabajadores.Keys.OrderBy(n => n).ToList();

            foreach (var nombre in nombres)
            {
                SelectWorker.Items.Add(nombre);
            }

            if (nombres.Count > 0)
            {
                if (!string.IsNullOrEmpty(_activeWorker) && _datos.Trabajadores.ContainsKey(_activeWorker))
                {
                    SelectWorker.SelectedItem = _activeWorker;
                }
                else
                {
                    SelectWorker.SelectedIndex = 0;
                    _activeWorker = nombres[0];
                }
            }
            else
            {
                _activeWorker = "";
            }

            SelectWorker.SelectionChanged += SelectWorker_SelectionChanged;
        }

        private void SelectWorker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SelectWorker.SelectedItem != null)
            {
                _activeWorker = SelectWorker.SelectedItem.ToString() ?? "";
            }
            else
            {
                _activeWorker = "";
            }
            ActualizarPanelCupo();
            ActualizarVistas();
        }

        private void BtnDecBase_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_activeWorker) || !_datos.Trabajadores.ContainsKey(_activeWorker)) return;
            var info = _datos.Trabajadores[_activeWorker];
            if (info.DiasBase > 0)
            {
                info.DiasBase--;
                GuardarDatos();
                ActualizarPanelCupo();
                ActualizarVistas();
            }
        }

        private void BtnIncBase_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_activeWorker) || !_datos.Trabajadores.ContainsKey(_activeWorker)) return;
            var info = _datos.Trabajadores[_activeWorker];
            info.DiasBase++;
            GuardarDatos();
            ActualizarPanelCupo();
            ActualizarVistas();
        }

        private void BtnDecExtras_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_activeWorker) || !_datos.Trabajadores.ContainsKey(_activeWorker)) return;
            var info = _datos.Trabajadores[_activeWorker];
            if (info.DiasExtras > 0)
            {
                info.DiasExtras--;
                GuardarDatos();
                ActualizarPanelCupo();
                ActualizarVistas();
            }
        }

        private void BtnIncExtras_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_activeWorker) || !_datos.Trabajadores.ContainsKey(_activeWorker)) return;
            var info = _datos.Trabajadores[_activeWorker];
            info.DiasExtras++;
            GuardarDatos();
            ActualizarPanelCupo();
            ActualizarVistas();
        }

        private void MenuMarcarVacaciones_Click(object sender, RoutedEventArgs e)
        {
            MenuMarcarVacaciones.IsChecked = true;
            MenuMarcarFestivos.IsChecked = false;
            _editMode = "vacaciones";
            ActualizarVistas();
        }

        private void MenuMarcarFestivos_Click(object sender, RoutedEventArgs e)
        {
            MenuMarcarVacaciones.IsChecked = false;
            MenuMarcarFestivos.IsChecked = true;
            _editMode = "festivos";
            ActualizarVistas();
        }

        private void ActualizarPanelCupo()
        {
            if (LabelQuotaSummary == null || ProgressBarQuota == null || TxtDaysBase == null || TxtDaysExtras == null) return;

            if (string.IsNullOrEmpty(_activeWorker) || !_datos.Trabajadores.ContainsKey(_activeWorker))
            {
                TxtDaysBase.Text = "22";
                TxtDaysExtras.Text = "0";
                ProgressBarQuota.Value = 0;
                LabelQuotaSummary.Text = "Sin trabajador activo";
                LabelQuotaSummary.Foreground = (SolidColorBrush)FindResource("ColorTextMuted");
                return;
            }

            var info = _datos.Trabajadores[_activeWorker];
            
            TxtDaysBase.Text = info.DiasBase.ToString();
            TxtDaysExtras.Text = info.DiasExtras.ToString();

            int totalDisponibles = info.DiasBase + info.DiasExtras;
            int consumidos = RangoVacacionesHelper.ContarDiasConsumidos(info.Vacaciones, info.Imputaciones, _datos.Festivos, _datos.Year);
            int restantes = totalDisponibles - consumidos;

            double pct = totalDisponibles > 0 ? ((double)consumidos / totalDisponibles) * 100 : 0;
            pct = Math.Min(100, Math.Max(0, pct));

            ProgressBarQuota.Value = pct;
            LabelQuotaSummary.Text = $"Cupo {_datos.Year}: {consumidos} de {totalDisponibles} (Quedan: {restantes})";

            if (restantes < 0)
            {
                ProgressBarQuota.Foreground = (SolidColorBrush)FindResource("ColorDanger");
                LabelQuotaSummary.Foreground = (SolidColorBrush)FindResource("ColorDanger");
            }
            else
            {
                ProgressBarQuota.Foreground = (SolidColorBrush)FindResource("ColorAccent");
                LabelQuotaSummary.Foreground = (SolidColorBrush)FindResource("ColorAccent");
            }
        }

        #endregion

        #region Renderizado General y Vistas

        private void ActualizarVistas()
        {
            ActualizarPanelVacacionesTexto();
            RenderCalendar();
        }

        private void ActualizarPanelVacacionesTexto()
        {
            if (PanelVacacionesTexto == null) return;

            PanelVacacionesTexto.Children.Clear();
            var sortedWorkers = _datos.Trabajadores.Keys.OrderBy(n => n).ToList();

            if (sortedWorkers.Count == 0)
            {
                var emptyTxt = new TextBlock
                {
                    Text = "No hay personal registrado en el sistema.",
                    FontStyle = FontStyles.Italic,
                    Foreground = (SolidColorBrush)FindResource("ColorTextMuted"),
                    FontSize = 13
                };
                PanelVacacionesTexto.Children.Add(emptyTxt);
                return;
            }

            foreach (var w in sortedWorkers)
            {
                var info = _datos.Trabajadores[w];
                string rangos = RangoVacacionesHelper.AgruparVacacionesEnTexto(info.Vacaciones, info.Imputaciones, _datos.Festivos, _datos.Year);

                // Crear Item de Visualización
                var borderItem = new Border
                {
                    Background = (SolidColorBrush)FindResource("ColorBgApp"),
                    BorderBrush = (SolidColorBrush)FindResource("ColorPrimary"),
                    BorderThickness = new Thickness(4, 0, 0, 0),
                    CornerRadius = new CornerRadius(0, 8, 8, 0),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 0, 0, 8)
                };

                var sp = new StackPanel();

                var lblWorker = new TextBlock
                {
                    Text = w,
                    FontWeight = FontWeights.Bold,
                    Foreground = (SolidColorBrush)FindResource("ColorTextMain"),
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 0, 2)
                };

                var lblRanges = new TextBlock
                {
                    Text = rangos,
                    FontStyle = FontStyles.Italic,
                    Foreground = (SolidColorBrush)FindResource("ColorTextMuted"),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap
                };

                sp.Children.Add(lblWorker);
                sp.Children.Add(lblRanges);
                borderItem.Child = sp;

                PanelVacacionesTexto.Children.Add(borderItem);
            }
        }

        #endregion

        #region Renderizado del Calendario Mensual e Interactividad

        private void RenderCalendar()
        {
            if (MonthsGrid == null) return;

            MonthsGrid.Children.Clear();
            // Meses dinámicos desde la configuración
            var meses = _config.MesesAMostrar.OrderBy(m => m).ToList();
            if (meses.Count == 0) meses = new List<int> { 6, 7, 8, 9 };

            // Ajustar columnas del UniformGrid dinámicamente
            int cols = meses.Count <= 2 ? meses.Count : (meses.Count <= 6 ? 2 : (meses.Count <= 9 ? 3 : 4));
            MonthsGrid.Columns = cols;

            string[] nombresMeses = { "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };

            foreach (int mes in meses)
            {
                var monthBorder = new Border
                {
                    BorderBrush = (SolidColorBrush)FindResource("ColorBorder"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Background = Brushes.White,
                    Padding = new Thickness(10),
                    Margin = new Thickness(8)
                };

                var monthPanel = new StackPanel();

                // Nombre de mes
                var lblMonthName = new TextBlock
                {
                    Text = $"{nombresMeses[mes]} {_visualizedYear}",
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Foreground = (FindResource("ColorPrimary") as SolidColorBrush) ?? Brushes.Indigo,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                monthPanel.Children.Add(lblMonthName);

                // Cabeceras de días (L, M, X, J, V, S, D)
                var headersGrid = new Grid();
                for (int i = 0; i < 7; i++) headersGrid.ColumnDefinitions.Add(new ColumnDefinition());
                string[] iniciales = { "L", "M", "X", "J", "V", "S", "D" };
                for (int i = 0; i < 7; i++)
                {
                    var headerCell = new TextBlock
                    {
                        Text = iniciales[i],
                        FontWeight = FontWeights.Bold,
                        FontSize = 11,
                        Foreground = (SolidColorBrush)FindResource("ColorTextMuted"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    Grid.SetColumn(headerCell, i);
                    headersGrid.Children.Add(headerCell);
                }
                monthPanel.Children.Add(headersGrid);

                // Grid de celdas de días
                var daysGrid = new Grid();
                for (int i = 0; i < 7; i++) daysGrid.ColumnDefinitions.Add(new ColumnDefinition());
                for (int i = 0; i < 6; i++) daysGrid.RowDefinitions.Add(new RowDefinition());

                DateTime firstDay = new DateTime(_visualizedYear, mes, 1);
                int startOffset = ((int)firstDay.DayOfWeek == 0) ? 6 : (int)firstDay.DayOfWeek - 1;
                int totalDays = DateTime.DaysInMonth(_visualizedYear, mes);

                int currentDay = 1;
                for (int r = 0; r < 6; r++)
                {
                    for (int c = 0; c < 7; c++)
                    {
                        int index = r * 7 + c;
                        if (index < startOffset || currentDay > totalDays)
                        {
                            // Celda vacía
                            var emptyCell = new Border { Background = Brushes.Transparent };
                            Grid.SetRow(emptyCell, r);
                            Grid.SetColumn(emptyCell, c);
                            daysGrid.Children.Add(emptyCell);
                        }
                        else
                        {
                            int dayVal = currentDay++;
                            string dateStr = $"{dayVal:00}/{mes:00}/{_visualizedYear}";

                            var dayCell = CrearCeldaDia(dayVal, dateStr, c >= 5);
                            Grid.SetRow(dayCell, r);
                            Grid.SetColumn(dayCell, c);
                            daysGrid.Children.Add(dayCell);
                        }
                    }
                }

                monthPanel.Children.Add(daysGrid);
                monthBorder.Child = monthPanel;
                MonthsGrid.Children.Add(monthBorder);
            }
        }

        private Border CrearCeldaDia(int dayVal, string dateStr, bool esFinSemana)
        {
            var cellBorder = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Height = 46,
                Margin = new Thickness(1.5),
                Cursor = Cursors.Hand,
                Tag = dateStr
            };

            // Contenedor principal de la celda
            var gridCell = new Grid();
            gridCell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.2, GridUnitType.Star) });
            gridCell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });

            // Número del día
            var txtNum = new TextBlock
            {
                Text = dayVal.ToString(),
                FontWeight = FontWeights.Medium,
                FontSize = 11,
                Foreground = (SolidColorBrush)FindResource("ColorTextMain"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(txtNum, 0);
            gridCell.Children.Add(txtNum);

            // Contenedor de chips de iniciales de trabajadores
            var chipsStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 1)
            };
            Grid.SetRow(chipsStack, 1);
            gridCell.Children.Add(chipsStack);

            cellBorder.Child = gridCell;

            // Determinar estados y colorear
            bool esFestivo = _datos.Festivos.Contains(dateStr);

            if (esFinSemana)
            {
                cellBorder.Background = (SolidColorBrush)FindResource("ColorWeekend");
                txtNum.Foreground = (SolidColorBrush)FindResource("ColorWeekendText");
            }
            if (esFestivo)
            {
                cellBorder.Background = (SolidColorBrush)FindResource("ColorFestivo");
                txtNum.Foreground = (SolidColorBrush)FindResource("ColorFestivoText");
                txtNum.FontWeight = FontWeights.Bold;
            }

            // Buscar vacaciones asignadas a este día
            var trabsVac = new List<string>();
            foreach (var kvp in _datos.Trabajadores)
            {
                if (kvp.Value.Vacaciones.Contains(dateStr))
                {
                    trabsVac.Add(kvp.Key);
                }
            }

            if (trabsVac.Count > 0)
            {
                bool esVacacionActivo = _editMode == "vacaciones" && trabsVac.Contains(_activeWorker);
                if (esVacacionActivo)
                {
                    var infoActivo = _datos.Trabajadores[_activeWorker];
                    int quotaYear = (infoActivo.Imputaciones != null && infoActivo.Imputaciones.TryGetValue(dateStr, out int y)) ? y : _visualizedYear;
                    if (quotaYear != _visualizedYear)
                    {
                        cellBorder.Background = (SolidColorBrush)FindResource("ColorVacacionOtroAño");
                        txtNum.Foreground = (SolidColorBrush)FindResource("ColorVacacionOtroAñoText");
                    }
                    else
                    {
                        cellBorder.Background = (SolidColorBrush)FindResource("ColorVacacionBase");
                        txtNum.Foreground = (SolidColorBrush)FindResource("ColorVacacionText");
                    }
                }
                else
                {
                    bool todosOtroAno = true;
                    foreach (var t in trabsVac)
                    {
                        if (_datos.Trabajadores.TryGetValue(t, out var tInfo))
                        {
                            int qYear = (tInfo.Imputaciones != null && tInfo.Imputaciones.TryGetValue(dateStr, out int y)) ? y : _visualizedYear;
                            if (qYear == _visualizedYear)
                            {
                                todosOtroAno = false;
                                break;
                            }
                        }
                    }

                    if (todosOtroAno)
                    {
                        cellBorder.Background = (SolidColorBrush)FindResource("ColorVacacionOtroAño");
                        txtNum.Foreground = (SolidColorBrush)FindResource("ColorVacacionOtroAñoText");
                    }
                    else
                    {
                        cellBorder.Background = new SolidColorBrush(Color.FromRgb(199, 210, 254)); // Indigo claro
                        txtNum.Foreground = new SolidColorBrush(Color.FromRgb(55, 48, 163)); // Indigo oscuro
                    }
                }
                txtNum.FontWeight = FontWeights.Bold;

                // Añadir chips de iniciales
                int maxChips = 2;
                for (int i = 0; i < Math.Min(maxChips, trabsVac.Count); i++)
                {
                    var chip = CrearChipIniciales(trabsVac[i], dateStr);
                    chipsStack.Children.Add(chip);
                }

                if (trabsVac.Count > maxChips)
                {
                    var moreChip = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(91, 44, 111)), // Violeta oscuro
                        CornerRadius = new CornerRadius(2),
                        Padding = new Thickness(2, 0, 2, 0),
                        Margin = new Thickness(1, 0, 1, 0)
                    };
                    var moreText = new TextBlock
                    {
                        Text = $"+{trabsVac.Count - maxChips}",
                        FontSize = 7.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White
                    };
                    moreChip.Child = moreText;
                    chipsStack.Children.Add(moreChip);
                }

                // Tooltip
                var sb = new StringBuilder();
                sb.AppendLine($"Vacaciones ({dateStr}):");
                foreach (var t in trabsVac) sb.AppendLine($"• {t}");
                cellBorder.ToolTip = sb.ToString().Trim();
            }

            // Eventos del ratón para Click y arrastre continuo (Drag-to-select)
            cellBorder.PreviewMouseLeftButtonDown += CellBorder_MouseDown;
            cellBorder.MouseEnter += CellBorder_MouseEnter;
            cellBorder.PreviewMouseLeftButtonUp += CellBorder_MouseUp;

            return cellBorder;
        }

        private Border CrearChipIniciales(string nombre, string dateStr)
        {
            string iniciales = "";
            var partes = nombre.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length >= 2)
            {
                iniciales = (partes[0][0].ToString() + partes[1][0].ToString()).ToUpper();
            }
            else if (partes.Length == 1)
            {
                iniciales = partes[0].Substring(0, Math.Min(2, partes[0].Length)).ToUpper();
            }

            if (_datos.Trabajadores.TryGetValue(nombre, out var workerInfo))
            {
                int quotaYear = (workerInfo.Imputaciones != null && workerInfo.Imputaciones.TryGetValue(dateStr, out int y)) ? y : _visualizedYear;
                if (quotaYear != _visualizedYear)
                {
                    iniciales = $"{iniciales}-{quotaYear}";
                }
            }

            var borderChip = new Border
            {
                Background = (SolidColorBrush)FindResource("ColorPrimary"),
                CornerRadius = new CornerRadius(2.5),
                Padding = new Thickness(2, 0, 2, 0),
                Margin = new Thickness(0.5, 0, 0.5, 0)
            };

            var txt = new TextBlock
            {
                Text = iniciales,
                FontSize = 7.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            borderChip.Child = txt;
            return borderChip;
        }

        #endregion

        #region Drag to select logic

        private void CellBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_editMode == "vacaciones" && string.IsNullOrEmpty(_activeWorker))
            {
                MostrarEstado("⚠️ Selecciona o añade un trabajador primero.");
                return;
            }

            var cell = sender as Border;
            if (cell == null || cell.Tag == null) return;
            string dateStr = cell.Tag.ToString() ?? "";

            _isDragging = true;
            _dragSelectionType = _editMode;
            cell.CaptureMouse();

            bool estaSeleccionado = false;
            if (_editMode == "festivos")
            {
                estaSeleccionado = _datos.Festivos.Contains(dateStr);
            }
            else
            {
                estaSeleccionado = _datos.Trabajadores[_activeWorker].Vacaciones.Contains(dateStr);
            }

            _dragAction = estaSeleccionado ? "deselect" : "select";
            ProcesarDia(dateStr, _dragAction);
            e.Handled = true;
        }

        private void CellBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!_isDragging || _dragSelectionType != _editMode || e.LeftButton != MouseButtonState.Pressed) return;

            var cell = sender as Border;
            if (cell == null || cell.Tag == null) return;
            string dateStr = cell.Tag.ToString() ?? "";

            ProcesarDia(dateStr, _dragAction);
        }

        private void CellBorder_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                var cell = sender as Border;
                if (cell != null)
                {
                    cell.ReleaseMouseCapture();
                }
                GuardarDatos();
                ActualizarPanelCupo();
                ActualizarVistas();
                e.Handled = true;
            }
        }

        private void ProcesarDia(string dateStr, string accion)
        {
            DateTime.TryParseExact(dateStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date);
            bool esWeekend = (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday);
            bool esLaborable = !esWeekend && !_datos.Festivos.Contains(dateStr);

            if (_editMode == "festivos")
            {
                if (accion == "deselect")
                {
                    _datos.Festivos.Remove(dateStr);
                }
                else
                {
                    // Eliminar de las vacaciones de cualquier trabajador si se vuelve festivo oficial
                    foreach (var kvp in _datos.Trabajadores)
                    {
                        kvp.Value.Vacaciones.Remove(dateStr);
                        kvp.Value.Imputaciones?.Remove(dateStr);
                    }
                    if (!_datos.Festivos.Contains(dateStr))
                    {
                        _datos.Festivos.Add(dateStr);
                    }
                }
            }
            else
            {
                if (string.IsNullOrEmpty(_activeWorker) || !_datos.Trabajadores.ContainsKey(_activeWorker)) return;

                var info = _datos.Trabajadores[_activeWorker];
                if (info.Imputaciones == null)
                {
                    info.Imputaciones = new Dictionary<string, int>();
                }

                if (accion == "deselect")
                {
                    info.Vacaciones.Remove(dateStr);
                    info.Imputaciones.Remove(dateStr);
                }
                else
                {
                    if (esLaborable && !info.Vacaciones.Contains(dateStr))
                    {
                        // Pintura silenciosa: el sobrecupo se indica visualmente en el panel de cupo (rojo)
                    }

                    // Quitar de festivos si se marca como vacaciones de un empleado
                    _datos.Festivos.Remove(dateStr);

                    if (!info.Vacaciones.Contains(dateStr))
                    {
                        info.Vacaciones.Add(dateStr);
                        info.Imputaciones[dateStr] = _datos.Year; // Guardamos el año de cupo activo actual
                    }
                }
            }

            // Actualizar vista intermedia para feedback de arrastre
            RenderCalendar();
        }

        #endregion

        #region Renderizado de Tabla Gantt

        private void TabGantt_Selected(object sender, RoutedEventArgs e)
        {
            RenderGantt();
        }

        private List<int> ObtenerAnosConDatos()
        {
            var anos = new HashSet<int> { _datos.Year };
            foreach (var festivo in _datos.Festivos)
            {
                if (DateTime.TryParseExact(festivo, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                {
                    anos.Add(d.Year);
                }
            }
            foreach (var worker in _datos.Trabajadores.Values)
            {
                foreach (var vac in worker.Vacaciones)
                {
                    if (DateTime.TryParseExact(vac, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    {
                        int qYear = (worker.Imputaciones != null && worker.Imputaciones.TryGetValue(vac, out int yVal)) ? yVal : d.Year;
                        anos.Add(qYear);
                    }
                }
            }
            return anos.OrderBy(y => y).ToList();
        }

        private (List<string> mesesSecuencia, List<DateTime> fechasEjeX) ObtenerSecuenciaGantt()
        {
            var todasFechas = new List<DateTime>();
            foreach (var kvp in _datos.Trabajadores)
            {
                foreach (var fStr in kvp.Value.Vacaciones)
                {
                    if (DateTime.TryParseExact(fStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    {
                        if (d.Year == _visualizedYear)
                        {
                            todasFechas.Add(d);
                        }
                    }
                }
            }

            DateTime minDate, maxDate;
            if (todasFechas.Count > 0)
            {
                minDate = todasFechas.Min();
                maxDate = todasFechas.Max();
            }
            else
            {
                minDate = new DateTime(_visualizedYear, 6, 1); // 1 de Junio
                maxDate = new DateTime(_visualizedYear, 9, 30); // 30 de Septiembre
            }

            var mesesRango = new List<string>();
            DateTime current = new DateTime(minDate.Year, minDate.Month, 1);
            DateTime limit = new DateTime(maxDate.Year, maxDate.Month, 1);

            while (current <= limit)
            {
                mesesRango.Add($"{current.Year}-{current.Month}");
                current = current.AddMonths(1);
            }

            var fechasEjeX = new List<DateTime>();
            foreach (var mStr in mesesRango)
            {
                var parts = mStr.Split('-');
                int y = int.Parse(parts[0]);
                int m = int.Parse(parts[1]);
                int totalDias = DateTime.DaysInMonth(y, m);
                for (int d = 1; d <= totalDias; d++)
                {
                    fechasEjeX.Add(new DateTime(y, m, d));
                }
            }

            return (mesesRango, fechasEjeX);
        }

        private void RenderGantt()
        {
            if (GanttTableGrid == null) return;

            GanttTableGrid.Children.Clear();
            GanttTableGrid.RowDefinitions.Clear();
            GanttTableGrid.ColumnDefinitions.Clear();

            var (mesesSecuencia, fechasEjeX) = ObtenerSecuenciaGantt();
            if (fechasEjeX.Count == 0) return;

            // Definir Columnas: 0 para Trabajadores, 1..N para Días
            GanttTableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            for (int i = 0; i < fechasEjeX.Count; i++)
            {
                GanttTableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            }

            // Definir Filas: 0 para Meses, 1 para Días, 2..N para Trabajadores + Fila final de cómputos (opcional)
            int totalFilas = 2 + _datos.Trabajadores.Count + (_config.OcultarComputoGantt ? 0 : 1);
            for (int i = 0; i < totalFilas; i++)
            {
                GanttTableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            // Fila 0: Meses
            var borderMesHeader = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                BorderBrush = (SolidColorBrush)FindResource("ColorBorder"),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = "MES",
                    FontWeight = FontWeights.Bold,
                    FontSize = 11,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                Height = 28
            };
            Grid.SetRow(borderMesHeader, 0);
            Grid.SetColumn(borderMesHeader, 0);
            GanttTableGrid.Children.Add(borderMesHeader);

            string[] nombresMeses = { "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };
            int currentColIndex = 1;

            foreach (var mStr in mesesSecuencia)
            {
                var parts = mStr.Split('-');
                int y = int.Parse(parts[0]);
                int m = int.Parse(parts[1]);
                int diasMes = DateTime.DaysInMonth(y, m);

                var borderMes = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                    BorderBrush = (SolidColorBrush)FindResource("ColorBorder"),
                    BorderThickness = new Thickness(1),
                    Child = new TextBlock
                    {
                        Text = $"{nombresMeses[m].ToUpper()} {y}",
                        FontWeight = FontWeights.Bold,
                        FontSize = 10,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    Height = 28
                };
                Grid.SetRow(borderMes, 0);
                Grid.SetColumn(borderMes, currentColIndex);
                Grid.SetColumnSpan(borderMes, diasMes);
                GanttTableGrid.Children.Add(borderMes);

                currentColIndex += diasMes;
            }

            // Fila 1: Días
            var borderDiaHeader = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderBrush = (SolidColorBrush)FindResource("ColorBorder"),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = "TRABAJADOR",
                    FontWeight = FontWeights.Bold,
                    FontSize = 11,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                Height = 24
            };
            Grid.SetRow(borderDiaHeader, 1);
            Grid.SetColumn(borderDiaHeader, 0);
            GanttTableGrid.Children.Add(borderDiaHeader);

            for (int i = 0; i < fechasEjeX.Count; i++)
            {
                var borderDia = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    BorderBrush = (SolidColorBrush)FindResource("ColorBorder"),
                    BorderThickness = new Thickness(1),
                    Child = new TextBlock
                    {
                        Text = fechasEjeX[i].Day.ToString(),
                        FontWeight = FontWeights.Bold,
                        FontSize = 10,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    Height = 24
                };
                Grid.SetRow(borderDia, 1);
                Grid.SetColumn(borderDia, i + 1);
                GanttTableGrid.Children.Add(borderDia);
            }

            // Filas de Trabajadores
            var sortedWorkers = _datos.Trabajadores.Keys.OrderBy(n => n).ToList();
            int rIndex = 2;

            foreach (var w in sortedWorkers)
            {
                var info = _datos.Trabajadores[w];

                // Celda del Nombre
                var borderWorker = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                    BorderBrush = (SolidColorBrush)FindResource("ColorBorder"),
                    BorderThickness = new Thickness(1),
                    Child = new TextBlock
                    {
                        Text = w,
                        FontWeight = FontWeights.Bold,
                        FontSize = 11,
                        Foreground = (SolidColorBrush)FindResource("ColorTextMain"),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(8, 0, 0, 0)
                    },
                    Height = 26
                };
                Grid.SetRow(borderWorker, rIndex);
                Grid.SetColumn(borderWorker, 0);
                GanttTableGrid.Children.Add(borderWorker);

                // Celdas del Calendario
                for (int i = 0; i < fechasEjeX.Count; i++)
                {
                    DateTime date = fechasEjeX[i];
                    string dateStr = $"{date.Day:00}/{date.Month:00}/{date.Year}";

                    bool esWeekend = (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday);
                    bool esFestivo = _datos.Festivos.Contains(dateStr);
                    bool esVacacion = info.Vacaciones.Contains(dateStr);

                    var borderCell = new Border
                    {
                        BorderBrush = (SolidColorBrush)FindResource("ColorBorder"),
                        BorderThickness = new Thickness(1),
                        Height = 26
                    };

                    if (esVacacion)
                    {
                        int quotaYear = (info.Imputaciones != null && info.Imputaciones.TryGetValue(dateStr, out int y)) ? y : date.Year;
                        if (quotaYear != date.Year)
                        {
                            borderCell.Background = (SolidColorBrush)FindResource("ColorVacacionOtroAño");
                            borderCell.BorderBrush = (SolidColorBrush)FindResource("ColorVacacionOtroAñoText");
                        }
                        else
                        {
                            borderCell.Background = (SolidColorBrush)FindResource("ColorVacacionBase");
                        }
                    }
                    else if (esWeekend || esFestivo)
                    {
                        borderCell.Background = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                    }
                    else
                    {
                        borderCell.Background = Brushes.White;
                    }

                    Grid.SetRow(borderCell, rIndex);
                    Grid.SetColumn(borderCell, i + 1);
                    GanttTableGrid.Children.Add(borderCell);
                }

                rIndex++;
            }

            // Fila de Cómputos Finales (condicional según configuración)
            if (!_config.OcultarComputoGantt)
            {
                var borderComputoHeader = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                    BorderBrush = (SolidColorBrush)FindResource("ColorBorder"),
                    BorderThickness = new Thickness(1),
                    Child = new TextBlock
                    {
                        Text = "DÍAS NETOS",
                        FontWeight = FontWeights.Bold,
                        FontSize = 10,
                        Foreground = (SolidColorBrush)FindResource("ColorTextMuted"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    Height = 30
                };
                Grid.SetRow(borderComputoHeader, rIndex);
                Grid.SetColumn(borderComputoHeader, 0);
                GanttTableGrid.Children.Add(borderComputoHeader);

                for (int i = 0; i < fechasEjeX.Count; i++)
                {
                    DateTime date = fechasEjeX[i];
                    string dateStr = $"{date.Day:00}/{date.Month:00}/{date.Year}";

                    int sumVacas = _datos.Trabajadores.Values.Count(info => info.Vacaciones.Contains(dateStr));

                    var borderCell = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                        BorderBrush = (SolidColorBrush)FindResource("ColorBorder"),
                        BorderThickness = new Thickness(1),
                        Child = new TextBlock
                        {
                            Text = sumVacas > 0 ? sumVacas.ToString() : "",
                            FontWeight = FontWeights.Bold,
                            FontSize = 9,
                            Foreground = (SolidColorBrush)FindResource("ColorPrimary"),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        Height = 30
                    };
                    Grid.SetRow(borderCell, rIndex);
                    Grid.SetColumn(borderCell, i + 1);
                    GanttTableGrid.Children.Add(borderCell);
                }
            }
        }

        #endregion

        #region Importación e Importador Inteligente Único

        private void BtnImportar_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Archivos Compatibles (*.json;*.csv)|*.json;*.csv|Archivos JSON (*.json)|*.json|Archivos CSV (*.csv)|*.csv",
                Title = "Importar Datos (JSON/CSV)"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string content = File.ReadAllText(openFileDialog.FileName, Encoding.UTF8);
                    bool esJson = openFileDialog.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

                    var res = DataManager.ImportarDesdeTexto(_datos, content, esJson);
                    _datos = res.DatosActualizados;
                    GuardarDatos();
                    ActualizarSelectTrabajadores();
                    ActualizarPanelCupo();
                    ActualizarVistas();
                    if (MenuLabelActiveYear != null)
                    {
                        MenuLabelActiveYear.Text = $"Año de vacaciones: {_datos.Year}";
                    }

                    MostrarEstado($"✅ Importación exitosa ({res.Tipo}): {res.Msg}");
                }
                catch (Exception ex)
                {
                    MostrarEstado($"❌ Error al importar: {ex.Message}");
                }
            }
        }

        #endregion

        #region Exportación de Archivos a JSON/CSV y Gantt CSV

        private void GuardarArchivoExportado(string filename, string content)
        {
            var saveFileDialog = new SaveFileDialog
            {
                FileName = filename,
                Filter = filename.EndsWith(".json") ? "Archivos JSON (*.json)|*.json" : "Archivos CSV (*.csv)|*.csv",
                Title = "Guardar Archivo de Exportación"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(saveFileDialog.FileName, content, Encoding.UTF8);
                    MostrarEstado($"✅ Archivo exportado: {System.IO.Path.GetFileName(saveFileDialog.FileName)}");
                }
                catch (Exception ex)
                {
                    MostrarEstado($"❌ Error al guardar: {ex.Message}");
                }
            }
        }

        private void ExportarTrabajadoresJson_Click(object sender, RoutedEventArgs e)
        {
            string json = DataManager.ExportarTrabajadoresJson(_datos);
            GuardarArchivoExportado($"export_trabajadores_{_datos.Year}.json", json);
        }

        private void ExportarTrabajadoresCsv_Click(object sender, RoutedEventArgs e)
        {
            string csv = DataManager.ExportarTrabajadoresCsv(_datos);
            GuardarArchivoExportado($"export_trabajadores_{_datos.Year}.csv", csv);
        }

        private void ExportarFestivosJson_Click(object sender, RoutedEventArgs e)
        {
            string json = DataManager.ExportarFestivosJson(_datos);
            GuardarArchivoExportado($"export_festivos_{_datos.Year}.json", json);
        }

        private void ExportarFestivosCsv_Click(object sender, RoutedEventArgs e)
        {
            string csv = DataManager.ExportarFestivosCsv(_datos);
            GuardarArchivoExportado($"export_festivos_{_datos.Year}.csv", csv);
        }

        private void ExportarVacacionesJson_Click(object sender, RoutedEventArgs e)
        {
            string json = DataManager.ExportarVacacionesJson(_datos);
            GuardarArchivoExportado($"export_vacaciones_{_datos.Year}.json", json);
        }

        private void ExportarVacacionesCsv_Click(object sender, RoutedEventArgs e)
        {
            string csv = DataManager.ExportarVacacionesCsv(_datos);
            GuardarArchivoExportado($"export_vacaciones_{_datos.Year}.csv", csv);
        }

        private void ExportarCsvGantt_Click(object sender, RoutedEventArgs e)
        {
            var (mesesSecuencia, fechasEjeX) = ObtenerSecuenciaGantt();
            if (fechasEjeX.Count == 0)
            {
                MostrarEstado("⚠️ No hay fechas que exportar.");
                return;
            }
            string csv = DataManager.ExportarGanttACSV(_datos, mesesSecuencia, fechasEjeX);
            GuardarArchivoExportado($"calendario_vacaciones_gantt_{_datos.Year}.csv", csv);
        }

        private void BtnPdfMensual_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                FileName = $"Calendario_Vacaciones_Mensual_{_datos.Year}.pdf",
                Filter = "Archivos PDF (*.pdf)|*.pdf",
                Title = "Exportar PDF Mensual"
            };

            if (saveFileDialog.ShowDialog() != true) return;

            try
            {
                var anos = ObtenerAnosConDatos();
                PdfExportService.ExportarMensual(saveFileDialog.FileName, _datos, _config, anos);
                // Abrir el archivo automáticamente
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = saveFileDialog.FileName,
                    UseShellExecute = true
                });
                MostrarEstado("✅ PDF Mensual exportado correctamente.");
            }
            catch (Exception ex)
            {
                MostrarEstado($"❌ Error al exportar PDF: {ex.Message}");
            }
        }

        private void BtnPdfGantt_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                FileName = $"Calendario_Vacaciones_Tabla_{_datos.Year}.pdf",
                Filter = "Archivos PDF (*.pdf)|*.pdf",
                Title = "Exportar PDF Gantt"
            };

            if (saveFileDialog.ShowDialog() != true) return;

            try
            {
                var anos = ObtenerAnosConDatos();
                PdfExportService.ExportarGantt(saveFileDialog.FileName, _datos, _config, anos);
                // Abrir el archivo automáticamente
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = saveFileDialog.FileName,
                    UseShellExecute = true
                });
                MostrarEstado("✅ PDF Gantt exportado correctamente.");
            }
            catch (Exception ex)
            {
                MostrarEstado($"❌ Error al exportar PDF Gantt: {ex.Message}");
            }
        }

        private void BtnExcelGantt_Click(object sender, RoutedEventArgs e)
        {
            var anos = ObtenerAnosConDatos();
            if (anos.Count == 0)
            {
                MostrarEstado("⚠️ No hay años con datos que exportar.");
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                FileName = $"calendario_vacaciones_gantt_{_datos.Year}.xlsx",
                Filter = "Archivos de Excel (*.xlsx)|*.xlsx",
                Title = "Exportar Gantt a Excel"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    ExcelExportService.Exportar(saveFileDialog.FileName, _datos, _config, anos);
                    // Abrir el archivo automáticamente
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = saveFileDialog.FileName,
                        UseShellExecute = true
                    });
                    MostrarEstado("✅ Excel Gantt exportado correctamente.");
                }
                catch (Exception ex)
                {
                    MostrarEstado($"❌ Error al exportar a Excel: {ex.Message}");
                }
            }
        }

        #endregion

        #region Eventos de Control Superior

        private void BtnPrevYear_Click(object sender, RoutedEventArgs e)
        {
            _visualizedYear--;
            LabelYear.Text = _visualizedYear.ToString();
            ActualizarVistas();
        }

        private void BtnNextYear_Click(object sender, RoutedEventArgs e)
        {
            _visualizedYear++;
            LabelYear.Text = _visualizedYear.ToString();
            ActualizarVistas();
        }

        private void PageTitleInput_LostFocus(object sender, RoutedEventArgs e)
        {
            GuardarDatos();
        }

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
                        if (trabajador.Imputaciones != null)
                        {
                            trabajador.Imputaciones.Clear();
                        }
                    }
                }

                if (dialog.LimpiarFestivos)
                {
                    _datos.Festivos.Clear();
                }

                GuardarDatos();
                ActualizarSelectTrabajadores();
                ActualizarPanelCupo();
                ActualizarVistas();

                MostrarEstado("✅ Datos limpiados correctamente según tu selección.");
            }
        }

        private void BtnAyuda_Click(object sender, RoutedEventArgs e)
        {
            string ayuda = "📖 Guía de Uso - Gestor de Vacaciones Pro (WPF)\n\n" +
                           "1. Gestión de Personal y Festivos:\n" +
                           "Accede a 'Archivo -> Configuración' para añadir/eliminar trabajadores y festivos. Permite multiselección y modificación rápida de días por lotes.\n\n" +
                           "2. Toolbar Horizontal del Trabajador:\n" +
                           "Situada debajo del menú superior, te permite:\n" +
                           "- Seleccionar el trabajador activo.\n" +
                           "- Incrementar/decrementar rápidamente sus días base y extras con los botones [◀] y [▶].\n" +
                           "- Visualizar en tiempo real el consumo de días de vacaciones y el saldo restante mediante la barra de progreso.\n\n" +
                           "3. Modos de Edición:\n" +
                           "Cambia el modo de interacción en 'Archivo -> Modo de Edición':\n" +
                           "- Marcar Vacaciones: Asigna vacaciones al trabajador activo pulsando o arrastrando sobre el calendario.\n" +
                           "- Marcar Festivos Oficiales: Convierte los días clicados directamente en festivos oficiales libres de cupo.\n\n" +
                           "4. Click y Arrastre Continuo:\n" +
                           "Haz clic en un día del calendario y arrastra para marcar o desmarcar de forma continua un rango de fechas.\n\n" +
                           "5. Exportación Consolidada Multiaño:\n" +
                           "Usa el menú 'Exportar' para generar reportes en Excel (.xlsx) y PDF de todos los años que contengan datos. El Excel separará los años en pestañas y el PDF paginará consecutivamente cada año.";

            MessageBox.Show(ayuda, "Guía de Uso", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnVerLogs_Click(object sender, RoutedEventArgs e)
        {
            var logWin = new Window
            {
                Title = "Registro de Logs de la Aplicación",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                FontFamily = new FontFamily("Segoe UI, Inter"),
                Icon = this.Icon
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lblHeader = new TextBlock
            {
                Text = "Historial de mensajes y estados cargados en la sesión actual:",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Margin = new Thickness(12, 12, 12, 6)
            };
            Grid.SetRow(lblHeader, 0);
            grid.Children.Add(lblHeader);

            var txtLogs = new TextBox
            {
                Text = string.Join(Environment.NewLine, _logMessages),
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 12,
                Background = Brushes.White,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                Padding = new Thickness(8),
                Margin = new Thickness(12, 6, 12, 12)
            };
            Grid.SetRow(txtLogs, 1);
            grid.Children.Add(txtLogs);

            var panelButtons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(12, 0, 12, 12)
            };

            var btnCopy = new Button
            {
                Content = "📋 Copiar todo",
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            btnCopy.Click += (s2, e2) =>
            {
                try
                {
                    Clipboard.SetText(txtLogs.Text);
                    MessageBox.Show("Logs copiados al portapapeles con éxito.", "Logs Copiados", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al copiar logs al portapapeles: {ex.Message}", "Error de Copiado", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            var btnClose = new Button
            {
                Content = "Cerrar",
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = Cursors.Hand
            };
            btnClose.Click += (s2, e2) => logWin.Close();

            panelButtons.Children.Add(btnCopy);
            panelButtons.Children.Add(btnClose);
            Grid.SetRow(panelButtons, 2);
            grid.Children.Add(panelButtons);

            logWin.Content = grid;
            logWin.ShowDialog();
        }

        private void MenuConfiguracion_Click(object sender, RoutedEventArgs e)
        {
            var configWindow = new ConfigurationWindow(_datos, _config);
            configWindow.Owner = this;
            configWindow.ShowDialog();

            if (configWindow.Aceptado)
            {
                // Guardar datos y configuración
                GuardarDatos();
                AppConfigManager.Guardar(_config);

                // Recargar la interfaz completa
                ActualizarSelectTrabajadores();
                ActualizarPanelCupo();
                ActualizarVistas();
                if (MenuLabelActiveYear != null)
                {
                    MenuLabelActiveYear.Text = $"Año de vacaciones: {_datos.Year}";
                }

                MostrarEstado("✅ Configuración aplicada correctamente.");
            }
        }

        private void MenuSalir_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        #endregion
    }
}