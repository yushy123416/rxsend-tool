using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("RX Toolbox")]
[assembly: AssemblyProduct("RX Toolbox")]
[assembly: AssemblyVersion(RxToolbox.AppVersion.Number)]
[assembly: AssemblyFileVersion(RxToolbox.AppVersion.Number)]
[assembly: AssemblyCopyright("Copyright © 2026 yushy")]
[assembly: AssemblyMetadata("License","GPL-3.0-only")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8",FrameworkDisplayName=".NET Framework 4.8")]

namespace RxToolbox {
 internal static class Program {
  [STAThread] public static void Main(string[] args) {
   if (args.Length > 0 && args[0] == "--apply-update") { Updates.Apply(args); return; }
   bool first;
   using (var mutex = new Mutex(true, "Local\\RxToolbox.v1", out first)) {
    if (!first) { MessageBox.Show("RX Toolbox 已在系统托盘运行。", "RX Toolbox"); return; }
    System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)0;
    Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
    Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Log.Write(e.Exception); MessageBox.Show(e.Exception.Message, "操作未完成"); };
    try { using (var app = new MainWindow()) Application.Run(app); } catch(Exception e) { Log.Write(e); MessageBox.Show(e.Message,"无法启动 RX Toolbox"); }
   }
  }
 }
 internal static class Log {
  public static void Write(Exception e) { try { Directory.CreateDirectory(Settings.Folder); string p=Path.Combine(Settings.Folder,"errors.log"); if(File.Exists(p) && new FileInfo(p).Length>256000) File.Delete(p); File.AppendAllText(p,DateTime.UtcNow.ToString("o")+" "+e+Environment.NewLine); } catch {} }
 }
 public sealed class Settings {
  public const string DefaultApi="https://neomio.nigigasaki.live/status.php";
  public static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RxToolbox");
  public string ApiUrl=DefaultApi, SelectedServer="202.189.21.228:27076", GithubRepo=Updates.DefaultRepository, MirrorUrl=Updates.DefaultMirrorUrl, ScreenDevice="";
  public string TimerServer="";
  public bool TimerVisible=false, CrosshairVisible=false, CloseToTray=true, AutoCheck=false;
  public int TimerX=32, TimerY=80, OffsetSeconds=0, CrosshairX=0, CrosshairY=0;
  public float CrosshairScale=1;
  public TimerAppearance TimerAppearance=new TimerAppearance();
  public CrosshairProfile Crosshair=new CrosshairProfile();
  public List<CrosshairPreset> Presets=new List<CrosshairPreset>();
  public static Settings Load() {
   try { var s=Json.Read<Settings>(File.ReadAllText(Path.Combine(Folder,"settings.json"))); if(s==null) return new Settings(); s.Crosshair=s.Crosshair??new CrosshairProfile(); s.Crosshair.Validate(); s.CrosshairScale=Math.Max(.25f,Math.Min(4,s.CrosshairScale)); s.OffsetSeconds=Math.Max(-300,Math.Min(300,s.OffsetSeconds)); s.Presets=s.Presets??new List<CrosshairPreset>();s.TimerAppearance=s.TimerAppearance??new TimerAppearance();s.TimerAppearance.Validate();Updates.UseDefaultSource(s); return s; }
   catch(Exception e) { if(!(e is FileNotFoundException)&&!(e is DirectoryNotFoundException)) Log.Write(e); return new Settings(); }
  }
  public void Save() {
   Directory.CreateDirectory(Folder); var p=Path.Combine(Folder,"settings.json"); var temp=p+".tmp";
   File.WriteAllText(temp,Json.Write(this),System.Text.Encoding.UTF8);
   if(File.Exists(p)) File.Replace(temp,p,null); else File.Move(temp,p);
  }
 }
 public static class Json {
  public static T Read<T>(string s) { return new JavaScriptSerializer { MaxJsonLength=2*1024*1024,RecursionLimit=32 }.Deserialize<T>(s); }
  public static string Write(object s) { return new JavaScriptSerializer().Serialize(s); }
 }
 internal interface IToolModule : IDisposable {
  string Title {get;} string Symbol {get;} Control View {get;}
  void Tick();
 }
 internal sealed class ToolContext {
  public Settings Settings; public MainWindow Window; public RoundService Rounds; public CrosshairOverlay Crosshair; public TimerOverlay Timer;public AutoJoinService AutoJoin;
  public void Save() { try { Settings.Save(); } catch(Exception e) { Log.Write(e); Window.SetStatus("设置保存失败："+e.Message); } }
  public Screen Display { get { foreach(var s in Screen.AllScreens) if(s.DeviceName==Settings.ScreenDevice) return s; return Screen.PrimaryScreen; } }
  public void UpdateCrosshair() { Crosshair.Update(Settings.Crosshair,Display.Bounds,Settings.CrosshairScale,Settings.CrosshairX,Settings.CrosshairY); if(Settings.CrosshairVisible) Crosshair.Show(); else Crosshair.Hide(); }
 }
}
