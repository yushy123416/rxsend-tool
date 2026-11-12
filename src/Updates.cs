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
 public sealed class UpdateManifest { public string version,url,sha256,notes;public string[] mirrors; }
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
  public static UpdateManifest Check(string repo,string mirror){return CheckCore(repo,mirror,HttpData.Text);}
  internal static UpdateManifest CheckCore(string repo,string mirror,Func<string,string> fetch){
   var sources=new List<string>();if(!string.IsNullOrWhiteSpace(mirror))sources.Add(HttpData.Https(mirror.Trim()).AbsoluteUri);
   Exception error=null;
   if(!string.IsNullOrWhiteSpace(repo)){
    if(!Regex.IsMatch(repo.Trim(),@"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$"))throw new InvalidDataException("GitHub 仓库格式应为 owner/repo");
    sources.Add("https://github.com/"+repo.Trim()+"/releases/latest/download/update.json");
   }
   if(sources.Count==0)throw new InvalidDataException("请先填写 GitHub 仓库或镜像清单地址");
   foreach(var u in sources){try{var m=Json.Read<UpdateManifest>(fetch(u));Validate(m);return m;}catch(Exception ex){error=ex;}}
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
  readonly Label updateStatus;readonly FlatButton check;UpdateManifest pending;string payload;bool busy,autoStarted;
  public SettingsModule(ToolContext c):base(c){
   Header("设置与更新","");
   var card=new CardPanel{Location=new System.Drawing.Point(0,124),Size=new System.Drawing.Size(936,172)};Page.Controls.Add(card);
   Ui.Label(card,"RXSend Tool",24,20,500,34,Ui.Heading);
   check=Ui.Button(card,"检查更新",752,26,160,delegate{if(payload!=null)Install();else if(pending!=null)Download();else Check();},true);
   updateStatus=Ui.Label(card,"",24,107,888,44,Ui.Small,Ui.Muted);
   var license=new CardPanel{Location=new System.Drawing.Point(0,316),Size=new System.Drawing.Size(936,96)};Page.Controls.Add(license);
   Ui.Label(license,"GNU GPL v3.0",24,16,500,30,Ui.Heading);Ui.Label(license,"Copyright © 2026 yushy",24,51,600,26,Ui.Small,Ui.Muted);Ui.Button(license,"查看许可证",752,29,160,License);
  }
  async void Check(){
   if(busy)return;
   if(string.IsNullOrWhiteSpace(C.Settings.GithubRepo)&&string.IsNullOrWhiteSpace(C.Settings.MirrorUrl)){updateStatus.Text="暂未配置可用更新源";return;}
   busy=true;check.Enabled=false;pending=null;payload=null;updateStatus.Text="正在检查更新…";string repo=C.Settings.GithubRepo,mirror=C.Settings.MirrorUrl;
   try{var result=await Task.Run(()=>Updates.Check(repo,mirror));if(Page.IsDisposed)return;if(Updates.ParseVersion(result.version)>Updates.Current){pending=result;updateStatus.Text="发现新版本 "+result.version;check.Text="下载更新";}else updateStatus.Text="已是最新版本";}
   catch(Exception ex){if(!Page.IsDisposed)updateStatus.Text=ex.Message;}
   finally{busy=false;if(!Page.IsDisposed)check.Enabled=true;}
  }
  async void Download(){if(busy||pending==null)return;busy=true;check.Enabled=false;updateStatus.Text="正在下载并校验…";var manifest=pending;try{payload=await Task.Run(()=>Updates.Download(manifest));if(Page.IsDisposed)return;updateStatus.Text="校验通过，可以安装";check.Text="安装并重启";}catch(Exception ex){if(!Page.IsDisposed)updateStatus.Text=ex.Message;}finally{busy=false;if(!Page.IsDisposed)check.Enabled=true;}}
  void Install(){if(payload==null||pending==null)return;try{C.Save();Updates.Install(payload,pending);C.Window.ExitForUpdate();}catch(Exception ex){updateStatus.Text=ex.Message;}}
  public override void Tick(){if(!autoStarted){autoStarted=true;if(C.Settings.AutoCheck&&(!string.IsNullOrWhiteSpace(C.Settings.GithubRepo)||!string.IsNullOrWhiteSpace(C.Settings.MirrorUrl)))Check();}}
  static string LicenseText(string file,string resource){string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,file);if(File.Exists(path))return File.ReadAllText(path);using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)){if(stream==null)return "";using(var reader=new StreamReader(stream))return reader.ReadToEnd();}}
  void License(){using(var f=new Form{Text="开源许可 · GPL-3.0",Size=new System.Drawing.Size(760,580),StartPosition=FormStartPosition.CenterParent}){var t=new TextBox{Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,BackColor=Ui.Card,ForeColor=Ui.Text,Font=Ui.Body};t.Text="RX Toolbox · GPL-3.0-only\r\nCopyright (c) 2026 yushy\r\n\r\n"+LicenseText("LICENSE","RxToolbox.LICENSE")+"\r\n\r\n"+LicenseText("THIRD-PARTY-NOTICES.md","RxToolbox.NOTICES");f.Controls.Add(t);f.ShowDialog(C.Window);}}
 }
}
