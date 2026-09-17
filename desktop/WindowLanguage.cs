using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using BD2Fishing.Localization;
namespace BD2Fishing.Desktop;

/// <summary>WPF presentation adapter; preserves source text for reversible live switching.
/// Listeners are detached when controls leave the visual tree (including regenerated board cells).</summary>
internal sealed class WindowLanguage : IDisposable
{
    private static readonly ConditionalWeakTable<Window,WindowLanguage> Windows=new();
    private static readonly DependencyProperty OwnerProperty=DependencyProperty.RegisterAttached("Owner",typeof(WindowLanguage),typeof(WindowLanguage));
    static WindowLanguage()
    {
        // Loaded/Unloaded are direct routed events; a handler on the Window cannot observe child controls.
        EventManager.RegisterClassHandler(typeof(FrameworkElement),FrameworkElement.LoadedEvent,new RoutedEventHandler((_,e)=>
        {if(e.OriginalSource is FrameworkElement element && Window.GetWindow(element) is Window root && Windows.TryGetValue(root,out var owner))owner.Attach(element);}));
        EventManager.RegisterClassHandler(typeof(FrameworkElement),FrameworkElement.UnloadedEvent,new RoutedEventHandler((_,e)=>
        {if(e.OriginalSource is DependencyObject node && node.GetValue(OwnerProperty) is WindowLanguage owner)owner.Detach(node);}));
    }
    private readonly Window window;
    private readonly Dictionary<DependencyObject,List<Target>> targets=new();
    public LanguageCatalog Catalog {get;}
    public WindowLanguage(Window window,string language)
    {
        this.window=window;Catalog=new(language);
        Windows.Add(window,this);
        Watch(window,Window.TitleProperty);
        Walk(window);
    }
    public int TrackedControlCount=>targets.Count;
    public void Include(DependencyObject node)=>Walk(node);
    public void Forget(DependencyObject node)
    {
        foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>().ToArray())Forget(child);
        Detach(node);
    }
    private void Walk(DependencyObject node)
    {
        Attach(node);
        foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())Walk(child);
    }
    private void Detach(DependencyObject node)
    {
        if(targets.Remove(node,out var entries))foreach(var entry in entries)entry.Dispose();
        node.ClearValue(OwnerProperty);
    }
    private void Attach(DependencyObject node)
    {
        // Template text inherits its control's translated content. Do not override a binding.
        if(node is FrameworkElement element && element.TemplatedParent!=null)return;
        if(node is TextBlock)Watch(node,TextBlock.TextProperty);
        if(node is ContentControl)Watch(node,ContentControl.ContentProperty);
        if(node is HeaderedContentControl)Watch(node,HeaderedContentControl.HeaderProperty);
        if(node is FrameworkElement){Watch(node,FrameworkElement.ToolTipProperty);Watch(node,AutomationProperties.NameProperty);}
    }
    private void Watch(DependencyObject node,DependencyProperty property)
    {
        if(node.GetValue(property) is not string)return;
        if(!targets.TryGetValue(node,out var entries)){targets[node]=entries=new();node.SetValue(OwnerProperty,this);}
        if(entries.Any(e=>e.Property==property))return;
        var target=new Target(node,property,Catalog);entries.Add(target);target.Render();
    }
    public void Select(string language)
    {
        Catalog.Select(language);
        foreach(var entry in targets.Values.SelectMany(v=>v).ToArray())entry.Render();
        window.Language=System.Windows.Markup.XmlLanguage.GetLanguage(Catalog.Language);
    }
    public void Dispose()
    {
        Windows.Remove(window);
        foreach(var node in targets.Keys.ToArray())Detach(node);
    }
    private sealed class Target:IDisposable
    {
        private readonly DependencyObject node;private readonly LanguageCatalog catalog;
        private readonly DependencyPropertyDescriptor descriptor;private string source;private bool rendering;
        public DependencyProperty Property{get;}
        public Target(DependencyObject node,DependencyProperty property,LanguageCatalog catalog)
        {
            this.node=node;Property=property;this.catalog=catalog;source=(string)node.GetValue(property);
            descriptor=DependencyPropertyDescriptor.FromProperty(property,node.GetType());descriptor.AddValueChanged(node,Changed);
        }
        private void Changed(object? sender,EventArgs e)
        {
            if(rendering || node.GetValue(Property) is not string value)return;
            source=value;Render();
        }
        public void Render()
        {
            rendering=true;try{node.SetCurrentValue(Property,catalog.Text(source));}finally{rendering=false;}
        }
        public void Dispose()=>descriptor.RemoveValueChanged(node,Changed);
    }
}
