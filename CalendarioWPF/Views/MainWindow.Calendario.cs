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
        }

        /// <summary>
        /// Repuebla el panel lateral con el resumen textual de vacaciones de cada trabajador
        /// para el año de cupo activo.
        /// </summary>
        private void ActualizarPanelVacacionesTexto()
        {
            if (PanelVacacionesTexto == null) return;

            PanelVacacionesTexto.Children.Clear();
            var sortedWorkers = _datos.Trabajadores.Keys.OrderBy(n => n).ToList();

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
                string rangos = RangoVacacionesHelper.AgruparVacacionesEnTexto(info.Vacaciones, info.Imputaciones, _datos.Festivos, _datos.Year);

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

            // Buscar trabajadores con vacaciones en este día
            var trabsVac = _datos.Trabajadores
                .Where(kvp => kvp.Value.Vacaciones.Contains(dateStr))
                .Select(kvp => kvp.Key)
                .ToList();

            if (trabsVac.Count > 0)
            {
                bool esVacacionActivo = _editMode == "vacaciones" && trabsVac.Contains(_activeWorker);
                if (esVacacionActivo)
                {
                    // Prioridad: mostrar el color según el cupo del trabajador activo
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
                    // Si todos los trabajadores tienen ese día imputado a otro cupo → lavanda; si hay mezcla → indigo
                    bool todosOtroAno = trabsVac.All(t =>
                    {
                        if (_datos.Trabajadores.TryGetValue(t, out var tInfo))
                        {
                            int qYear = (tInfo.Imputaciones != null && tInfo.Imputaciones.TryGetValue(dateStr, out int y)) ? y : _visualizedYear;
                            return qYear != _visualizedYear;
                        }
                        return false;
                    });

                    if (todosOtroAno)
                    {
                        cellBorder.Background = (SolidColorBrush)FindResource("ColorVacacionOtroAño");
                        txtNum.Foreground = (SolidColorBrush)FindResource("ColorVacacionOtroAñoText");
                    }
                    else
                    {
                        // Mixto: hay trabajadores del cupo actual y de otro cupo → Indigo claro
                        cellBorder.Background = new SolidColorBrush(Color.FromRgb(199, 210, 254));
                        txtNum.Foreground = new SolidColorBrush(Color.FromRgb(55, 48, 163));
                    }
                }
                txtNum.FontWeight = FontWeights.Bold;

                // Añadir chips de iniciales (máximo 2 + contador de exceso)
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

                // Tooltip con la lista de trabajadores
                var sb = new StringBuilder();
                sb.AppendLine($"Vacaciones ({dateStr}):");
                foreach (var t in trabsVac) sb.AppendLine($"• {t}");
                cellBorder.ToolTip = sb.ToString().Trim();
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

        /// <summary>
        /// Finaliza el arrastre al soltar el ratón, guarda los cambios y refresca la vista.
        /// </summary>
        private void CellBorder_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                (sender as Border)?.ReleaseMouseCapture();
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
                if (accion == "deselect")
                {
                    _datos.Festivos.Remove(dateStr);
                }
                else
                {
                    // Al marcar festivo, eliminar de las vacaciones de todos los trabajadores
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
                if (string.IsNullOrEmpty(_activeWorker) || !_datos.Trabajadores.ContainsKey(_activeWorker)) return;

                var info = _datos.Trabajadores[_activeWorker];
                info.Imputaciones ??= new System.Collections.Generic.Dictionary<string, int>();

                if (accion == "deselect")
                {
                    info.Vacaciones.Remove(dateStr);
                    info.Imputaciones.Remove(dateStr);
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
                    }
                }
            }

            // Refrescar visualmente durante el arrastre para feedback inmediato
            RenderCalendar();
        }

        #endregion
    }
}
