using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace RxToolbox {
 internal sealed class AutoJoinModule : ToolModule {
  public override string Title {get{return "自动进服";}}
  public override string Symbol {get{return "→";}}
  readonly SelectField servers;readonly FlatButton start;readonly Label status;readonly List<string> keys=new List<string>();
  public AutoJoinModule(ToolContext context):base(context){
   Header("自动进服","");
   var selection=new CardPanel{Location=new Point(0,124),Size=new Size(936,156)};Page.Controls.Add(selection);
   Ui.Label(selection,"目标服务器",24,16,600,30,Ui.Heading);
   servers=Ui.Combo(selection,24,64,696);servers.AccessibleName="自动进服目标服务器";
   start=Ui.Button(selection,"开始自动进服",744,64,168,Toggle,true);
   Ui.Label(selection,"每 5 秒检测空位，通过 Steam 发起进服",24,112,888,26,Ui.Small,Ui.Muted);
   var task=new CardPanel{Location=new Point(0,300),Size=new Size(936,140)};Page.Controls.Add(task);
   Ui.Label(task,"任务状态",24,16,600,30,Ui.Heading);status=Ui.Label(task,"尚未启动",24,62,888,56,Ui.Body,Ui.Muted);
   C.Rounds.Changed+=UpdateServers;UpdateServers();Tick();
  }
  void UpdateServers(){
   if(Page.IsDisposed)return;string selected=servers.SelectedIndex>=0&&servers.SelectedIndex<keys.Count?keys[servers.SelectedIndex]:C.Settings.SelectedServer;
   foreach(var server in C.Rounds.Servers){if(keys.Contains(server.Key))continue;keys.Add(server.Key);servers.Items.Add(server.ShortName);}
   int index=keys.IndexOf(selected);if(index>=0)servers.SelectedIndex=index;else if(keys.Count>0&&servers.SelectedIndex<0)servers.SelectedIndex=0;
  }
  void Toggle(){if(C.AutoJoin.Running)C.AutoJoin.Stop();else{if(servers.SelectedIndex<0)return;string key=keys[servers.SelectedIndex];var server=C.Rounds.Servers.Find(s=>s.Key==key);try{C.AutoJoin.Start(server);C.AutoJoin.Tick();}catch(Exception ex){C.Window.SetStatus(ex.Message);}}Tick();}
  public override void Tick(){if(!Page.Visible)return;start.Text=C.AutoJoin.Running?"取消自动进服":"开始自动进服";servers.Enabled=!C.AutoJoin.Running;start.Enabled=C.AutoJoin.Running||servers.SelectedIndex>=0;status.Text=string.IsNullOrEmpty(C.AutoJoin.Status)?"尚未启动":C.AutoJoin.Status;}
  public override void Dispose(){C.Rounds.Changed-=UpdateServers;base.Dispose();}
 }
}
