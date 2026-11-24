using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RxToolbox {
 internal sealed class FullscreenRiskDialog : Form {
  internal const int ConsentVersion=1;
  internal const string Explanation="用途：让 64 位 GMod 在全屏模式下显示工具的准星和回合计时。仅支持 DirectX 9，属于实验性功能。\r\n\r\n风险：需要向游戏加载叠加模块，可能被游戏或服务器反作弊拦截，存在被踢出或账号处罚风险；不能保证安全或兼容。也可能引起崩溃、卡顿，或与 Steam、NVIDIA、Discord 等覆盖层冲突。\r\n\r\n限制：游戏内仅显示，不能拖动、右键或选服；请在工具主界面调整。尚未完成多服务器、长时间运行及切屏兼容验证。\r\n\r\n启用后会保存开关，下次启动工具时自动恢复，并在检测到 GMod 时加载。关闭后停止绘制，模块仍留在游戏进程内，退出 GMod 后才完全释放。";
  internal FullscreenRiskDialog(){
   Text="实验性全屏模式 · 使用前确认";ClientSize=new Size(660,530);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;BackColor=Ui.Bg;AutoScaleMode=AutoScaleMode.Dpi;
   Ui.Label(this,"请先了解用途与风险",24,18,610,38,Ui.Heading);
   var text=new TextBox{Text=Explanation,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=Ui.Bg,ForeColor=Ui.Text,Font=Ui.Body,Bounds=new Rectangle(24,68,612,330),TabStop=false};Controls.Add(text);
   var confirm=Ui.Button(this,"了解风险并启用",418,464,218,()=>{DialogResult=DialogResult.OK;Close();},true);confirm.Enabled=false;
   Ui.Check(this,"我已阅读并接受上述风险及下次启动自动恢复",24,414,false,v=>confirm.Enabled=v).Width=610;
   var cancel=Ui.Button(this,"取消",282,464,120,()=>{DialogResult=DialogResult.Cancel;Close();});CancelButton=cancel;AcceptButton=cancel;ActiveControl=cancel;
  }
 }
 internal sealed class FullscreenOverlay : IDisposable {
  internal const int Slot=2048*2048*4,Header=128,Capacity=Header+Slot*2;
  readonly ToolContext c;MemoryMappedFile map;MemoryMappedViewAccessor view;Mutex gate;Process game;
  int crossRevision=-1,timerRevision=-1;bool crossVisible,timerVisible,disposed,loading;int failedPid;DateTime nextFind;
  public string Status="已关闭";
  [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
  public bool Covering {get;private set;}
  public bool Enabled {get{return IsAuthorized(c.Settings);}}
  internal static bool IsAuthorized(Settings s){return s.ExperimentalFullscreen&&s.FullscreenConsentVersion==FullscreenRiskDialog.ConsentVersion;}
  public FullscreenOverlay(ToolContext context){c=context;}
  public void Tick(){
   if(disposed)return;
   if(!Enabled){CloseMapping();RestoreExternal();Status="已关闭";return;}
   try{
    if(game!=null&&game.HasExited)CloseMapping();
    if(game==null)RestoreExternal();
    if(game==null&&!loading&&DateTime.UtcNow>=nextFind){nextFind=DateTime.UtcNow.AddSeconds(3);FindGame();}
    if(view==null)return;
    bool owned=false;try{try{owned=gate.WaitOne(0);}catch(AbandonedMutexException){owned=true;}if(!owned)return;
     view.Write(0,0x52584653);view.Write(4,1);view.Write(8,1);view.Write(12,unchecked((uint)Environment.TickCount));
     Publish(0,c.Crosshair,c.Settings.CrosshairVisible,ref crossRevision,ref crossVisible,c.Settings.CrosshairX,c.Settings.CrosshairY);
     var screen=Screen.FromHandle(game.MainWindowHandle).Bounds;
     Publish(1,c.Timer,c.Settings.TimerVisible,ref timerRevision,ref timerVisible,c.Settings.TimerX-screen.Left,c.Settings.TimerY-screen.Top);
     int state=view.ReadInt32(16);Covering=state==2&&unchecked((uint)Environment.TickCount-view.ReadUInt32(72))<2500&&GetForegroundWindow()==game.MainWindowHandle;RestoreExternal();if(!loading)Status=state==2?"已连接 GMod · 全屏叠加运行中":state==-3?"游戏内绘制失败（0x"+view.ReadUInt32(76).ToString("X8")+"）":state==3?"已连接 GMod · 请开启准星或悬浮计时":state<0?"叠加初始化失败 · 请关闭模式并重启 GMod":"已连接 GMod · 等待全屏画面";
    }finally{if(owned)gate.ReleaseMutex();}
   }catch(Exception ex){Status="全屏叠加失败："+ex.Message;Log.Write(ex);if(game!=null)failedPid=game.Id;CloseMapping();}
  }
  void Publish(int index,LayeredOverlay overlay,bool visible,ref int revision,ref bool wasVisible,int x,int y){
   int offset=32+index*20;view.Write(offset+8,x);view.Write(offset+12,y);
   Bitmap bitmap=overlay.Surface;if(!visible||bitmap==null){view.Write(offset,0);view.Write(offset+4,0);wasVisible=false;return;}
   if(bitmap.Width>2048||bitmap.Height>2048)throw new InvalidDataException("叠加图像超过尺寸限制");
   if(revision!=overlay.SurfaceRevision||!wasVisible){
    var bits=bitmap.LockBits(new Rectangle(Point.Empty,bitmap.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
    try{byte[] row=new byte[bitmap.Width*4];for(int yrow=0;yrow<bitmap.Height;yrow++){Marshal.Copy(IntPtr.Add(bits.Scan0,yrow*bits.Stride),row,0,row.Length);view.WriteArray(Header+index*Slot+(long)yrow*row.Length,row,0,row.Length);}}
    finally{bitmap.UnlockBits(bits);}
    revision=overlay.SurfaceRevision;view.Write(offset,bitmap.Width);view.Write(offset+4,bitmap.Height);view.Write(offset+16,unchecked(view.ReadInt32(offset+16)+1));wasVisible=true;
   }
  }
  async void FindGame(){
   int pid=0;try{
   Process chosen=null;foreach(var p in Process.GetProcessesByName("gmod_win64")){if(p.MainWindowHandle!=IntPtr.Zero){if(chosen!=null){chosen.Dispose();p.Dispose();Status="检测到多个 GMod 窗口，请只保留一个";return;}chosen=p;}else p.Dispose();}
   if(chosen==null){Status="已启用 · 等待启动 64 位 GMod";return;}
   if(chosen.Id==failedPid){chosen.Dispose();return;}
   game=chosen;pid=game.Id;gate=new Mutex(false,"Local\\RxFullscreen-lock-v1-"+pid);map=MemoryMappedFile.CreateOrOpen("Local\\RxFullscreen-v1-"+pid,Capacity);view=map.CreateViewAccessor();crossRevision=timerRevision=-1;crossVisible=timerVisible=false;
   loading=true;Status="正在连接 GMod…";
   string dll=Extract("RxFullscreen.dll"),helper=Extract("RxFullscreenLoader.exe");int code=await Task.Run(()=>{
     using(var p=Process.Start(new ProcessStartInfo(helper,pid+" \""+dll+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden})){if(!p.WaitForExit(20000))throw new IOException("加载超时，请重启 GMod 后重试");return p.ExitCode;}
    });if(disposed)return;if(!Enabled){CloseMapping();return;}if(code!=0){failedPid=pid;CloseMapping();Status="连接失败（"+code+"）· 请关闭模式并重启 GMod 后重试";}
   }catch(Exception ex){failedPid=pid;CloseMapping();Status="连接失败："+ex.Message;Log.Write(ex);}finally{loading=false;}
  }
  static string Extract(string name){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("RxToolbox."+name)){
   if(stream==null)throw new IOException("当前构建缺少全屏组件");using(var bytes=new MemoryStream()){stream.CopyTo(bytes);byte[] data=bytes.ToArray();string folder=Path.Combine(Settings.Folder,"fullscreen",Updates.Hash(data));Directory.CreateDirectory(folder);string file=Path.Combine(folder,name);if(!File.Exists(file)||Updates.Hash(File.ReadAllBytes(file))!=Updates.Hash(data))File.WriteAllBytes(file,data);return file;}
  }}
  public void Disable(){c.Settings.ExperimentalFullscreen=false;failedPid=0;CloseMapping();RestoreExternal();Status="已关闭 · 退出 GMod 后完全释放模块";}
  void RestoreExternal(){if(c.Crosshair.Visible!=(c.Settings.CrosshairVisible&&!Covering)){if(c.Settings.CrosshairVisible&&!Covering)c.Crosshair.Show();else c.Crosshair.Hide();}if(c.Timer.Visible!=(c.Settings.TimerVisible&&!Covering)){if(c.Settings.TimerVisible&&!Covering)c.Timer.Show();else c.Timer.Hide();}}
  void CloseMapping(){Covering=false;if(view!=null){bool held=false;try{try{held=gate.WaitOne(100);}catch(AbandonedMutexException){held=true;}if(held)view.Write(8,0);}finally{if(held)gate.ReleaseMutex();}view.Dispose();view=null;}if(map!=null){map.Dispose();map=null;}if(gate!=null){gate.Dispose();gate=null;}if(game!=null){game.Dispose();game=null;}}
  public void Dispose(){disposed=true;CloseMapping();}
 }
}
