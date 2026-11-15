using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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
   if (args.Length > 0 && args[0] == "--nvidia-filters") { try { NvidiaFilters.RunHelper(args); } catch(Exception e) { Log.Write(e); Environment.ExitCode=1; } return; }
   System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)0;
   Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
   using (var mutex = new Mutex(false, SingleInstance.MutexName)) {
    bool owns=SingleInstance.TryAcquire(mutex);
    if (!owns) { using(var dialog=new DuplicateInstanceDialog(mutex))owns=dialog.ShowDialog()==DialogResult.OK;if(!owns)return; }
    Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Log.Write(e.Exception); MessageBox.Show(Privacy.Redact(e.Exception.Message), "操作未完成"); };
    try { using (var app = new MainWindow()) Application.Run(app); } catch(Exception e) { Log.Write(e); MessageBox.Show(Privacy.Redact(e.Message),"无法启动 RX Toolbox"); }
    finally{mutex.ReleaseMutex();}
   }
  }
 }
 internal static class Log {
  public static void Write(Exception e) { try { Directory.CreateDirectory(Settings.Folder); string p=Path.Combine(Settings.Folder,"errors.log"); if(File.Exists(p) && new FileInfo(p).Length>256000) File.Delete(p); File.AppendAllText(p,DateTime.UtcNow.ToString("o")+" "+Privacy.Redact(e.ToString())+Environment.NewLine); } catch {} }
 }
 public sealed class Settings {
  public static string DefaultApi {get{return EndpointCatalog.Api;}}
  public static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RxToolbox");
  public string SelectedServer="", GithubRepo=Updates.DefaultRepository, MirrorUrl=Updates.DefaultMirrorUrl, ScreenDevice="";
  public string ProtectedApiUrl;
  [ScriptIgnore] public string ApiUrl {
   get {if(string.IsNullOrEmpty(ProtectedApiUrl))return DefaultApi;try{return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(ProtectedApiUrl),null,DataProtectionScope.CurrentUser));}catch{throw new InvalidDataException("本机连接配置无法解密，请重置设置。");}}
   set {ProtectedApiUrl=string.IsNullOrEmpty(value)||value==DefaultApi?null:Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(HttpData.Https(value).AbsoluteUri),null,DataProtectionScope.CurrentUser));}
  }
  public string TimerServer="";
  public bool TimerVisible=false, CrosshairVisible=false, CloseToTray=true, AutoCheck=false;
  public int TimerX=32, TimerY=80, OffsetSeconds=0, CrosshairX=0, CrosshairY=0;
  public float CrosshairScale=2;
  public int CrosshairDisplayVersion=1;
  public TimerAppearance TimerAppearance=new TimerAppearance();
  public CrosshairProfile Crosshair=new CrosshairProfile();
  public List<CrosshairPreset> Presets=new List<CrosshairPreset>();
  public static Settings Load() {
   try { var text=File.ReadAllText(Path.Combine(Folder,"settings.json")); var s=FromJson(text); if(s==null) return new Settings(); s.Crosshair=s.Crosshair??new CrosshairProfile(); s.Crosshair.Validate(); if(s.CrosshairDisplayVersion<2){if(s.CrosshairScale==1)s.CrosshairScale=2;s.CrosshairDisplayVersion=2;} s.CrosshairScale=Math.Max(.25f,Math.Min(4,s.CrosshairScale)); s.OffsetSeconds=Math.Max(-300,Math.Min(300,s.OffsetSeconds)); s.Presets=s.Presets??new List<CrosshairPreset>();s.TimerAppearance=s.TimerAppearance??new TimerAppearance();s.TimerAppearance.Validate();Updates.UseDefaultSource(s); try{s.Save();}catch(Exception e){Log.Write(e);} return s; }
   catch(Exception e) { if(!(e is FileNotFoundException)&&!(e is DirectoryNotFoundException)) Log.Write(e); return new Settings(); }
  }
  internal static Settings FromJson(string text){
   var s=Json.Read<Settings>(text);if(s==null)return null;
   var legacy=Json.Read<Dictionary<string,object>>(text);object api;
   if(legacy.TryGetValue("ApiUrl",out api)&&api is string)s.ApiUrl=(string)api;
   using(var rounds=new RoundService(s)){} // Convert legacy IP:port selections before saving.
   return s;
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
