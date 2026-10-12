using System.Windows;
using System.Windows.Controls;

namespace ImogeIndexWPF;

public partial class IncommingProgressBar : UserControl {
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(IncommingProgressBar),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double Value {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public IncommingProgressBar() {
        InitializeComponent();
    }
}
