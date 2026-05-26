using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CalendarioWPF
{
    /// <summary>
    /// Clase parcial de MainWindow: renderizado de la Vista Tabla Gantt.
    /// Contiene la generación dinámica del grid Gantt en código, el cálculo del eje temporal
    /// y la obtención de los años de cupo con datos registrados.
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Renderizado de Tabla Gantt

        /// <summary>
        /// Evento disparado cuando el usuario selecciona la pestaña Vista Gantt.
        /// Regenera la tabla para asegurar que refleja el estado actual de los datos.
        /// </summary>
        private void TabGantt_Selected(object sender, RoutedEventArgs e)
        {
            RenderGantt();
        }

        /// <summary>
        /// Calcula el conjunto de años de cupo que tienen datos en el plan actual.
        /// Incluye el año de cupo activo, los años naturales de los festivos y los años de cupo
        /// de las imputaciones de vacaciones de todos los trabajadores.
        /// </summary>
        /// <returns>Lista ordenada de años de cupo con datos.</returns>
        private List<int> ObtenerAnosConDatos()
        {
            var anos = new HashSet<int> { _datos.Year };

            foreach (var festivo in _datos.Festivos)
            {
                if (DateTime.TryParseExact(festivo, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d))
                    anos.Add(d.Year);
            }

            foreach (var worker in _datos.Trabajadores.Values)
            {
                foreach (var vac in worker.Vacaciones)
                {
                    if (DateTime.TryParseExact(vac, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d))
                    {
                        int qYear = (worker.Imputaciones != null && worker.Imputaciones.TryGetValue(vac, out int yVal)) ? yVal : d.Year;
                        anos.Add(qYear);
                    }
                }
            }

            return anos.OrderBy(y => y).ToList();
        }

        /// <summary>
        /// Calcula el eje temporal de la tabla Gantt para el año visualizado: obtiene el rango de meses
        /// que abarca desde la primera a la última vacación, y genera la secuencia completa de días.
        /// Si no hay vacaciones, usa Junio-Septiembre como rango por defecto.
        /// </summary>
        /// <returns>
        /// Tupla con la lista de meses en formato "AAAA-M" y la lista completa de fechas del eje X.
        /// </returns>
        private (List<string> mesesSecuencia, List<DateTime> fechasEjeX) ObtenerSecuenciaGantt()
        {
            // Filtramos por año de CUPO (imputación), no por año natural de la fecha.
            // Esto corrige el bug que mostraba Gantt vacío cuando todas las vacaciones del año
            // visualizado eran imputaciones de otro año natural al mismo cupo.
            var todasFechas = _datos.Trabajadores.Values
                .SelectMany(info => info.Vacaciones.Select(fStr => {
                    if (!DateTime.TryParseExact(fStr, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d))
                        return (Valid: false, Date: DateTime.MinValue);
                    int quotaYear = (info.Imputaciones != null && info.Imputaciones.TryGetValue(fStr, out int y)) ? y : d.Year;
                    return (Valid: quotaYear == _visualizedYear, Date: d);
                }))
                .Where(t => t.Valid)
                .Select(t => t.Date)
                .ToList();

            DateTime minDate = todasFechas.Count > 0 ? todasFechas.Min() : new DateTime(_visualizedYear, 6, 1);
            DateTime maxDate = todasFechas.Count > 0 ? todasFechas.Max() : new DateTime(_visualizedYear, 9, 30);

            var mesesRango = new List<string>();
            DateTime current = new DateTime(minDate.Year, minDate.Month, 1);
            DateTime limit = new DateTime(maxDate.Year, maxDate.Month, 1);

            while (current <= limit)
            {
                mesesRango.Add($"{current.Year}-{current.Month}");
                current = current.AddMonths(1);
            }

            var fechasEjeX = mesesRango.SelectMany(mStr =>
            {
                var parts = mStr.Split('-');
                int y = int.Parse(parts[0]);
                int m = int.Parse(parts[1]);
                return Enumerable.Range(1, DateTime.DaysInMonth(y, m)).Select(d => new DateTime(y, m, d));
            }).ToList();

            return (mesesRango, fechasEjeX);
        }

        /// <summary>
        /// Construye la tabla Gantt visualmente en el <c>GanttTableGrid</c> para el año visualizado.
        /// La tabla tiene una columna de nombres, columnas por día y filas para: cabecera de meses,
        /// cabecera de días, una fila por trabajador, y opcionalmente una fila de totales netos.
        /// </summary>
        private void RenderGantt()
        {
            if (GanttTableGrid == null) return;

            GanttTableGrid.Children.Clear();
            GanttTableGrid.RowDefinitions.Clear();
            GanttTableGrid.ColumnDefinitions.Clear();

            var (mesesSecuencia, fechasEjeX) = ObtenerSecuenciaGantt();
            if (fechasEjeX.Count == 0) return;

            // Columna de nombres + una columna por día
            GanttTableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            for (int i = 0; i < fechasEjeX.Count; i++)
                GanttTableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });

            // Filas: meses (0), días (1), trabajadores (2..N), totales (N+1 opcional)
            int totalFilas = 2 + _datos.Trabajadores.Count + (_config.OcultarComputoGantt ? 0 : 1);
            for (int i = 0; i < totalFilas; i++)
                GanttTableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            string[] nombresMeses = { "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };

            // ── Fila 0: Cabecera de Meses ────────────────────────────────────────

            var borderMesHeaderLabel = CrearCeldaEncabezado("MES", 28);
            Grid.SetRow(borderMesHeaderLabel, 0);
            Grid.SetColumn(borderMesHeaderLabel, 0);
            GanttTableGrid.Children.Add(borderMesHeaderLabel);

            int currentColIndex = 1;
            foreach (var mStr in mesesSecuencia)
            {
                var parts = mStr.Split('-');
                int y = int.Parse(parts[0]);
                int m = int.Parse(parts[1]);
                int diasMes = DateTime.DaysInMonth(y, m);

                var borderMes = CrearCeldaEncabezado($"{nombresMeses[m].ToUpper()} {y}", 28, fontSize: 10);
                Grid.SetRow(borderMes, 0);
                Grid.SetColumn(borderMes, currentColIndex);
                Grid.SetColumnSpan(borderMes, diasMes);
                GanttTableGrid.Children.Add(borderMes);

                currentColIndex += diasMes;
            }

            // ── Fila 1: Cabecera de Días ─────────────────────────────────────────

            var borderTrabHeader = CrearCeldaEncabezadoSecundario("TRABAJADOR", 24);
            Grid.SetRow(borderTrabHeader, 1);
            Grid.SetColumn(borderTrabHeader, 0);
            GanttTableGrid.Children.Add(borderTrabHeader);

            for (int i = 0; i < fechasEjeX.Count; i++)
            {
                var borderDia = CrearCeldaEncabezadoSecundario(fechasEjeX[i].Day.ToString(), 24);
                Grid.SetRow(borderDia, 1);
                Grid.SetColumn(borderDia, i + 1);
                GanttTableGrid.Children.Add(borderDia);
            }

            // ── Filas de Trabajadores ────────────────────────────────────────────

            var sortedWorkers = _datos.Trabajadores.Keys.OrderBy(n => n).ToList();
            int rIndex = 2;

            foreach (var w in sortedWorkers)
            {
                var info = _datos.Trabajadores[w];

                // Celda de nombre
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

                // Celdas de días del trabajador
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
                        borderCell.Background = quotaYear != date.Year
                            ? (SolidColorBrush)FindResource("ColorVacacionOtroAño")
                            : (SolidColorBrush)FindResource("ColorVacacionBase");

                        if (quotaYear != date.Year)
                            borderCell.BorderBrush = (SolidColorBrush)FindResource("ColorVacacionOtroAñoText");
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

            // ── Fila de Totales (condicional) ─────────────────────────────────────

            if (!_config.OcultarComputoGantt)
            {
                var borderComputoLabel = new Border
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
                Grid.SetRow(borderComputoLabel, rIndex);
                Grid.SetColumn(borderComputoLabel, 0);
                GanttTableGrid.Children.Add(borderComputoLabel);

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

        // ── Helpers privados de construcción de celdas ────────────────────────────

        /// <summary>
        /// Crea una celda de encabezado principal (fila 0 del Gantt) con fondo gris oscuro.
        /// </summary>
        private Border CrearCeldaEncabezado(string text, double height, int fontSize = 11)
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                BorderBrush = (SolidColorBrush)FindResource("ColorBorder"),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = text,
                    FontWeight = FontWeights.Bold,
                    FontSize = fontSize,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                Height = height
            };
        }

        /// <summary>
        /// Crea una celda de encabezado secundario (fila 1 del Gantt) con fondo gris medio.
        /// </summary>
        private Border CrearCeldaEncabezadoSecundario(string text, double height)
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderBrush = (SolidColorBrush)FindResource("ColorBorder"),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = text,
                    FontWeight = FontWeights.Bold,
                    FontSize = 10,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                Height = height
            };
        }

        #endregion
    }
}
