using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CalendarioWPF.Services;

namespace CalendarioWPF
{
    /// <summary>
    /// Clase parcial de MainWindow: gestión del panel de cupo activo y la barra de toolbar del trabajador.
    /// Contiene la lógica de selección de trabajador, actualización de días base/extras y
    /// el refresco del panel de progreso de cupo.
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Gestión de Personal y Controles del Toolbar

        /// <summary>
        /// Repuebla el ComboBox de trabajadores manteniendo la selección activa cuando es posible.
        /// Desconecta y reconecta el evento SelectionChanged para evitar disparos espúreos.
        /// </summary>
        private void ActualizarSelectTrabajadores()
        {
            SelectWorker.SelectionChanged -= SelectWorker_SelectionChanged;
            SelectWorker.Items.Clear();

            var nombres = _datos.Trabajadores
                .Where(kvp => string.IsNullOrEmpty(_filtroDpto) || kvp.Value.Departamento == _filtroDpto)
                .Select(kvp => kvp.Key)
                .OrderBy(n => n)
                .ToList();

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
            ActualizarComboFiltroDpto();
        }

        /// <summary>
        /// Actualiza las opciones del combo de filtro de departamento y mantiene la selección.
        /// </summary>
        private void ActualizarComboFiltroDpto()
        {
            CmbFiltroDpto.SelectionChanged -= CmbFiltroDpto_SelectionChanged;
            CmbFiltroDpto.Items.Clear();

            CmbFiltroDpto.Items.Add("Todos");
            if (_datos.Departamentos != null)
            {
                foreach (var dpt in _datos.Departamentos.OrderBy(d => d))
                {
                    CmbFiltroDpto.Items.Add(dpt);
                }
            }

            if (!string.IsNullOrEmpty(_filtroDpto) && CmbFiltroDpto.Items.Contains(_filtroDpto))
            {
                CmbFiltroDpto.SelectedItem = _filtroDpto;
            }
            else
            {
                CmbFiltroDpto.SelectedIndex = 0;
                _filtroDpto = "";
            }
            
            CmbFiltroDpto.SelectionChanged += CmbFiltroDpto_SelectionChanged;
        }

        private void CmbFiltroDpto_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string sel = CmbFiltroDpto.SelectedItem?.ToString() ?? "";
            _filtroDpto = sel == "Todos" ? "" : sel;
            ActualizarSelectTrabajadores(); // Para aplicar el filtro al SelectWorker
            ActualizarVistas();
        }

        /// <summary>
        /// Actualiza el trabajador activo cuando el usuario cambia la selección del ComboBox.
        /// </summary>
        private void SelectWorker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _activeWorker = SelectWorker.SelectedItem?.ToString() ?? "";
            ActualizarPanelCupo();
            
            if (!_isGanttInteraction)
                ActualizarVistas();
        }

        // ── Controles de Días Base ────────────────────────────────────────────────

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

        // ── Controles de Días Extra ───────────────────────────────────────────────

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

        // ── Modos de Edición ─────────────────────────────────────────────────────

        /// <summary>
        /// Activa el modo de marcado de vacaciones y desactiva el de festivos.
        /// </summary>
        private void MenuMarcarVacaciones_Click(object sender, RoutedEventArgs e)
        {
            MenuMarcarVacaciones.IsChecked = true;
            MenuMarcarFestivos.IsChecked = false;
            _editMode = "vacaciones";
            ActualizarVistas();
        }

        /// <summary>
        /// Activa el modo de marcado de festivos oficiales y desactiva el de vacaciones.
        /// </summary>
        private void MenuMarcarFestivos_Click(object sender, RoutedEventArgs e)
        {
            MenuMarcarVacaciones.IsChecked = false;
            MenuMarcarFestivos.IsChecked = true;
            _editMode = "festivos";
            ActualizarVistas();
        }

        // ── Panel de Cupo ─────────────────────────────────────────────────────────

        /// <summary>
        /// Actualiza la barra de progreso y el texto de resumen de consumo del cupo del trabajador activo.
        /// Colorea en rojo si el trabajador ha superado su cupo disponible.
        /// </summary>
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
            var festivosTrabajador = RangoVacacionesHelper.ObtenerFestivosTrabajador(_activeWorker, _datos);
            int consumidos = RangoVacacionesHelper.ContarDiasConsumidos(info.Vacaciones, info.Imputaciones, festivosTrabajador, _datos.Year);
            int restantes = totalDisponibles - consumidos;

            double pct = totalDisponibles > 0 ? ((double)consumidos / totalDisponibles) * 100 : 0;
            pct = Math.Min(100, Math.Max(0, pct));

            ProgressBarQuota.Value = pct;
            LabelQuotaSummary.Text = $"Cupo {_datos.Year}: {consumidos} de {totalDisponibles} (Quedan: {restantes})";

            bool excedido = restantes < 0;
            var colorIndicador = excedido
                ? (SolidColorBrush)FindResource("ColorDanger")
                : (SolidColorBrush)FindResource("ColorAccent");

            ProgressBarQuota.Foreground = colorIndicador;
            LabelQuotaSummary.Foreground = colorIndicador;
        }

        #endregion
    }
}
