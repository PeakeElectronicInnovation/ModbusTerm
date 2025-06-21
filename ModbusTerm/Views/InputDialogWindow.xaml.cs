using System.Windows;

namespace ModbusTerm.Views
{
    /// <summary>
    /// Interaction logic for InputDialogWindow.xaml
    /// </summary>
    public partial class InputDialogWindow : Window
    {
        /// <summary>
        /// Gets or sets the message shown to the user
        /// </summary>
        public string Message
        {
            get => MessageText.Text;
            set => MessageText.Text = value;
        }

        /// <summary>
        /// Gets or sets the input text
        /// </summary>
        public string Input
        {
            get => InputTextBox.Text;
            set => InputTextBox.Text = value;
        }

        /// <summary>
        /// Initializes a new instance of the InputDialogWindow class
        /// </summary>
        public InputDialogWindow()
        {
            InitializeComponent();
            
            // Set focus to the input box
            Loaded += (s, e) => InputTextBox.Focus();
        }

        /// <summary>
        /// Handles the OK button click
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Handles the Cancel button click
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
