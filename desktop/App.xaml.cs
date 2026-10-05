using System.IO;
using BD2Fishing.Localization;
using System.Reflection;
using System.Text.Json;
using System.Windows;
namespace BD2Fishing.Desktop;
public partial class App:Application
{
 // Optional shared .NET launcher entry; standalone Main and normal startup remain unchanged.
 private string[]? hostedArguments;
 public static int RunHosted(string[] args, Action<Application>? configure = null)
 {
  var application = new App { hostedArguments = args };
  application.InitializeComponent(); configure?.Invoke(application);
  return application.Run();
 }
    public static string DisplayVersion => typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+' )[0] ?? typeof(App).Assembly.GetName().Version!.ToString(3);
 private Mutex? single;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException+=(_,a)=>FishingDiagnostics.Write(FishingIdentity.DataRoot,"dispatcher.unhandled",error:a.Exception);
        AppDomain.CurrentDomain.UnhandledException+=(_,a)=>FishingDiagnostics.Write(FishingIdentity.DataRoot,"process.unhandled",error:a.ExceptionObject as Exception);
        if((hostedArguments ?? e.Args).Length==2&&(hostedArguments ?? e.Args)[0]=="--identity")
        {File.WriteAllText((hostedArguments ?? e.Args)[1],JsonSerializer.Serialize(new{runtime=FishingIdentity.RuntimeName,toolFingerprint=BD2Fishing.Compatibility.HookCompiler.ToolFingerprint,compatibility="local-interface-adaptation",version=typeof(App).Assembly.GetName().Version!.ToString(),displayVersion=DisplayVersion,defaultNextCastMs=1000,defaultCastGauge=0.9,defaultAutoSell=true,defaultKeepLegendary=true,defaultKeepLocked=true,defaultKeepUnknown=true,defaultAutoApproach=true,defaultAutoBait=true,defaultAutoMapRenewal=true,protectedLegendaryGrade=FishingSalePlan.LegendaryGrade}));Shutdown();return;}
        if((hostedArguments ?? e.Args).Length==3&&(hostedArguments ?? e.Args)[0]=="--check-client")
        {
            try { var result=await Task.Run(()=>BD2Fishing.Compatibility.HookCompiler.Prepare((hostedArguments ?? e.Args)[1]));File.WriteAllText((hostedArguments ?? e.Args)[2],JsonSerializer.Serialize(result.Report));Shutdown(); }
            catch(Exception ex){File.WriteAllText((hostedArguments ?? e.Args)[2],JsonSerializer.Serialize(new{Status="unsupported",Error=ex.ToString(),Injection=false}));Shutdown(1);}return;
        }
        if((hostedArguments ?? e.Args).Length==2&&((hostedArguments ?? e.Args)[0]=="--smoke"||(hostedArguments ?? e.Args)[0]=="--connection-smoke"))
        {
            try{Directory.CreateDirectory((hostedArguments ?? e.Args)[1]);var window=new FishingWindow(Path.Combine(Path.GetFullPath((hostedArguments ?? e.Args)[1]),"isolated",Guid.NewGuid().ToString("N")));MainWindow=window;ShutdownMode=ShutdownMode.OnExplicitShutdown;if((hostedArguments ?? e.Args)[0]=="--connection-smoke")await window.ConnectionSmokeAsync((hostedArguments ?? e.Args)[1]);else await window.SmokeAsync((hostedArguments ?? e.Args)[1]);Shutdown(0);}
            catch(Exception ex){File.WriteAllText(Path.Combine((hostedArguments ?? e.Args)[1],"failure.txt"),ex.ToString());Shutdown(1);}return;
        }
        single=new Mutex(true,"Local\\BD2Fishing.Desktop",out bool first);
        if(!first){var catalog=new LanguageCatalog(LanguagePreference.Read(FishingIdentity.DataRoot));MessageBox.Show(catalog.Text("钓鱼工具已经打开，请使用现有窗口。"),catalog.Text("BD2 钓鱼"));Shutdown();return;}
        var main=new FishingWindow();MainWindow=main;main.Show();
    }
    protected override void OnExit(ExitEventArgs e){single?.Dispose();base.OnExit(e);}
}
