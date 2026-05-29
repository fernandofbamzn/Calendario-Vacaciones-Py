using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CalendarioWPF.Services;

namespace CalendarioWPF
{
    /// <summary>
    /// Clase parcial de MainWindow: renderizado del calendario mensual interactivo,
    /// panel de texto de vacaciones y lógica de arrastre (drag-to-select).
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Renderizado General

        /// <summary>
        /// Refresca ambas áreas de la pestaña Vista Calendario: el grid de meses y el panel textual lateral.
        /// </summary>
        private void ActualizarVistas()
        {
            ActualizarPanelVacacionesTexto();
            RenderCalendar();
            
            if (MainTabControl != null && MainTabControl.SelectedIndex == 1)
            {
                RenderGantt();
            }
        }

        /// <summary>
        /// Repuebla el panel lateral con el resumen textual de vacaciones de cada trabajador
        /// para el año de cupo activo.
        /// </summary>
        private void ActualizarPanelVacacionesTexto()
        {
            if (PanelVacacionesTexto == null) return;

            PanelVacacionesTexto.Children.Clear();
            var sortedWorkers = _datos.Trabajadores
                .Where(kvp => string.IsNullOrEmpty(_filtroDpto) || kvp.Value.Departamento == _filtroDpto)
                .Select(kvp => kvp.Key)
                .OrderBy(n => n)
                .ToList();

            if (sortedWorkers.Count == 0)
            {
                PanelVacacionesTexto.Children.Add(new TextBlock
                {
                    Text = "No hay personal registrado en el sistema.",
                    FontStyle = FontStyles.Italic,
                    Foreground = (SolidColorBrush)FindResource("ColorTextMuted"),
                    FontSize = 13
                });
                return;
            }

            foreach (var w in sortedWorkers)
            {
                var info = _datos.Trabajadores[w];
                var vPropias = new List<string>();
                var vCierres = new List<string>();
                string wDept = info.Departamento ?? "General";
                
                foreach (var v in info.Vacaciones)
                {
                    bool isClosure = _datos.Cierres != null && (
                        (_datos.Cierres.ContainsKey(wDept) && _datos.Cierres[wDept].Contains(v)) ||
                        (_datos.Cierres.ContainsKey("__todos__") && _datos.Cierres["__todos__"].Contains(v))
                    );
                    if (isClosure) vCierres.Add(v);
                    else vPropias.Add(v);
                }

                var festivosTrabajador = RangoVacacionesHelper.ObtenerFestivosTrabajador(w, _datos);
                string rangos = "";
                if (vPropias.Count > 0)
                {
                    rangos += "Libres: " + RangoVacacionesHelper.AgruparVacacionesEnTexto(vPropias, info.Imputaciones, festivosTrabajador, _datos.Year);
                }
                
                if (vCierres.Count > 0)
                {
                    if (rangos.Length > 0) rangos += "\n";
                    rangos += "🔒 Cierres: " + RangoVacacionesHelper.AgruparVacacionesEnTexto(vCierres, info.Imputaciones, festivosTrabajador, _datos.Year);
                }
                
                if (string.IsNullOrEmpty(rangos)) rangos = "Ninguna";

                var conflictosWorker = new List<string>();
                foreach (var vac in info.Vacaciones)
                {
                    if (RangoVacacionesHelper.EsIncompatible(w, vac, _datos))
                        conflictosWorker.Add(vac.Substring(0, 5));
                }
                if (conflictosWorker.Count > 0)
                {
                    rangos += $"\n⚠️ Incompatibilidades: {string.Join(", ", conflictosWorker)}";
                }

                SolidColorBrush borderBrush = (SolidColorBrush)FindResource("ColorPrimary");
                if (!string.IsNullOrEmpty(info.Departamento) && _datos.DepartamentosColores != null && _datos.DepartamentosColores.TryGetValue(info.Departamento, out string hexColor))
                {
                    try
                    {                        
                        borderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
                    }
                    catch { }
                }

                var borderItem = new Border
                {
                    Background = (SolidColorBrush)FindResource("ColorBgApp"),
                    BorderBrush = borderBrush,
                    BorderThickness = new Thickness(4, 0, 0, 0),
                    CornerRadius = new CornerRadius(0, 8, 8, 0),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 0, 0, 8)
                };

                var sp = new StackPanel();
                sp.Children.Add(new TextBlock
                {
                    Text = w,
                    FontWeight = FontWeights.Bold,
                    Foreground = (SolidColorBrush)FindResource("ColorTextMain"),
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 0, 2)
                });
                sp.Children.Add(new TextBlock
                {
                    Text = rangos,
                    FontStyle = FontStyles.Italic,
                    Foreground = (SolidColorBrush)FindResource("ColorTextMuted"),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap
                });

