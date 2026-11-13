using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RxToolbox {
 internal sealed class NvidiaFiltersModule : ToolModule {
  public override string Title{get{return "NVIDIA 滤镜";}}public override string Symbol{get{return "◈";}}
  readonly Label gpu,app,game,result,backup;
  readonly FlatButton detect,choose,apply,restore,open;
  NvidiaEnvironment environment;string selected="";bool busy,checkedOnce,disposed;
  public NvidiaFiltersModule(ToolContext c):base(c){
   Header("NVIDIA 游戏滤镜","GMod 实验性兼容配置");
   Ui.Label(Page,"GMod 实验性兼容配置",2,78,900,30,Ui.Body,Ui.Muted);
   var card=new CardPanel{Location=new Point(0,124),Size=new Size(936,208)};Page.Controls.Add(card);
   Ui.Label(card,"使用前检查",24,16,850,30,Ui.Heading);
   Ui.Label(card,"NVIDIA 显卡",24,58,140);gpu=Ui.Label(card,"等待检测",172,58,736,32);
   Ui.Label(card,"NVIDIA App",24,102,140);app=Ui.Label(card,"等待检测",172,102,736,32);
   Ui.Label(card,"游戏程序",24,146,140);game=Ui.Label(card,"等待检测",172,146,736,42,Ui.Small,Ui.Muted);
   detect=Ui.Button(Page,"重新检测",0,350,130,()=>RefreshEnvironment());
   choose=Ui.Button(Page,"选择游戏程序",144,350,156,ChooseGame);
   open=Ui.Button(Page,"打开 NVIDIA App",314,350,180,OpenApp);
   var explanation=new CardPanel{Location=new Point(0,410),Size=new Size(936,162)};Page.Controls.Add(explanation);
   Ui.Label(explanation,"兼容性说明",24,16,850,28,Ui.Heading);
   Ui.Label(explanation,"尝试为所选 GMod 程序开启驱动中的 Ansel / 滤镜许可，自动备份原设置。\r\nNVIDIA App 仍可能限制游戏；配置写入成功不代表滤镜一定可用。\r\n请先退出游戏。启用后，在 NVIDIA App 开启游戏内覆盖及游戏滤镜，再进游戏按 Alt+F3。",24,52,884,96,Ui.Body,Ui.Muted);
   apply=Ui.Button(Page,"尝试启用（实验性）",0,594,220,()=>Operate(false),true);
   restore=Ui.Button(Page,"还原原配置",234,594,156,()=>Operate(true));
   backup=Ui.Label(Page,"",408,596,516,36,Ui.Small,Ui.Muted);
   result=Ui.Label(Page,"首次使用会自动检测环境。修改驱动配置时，Windows 会请求管理员权限。",0,650,930,94,Ui.Body,Ui.Muted);
   UpdateButtons();
  }
  async void RefreshEnvironment(){
   if(busy||disposed)return;busy=true;checkedOnce=true;UpdateButtons();
   try{environment=await Task.Run(()=>NvidiaFilters.Detect(selected));if(disposed)return;gpu.Text=environment.Gpu;gpu.ForeColor=environment.HasGpu?Ui.Text:Ui.Muted;app.Text=environment.App;game.Text=string.IsNullOrEmpty(environment.Game)?"未找到 GMod，请手动选择游戏程序":environment.Game;
    if(!string.IsNullOrEmpty(environment.Error))result.Text=environment.Error;
   }catch(Exception e){if(!disposed)result.Text="检测失败："+e.Message;}finally{busy=false;if(!disposed)UpdateButtons();}
  }
  void UpdateButtons(){
   detect.Enabled=choose.Enabled=!busy;open.Enabled=!busy&&environment!=null&&File.Exists(environment.AppPath);
   bool saved=File.Exists(NvidiaFilters.BackupPath);apply.Enabled=!busy&&!saved&&environment!=null&&environment.Ready;
   restore.Enabled=!busy&&saved&&environment!=null&&environment.HasGpu;
   backup.Text=saved?"已有原配置备份 · 还原后可重新启用":"未修改驱动配置";
   if(busy)backup.Text="正在处理，请稍候…";
  }
  void ChooseGame(){using(var dialog=new OpenFileDialog{Title="选择 GMod 游戏程序",Filter="GMod 程序|gmod.exe;gmod_win64.exe;hl2.exe",CheckFileExists=true}){if(dialog.ShowDialog(C.Window)!=DialogResult.OK)return;try{selected=NvidiaFilters.ValidateGame(dialog.FileName);RefreshEnvironment();}catch(Exception e){result.Text=e.Message;}}}
  void OpenApp(){try{if(environment!=null&&File.Exists(environment.AppPath))Process.Start(new ProcessStartInfo(environment.AppPath){UseShellExecute=true});}catch(Exception e){result.Text="无法打开 NVIDIA App："+e.Message;}}
  async void Operate(bool undo){
   if(busy||environment==null)return;busy=true;UpdateButtons();string token=Guid.NewGuid().ToString("N"),resultPath=NvidiaFilters.ResultPath(token);
   try{
    string arguments="--nvidia-filters "+(undo?"restore":"apply")+" \""+(undo?"-":environment.Game)+"\" "+token;
    using(var process=Process.Start(new ProcessStartInfo(Application.ExecutablePath,arguments){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden})){
     if(process==null)throw new InvalidOperationException("无法启动滤镜配置助手。");await Task.Run(()=>process.WaitForExit());
     string message=File.Exists(resultPath)?Json.Read<string>(File.ReadAllText(resultPath)):"未收到配置结果。请重新检测；如有备份，可使用还原按钮。";
     if(!disposed)result.Text=message;
    }
   }catch(Win32Exception e){if(!disposed)result.Text=e.NativeErrorCode==1223?"已取消管理员授权，配置助手未执行。":"无法执行："+e.Message;}
   catch(Exception e){Log.Write(e);if(!disposed)result.Text=e.Message;}
   finally{try{if(File.Exists(resultPath))File.Delete(resultPath);}catch{}busy=false;if(!disposed)RefreshEnvironment();}
  }
  public override void Tick(){if(Page.Visible&&!checkedOnce&&!busy)RefreshEnvironment();}
  public override void Dispose(){disposed=true;base.Dispose();}
 }
}
