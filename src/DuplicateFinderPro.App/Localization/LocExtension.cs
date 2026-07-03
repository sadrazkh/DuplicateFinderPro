using System.Windows.Data;
using System.Windows.Markup;

namespace DuplicateFinderPro.App.Localization;

/// <summary>
/// XAML markup extension: <c>{loc:Loc Some.Key}</c> binds a UI string to the
/// live localization table so it updates when the language changes.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension() { }
    public LocExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = Localization.Instance,
            Mode = BindingMode.OneWay,
        };
        return binding.ProvideValue(serviceProvider);
    }
}
