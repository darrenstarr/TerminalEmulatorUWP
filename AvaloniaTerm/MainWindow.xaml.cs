using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AvaloniaTerm
{
    public class MainWindow : Window
    {
        public TerminalView View { get; set; }
        public Button ClickMe { get; set; }

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            View = this.FindControl<TerminalView>("Editor");
            ClickMe = this.FindControl<Button>("clickMe");

            ClickMe.Click += ClickMe_Click;
            View.KeyDown += View_KeyDown;
            //View.TextInput += View_TextInput;
        }

        private void View_KeyDown(object sender, Avalonia.Input.KeyEventArgs e)
        {
            throw new System.NotImplementedException();
        }

        private void View_TextInput(object sender, Avalonia.Input.TextInputEventArgs e)
        {
            foreach(var ch in e.Text.ToCharArray())
                View.TerminalEmulator.HandleKeyPress(ch);
        }

        private void ClickMe_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            View.TerminalEmulator.Append('A');
        }

        private void InitializeComponent()
        {
            var theme = new Avalonia.Themes.Default.DefaultTheme();
            theme.FindResource("Button");
            AvaloniaXamlLoader.Load(this);
        }
    }
}
