using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RxToolbox {
 internal static class SingleInstance {
  internal const string MutexName="Local\\RxToolbox.v1";
  internal static readonly int ExitMessage=(int)RegisterWindowMessage("RxToolbox.v1.ExitForReplacement");
  delegate bool EnumWindowProc(IntPtr window,IntPtr data);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint RegisterWindowMessage(string name);
  [DllImport("user32.dll")]static extern bool EnumWindows(EnumWindowProc callback,IntPtr data);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr window,StringBuilder text,int size);
  [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
  [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);

  internal static bool TryAcquire(Mutex mutex){try{return mutex.WaitOne(0);}catch(AbandonedMutexException){return true;}}
  internal static bool IsApplication(string path){
   try{return FileVersionInfo.GetVersionInfo(path).ProductName=="RX Toolbox"&&AssemblyName.GetAssemblyName(path).Name=="RxToolbox";}
   catch(Exception ex){if(!(ex is IOException)&&!(ex is UnauthorizedAccessException)&&!(ex is BadImageFormatException)&&!(ex is ArgumentException))throw;return false;}
  }
  internal static void ClosePrevious(){
   var windows=new Dictionary<int,IntPtr>();int currentId,session;
   using(var current=Process.GetCurrentProcess()){currentId=current.Id;session=current.SessionId;}
   EnumWindows(delegate(IntPtr window,IntPtr unused){
    var text=new StringBuilder(128);GetWindowText(window,text,text.Capacity);
    if(text.ToString()!="RXSend Tool")return true;
    uint id;GetWindowThreadProcessId(window,out id);if(id!=currentId)windows[(int)id]=window;return true;
   },IntPtr.Zero);
   foreach(var entry in windows){
    try{using(var previous=Process.GetProcessById(entry.Key)){
     if(previous.SessionId!=session||!IsApplication(previous.MainModule.FileName))continue;
     // New versions save their settings and exit; older versions ignore this message.
     if(ExitMessage!=0)PostMessage(entry.Value,ExitMessage,IntPtr.Zero,IntPtr.Zero);
     if(previous.WaitForExit(2500))continue;
     // The button also supports legacy versions which cannot receive the exit request.
     previous.Kill();if(!previous.WaitForExit(5000))throw new IOException("上一个程序尚未退出，请稍后重试。");
    }}catch(ArgumentException){}catch(InvalidOperationException){}
   }
  }
 }
 internal sealed class DuplicateInstanceDialog : Form {
  readonly Label status;readonly FlatButton closePrevious,cancel;readonly Func<Task> stop;readonly Func<bool> acquire;bool busy;
  internal DuplicateInstanceDialog(Mutex mutex):this(()=>Task.Run(()=>SingleInstance.ClosePrevious()),()=>SingleInstance.TryAcquire(mutex)){}
  internal DuplicateInstanceDialog(Func<Task> stopPrevious,Func<bool> acquireInstance){
   stop=stopPrevious;acquire=acquireInstance;
   Text="RXSend Tool · 已在运行";ClientSize=new Size(520,248);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterScreen;BackColor=Ui.Bg;ForeColor=Ui.Text;Font=Ui.Body;AutoScaleMode=AutoScaleMode.None;
   Ui.Label(this,"程序已在运行",24,22,472,34,Ui.Heading);
   Ui.Label(this,"可以关闭上一个程序，再打开当前版本。\n旧程序的自动进服监听和悬浮窗会停止。",24,68,472,56,Ui.Body,Ui.Muted);
   status=Ui.Label(this,"上一个程序也可能正在系统托盘中运行。",24,132,472,40,Ui.Small,Ui.Muted);
   closePrevious=Ui.Button(this,"关闭上一个程序",204,190,176,Replace,true);
   cancel=Ui.Button(this,"取消",392,190,104,()=>{DialogResult=DialogResult.Cancel;});cancel.DialogResult=DialogResult.Cancel;
   AcceptButton=closePrevious;CancelButton=cancel;
   FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy&&DialogResult!=DialogResult.OK)e.Cancel=true;};
   using(var graphics=CreateGraphics()){float scale=graphics.DpiX/96f;if(scale!=1)Scale(new SizeF(scale,scale));}
  }
  async void Replace(){
   if(busy)return;busy=true;closePrevious.Enabled=false;cancel.Enabled=false;status.Text="正在关闭上一个程序，请稍候…";
   try{
    await stop();var wait=Stopwatch.StartNew();
    do{
     // Mutex ownership must stay on the UI thread which will run the new main window.
     if(acquire()){DialogResult=DialogResult.OK;Close();return;}
     await Task.Delay(100);
    }while(wait.ElapsedMilliseconds<5000);
    status.Text="上一个程序尚未退出，请稍后重试，或从托盘退出。";
   }catch(Exception ex){Log.Write(ex);status.Text="关闭失败："+ex.Message;}
   finally{busy=false;if(!IsDisposed){closePrevious.Enabled=true;cancel.Enabled=true;}}
  }
 }
}
