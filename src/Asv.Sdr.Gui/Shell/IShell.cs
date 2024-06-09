using Material.Icons;
using ReactiveUI.Fody.Helpers;

namespace Asv.Sdr.Gui;

public interface IShell
{
    
}

public interface IShellPage
{
    public MaterialIconKind Icon { get; }
    public string Title { get; }
    string Id { get; }
}

public class ShellPage : DisposableReactiveObjectWithValidation, IShellPage
{
    public ShellPage(string id)
    {
        Id = id;
    }
    public string Id { get; }
    
    [Reactive]
    public MaterialIconKind Icon { get; set; }
    [Reactive]
    public string Title { get; set; }
   
}