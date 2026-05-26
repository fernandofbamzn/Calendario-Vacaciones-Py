using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using CalendarioWPF.Services;

namespace CalendarioWPF
{
    /// <summary>
    /// Clase parcial de MainWindow: todos los eventos de importación, exportación de datos
    /// (JSON, CSV, PDF, Excel) y la ventana de visualización del registro de logs.
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Importación

        /// <summary>
        /// Abre un diálogo de archivo y procesa la importación de datos JSON o CSV,
        /// actualizando el estado de la aplicación y la interfaz de forma inmediata.
        /// </summary>
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
                        MenuLabelActiveYear.Text = $"Año de vacaciones: {_datos.Year}";

                    MostrarEstado($"✅ Importación exitosa ({res.Tipo}): {res.Msg}");
                }
                catch (Exception ex)
                {
                    MostrarEstado($"❌ Error al importar: {ex.Message}");
                }
            }
        }

        #endregion

        #region Exportación JSON / CSV

        /// <summary>
        /// Muestra un diálogo de guardado y escribe el contenido exportado en el archivo elegido.
        /// </summary>
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
                    MostrarEstado($"✅ Archivo exportado: {Path.GetFileName(saveFileDialog.FileName)}");
                }
                catch (Exception ex)
                {
                    MostrarEstado($"❌ Error al guardar: {ex.Message}");
                }
            }
        }

        private void ExportarTrabajadoresJson_Click(object sender, RoutedEventArgs e)
            => GuardarArchivoExportado($"export_trabajadores_{_datos.Year}.json", DataManager.ExportarTrabajadoresJson(_datos));

        private void ExportarTrabajadoresCsv_Click(object sender, RoutedEventArgs e)
            => GuardarArchivoExportado($"export_trabajadores_{_datos.Year}.csv", DataManager.ExportarTrabajadoresCsv(_datos));

        private void ExportarFestivosJson_Click(object sender, RoutedEventArgs e)
            => GuardarArchivoExportado($"export_festivos_{_datos.Year}.json", DataManager.ExportarFestivosJson(_datos));

        private void ExportarFestivosCsv_Click(object sender, RoutedEventArgs e)
            => GuardarArchivoExportado($"export_festivos_{_datos.Year}.csv", DataManager.ExportarFestivosCsv(_datos));

        private void ExportarVacacionesJson_Click(object sender, RoutedEventArgs e)
            => GuardarArchivoExportado($"export_vacaciones_{_datos.Year}.json", DataManager.ExportarVacacionesJson(_datos));

        private void ExportarVacacionesCsv_Click(object sender, RoutedEventArgs e)
            => GuardarArchivoExportado($"export_vacaciones_{_datos.Year}.csv", DataManager.ExportarVacacionesCsv(_datos));

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

        #endregion

        #region Exportación PDF

        /// <summary>
        /// Genera el reporte PDF mensual (vista de cuadrícula de meses) y lo abre automáticamente.
        /// </summary>
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
                AbrirArchivo(saveFileDialog.FileName);
                MostrarEstado("✅ PDF Mensual exportado correctamente.");
            }
            catch (Exception ex)
            {
                MostrarEstado($"❌ Error al exportar PDF: {ex.Message}");
            }
        }

        /// <summary>
        /// Genera el reporte PDF tipo Gantt (tabla de días) y lo abre automáticamente.
        /// </summary>
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
                AbrirArchivo(saveFileDialog.FileName);
                MostrarEstado("✅ PDF Gantt exportado correctamente.");
            }
            catch (Exception ex)
            {
                MostrarEstado($"❌ Error al exportar PDF Gantt: {ex.Message}");
            }
        }

        #endregion

        #region Exportación Excel

        /// <summary>
        /// Genera el libro Excel con pestañas Gantt por año de cupo y lo abre automáticamente.
        /// </summary>
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
                    AbrirArchivo(saveFileDialog.FileName);
                    MostrarEstado("✅ Excel Gantt exportado correctamente.");
                }
                catch (Exception ex)
                {
                    MostrarEstado($"❌ Error al exportar a Excel: {ex.Message}");
                }
            }
        }

        #endregion

        #region Ventana de Logs

        /// <summary>
        /// Abre una ventana modal que muestra el historial de mensajes de estado registrados
        /// durante la sesión actual, con opción de copiar al portapapeles.
        /// </summary>
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
                    MessageBox.Show($"Error al copiar logs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        #endregion

        #region Helpers privados

        /// <summary>
        /// Abre un archivo con la aplicación predeterminada del sistema operativo.
        /// </summary>
        private static void AbrirArchivo(string path)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }

        #endregion
    }
}
