using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RxToolbox {
 public sealed class UpdateManifest { public string version,url,sha256,notes;public string[] mirrors;internal string SourceLabel,ReleaseUrl,ReleaseNotes; }
 public static class Updates {
  public const string DefaultRepository="yushy123416/rxsend-tool";
  public const string DefaultMirrorUrl="https://gitee.com/ysyyyy/rxsend-tool/raw/main/updates/update.json";
  public static readonly Version Current=Version.Parse(Assembly.GetExecutingAssembly().GetName().Version.ToString(3));
  internal static void UseDefaultSource(Settings settings){
   if(string.IsNullOrWhiteSpace(settings.GithubRepo)&&string.IsNullOrWhiteSpace(settings.MirrorUrl))settings.GithubRepo=DefaultRepository;
   if(settings.GithubRepo==DefaultRepository&&string.IsNullOrWhiteSpace(settings.MirrorUrl))settings.MirrorUrl=DefaultMirrorUrl;
  }
  public static Version ParseVersion(string text){Version v;if(string.IsNullOrWhiteSpace(text)||!Version.TryParse(text.Trim().TrimStart('v','V'),out v)||v.Major<0)throw new InvalidDataException("更新版本号无效");return new Version(v.Major,v.Minor,Math.Max(0,v.Build));}
  public static void Validate(UpdateManifest m){if(m==null)throw new InvalidDataException("更新清单为空");ParseVersion(m.version);HttpData.Https(m.url);if(m.sha256==null||!Regex.IsMatch(m.sha256,"^[a-fA-F0-9]{64}$"))throw new InvalidDataException("更新清单缺少 SHA-256 校验值");if(m.mirrors!=null)foreach(var u in m.mirrors)HttpData.Https(u);}
  public static UpdateManifest Check(string repo,string mirror){var manifest=CheckCore(repo,mirror,HttpData.Text);if(ParseVersion(manifest.version)>Current)ReleaseAnnouncements.Load(manifest,repo,mirror,HttpData.Text);return manifest;}
  internal static UpdateManifest CheckCore(string repo,string mirror,Func<string,string> fetch){
   var sources=new List<string>();if(!string.IsNullOrWhiteSpace(mirror))sources.Add(HttpData.Https(mirror.Trim()).AbsoluteUri);
   Exception error=null;UpdateManifest withoutNotes=null;
   if(!string.IsNullOrWhiteSpace(repo)){
    if(!Regex.IsMatch(repo.Trim(),@"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$"))throw new InvalidDataException("GitHub 仓库格式应为 owner/repo");
    sources.Add("https://github.com/"+repo.Trim()+"/releases/latest/download/update.json");
   }
   if(sources.Count==0)throw new InvalidDataException("请先填写 GitHub 仓库或镜像清单地址");
   foreach(var u in sources){try{
    var m=Json.Read<UpdateManifest>(fetch(u));Validate(m);m.SourceLabel=new Uri(u).Host=="gitee.com"?"Gitee":new Uri(u).Host=="github.com"?"GitHub":"自定义更新源";
    // Never replace a newer primary release with an older fallback just to obtain notes.
    if(withoutNotes!=null&&(ParseVersion(m.version)<ParseVersion(withoutNotes.version)||(ParseVersion(m.version)==ParseVersion(withoutNotes.version)&&!string.Equals(m.sha256,withoutNotes.sha256,StringComparison.OrdinalIgnoreCase))))continue;
    if(!string.IsNullOrWhiteSpace(m.notes))return m;if(withoutNotes==null||ParseVersion(m.version)>ParseVersion(withoutNotes.version))withoutNotes=m;
   }catch(Exception ex){error=ex;}}
   if(withoutNotes!=null)return withoutNotes;
   var web=error as System.Net.WebException;var response=web==null?null:web.Response as System.Net.HttpWebResponse;
   if(response!=null&&response.StatusCode==System.Net.HttpStatusCode.NotFound)throw new InvalidDataException("尚未发布可用更新",error);
   throw new IOException("更新源暂不可用："+error.Message,error);
  }
  public static string Hash(byte[] data){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant();}
  public static string Download(UpdateManifest m){return DownloadCore(m,url=>HttpData.Get(url,32*1024*1024,30000));}
  internal static string DownloadCore(UpdateManifest m,Func<string,byte[]> fetch){
   Validate(m);Exception error=null;foreach(string url in new[]{m.url}.Concat(m.mirrors??new string[0])){
    try{byte[] bytes=fetch(url);if(!string.Equals(Hash(bytes),m.sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新文件 SHA-256 不匹配");if(bytes.Length<1024||bytes[0]!=77||bytes[1]!=90)throw new InvalidDataException("更新文件不是 Windows 程序");
     string dir=Path.Combine(Settings.Folder,"updates",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);string file=Path.Combine(dir,"RxToolbox.next.exe");File.WriteAllBytes(file,bytes);
     var identity=AssemblyName.GetAssemblyName(file);if(identity.Name!="RxToolbox"||new Version(identity.Version.Major,identity.Version.Minor,identity.Version.Build)!=ParseVersion(m.version)){File.Delete(file);throw new InvalidDataException("更新文件版本或产品标识不匹配");}return file;
    }catch(Exception ex){error=ex;}
   }throw new IOException("下载失败："+error.Message,error);
  }
  static string Quote(string s){if(s.Contains("\""))throw new InvalidDataException("路径无效");return "\""+s+"\"";}
  public static void Install(string payload,UpdateManifest manifest){
   string target=Application.ExecutablePath;string helper=Path.Combine(Path.GetDirectoryName(payload),"RxToolbox.updater.exe");File.Copy(target,helper,true);
   Process.Start(new ProcessStartInfo(helper,"--apply-update "+Quote(target)+" "+Process.GetCurrentProcess().Id+" "+Quote(payload)+" "+manifest.sha256){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
  }
  public static void Apply(string[] args){
   string target=null,backup=null,temp=null;bool replaced=false;
   try{
    if(args.Length!=5)throw new InvalidDataException("更新参数无效");target=Path.GetFullPath(args[1]);string payload=Path.GetFullPath(args[3]);string root=Path.GetFullPath(Path.Combine(Settings.Folder,"updates"))+Path.DirectorySeparatorChar;
    if(!payload.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!target.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新路径无效");
    if(!string.Equals(Hash(File.ReadAllBytes(payload)),args[4],StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("安装前校验失败");
    int pid;if(!int.TryParse(args[2],out pid))throw new InvalidDataException("进程号无效");try{using(var p=Process.GetProcessById(pid)){if(!p.WaitForExit(20000))throw new IOException("工具箱尚未退出");}}catch(ArgumentException){}
    backup=target+".previous";temp=target+".new";File.Copy(payload,temp,true);File.Replace(temp,target,backup,true);replaced=true;
    Process.Start(new ProcessStartInfo(target){UseShellExecute=true});
    try{File.Delete(backup);File.Delete(payload);}catch{}
   }catch(Exception ex){Log.Write(ex);if(replaced&&File.Exists(backup)){try{File.Copy(backup,target,true);Process.Start(target);}catch(Exception rollback){Log.Write(rollback);}}MessageBox.Show("更新未完成，原版本已保留。\n"+ex.Message,"RX Toolbox");}
   finally{try{if(temp!=null&&File.Exists(temp))File.Delete(temp);}catch{}}
  }
 }
 internal sealed class SettingsModule : ToolModule {
  public override string Title{get{return "设置与更新";}}public override string Symbol{get{return "⚙";}}
  readonly Label fullscreenStatus;readonly FlatButton fullscreenToggle;
  readonly Label updateStatus;readonly FlatButton check;UpdateManifest pending;string payload;bool busy,autoStarted;
  internal Func<string,string,UpdateManifest> CheckUpdate=Updates.Check;
  internal Func<UpdateManifest,string> DownloadUpdate=Updates.Download;
  internal Action<string,UpdateManifest> ApplyUpdate;
  internal Func<UpdateManifest,bool,DialogResult> ConfirmUpdate;
  public SettingsModule(ToolContext c):base(c){
   Header("设置与更新","");
   var card=new CardPanel{Location=new System.Drawing.Point(0,124),Size=new System.Drawing.Size(936,172)};Page.Controls.Add(card);
   Ui.Label(card,"RXSend Tool",24,20,500,34,Ui.Heading);Ui.Label(card,"启动后自动检查，先看更新公告，由你决定是否更新。",24,65,700,30,Ui.Small,Ui.Muted);
   ConfirmUpdate=(manifest,ready)=>{using(var dialog=new UpdateAnnouncementDialog(manifest,ready))return dialog.ShowDialog(C.Window);};
   ApplyUpdate=(file,manifest)=>{C.Save();Updates.Install(file,manifest);C.Window.ExitForUpdate();};
   check=Ui.Button(card,"检查更新",752,26,160,async delegate{if(pending!=null)await ShowUpdateAnnouncement();else await CheckForUpdates();},true);
   updateStatus=Ui.Label(card,"",24,107,888,44,Ui.Small,Ui.Muted);
   var license=new CardPanel{Location=new System.Drawing.Point(0,316),Size=new System.Drawing.Size(936,96)};Page.Controls.Add(license);
   Ui.Label(license,"GNU GPL v3.0",24,16,500,30,Ui.Heading);Ui.Button(license,"查看许可证",752,29,160,License);
   var fullscreen=new CardPanel{Location=new System.Drawing.Point(0,432),Size=new System.Drawing.Size(936,238)};Page.Controls.Add(fullscreen);
   Ui.Label(fullscreen,"实验性全屏模式",24,16,560,34,Ui.Heading);
   Ui.Label(fullscreen,"让 64 位 GMod 全屏显示准星和回合计时；启用后重启工具自动恢复。",24,60,880,28,Ui.Small,Ui.Muted);
   Ui.Label(fullscreen,"可能被反作弊拦截、引起崩溃或覆盖层冲突。开启前必须阅读并确认风险。",24,91,880,28,Ui.Small,Ui.Muted);
   fullscreenToggle=Ui.Button(fullscreen,"开启实验模式",24,137,190,ToggleFullscreen,true);
   Ui.Button(fullscreen,"用途与风险",230,137,156,()=>{MessageBox.Show(C.Window,FullscreenRiskDialog.Explanation,"实验性全屏模式 · 用途与风险",MessageBoxButtons.OK,MessageBoxIcon.Warning);});
   fullscreenStatus=Ui.Label(fullscreen,"",24,190,888,30,Ui.Small,Ui.Muted);Tick();
  }
  internal async Task CheckForUpdates(){
   if(busy)return;
   if(string.IsNullOrWhiteSpace(C.Settings.GithubRepo)&&string.IsNullOrWhiteSpace(C.Settings.MirrorUrl)){updateStatus.Text="暂未配置可用更新源";return;}
   busy=true;check.Enabled=false;updateStatus.Text="正在检查更新…";string repo=C.Settings.GithubRepo,mirror=C.Settings.MirrorUrl;bool offer=false;
   try{var result=await Task.Run(()=>CheckUpdate(repo,mirror));if(Page.IsDisposed)return;if(Updates.ParseVersion(result.version)>Updates.Current){if(pending==null||pending.version!=result.version||pending.sha256!=result.sha256)payload=null;pending=result;C.Window.SetUpdateAvailable(true);updateStatus.Text="发现新版本 "+result.version+"，请查看公告后选择是否更新";check.Text="查看更新公告";offer=true;}else if(pending==null){C.Window.SetUpdateAvailable(false);updateStatus.Text="已是最新版本";}}
   catch(Exception ex){if(!Page.IsDisposed){updateStatus.Text=ex.Message;C.Window.SetStatus("检查更新未完成："+ex.Message);}}
   finally{busy=false;if(!Page.IsDisposed)check.Enabled=true;}
   if(offer&&!Page.IsDisposed)await ShowUpdateAnnouncement();
  }
  internal async Task ShowUpdateAnnouncement(){
   if(busy||pending==null||Page.IsDisposed)return;busy=true;check.Enabled=false;var manifest=pending;
   try{
    if(ConfirmUpdate(manifest,payload!=null)!=DialogResult.OK){updateStatus.Text="已暂缓更新 · 可点击左上角「有新版本」再次查看";return;}
    if(payload==null){updateStatus.Text="正在下载并校验…";C.Window.SetStatus(updateStatus.Text);payload=await Task.Run(()=>DownloadUpdate(manifest));if(Page.IsDisposed)return;}
    updateStatus.Text="校验通过，正在安装并重启…";C.Window.SetStatus(updateStatus.Text);ApplyUpdate(payload,manifest);
   }catch(Exception ex){if(!Page.IsDisposed){updateStatus.Text="更新未完成："+ex.Message;C.Window.SetStatus(updateStatus.Text);}}
   finally{busy=false;if(!Page.IsDisposed){check.Enabled=true;check.Text="查看更新公告";}}
  }
  internal async void StartAutoUpdate(){if(autoStarted)return;autoStarted=true;await CheckForUpdates();}
  void ToggleFullscreen(){
   if(C.Fullscreen.Enabled){C.Fullscreen.Disable();C.Save();Tick();return;}
   using(var dialog=new FullscreenRiskDialog()){if(dialog.ShowDialog(C.Window)!=DialogResult.OK)return;}
   C.Settings.FullscreenConsentVersion=FullscreenRiskDialog.ConsentVersion;C.Settings.ExperimentalFullscreen=true;C.Save();C.Fullscreen.Tick();Tick();
  }
  public override void Tick(){fullscreenToggle.Text=C.Fullscreen.Enabled?"关闭实验模式":"开启实验模式";fullscreenStatus.Text=C.Fullscreen.Status;}
  static string LicenseText(string file,string resource){string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,file);if(File.Exists(path))return File.ReadAllText(path);using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)){if(stream==null)return "";using(var reader=new StreamReader(stream))return reader.ReadToEnd();}}
  void License(){using(var f=new Form{Text="开源许可 · GPL-3.0",Size=new System.Drawing.Size(760,580),StartPosition=FormStartPosition.CenterParent}){var t=new TextBox{Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,BackColor=Ui.Card,ForeColor=Ui.Text,Font=Ui.Body};t.Text="RX Toolbox · GPL-3.0-only\r\nCopyright (c) 2026 yushy\r\n\r\n"+LicenseText("LICENSE","RxToolbox.LICENSE")+"\r\n\r\n"+LicenseText("THIRD-PARTY-NOTICES.md","RxToolbox.NOTICES");f.Controls.Add(t);f.ShowDialog(C.Window);}}
 }
}