                borderItem.Child = sp;
                PanelVacacionesTexto.Children.Add(borderItem);
            }
        }

        #endregion

        #region Renderizado del Calendario Mensual

        /// <summary>
        /// Genera dinámicamente el grid de meses del calendario según la configuración activa.
        /// Para cada mes de <c>_config.MesesAMostrar</c> crea la rejilla de celdas de días
        /// con su color, evento de ratón y chips de trabajadores.
        /// </summary>
        private void RenderCalendar()
        {
            if (MonthsGrid == null) return;

            MonthsGrid.Children.Clear();
            var meses = _config.MesesAMostrar.OrderBy(m => m).ToList();
            if (meses.Count == 0) meses = new List<int> { 6, 7, 8, 9 };

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

                // Nombre del mes
                monthPanel.Children.Add(new TextBlock
                {
                    Text = $"{nombresMeses[mes]} {_visualizedYear}",
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Foreground = (FindResource("ColorPrimary") as SolidColorBrush) ?? Brushes.Indigo,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                });

                // Cabeceras de días de la semana
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

        /// <summary>
        /// Construye el elemento visual Border de una celda de día del calendario,
        /// aplicando el color adecuado según su estado (fin de semana, festivo, vacaciones del cupo
        /// activo, vacaciones de otro cupo o mixtas) y añadiendo chips de iniciales de trabajadores.
        /// </summary>
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

            var gridCell = new Grid();
            gridCell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.2, GridUnitType.Star) });
            gridCell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });

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

            // Aplicar color de fondo según estado del día
            bool esFestivo = _datos.Festivos.Contains(dateStr) || 
                             (!string.IsNullOrEmpty(_filtroDpto) && 
                              _datos.FestivosDepartamento != null && 
                              _datos.FestivosDepartamento.ContainsKey(_filtroDpto) && 
                              _datos.FestivosDepartamento[_filtroDpto].Contains(dateStr));

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

            // Buscar trabajadores con vacaciones en este día (filtrados por dpto si aplica)
            var trabsVac = _datos.Trabajadores
                .Where(kvp => kvp.Value.Vacaciones.Contains(dateStr) && (string.IsNullOrEmpty(_filtroDpto) || kvp.Value.Departamento == _filtroDpto))
                .Select(kvp => kvp.Key)
                .ToList();

            if (trabsVac.Count > 0)
            {
                bool esVacacionActivo = _editMode == "vacaciones" && trabsVac.Contains(_activeWorker);
                if (esVacacionActivo)
                {
                    var infoActivo = _datos.Trabajadores[_activeWorker];
                    int quotaYear = (infoActivo.Imputaciones != null && infoActivo.Imputaciones.TryGetValue(dateStr, out int y)) ? y : _visualizedYear;
                    
                    string dpto = infoActivo.Departamento ?? "General";
                    string hexColor = (_datos.DepartamentosColores != null && _datos.DepartamentosColores.TryGetValue(dpto, out var c)) ? c : "#C7D2FE";
                    Color baseColor = (Color)ColorConverter.ConvertFromString(hexColor);
                    
                    bool esCierreActivo = _datos.Cierres != null && (
                        (_datos.Cierres.ContainsKey(dpto) && _datos.Cierres[dpto].Contains(dateStr)) ||
                        (_datos.Cierres.ContainsKey("__todos__") && _datos.Cierres["__todos__"].Contains(dateStr))
                    );
                    
                    if (quotaYear != _visualizedYear)
                    {
                        cellBorder.Background = new SolidColorBrush(Color.FromArgb(120, baseColor.R, baseColor.G, baseColor.B));
                        txtNum.Foreground = Brushes.Gray;
                    }
                    else if (esCierreActivo)
                    {
                        // Cierre patronal: tono más oscuro del departamento
                        cellBorder.Background = new SolidColorBrush(Color.FromRgb(
                            (byte)Math.Max(0, baseColor.R - 40),
                            (byte)Math.Max(0, baseColor.G - 40),
                            (byte)Math.Max(0, baseColor.B - 40)));
                        txtNum.Foreground = Brushes.White;
                    }
                    else
                    {
                        cellBorder.Background = new SolidColorBrush(baseColor);
                        txtNum.Foreground = Brushes.White;
                    }
                    
                    if (esCierreActivo)
                    {
                        txtNum.Text = "🔒" + txtNum.Text;
                    }
                }
                else
                {
                    bool todosOtroAno = trabsVac.All(t =>
                    {
                        if (_datos.Trabajadores.TryGetValue(t, out var tInfo))
                        {
                            int qYear = (tInfo.Imputaciones != null && tInfo.Imputaciones.TryGetValue(dateStr, out int y)) ? y : _visualizedYear;
                            return qYear != _visualizedYear;
                        }
                        return false;
                    });

                    string dpto = _datos.Trabajadores[trabsVac[0]].Departamento ?? "General";
                    string hexColor = (_datos.DepartamentosColores != null && _datos.DepartamentosColores.TryGetValue(dpto, out var c)) ? c : "#C7D2FE"; // Default indigo-200
                    Color baseColor = (Color)ColorConverter.ConvertFromString(hexColor);

                    bool esCierrePatronal = _datos.Cierres != null && (
                        (_datos.Cierres.ContainsKey(dpto) && _datos.Cierres[dpto].Contains(dateStr)) || 
                        (_datos.Cierres.ContainsKey("__todos__") && _datos.Cierres["__todos__"].Contains(dateStr))
                    );

                    if (todosOtroAno)
                    {
                        cellBorder.Background = new SolidColorBrush(Color.FromArgb(120, baseColor.R, baseColor.G, baseColor.B));
                        txtNum.Foreground = Brushes.Gray;
                    }
                    else if (esCierrePatronal)
                    {
                        cellBorder.Background = new SolidColorBrush(Color.FromRgb(
                            (byte)Math.Max(0, baseColor.R - 40),
                            (byte)Math.Max(0, baseColor.G - 40),
                            (byte)Math.Max(0, baseColor.B - 40)));
                        txtNum.Foreground = Brushes.White;
                    }
                    else
                    {
                        cellBorder.Background = new SolidColorBrush(baseColor);
                        txtNum.Foreground = Brushes.White;
                    }
                    
                    if (esCierrePatronal)
                    {
                        txtNum.Text = "🔒" + txtNum.Text;
                    }
                }
                txtNum.FontWeight = FontWeights.Bold;

                // Comprobar Incompatibilidad
                bool hayIncompatibilidad = trabsVac.Any(t => RangoVacacionesHelper.EsIncompatible(t, dateStr, _datos));
                if (hayIncompatibilidad)
                {
                    txtNum.Text = "!" + txtNum.Text;
                    txtNum.Foreground = Brushes.Red;
                }

                // Añadir chips de iniciales
                int maxChips = 2;
                for (int i = 0; i < Math.Min(maxChips, trabsVac.Count); i++)
                {
                    chipsStack.Children.Add(CrearChipIniciales(trabsVac[i], dateStr));
                }

                if (trabsVac.Count > maxChips)
                {
                    var moreChip = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(91, 44, 111)),
                        CornerRadius = new CornerRadius(2),
                        Padding = new Thickness(2, 0, 2, 0),
                        Margin = new Thickness(1, 0, 1, 0)
                    };
                    moreChip.Child = new TextBlock
                    {
                        Text = $"+{trabsVac.Count - maxChips}",
                        FontSize = 7.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White
                    };
                    chipsStack.Children.Add(moreChip);
                }

                var incompDict = new Dictionary<string, List<string>>();
                foreach (var t in trabsVac)
                {
                    if (RangoVacacionesHelper.EsIncompatible(t, dateStr, _datos))
                    {
                        var conflictosDeT = new List<string>();
                        var infoT = _datos.Trabajadores[t];
                        bool dptoIncomp = _datos.DepartamentosIncompatibles != null && _datos.DepartamentosIncompatibles.Contains(infoT.Departamento);

                        foreach (var kvp in _datos.Trabajadores)
                        {
                            if (kvp.Key == t) continue;
                            if (!kvp.Value.Vacaciones.Contains(dateStr)) continue;

                            bool choca = false;
                            if (_datos.Incompatibilidades != null && _datos.Incompatibilidades.TryGetValue(t, out var list) && list.Contains(kvp.Key))
                                choca = true;
                            else if (dptoIncomp && kvp.Value.Departamento == infoT.Departamento)
                                choca = true;

                            if (choca)
                            {
                                string dept = string.IsNullOrEmpty(infoT.Departamento) ? "General" : infoT.Departamento;
                                string otherDept = string.IsNullOrEmpty(kvp.Value.Departamento) ? "General" : kvp.Value.Departamento;
                                bool isClosure = _datos.Cierres != null && (
                                    (_datos.Cierres.ContainsKey("__todos__") && _datos.Cierres["__todos__"].Contains(dateStr)) ||
                                    (_datos.Cierres.ContainsKey(dept) && _datos.Cierres[dept].Contains(dateStr)) ||
                                    (_datos.Cierres.ContainsKey(otherDept) && _datos.Cierres[otherDept].Contains(dateStr))
                                );
                                if (!isClosure) conflictosDeT.Add(kvp.Key);
                            }
                        }
                        incompDict[t] = conflictosDeT;
                    }
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Vacaciones ({dateStr}):");
                foreach (var t in trabsVac)
                {
                    if (incompDict.ContainsKey(t) && incompDict[t].Count > 0)
                        sb.AppendLine($"• ! {t} (Choca con: {string.Join(", ", incompDict[t])})");
                    else if (incompDict.ContainsKey(t))
                        sb.AppendLine($"• ! {t}");
                    else
                        sb.AppendLine($"• {t}");
                }
                cellBorder.ToolTip = sb.ToString().Trim();
            }
            else if (!esFinSemana && !esFestivo)
            {
                // Revisar si hay cierre de empresa que mostrar
                var closures = _datos.Cierres?.Where(c => c.Value.Contains(dateStr)).Select(c => c.Key).ToList();
                if (closures != null && closures.Count > 0)
                {
                    bool showClosure = string.IsNullOrEmpty(_filtroDpto) || closures.Contains(_filtroDpto) || closures.Contains("__todos__");
                    if (showClosure)
                    {
                        string dpto = (closures.Contains(_filtroDpto) ? _filtroDpto : (closures.Contains("__todos__") ? "__todos__" : closures[0]));
                        string hex = (_datos.DepartamentosColores != null && _datos.DepartamentosColores.TryGetValue(dpto, out var c)) ? c : null;
                        cellBorder.Background = hex != null ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)) : (SolidColorBrush)FindResource("ColorVacacionBase");
                        cellBorder.Opacity = 0.5;
                        cellBorder.ToolTip = $"Cierre patronal: {(dpto == "__todos__" ? "General" : dpto)}";
                        txtNum.Text = "🔒" + txtNum.Text;
                        txtNum.Foreground = Brushes.DarkSlateGray;
                        txtNum.FontWeight = FontWeights.Bold;
                    }
                }
            }

            // Eventos de interacción
            cellBorder.PreviewMouseLeftButtonDown += CellBorder_MouseDown;
            cellBorder.MouseEnter += CellBorder_MouseEnter;
            cellBorder.PreviewMouseLeftButtonUp += CellBorder_MouseUp;

            return cellBorder;
        }

        /// <summary>
        /// Crea un chip visual con las iniciales del trabajador (y el sufijo de año si es de otro cupo).
        /// </summary>
        private Border CrearChipIniciales(string nombre, string dateStr)
        {
            string iniciales = "";
            var partes = nombre.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length >= 2)
                iniciales = (partes[0][0].ToString() + partes[1][0].ToString()).ToUpper();
            else if (partes.Length == 1)
                iniciales = partes[0].Substring(0, Math.Min(2, partes[0].Length)).ToUpper();

            if (_datos.Trabajadores.TryGetValue(nombre, out var workerInfo))
            {
                int quotaYear = (workerInfo.Imputaciones != null && workerInfo.Imputaciones.TryGetValue(dateStr, out int y)) ? y : _visualizedYear;
                if (quotaYear != _visualizedYear)
                    iniciales = $"{iniciales}-{quotaYear}";
            }

            var borderChip = new Border
            {
                Background = (SolidColorBrush)FindResource("ColorPrimary"),
                CornerRadius = new CornerRadius(2.5),
                Padding = new Thickness(2, 0, 2, 0),
                Margin = new Thickness(0.5, 0, 0.5, 0)
            };
            borderChip.Child = new TextBlock
            {
                Text = iniciales,
                FontSize = 7.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            return borderChip;
        }

        #endregion

        #region Drag-to-Select (Arrastre para selección de rangos)

        /// <summary>
        /// Inicia el arrastre al pulsar el botón izquierdo del ratón sobre una celda.
        /// Determina si la acción será de "select" (marcar) o "deselect" (desmarcar).
        /// </summary>
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
            _diasAsignadosEnDrag.Clear();
            _dragSelectionType = _editMode;
            cell.CaptureMouse();

            bool estaSeleccionado = _editMode == "festivos"
                ? _datos.Festivos.Contains(dateStr)
                : _datos.Trabajadores[_activeWorker].Vacaciones.Contains(dateStr);

            _dragAction = estaSeleccionado ? "deselect" : "select";
            ProcesarDia(dateStr, _dragAction);
            e.Handled = true;
        }

        /// <summary>
        /// Extiende la selección o deselección al arrastar el ratón sobre celdas adyacentes.
        /// </summary>
        private void CellBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!_isDragging || _dragSelectionType != _editMode || e.LeftButton != MouseButtonState.Pressed) return;

            var cell = sender as Border;
            if (cell == null || cell.Tag == null) return;
            ProcesarDia(cell.Tag.ToString() ?? "", _dragAction);
        }

        private List<string> _diasAsignadosEnDrag = new List<string>();

        private void CellBorder_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                (sender as Border)?.ReleaseMouseCapture();

                // Avisar de incompatibilidades
                if (_dragSelectionType == "vacaciones" && _dragAction == "select" && !string.IsNullOrEmpty(_activeWorker) && _diasAsignadosEnDrag.Count > 0)
                {
                    var conflictosMsg = new List<string>();
                    
                    var incompDirectos = (_datos.Incompatibilidades != null && _datos.Incompatibilidades.TryGetValue(_activeWorker, out var list)) 
                        ? list : new List<string>();
                    
                    var dptoInfo = _datos.Trabajadores.TryGetValue(_activeWorker, out var info) ? info.Departamento : "General";
                    bool dptoIncomp = _datos.DepartamentosIncompatibles != null && _datos.DepartamentosIncompatibles.Contains(dptoInfo);

                    foreach (var fecha in _diasAsignadosEnDrag)
                    {
                        // Ignorar si la fecha es un cierre de empresa
                        bool esCierre = _datos.Cierres != null && (
                            (_datos.Cierres.ContainsKey("__todos__") && _datos.Cierres["__todos__"].Contains(fecha)) ||
                            (_datos.Cierres.ContainsKey(dptoInfo) && _datos.Cierres[dptoInfo].Contains(fecha))
                        );
                        
                        if (esCierre) continue;

                        var coincidentes = new List<string>();
                        foreach (var kvp in _datos.Trabajadores)
                        {
                            if (kvp.Key == _activeWorker) continue;
                            if (!kvp.Value.Vacaciones.Contains(fecha)) continue;

                            if (incompDirectos.Contains(kvp.Key) || (dptoIncomp && kvp.Value.Departamento == dptoInfo))
                            {
                                coincidentes.Add(kvp.Key);
                            }
                        }

                        if (coincidentes.Count > 0)
                        {
                            conflictosMsg.Add($"- Día {fecha}: coincide con {string.Join(", ", coincidentes)}");
                        }
                    }

                    if (conflictosMsg.Count > 0)
                    {
                        System.Windows.MessageBox.Show($"¡Atención! Has asignado vacaciones a {_activeWorker} que entran en conflicto por incompatibilidad:\n\n{string.Join("\n", conflictosMsg)}", "Incompatibilidad detectada", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    }
                }

                GuardarDatos();
                ActualizarPanelCupo();
                ActualizarVistas();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Procesa la acción de marcar o desmarcar un día concreto según el modo de edición activo.
        /// Cuando se marca un día como festivo, se elimina automáticamente de las vacaciones de todos los trabajadores.
        /// Cuando se marca como vacación, se registra la imputación al año de cupo activo (<c>_datos.Year</c>).
        /// </summary>
        private void ProcesarDia(string dateStr, string accion)
        {
            System.Globalization.CultureInfo.InvariantCulture.GetType(); // touch to avoid warning
            DateTime.TryParseExact(dateStr, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date);
            bool esWeekend = (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday);
            bool esLaborable = !esWeekend && !_datos.Festivos.Contains(dateStr);

            if (_editMode == "festivos")
            {
                if (string.IsNullOrEmpty(_filtroDpto))
                {
                    // Festivo Global
                    if (accion == "deselect")
                    {
                        _datos.Festivos.Remove(dateStr);
                    }
                    else
                    {
                        // Al marcar festivo global, eliminar de las vacaciones de todos los trabajadores
                        foreach (var kvp in _datos.Trabajadores)
                        {
                            kvp.Value.Vacaciones.Remove(dateStr);
                            kvp.Value.Imputaciones?.Remove(dateStr);
                        }
                        if (!_datos.Festivos.Contains(dateStr))
                            _datos.Festivos.Add(dateStr);
                    }
                }
                else
                {
                    // Festivo de Departamento
                    if (_datos.FestivosDepartamento == null) _datos.FestivosDepartamento = new Dictionary<string, List<string>>();
                    if (!_datos.FestivosDepartamento.ContainsKey(_filtroDpto)) _datos.FestivosDepartamento[_filtroDpto] = new List<string>();

                    if (accion == "deselect")
                    {
                        _datos.FestivosDepartamento[_filtroDpto].Remove(dateStr);
                    }
                    else
                    {
                        // Eliminar vacaciones solo de los trabajadores de ese departamento
                        foreach (var kvp in _datos.Trabajadores)
                        {
                            if (kvp.Value.Departamento == _filtroDpto)
                            {
                                kvp.Value.Vacaciones.Remove(dateStr);
                                kvp.Value.Imputaciones?.Remove(dateStr);
                            }
                        }
                        if (!_datos.FestivosDepartamento[_filtroDpto].Contains(dateStr))
                            _datos.FestivosDepartamento[_filtroDpto].Add(dateStr);
                    }
                }
            }
            else
            {
                if (string.IsNullOrEmpty(_activeWorker) || !_datos.Trabajadores.ContainsKey(_activeWorker)) return;

                var info = _datos.Trabajadores[_activeWorker];
                info.Imputaciones ??= new System.Collections.Generic.Dictionary<string, int>();

                if (accion == "deselect")
                {
                    info.Vacaciones.Remove(dateStr);
                    info.Imputaciones.Remove(dateStr);
                    _diasAsignadosEnDrag.Remove(dateStr);
                }
                else
                {
                    // Quitar de festivos si se marca como vacación
                    _datos.Festivos.Remove(dateStr);

                    if (!info.Vacaciones.Contains(dateStr))
                    {
                        info.Vacaciones.Add(dateStr);
                        // Imputar al año de cupo activo
                        info.Imputaciones[dateStr] = _datos.Year;
                        if (!_diasAsignadosEnDrag.Contains(dateStr)) _diasAsignadosEnDrag.Add(dateStr);
                    }
                }
            }

            // Refrescar visualmente durante el arrastre para feedback inmediato
            RenderCalendar();
        }

        #endregion
    }
}
