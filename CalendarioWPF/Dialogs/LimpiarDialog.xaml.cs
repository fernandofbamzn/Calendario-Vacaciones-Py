using System.Windows;

namespace CalendarioWPF.Dialogs
{
    public partial class LimpiarDialog : Window
    {
        public bool LimpiarFestivos { get; private set; }
        public bool LimpiarVacaciones { get; private set; }
        public bool LimpiarTrabajadores { get; private set; }

        public LimpiarDialog()
        {
            InitializeComponent();
        }

        private void ChkTrabajadores_Checked(object sender, RoutedEventArgs e)
        {
            if (ChkTrabajadores.IsChecked == true)
            {
                ChkVacaciones.IsChecked = true;
                ChkVacaciones.IsEnabled = false;
                TxtAviso.Visibility = Visibility.Visible;
            }
            else
            {
                ChkVacaciones.IsEnabled = true;
                TxtAviso.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnLimpiar_Click(object sender, RoutedEventArgs e)
        {
            LimpiarFestivos = ChkFestivos.IsChecked == true;
            LimpiarVacaciones = ChkVacaciones.IsChecked == true;
            LimpiarTrabajadores = ChkTrabajadores.IsChecked == true;

            if (!LimpiarFestivos && !LimpiarVacaciones && !LimpiarTrabajadores)
            {
                MessageBox.Show("Por favor, selecciona al menos una opción para limpiar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
