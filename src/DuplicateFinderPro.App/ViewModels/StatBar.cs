using System.Windows.Media;

namespace DuplicateFinderPro.App.ViewModels;

/// <summary>One row of a simple horizontal bar chart on the Statistics tab.</summary>
public sealed class StatBar
{
    public StatBar(string label, string valueText, double fraction, Brush brush)
    {
        Label = label;
        ValueText = valueText;
        Fraction = Math.Clamp(fraction, 0, 1);
        Brush = brush;
    }

    public string Label { get; }
    public string ValueText { get; }

    /// <summary>0..1 fraction of the largest value, used for bar width.</summary>
    public double Fraction { get; }

    public Brush Brush { get; }

    /// <summary>Percentage 0..100 for a star-sized bar width in XAML.</summary>
    public double Percent => Fraction * 100.0;
}
