using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
namespace RxToolbox {
 internal sealed class AutoJoinModule : ToolModule {
  public override string Title {get{return "自动进服";}}public override string Symbol {get{return "→";}}
  readonly Panel servers;readonly FlatButton start;readonly Label status,metrics;readonly List<FluentCheck> checks=new List<FluentCheck>();readonly List<Label> counts=new List<Label>();readonly List<string> keys=new List<string>();readonly Timer feedback=new Timer{Interval=200};
  public AutoJoinModule(ToolContext context):base(context){
   Header("自动进服","");
   var selection=new CardPanel{Location=new Point(0,124),Size=new Size(936,292)};Page.Controls.Add(selection);
   Ui.Label(selection,"目标服务器",24,16,500,30,Ui.Heading);start=Ui.Button(selection,"开始自动进服",744,16,168,Toggle,true);
   servers=new Panel{Location=new Point(24,64),Size=new Size(888,208),BackColor=Ui.Card};selection.Controls.Add(servers);
   var task=new CardPanel{Location=new Point(0,436),Size=new Size(936,104)};Page.Controls.Add(task);
   Ui.Label(task,"任务状态",24,14,180,30,Ui.Heading);status=Ui.Label(task,"选择服务器后开始监听",212,14,700,30,Ui.Body,Ui.Accent);
   metrics=Ui.Label(task,"",24,50,888,30,Ui.Small,Ui.Muted);
   C.Rounds.Changed+=UpdateServers;UpdateServers();feedback.Tick+=delegate{Tick();};feedback.Start();Tick();
  }
  void UpdateServers(){if(Page.IsDisposed)return;foreach(var server in C.Rounds.Servers){if(keys.Contains(server.Key))continue;int i=keys.Count;keys.Add(server.Key);int col=i%2,row=i/2;
   var tile=new CardPanel{Location=new Point(col*450,row*40),Size=new Size(438,36)};servers.Controls.Add(tile);
   var check=Ui.Check(tile,server.ShortName,10,3,server.Key==C.Settings.SelectedServer,v=>Tick());check.Width=260;checks.Add(check);
   var number=Ui.Label(tile,"等待查询",286,4,138,28,Ui.Small,Ui.Muted);number.TextAlign=ContentAlignment.MiddleRight;counts.Add(number);tile.Cursor=Cursors.Hand;Action toggle=delegate{if(!C.AutoJoin.Running){check.Checked=!check.Checked;Tick();}};tile.Click+=delegate{toggle();};number.Click+=delegate{toggle();};
  }}
  void Toggle(){if(C.AutoJoin.Running)C.AutoJoin.Stop();else{var selected=new List<ServerEntry>();for(int i=0;i<checks.Count;i++)if(checks[i].Checked){var entry=C.Rounds.Servers.Find(s=>s.Key==keys[i]);if(entry!=null)selected.Add(entry);}try{C.AutoJoin.Start(selected);C.AutoJoin.Tick();}catch(Exception ex){C.Window.SetStatus(ex.Message);}}Tick();}
  public override void Tick(){if(!Page.Visible)return;var join=C.AutoJoin;int selected=0;for(int i=0;i<checks.Count;i++){if(checks[i].Checked)selected++;checks[i].Enabled=!join.Running;var entry=C.Rounds.Servers.Find(s=>s.Key==keys[i]);if(entry==null)continue;counts[i].Text=entry.InfoError!=null?"查询失败":entry.Info==null?"等待查询":entry.Info.Players+" / "+entry.Info.MaxPlayers+" 人";counts[i].ForeColor=entry.InfoError!=null?Ui.Muted:entry.Info!=null&&entry.Info.Players<entry.Info.MaxPlayers?Ui.Accent:Ui.Muted;}
   start.Text=join.Running?"取消自动进服":"开始自动进服";start.Enabled=join.Running||selected>0;
   status.Text=join.Running?(join.Querying?"● 正在查询人数" : "● 等待下一轮 · "+(join.NextQueryMs/1000f).ToString("0.0")+" 秒"):string.IsNullOrEmpty(join.Status)?"已选 "+selected+" 个服务器":join.Status;
   var elapsed=join.Elapsed;
   metrics.Text="运行时间  "+((int)elapsed.TotalHours).ToString("00")+":"+elapsed.Minutes.ToString("00")+":"+elapsed.Seconds.ToString("00");
  }
  public override void Dispose(){feedback.Dispose();C.Rounds.Changed-=UpdateServers;base.Dispose();}
 }
}
