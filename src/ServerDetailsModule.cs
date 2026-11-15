using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RxToolbox {
 internal sealed class ServerDetailsModule : ToolModule {
  public override string Title{get{return "服务器信息";}}public override string Symbol{get{return "ⓘ";}}
  readonly ServerDetailsService service=new ServerDetailsService();readonly SelectField servers;
  readonly Label name,counts,map,latency,playerState,game,phase,bots;readonly ListView players;readonly ImageList rowHeight;
  readonly List<string> keys=new List<string>();A2sPlayer[] rendered;bool filling;string shownError;
  public ServerDetailsModule(ToolContext context):base(context){
   Header("服务器信息","");
   Ui.Label(Page,"选择服务器",0,84,760,28,Ui.Small,Ui.Muted);
   servers=Ui.Combo(Page,0,124,760);Ui.Button(Page,"查询",776,124,160,QuerySelected,true);
   var summary=new CardPanel{Location=new Point(0,184),Size=new Size(936,170)};Page.Controls.Add(summary);
   name=Ui.Label(summary,"选择服务器后查询",24,14,888,30,Ui.Heading,Ui.Accent);name.UseMnemonic=false;Ui.Line(summary,24,56,888);
   game=InfoValue(summary,"游戏",24,70);map=InfoValue(summary,"地图",24,98);phase=InfoValue(summary,"回合状态",24,126);
   bots=InfoValue(summary,"bot 数量",484,70);counts=InfoValue(summary,"在线玩家",484,98);latency=InfoValue(summary,"延迟",484,126);
   Ui.Label(Page,"玩家列表",0,372,936,30,Ui.Heading);
   playerState=Ui.Label(Page,"",0,408,936,28,Ui.Small,Ui.Muted);
   var table=new CardPanel{Location=new Point(0,446),Size=new Size(936,274)};Page.Controls.Add(table);
   players=new ServerListView{Location=new Point(1,5),Size=new Size(934,264),View=System.Windows.Forms.View.Details,FullRowSelect=true,HideSelection=false,MultiSelect=false,BorderStyle=BorderStyle.None,BackColor=Ui.Card,ForeColor=Ui.Text,Font=Ui.Body,HeaderStyle=ColumnHeaderStyle.Nonclickable,OwnerDraw=true,AccessibleName="服务器玩家，按本次在线时长降序排列"};
   rowHeight=new ImageList{ImageSize=new Size(1,36),ColorDepth=ColorDepth.Depth32Bit};players.SmallImageList=rowHeight;
   players.Columns.Add("序号",80);players.Columns.Add("玩家",592);players.Columns.Add("本次在线时长 ↓",250);table.Controls.Add(players);
   players.SizeChanged+=delegate{FitColumns();};
   players.DrawColumnHeader+=delegate(object sender,DrawListViewColumnHeaderEventArgs e){using(var b=new SolidBrush(Ui.Card))e.Graphics.FillRectangle(b,e.Bounds);TextRenderer.DrawText(e.Graphics,e.Header.Text,Ui.Small,new Rectangle(e.Bounds.X+14,e.Bounds.Y,e.Bounds.Width-20,e.Bounds.Height),Ui.Muted,TextFormatFlags.VerticalCenter|Ui.PaintFlags);using(var pen=new Pen(Ui.Border))e.Graphics.DrawLine(pen,e.Bounds.Left,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1);};
   players.DrawItem+=delegate{};
   players.DrawSubItem+=delegate(object sender,DrawListViewSubItemEventArgs e){using(var b=new SolidBrush(e.Item.Selected?Ui.Selection:Ui.Card))e.Graphics.FillRectangle(b,e.Bounds);TextRenderer.DrawText(e.Graphics,e.SubItem.Text,Ui.Body,new Rectangle(e.Bounds.X+14,e.Bounds.Y,e.Bounds.Width-20,e.Bounds.Height),e.ColumnIndex==2?Ui.Accent:Ui.Text,Ui.PaintFlags|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);};
   servers.SelectedIndexChanged+=delegate{if(!filling)QuerySelected();};
   service.Changed+=Render;C.Rounds.Changed+=UpdateServers;UpdateServers();Render();
  }
  static Label InfoValue(Control card,string title,int x,int y){Ui.Label(card,title+"：",x,y,100,26,Ui.Body,Ui.Muted);var value=Ui.Label(card,"—",x+104,y,324,26,Ui.Body,Ui.Text);value.UseMnemonic=false;return value;}
  static void Value(Label label,string text){label.Text=string.IsNullOrEmpty(text)?"—":Privacy.Redact(text);label.ForeColor=Ui.Accent;}
  void FitColumns(){int available=Math.Max(3,players.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-4);int first=available*80/922,second=available*592/922;int[] widths={first,second,available-first-second};for(int i=0;i<widths.Length;i++)if(players.Columns[i].Width!=widths[i])players.Columns[i].Width=widths[i];}
  void UpdateServers(){
   if(Page.IsDisposed)return;filling=true;try{foreach(var server in C.Rounds.Servers)if(!keys.Contains(server.Key)){keys.Add(server.Key);servers.Items.Add(server);}}finally{filling=false;}
   if(servers.SelectedIndex<0&&keys.Count>0){int selected=keys.IndexOf(C.Settings.SelectedServer);servers.SelectedIndex=selected<0?0:selected;}
  }
  void QuerySelected(){var selected=servers.SelectedItem as ServerEntry;if(selected==null)return;service.Select(selected);Tick();}
  public override void Tick(){if(!Page.Visible||Page.FindForm()!=C.Window||!C.Window.Visible)return;Render();if(C.Window.AutomaticServerQueriesEnabled)Poll();}
  async void Poll(){try{await service.PollAsync();}catch(Exception ex){if(!Page.IsDisposed){C.Window.SetStatus("查询失败，稍后自动重试。");Log.Write(ex);}}}
  void Render(){
   if(Page.IsDisposed)return;FitColumns();var selected=service.Selected;var snapshot=service.Snapshot;var info=snapshot==null?null:snapshot.Info;
   name.Text=info==null?selected==null?"选择服务器后查询":selected.ShortName:Privacy.Redact(info.Name);
   Value(game,info==null?null:info.Game);Value(map,info==null?null:info.Map);Value(bots,info==null?null:info.Bots.ToString());
   Value(counts,info==null?null:info.Players+" / "+info.MaxPlayers);Value(latency,info==null?null:info.Latency+" ms");
   var known=selected==null?null:C.Rounds.Servers.Find(s=>s.Key==selected.Key);var round=known==null?null:known.Round;
   Value(phase,round==null?"暂无":round.Stale?"数据过期":round.Data.round_phase);
   string error=snapshot==null?null:snapshot.InfoError;if(Page.Visible&&error!=shownError){C.Window.SetStatus(error??"");shownError=error;}
   var rows=snapshot==null?null:snapshot.Players;
   playerState.Text=snapshot!=null&&snapshot.PlayersError!=null?snapshot.PlayersError+(rows==null?"":" · 显示上次成功的列表 "+snapshot.PlayersAt.ToLocalTime().ToString("HH:mm:ss")):rows==null?"":rows.Length==0?"服务器未公开玩家明细，或当前没有可公开的玩家。":"共 "+rows.Length+" 条玩家记录  ·  更新于 "+snapshot.PlayersAt.ToLocalTime().ToString("HH:mm:ss");
   if(object.ReferenceEquals(rows,rendered))return;rendered=rows;players.BeginUpdate();try{players.Items.Clear();if(rows!=null){var sorted=SortPlayers(rows);for(int i=0;i<sorted.Length;i++){var player=sorted[i];players.Items.Add(new ListViewItem(new[]{(i+1).ToString(),string.IsNullOrEmpty(player.Name)?"（未公开姓名）":Privacy.Redact(player.Name),Duration(player.DurationSeconds)}));}}}finally{players.EndUpdate();}
  }
  internal static A2sPlayer[] SortPlayers(A2sPlayer[] rows){return rows.OrderByDescending(player=>player.DurationSeconds).ToArray();}
  internal static string Duration(float seconds){var duration=TimeSpan.FromSeconds(seconds);return ((long)duration.TotalHours).ToString("00")+":"+duration.Minutes.ToString("00")+":"+duration.Seconds.ToString("00");}
  public override void Dispose(){C.Rounds.Changed-=UpdateServers;service.Changed-=Render;service.Dispose();base.Dispose();rowHeight.Dispose();}
 }
}
