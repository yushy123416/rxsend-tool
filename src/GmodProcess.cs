using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace RxToolbox {
 // A filename is only a discovery hint. Validate the running architecture and
 // installation marker before allowing the fullscreen loader to use the process.
 internal static class GmodProcess {
  internal static readonly string[] Names={"gmod_win64","gmod"};
  [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
  [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool QueryFullProcessImageName(IntPtr process,int flags,StringBuilder name,ref int size);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool IsWow64Process2(IntPtr process,out ushort processMachine,out ushort nativeMachine);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool IsWow64Process(IntPtr process,out bool wow);
  [StructLayout(LayoutKind.Sequential)]struct SystemInfo {public ushort Architecture,Reserved;public uint PageSize;public IntPtr Minimum,Maximum,Mask;public uint Processors,Type,Granularity;public ushort Level,Revision;}
  [DllImport("kernel32.dll")]static extern void GetNativeSystemInfo(out SystemInfo info);
  internal static bool IsSupportedImage(string path){
   if(string.IsNullOrEmpty(path))return false;
   string name=Path.GetFileName(path);
   return name.Equals("gmod.exe",StringComparison.OrdinalIgnoreCase)||name.Equals("gmod_win64.exe",StringComparison.OrdinalIgnoreCase);
  }
  internal static bool HasGameMarker(string path){
   if(!IsSupportedImage(path)||!File.Exists(path))return false;
   var directory=new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path)));
   for(int i=0;i<4&&directory!=null;i++,directory=directory.Parent)if(File.Exists(Path.Combine(directory.FullName,"garrysmod","gameinfo.txt")))return true;
   return false;
  }
  internal static bool IsAmd64(IntPtr process){
   try{ushort machine,native;if(!IsWow64Process2(process,out machine,out native))return false;return (machine==0?native:machine)==0x8664;}
   catch(EntryPointNotFoundException){bool wow;SystemInfo info;GetNativeSystemInfo(out info);return info.Architecture==9&&IsWow64Process(process,out wow)&&!wow;}
  }
  internal static bool Validate(int pid,out string reason){
   reason="检测到 GMod，但无法验证进程（权限不足或进程已退出）";
   IntPtr handle=OpenProcess(0x1000,false,pid);if(handle==IntPtr.Zero)return false;
   try{
    if(!IsAmd64(handle)){reason="检测到非 x64 游戏 · 请在 Steam 选择 64 位 GMod";return false;}
    var path=new StringBuilder(32768);int size=path.Capacity;
    if(!QueryFullProcessImageName(handle,0,path,ref size))return false;
    if(!HasGameMarker(path.ToString())){reason="检测到同名程序，但无法验证 GMod 安装目录";return false;}
    reason=null;return true;
   }finally{CloseHandle(handle);}
  }
  internal static List<Process> FindTargets(out string reason){
   var targets=new List<Process>();reason=null;int session;using(var current=Process.GetCurrentProcess())session=current.SessionId;
   foreach(string name in Names)foreach(var process in Process.GetProcessesByName(name)){
    bool keep=false;
    try{
     if(process.SessionId!=session||process.MainWindowHandle==IntPtr.Zero)continue;
     string rejected;if(!Validate(process.Id,out rejected)){reason=rejected;continue;}
     targets.Add(process);keep=true;
    }catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
    finally{if(!keep)process.Dispose();}
   }
   return targets;
  }
 }
}
