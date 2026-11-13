using System;
using System.Runtime.InteropServices;
using System.Text;

namespace RxToolbox {
 internal interface INvidiaProfileApi {
  IntPtr Application(string path,out string registeredName);IntPtr Profile(string name);string ProfileName(IntPtr profile);IntPtr CreateProfile(string name);
  void AddApplication(IntPtr profile,string path);void DeleteApplication(IntPtr profile,string name);
  NvidiaSettingBackup Read(IntPtr profile,uint id);void Write(IntPtr profile,uint id,uint value);void DeleteSetting(IntPtr profile,uint id);void Save();
 }
 // Layouts and entry points follow NVIDIA/nvapi (nvapi.h, nvapi_interface.h).
 // Only DWORD settings are exposed; inherited/predefined values are never restored as user overrides.
 internal sealed class NvidiaApi : IDisposable,INvidiaProfileApi {
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadLibraryEx(string file,IntPtr reserved,uint flags);
  [DllImport("kernel32.dll",CharSet=CharSet.Ansi)] static extern IntPtr GetProcAddress(IntPtr library,string name);
  [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr library);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Query(uint id);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Simple();
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int NewSession(out IntPtr session);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int SessionCall(IntPtr session);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Gpus([Out] IntPtr[] handles,out uint count);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int GpuName(IntPtr gpu,[Out] StringBuilder name);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl,CharSet=CharSet.Unicode)] delegate int FindApp(IntPtr session,string name,out IntPtr profile,ref DrsApplication app);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl,CharSet=CharSet.Unicode)] delegate int FindProfile(IntPtr session,string name,out IntPtr profile);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int ProfileInfo(IntPtr session,IntPtr profile,ref DrsProfile info);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int NewProfile(IntPtr session,ref DrsProfile info,out IntPtr profile);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int AppCall(IntPtr session,IntPtr profile,ref DrsApplication app);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl,CharSet=CharSet.Unicode)] delegate int RemoveApp(IntPtr session,IntPtr profile,string name);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int ReadSetting(IntPtr session,IntPtr profile,uint id,ref DrsSetting setting);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int WriteSetting(IntPtr session,IntPtr profile,ref DrsSetting setting);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int RemoveSetting(IntPtr session,IntPtr profile,uint id);
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode,Pack=4)] internal struct DrsApplication {
   public uint Version,Predefined;
   [MarshalAs(UnmanagedType.ByValTStr,SizeConst=2048)] public string Name;
   [MarshalAs(UnmanagedType.ByValTStr,SizeConst=2048)] public string Friendly;
   [MarshalAs(UnmanagedType.ByValTStr,SizeConst=2048)] public string Launcher;
   [MarshalAs(UnmanagedType.ByValTStr,SizeConst=2048)] public string Folder;
   public uint Flags;
   [MarshalAs(UnmanagedType.ByValTStr,SizeConst=2048)] public string Command;
   public static DrsApplication Create(string name){return new DrsApplication{Version=(uint)Marshal.SizeOf(typeof(DrsApplication))|0x40000,Name=name,Friendly="Garry's Mod",Launcher="",Folder="",Command=""};}
  }
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode,Pack=4)] internal struct DrsProfile {
   public uint Version;
   [MarshalAs(UnmanagedType.ByValTStr,SizeConst=2048)] public string Name;
   public uint GpuSupport,Predefined,Apps,Settings;
   public static DrsProfile Create(string name){return new DrsProfile{Version=(uint)Marshal.SizeOf(typeof(DrsProfile))|0x10000,Name=name};}
  }
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode,Pack=4)] internal struct DrsSetting {
   public uint Version;
   [MarshalAs(UnmanagedType.ByValTStr,SizeConst=2048)] public string Name;
   public uint Id,Type,Location,CurrentPredefined,PredefinedValid;
   [MarshalAs(UnmanagedType.ByValArray,SizeConst=4100)] public byte[] Predefined;
   [MarshalAs(UnmanagedType.ByValArray,SizeConst=4100)] public byte[] Current;
   public static DrsSetting Create(uint id,uint value){var s=new DrsSetting{Version=(uint)Marshal.SizeOf(typeof(DrsSetting))|0x10000,Name="",Id=id,Predefined=new byte[4100],Current=new byte[4100]};Buffer.BlockCopy(BitConverter.GetBytes(value),0,s.Current,0,4);return s;}
  }
  IntPtr library,session;Query query;bool initialized;
  T Fn<T>(uint id) where T:class {var p=query(id);if(p==IntPtr.Zero)throw new InvalidOperationException("当前 NVIDIA 驱动不支持此功能，请更新驱动。");return Marshal.GetDelegateForFunctionPointer(p,typeof(T)) as T;}
  static void Check(int code){if(code==0)return;if(code==-137||code==-175)throw new InvalidOperationException("驱动拒绝写入，请使用管理员权限重试。");throw new InvalidOperationException("NVIDIA 驱动操作失败（NVAPI "+code+"），未确认配置生效。");}
  public NvidiaApi(){try{library=LoadLibraryEx(Environment.Is64BitProcess?"nvapi64.dll":"nvapi.dll",IntPtr.Zero,0x800);if(library==IntPtr.Zero)throw new InvalidOperationException("未检测到 NVIDIA 驱动，请确认使用 N 卡并安装驱动。");var p=GetProcAddress(library,"nvapi_QueryInterface");if(p==IntPtr.Zero)throw new InvalidOperationException("NVIDIA 驱动接口不可用。");query=(Query)Marshal.GetDelegateForFunctionPointer(p,typeof(Query));Check(Fn<Simple>(0x0150e828)());initialized=true;}catch{Dispose();throw;}}
  public string Gpu(){var handles=new IntPtr[64];uint count;Check(Fn<Gpus>(0xe5ac921f)(handles,out count));if(count==0)throw new InvalidOperationException("未检测到可用 NVIDIA 显卡。");var names=new System.Collections.Generic.List<string>();for(int i=0;i<count&&i<handles.Length;i++){var name=new StringBuilder(64);Check(Fn<GpuName>(0xceee8e9f)(handles[i],name));names.Add(name.ToString());}return string.Join(" / ",names.ToArray());}
  public void Open(){Check(Fn<NewSession>(0x0694d52e)(out session));Check(Fn<SessionCall>(0x375dbd6b)(session));}
  public void Save(){Check(Fn<SessionCall>(0xfcbc7e14)(session));}
  public IntPtr Application(string path,out string registeredName){var app=DrsApplication.Create("");IntPtr profile;int code=Fn<FindApp>(0xeee566b2)(session,path,out profile,ref app);registeredName=app.Name;if(code==-166)return IntPtr.Zero;Check(code);return profile;}
  public IntPtr Profile(string name){IntPtr profile;int code=Fn<FindProfile>(0x7e4a9a0b)(session,name,out profile);if(code==-163)return IntPtr.Zero;Check(code);return profile;}
  public string ProfileName(IntPtr profile){var info=DrsProfile.Create("");Check(Fn<ProfileInfo>(0x61cd6fd6)(session,profile,ref info));return info.Name;}
  public IntPtr CreateProfile(string name){var info=DrsProfile.Create(name);IntPtr profile;Check(Fn<NewProfile>(0xcc176068)(session,ref info,out profile));return profile;}
  public void AddApplication(IntPtr profile,string path){var app=DrsApplication.Create(path);Check(Fn<AppCall>(0x4347a9de)(session,profile,ref app));}
  public void DeleteApplication(IntPtr profile,string name){Check(Fn<RemoveApp>(0x2c694bc6)(session,profile,name));}
  public NvidiaSettingBackup Read(IntPtr profile,uint id){var s=DrsSetting.Create(id,0);int code=Fn<ReadSetting>(0x73bf8338)(session,profile,id,ref s);if(code==-160)return new NvidiaSettingBackup{Id=id};Check(code);if(s.Type!=0)throw new InvalidOperationException("驱动返回了不兼容的滤镜配置类型，已停止操作。");return new NvidiaSettingBackup{Id=id,UserValue=s.Location==0&&s.CurrentPredefined==0,Value=BitConverter.ToUInt32(s.Current,0)};}
  public void Write(IntPtr profile,uint id,uint value){var s=DrsSetting.Create(id,value);Check(Fn<WriteSetting>(0x577dd202)(session,profile,ref s));}
  public void DeleteSetting(IntPtr profile,uint id){int code=Fn<RemoveSetting>(0xe4a26362)(session,profile,id);if(code!=-160)Check(code);}
  public void Dispose(){if(query!=null){if(session!=IntPtr.Zero){Fn<SessionCall>(0xdad9cff8)(session);session=IntPtr.Zero;}if(initialized){Fn<Simple>(0xd22bdd7e)();initialized=false;}}if(library!=IntPtr.Zero){FreeLibrary(library);library=IntPtr.Zero;}}
 }
}
