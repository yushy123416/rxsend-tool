using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace RxToolbox {
 public sealed class NvidiaSettingBackup {public uint Id,Value;public bool UserValue;}
 public sealed class NvidiaFilterBackup {
  public int Version=1;public string Executable,Profile,AddedApplication;public bool Applied;
  public List<NvidiaSettingBackup> Settings=new List<NvidiaSettingBackup>();
 }
 internal sealed class NvidiaEnvironment {
  public string Gpu="未检测",App="未检测",AppPath="",Game="",Error="";
  public bool HasGpu;public bool Ready{get{return HasGpu&&File.Exists(AppPath)&&File.Exists(Game)&&string.IsNullOrEmpty(Error);}}
 }
 internal static class NvidiaFilters {
  // Public NVIDIA NvApiDriverSettings.h: ANSEL_ENABLE, ANSEL_ALLOWLISTED, ANSEL_ALLOW.
  internal static readonly uint[] SettingIds={0x1075D972,0x1085DA8A,0x1035DB89};
  internal const string OwnProfile="RXSend Tool - Garry's Mod";
  internal static string BackupPath{get{return Path.Combine(Settings.Folder,"nvidia-gmod-backup.json");}}
  // DRS stores application paths in lowercase with forward slashes. Keep the full
  // path comparison so an executable with the same name elsewhere cannot match.
  internal static bool SameApplicationPath(string left,string right){return !string.IsNullOrEmpty(left)&&!string.IsNullOrEmpty(right)&&string.Equals(left.Replace('/','\\'),right.Replace('/','\\'),StringComparison.OrdinalIgnoreCase);}
  internal static bool IsGmodProfile(string name){return name==OwnProfile||Regex.Replace(name??"","[^a-zA-Z0-9]","").Equals("garrysmod",StringComparison.OrdinalIgnoreCase);}
  internal static string ValidateGame(string path){
   if(string.IsNullOrWhiteSpace(path)||!File.Exists(path))throw new InvalidOperationException("请选择已安装的 GMod 游戏程序。");
   path=Path.GetFullPath(path);string name=Path.GetFileName(path).ToLowerInvariant();
   if(name!="gmod.exe"&&name!="gmod_win64.exe"&&name!="hl2.exe")throw new InvalidOperationException("请选择 gmod.exe、gmod_win64.exe 或旧版 hl2.exe。");
   var directory=new DirectoryInfo(Path.GetDirectoryName(path));for(int i=0;i<4&&directory!=null;i++,directory=directory.Parent)if(File.Exists(Path.Combine(directory.FullName,"garrysmod","gameinfo.txt")))return path;
   throw new InvalidOperationException("没有找到 garrysmod/gameinfo.txt，无法确认这是 GMod 安装目录。");
  }
  internal static IEnumerable<string> LibraryPaths(string steam){
   var paths=new List<string>();if(string.IsNullOrWhiteSpace(steam))return paths;paths.Add(steam);
   string file=Path.Combine(steam,"steamapps","libraryfolders.vdf");if(File.Exists(file))foreach(Match match in Regex.Matches(File.ReadAllText(file),"\"(?:path|[0-9]+)\"\\s+\"([^\"]+)\"")){string value=match.Groups[1].Value.Replace("\\\\","\\");if(Path.IsPathRooted(value))paths.Add(value);}
   return paths.Distinct(StringComparer.OrdinalIgnoreCase);
  }
  static string FindGame(){
   string steam=Convert.ToString(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam","SteamPath",null));
   foreach(string library in LibraryPaths(steam)){
    string folder="GarrysMod",manifest=Path.Combine(library,"steamapps","appmanifest_4000.acf");
    if(File.Exists(manifest)){var match=Regex.Match(File.ReadAllText(manifest),"\"installdir\"\\s+\"([^\"]+)\"");if(match.Success&&Path.GetFileName(match.Groups[1].Value)==match.Groups[1].Value)folder=match.Groups[1].Value;}
    string root=Path.Combine(library,"steamapps","common",folder);
    foreach(string relative in new[]{"gmod_win64.exe",@"bin\win64\gmod.exe","gmod.exe","hl2.exe"}){string path=Path.Combine(root,relative);try{return ValidateGame(path);}catch(InvalidOperationException){}}
   }return "";
  }
  static string FindApp(){
   foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})using(var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,view))using(var key=root.OpenSubKey(@"SOFTWARE\NVIDIA Corporation\Global\NvApp")){
    if(key==null||Convert.ToString(key.GetValue("Installed"))!="1")continue;string path=Convert.ToString(key.GetValue("FullPath"));
    if(File.Exists(path)&&string.Equals(Path.GetFileName(path),"NVIDIA App.exe",StringComparison.OrdinalIgnoreCase))return path;
   }return "";
  }
  internal static NvidiaEnvironment Detect(string selected){
   var state=new NvidiaEnvironment();try{using(var api=new NvidiaApi()){state.Gpu=api.Gpu();state.HasGpu=true;}}catch(Exception e){state.Gpu=e.Message;}
   try{state.AppPath=FindApp();state.App=state.AppPath==""?"未检测到 NVIDIA App（GeForce Experience 不算）":"NVIDIA App "+FileVersionInfo.GetVersionInfo(state.AppPath).ProductVersion;}catch(Exception e){state.App="检测失败："+e.Message;}
   try{state.Game=string.IsNullOrEmpty(selected)?FindGame():ValidateGame(selected);}catch(Exception e){state.Error=e.Message;}
   return state;
  }
  static void EnsureClosed(){foreach(string name in new[]{"gmod","gmod_win64","hl2"})foreach(var process in Process.GetProcessesByName(name))using(process)throw new InvalidOperationException("请先退出 GMod，再修改或还原滤镜配置。");}
  internal static void ValidateBackup(NvidiaFilterBackup backup){
   if(backup==null||backup.Version!=1||!IsGmodProfile(backup.Profile)||string.IsNullOrWhiteSpace(backup.Executable)||backup.Settings==null||backup.Settings.Count!=SettingIds.Length||backup.Settings.Any(s=>s==null)||!backup.Settings.Select(s=>s.Id).OrderBy(x=>x).SequenceEqual(SettingIds.OrderBy(x=>x)))throw new InvalidOperationException("滤镜备份无效，已停止操作并保留文件。");
   if(!string.IsNullOrEmpty(backup.AddedApplication)&&!SameApplicationPath(backup.AddedApplication,backup.Executable))throw new InvalidOperationException("滤镜备份中的程序路径不匹配。");
  }
  internal static void WriteBackup(NvidiaFilterBackup backup){Directory.CreateDirectory(Settings.Folder);string temp=BackupPath+".tmp";File.WriteAllText(temp,Json.Write(backup));if(File.Exists(BackupPath))File.Replace(temp,BackupPath,null);else File.Move(temp,BackupPath);}
  internal static void Apply(string executable){
   executable=ValidateGame(executable);var environment=Detect(executable);if(!environment.Ready)throw new InvalidOperationException("需要检测到 NVIDIA 显卡、可用驱动和 NVIDIA App 后才能启用。");EnsureClosed();
   if(File.Exists(BackupPath))throw new InvalidOperationException("已有配置备份，请先还原，再重新尝试启用。备份未被覆盖。");
   using(var api=new NvidiaApi()){
    api.Open();StageApply(api,executable,WriteBackup);
   }
   using(var api=new NvidiaApi()){api.Open();string registered;var profile=api.Application(executable,out registered);if(profile==IntPtr.Zero||!IsGmodProfile(api.ProfileName(profile))||SettingIds.Any(id=>{var s=api.Read(profile,id);return !s.UserValue||s.Value!=1;}))throw new InvalidOperationException("写入后的驱动配置校验失败，请使用还原按钮恢复原配置。");}
   var saved=Json.Read<NvidiaFilterBackup>(File.ReadAllText(BackupPath));saved.Applied=true;WriteBackup(saved);
  }
  internal static void StageApply(INvidiaProfileApi api,string executable,Action<NvidiaFilterBackup> persist){
    string registered;IntPtr profile=api.Application(executable,out registered);bool add=profile==IntPtr.Zero;
    if(!add&&!IsGmodProfile(api.ProfileName(profile)))throw new InvalidOperationException("此程序使用其他游戏的驱动配置。为避免影响其他游戏，已停止修改。");
    if(add){profile=api.Profile(OwnProfile);if(profile==IntPtr.Zero)profile=api.CreateProfile(OwnProfile);}
    var backup=new NvidiaFilterBackup{Executable=executable,Profile=api.ProfileName(profile),AddedApplication=add?executable:null};
    foreach(uint id in SettingIds)backup.Settings.Add(api.Read(profile,id));
    // Save recovery information BEFORE any persistent driver change. A failed save keeps it for recovery.
    persist(backup);
    if(add)api.AddApplication(profile,executable);foreach(uint id in SettingIds)api.Write(profile,id,1);api.Save();
  }
  internal static void Restore(){
   var backup=Json.Read<NvidiaFilterBackup>(File.ReadAllText(BackupPath));ValidateBackup(backup);EnsureClosed();
   using(var api=new NvidiaApi()){
    api.Gpu();api.Open();if(!StageRestore(api,backup)){File.Delete(BackupPath);return;}
   }
   // Keep the backup until a fresh DRS session confirms restoration.
   using(var api=new NvidiaApi()){api.Open();var profile=api.Profile(backup.Profile);if(profile==IntPtr.Zero)throw new InvalidOperationException("还原校验失败，备份已保留。");foreach(var item in backup.Settings){var current=api.Read(profile,item.Id);if(current.UserValue!=item.UserValue||(item.UserValue&&current.Value!=item.Value))throw new InvalidOperationException("还原校验失败，备份已保留。");}if(!string.IsNullOrEmpty(backup.AddedApplication)){string registered;var mapped=api.Application(backup.Executable,out registered);if(mapped==profile)throw new InvalidOperationException("游戏程序关联的还原校验失败，备份已保留。");}}
   File.Delete(BackupPath);
  }
  internal static bool StageRestore(INvidiaProfileApi api,NvidiaFilterBackup backup){
    ValidateBackup(backup);IntPtr profile=api.Profile(backup.Profile);
    if(profile==IntPtr.Zero){if(backup.Applied)throw new InvalidOperationException("原驱动配置已不存在，可能已更新或重装驱动。备份已保留。");return false;}
    // Refuse to overwrite unrelated later edits. Failed/incomplete application is allowed to contain old values.
    foreach(var item in backup.Settings){var current=api.Read(profile,item.Id);bool old=current.UserValue==item.UserValue&&(!item.UserValue||current.Value==item.Value);bool ours=current.UserValue&&current.Value==1;if(!old&&!ours)throw new InvalidOperationException("滤镜配置已被其他程序改动，已停止还原并保留备份。");}
    string added=null;
    if(!string.IsNullOrEmpty(backup.AddedApplication)){string registered;IntPtr mapped=api.Application(backup.Executable,out registered);if(mapped!=IntPtr.Zero){if(mapped!=profile||!SameApplicationPath(registered,backup.AddedApplication))throw new InvalidOperationException("游戏程序的驱动关联已改变，已停止还原。");added=registered;}}
    foreach(var item in backup.Settings){if(item.UserValue)api.Write(profile,item.Id,item.Value);else api.DeleteSetting(profile,item.Id);}
    if(added!=null)api.DeleteApplication(profile,added);
    api.Save();
    return true;
  }
  internal static string ResultPath(string token){Guid parsed;if(!Guid.TryParseExact(token,"N",out parsed))throw new ArgumentException("无效的操作编号");return Path.Combine(Settings.Folder,"nvidia-result-"+token+".json");}
  internal static void RunHelper(string[] args){
   if(args.Length!=4)throw new ArgumentException("无效的滤镜操作参数");string result=ResultPath(args[3]);string message;int code=0;
   try{using(var identity=WindowsIdentity.GetCurrent())if(!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))throw new InvalidOperationException("需要管理员权限才能写入 NVIDIA 驱动配置。");
    using(var operation=new System.Threading.Mutex(false,"Local\\RxToolbox.NvidiaFilters")){
     if(!operation.WaitOne(0))throw new InvalidOperationException("另一项滤镜配置操作正在进行，请稍后重试。");
     try{if(args[1]=="apply"){Apply(args[2]);message="已写入并校验驱动配置。请开启 NVIDIA 游戏内覆盖和“游戏滤镜和照片模式”，重启 GMod 后按 Alt+F3 验证。若仍提示不支持，说明此版本 NVIDIA App / 驱动限制了该游戏。";}else if(args[1]=="restore"){Restore();message="已还原原有驱动设置。请重启 GMod。";}else throw new ArgumentException("未知的滤镜操作");}finally{operation.ReleaseMutex();}
    }
   }catch(Exception e){Log.Write(e);message=e.Message;code=1;}
   Directory.CreateDirectory(Settings.Folder);File.WriteAllText(result,Json.Write(message));Environment.ExitCode=code;
  }
 }
}
