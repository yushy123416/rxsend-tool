using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RxToolbox {
 internal sealed class MainWindow : Form {
  readonly List<IToolModule> modules=new List<IToolModule>();readonly List<FlatButton> nav=new List<FlatButton>();
  readonly Panel host=new BufferedPanel();readonly Label status;readonly NotifyIcon tray;readonly Timer clock=new Timer();ToolContext context;bool exit,resourcesDisposed;DateTime nextPoll=DateTime.MinValue;
  internal float UiScale=1;
  public MainWindow() {
   SuspendLayout();
   Text="RXSend Tool";ClientSize=new Size(1200,820);MinimumSize=new Size(1036,739);StartPosition=FormStartPosition.CenterScreen;BackColor=Ui.Bg;ForeColor=Ui.Text;Font=Ui.Body;AutoScaleMode=AutoScaleMode.None;Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath)??SystemIcons.Application;DoubleBuffered=true;
   var side=new Panel{BackColor=Ui.Side,Dock=DockStyle.Left,Size=new Size(208,820)};Controls.Add(side);
   Ui.Label(side,"RXSend Tool",26,36,170,36,Ui.Heading);
   Ui.Label(side,"v"+Updates.Current.ToString(3),26,80,170,24,Ui.Small,Ui.Muted);
   host.Location=new Point(236,24);host.Size=new Size(936,746);host.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(host);
   status=Ui.Label(this,"",236,786,936,24,Ui.Small,Ui.Muted);status.Anchor=AnchorStyles.Left|AnchorStyles.Bottom|AnchorStyles.Right;
   context=new ToolContext{Settings=Settings.Load(),Window=this};context.Rounds=new RoundService(context.Settings);context.AutoJoin=new AutoJoinService();context.Crosshair=new CrosshairOverlay();context.Timer=new TimerOverlay(context.Settings,context.Rounds);context.Timer.Moved+=context.Save;context.Timer.SelectionChanged+=context.Save;context.Timer.ConfigureRequested+=EditTimer;
   modules.Add(new RoundsModule(context));modules.Add(new CrosshairModule(context));modules.Add(new AutoJoinModule(context));modules.Add(new SettingsModule(context));
   string[] glyphs={"\uE916","\uE81D","\uE768","\uE713"};
   for(int i=0;i<modules.Count;i++){int index=i;bool bottom=i==modules.Count-1;var b=Ui.Button(side,modules[i].Title,12,bottom?760:188+i*52,184,()=>SelectModule(index));b.Height=44;b.Navigation=true;b.Glyph=glyphs[i];b.BackColor=Ui.Side;if(bottom)b.Anchor=AnchorStyles.Left|AnchorStyles.Bottom;nav.Add(b);modules[i].View.Dock=DockStyle.Fill;modules[i].View.Visible=false;host.Controls.Add(modules[i].View);}
   SelectModule(0);
   var menu=new ContextMenuStrip();menu.Items.Add("打开工具箱",null,delegate{Restore();});menu.Items.Add("显示 / 隐藏准星",null,delegate{ToggleCrosshair();});menu.Items.Add("显示 / 隐藏计时",null,delegate{ToggleTimer();});menu.Items.Add("取消自动进服",null,delegate{context.AutoJoin.Stop();});menu.Items.Add(new ToolStripSeparator());menu.Items.Add("退出",null,delegate{exit=true;Close();});
   tray=new NotifyIcon{Text="RXSend Tool",Icon=Icon,Visible=true,ContextMenuStrip=menu};tray.DoubleClick+=delegate{Restore();};
   clock.Interval=1000;clock.Tick+=delegate{Tick();};clock.Start();
   Shown+=delegate{int dark=0;try{Native.DwmSetWindowAttribute(Handle,20,ref dark,4);}catch{}bool a=Native.RegisterHotKey(Handle,1,0x4003,(uint)Keys.F8),b=Native.RegisterHotKey(Handle,2,0x4003,(uint)Keys.F9);if(!a||!b)SetStatus("部分快捷键被其他程序占用，请使用托盘菜单");context.UpdateCrosshair();context.Timer.Update(context.Rounds.TimerSelected,context.Rounds.Error);if(context.Settings.TimerVisible)context.Timer.Show();Tick();};
   Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplayChanged;
   ResumeLayout(false); using(var dpi=CreateGraphics()) { UiScale=dpi.DpiX/96f; if(UiScale!=1) Scale(new SizeF(UiScale,UiScale)); } ScaleColumns(this,UiScale);
   var work=Screen.FromControl(this).WorkingArea;MinimumSize=new Size(Math.Min(MinimumSize.Width,work.Width),Math.Min(MinimumSize.Height,work.Height));Size=new Size(Math.Min(Width,work.Width),Math.Min(Height,work.Height));
   FormClosing+=delegate(object sender,FormClosingEventArgs e){if(!exit&&e.CloseReason==CloseReason.UserClosing&&context.Settings.CloseToTray){e.Cancel=true;Hide();}else{exit=true;context.Save();}};
  }
  static void ScaleColumns(Control c,float factor){var list=c as ListView;if(list!=null){foreach(ColumnHeader h in list.Columns)h.Width=(int)(h.Width*factor);if(list.SmallImageList!=null)list.SmallImageList.ImageSize=new Size(1,Math.Max(16,(int)(36*factor)));}var page=c as Panel;if(page!=null&&page.AutoScroll)page.AutoScrollMinSize=new Size((int)(936*factor),(int)(738*factor));foreach(Control child in c.Controls)ScaleColumns(child,factor);}
  void DisplayChanged(object s,EventArgs e){if(IsHandleCreated&&!IsDisposed)BeginInvoke((Action)delegate{context.UpdateCrosshair();});}
  public void SetStatus(string s){status.Text=s;}
  void SelectModule(int i){status.Text="";host.SuspendLayout();try{for(int k=0;k<modules.Count;k++){if(k!=i)modules[k].View.Hide();nav[k].Selected=k==i;nav[k].Invalidate();}modules[i].View.BringToFront();modules[i].View.Show();}finally{host.ResumeLayout(true);}modules[i].Tick();host.Refresh();modules[i].View.Refresh();}
  void Tick(){context.AutoJoin.Tick();foreach(var m in modules)m.Tick();if(context.Settings.TimerVisible)context.Timer.Update(context.Rounds.TimerSelected,context.Rounds.Error);if(DateTime.UtcNow>=nextPoll){nextPoll=DateTime.UtcNow.AddSeconds(Visible||context.Settings.TimerVisible?10:30);context.Rounds.Refresh(false);}}
  public void ToggleCrosshair(){context.Settings.CrosshairVisible=!context.Settings.CrosshairVisible;context.UpdateCrosshair();context.Save();foreach(var m in modules)m.Tick();}
  public void ToggleTimer(){context.Settings.TimerVisible=!context.Settings.TimerVisible;context.Timer.Update(context.Rounds.TimerSelected,context.Rounds.Error);if(context.Settings.TimerVisible)context.Timer.Show();else context.Timer.Hide();context.Save();foreach(var m in modules)m.Tick();}
  public void Restore(){Show();WindowState=FormWindowState.Normal;Activate();}
  public void ExitForUpdate(){exit=true;Close();}
  public void EditTimer(){using(var editor=new TimerDesigner(context.Settings.TimerAppearance)){if(editor.ShowDialog(this)!=DialogResult.OK)return;context.Settings.TimerAppearance=editor.Appearance;context.Save();context.Timer.Update(context.Rounds.TimerSelected,context.Rounds.Error);}}
  protected override void WndProc(ref Message m){if(m.Msg==0x312){if(m.WParam.ToInt32()==1)ToggleCrosshair();if(m.WParam.ToInt32()==2)ToggleTimer();}base.WndProc(ref m);}
  protected override void Dispose(bool disposing){
   if(disposing&&!resourcesDisposed){
    resourcesDisposed=true;Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplayChanged;clock.Stop();clock.Dispose();
    if(tray!=null){var menu=tray.ContextMenuStrip;tray.ContextMenuStrip=null;tray.Visible=false;if(menu!=null)menu.Dispose();tray.Dispose();}
    if(context!=null){if(IsHandleCreated){Native.UnregisterHotKey(Handle,1);Native.UnregisterHotKey(Handle,2);}foreach(var module in modules)module.Dispose();context.AutoJoin.Dispose();context.Rounds.Dispose();context.Crosshair.Dispose();context.Timer.Dispose();}
   }
   base.Dispose(disposing);
  }
 }
 internal abstract class ToolModule : IToolModule {
  protected readonly ToolContext C;protected readonly Panel Page=new BufferedPanel{BackColor=Ui.Bg,AutoScroll=true,AutoScrollMinSize=new Size(936,738)};
  public abstract string Title{get;}public abstract string Symbol{get;}public Control View{get{return Page;}}
  protected ToolModule(ToolContext c){C=c;}
  protected void Header(string title,string subtitle){Ui.Label(Page,title,0,12,700,58,Ui.Title);}
  public virtual void Tick(){}public virtual void Dispose(){Page.Dispose();}
 }
 internal sealed class RoundsModule : ToolModule {
  public override string Title{get{return "回合计时";}}public override string Symbol{get{return "◷";}}
  readonly ListView list;readonly ImageList rowHeight;readonly Label time,name,phase,rounds,details;readonly FlatButton overlay,refresh;
  public RoundsModule(ToolContext c):base(c){
   Header("回合计时","服务器状态，一眼掌握。");
   var card=new CardPanel{Location=new Point(0,124),Size=new Size(936,160)};Page.Controls.Add(card);
   Ui.Label(card,"当前服务器",24,19,206,24,Ui.Small,Ui.Muted);name=Ui.Label(card,"娘娘测试服",24,52,208,36,Ui.Heading);
   phase=Ui.Label(card,"等待同步",24,105,184,26,Ui.Body,Ui.Accent);
   Ui.Line(card,244,29,1,100);time=Ui.Label(card,"--:--",266,28,242,88,Ui.Number,Ui.Accent);Ui.Label(card,"当前阶段剩余时间",277,118,225,22,Ui.Small,Ui.Muted);
   Ui.Line(card,530,29,1,100);rounds=Ui.Label(card,"剩余回合  —",557,33,350,34,Ui.Heading);details=Ui.Label(card,"",557,81,350,56,Ui.Body,Ui.Muted);
   overlay=Ui.Button(Page,"开启悬浮计时",0,304,182,()=>C.Window.ToggleTimer(),true);
   refresh=Ui.Button(Page,"刷新",196,304,100,()=>C.Rounds.Refresh(true));Ui.Label(Page,"时间补偿",322,309,80);Ui.Numeric(Page,407,305,C.Settings.OffsetSeconds,-300,300,v=>{C.Settings.OffsetSeconds=(int)v;C.Save();Tick();});Ui.Label(Page,"秒",530,309,32,28,Ui.Body,Ui.Muted);
   Ui.Button(Page,"悬浮窗样式",792,304,144,()=>C.Window.EditTimer());
   Ui.Label(Page,"服务器列表",0,362,250,30,Ui.Heading);
   var table=new CardPanel{Location=new Point(0,404),Size=new Size(936,264)};Page.Controls.Add(table);
   list=new ServerListView{Location=new Point(1,5),Size=new Size(934,254),View=System.Windows.Forms.View.Details,FullRowSelect=true,HideSelection=false,MultiSelect=false,BorderStyle=BorderStyle.None,BackColor=Ui.Card,ForeColor=Ui.Text,Font=Ui.Body,HeaderStyle=ColumnHeaderStyle.Nonclickable,OwnerDraw=true,AccessibleName="服务器状态列表"};
   rowHeight=new ImageList{ImageSize=new Size(1,36),ColorDepth=ColorDepth.Depth32Bit};list.SmallImageList=rowHeight;
   list.Columns.Add("服务器",168);list.Columns.Add("阶段",126);list.Columns.Add("剩余回合",98);list.Columns.Add("倒计时",104);list.Columns.Add("人数",88);list.Columns.Add("延迟",96);list.Columns.Add("地图",230);
   list.DrawColumnHeader+=delegate(object sender,DrawListViewColumnHeaderEventArgs e){using(var b=new SolidBrush(Ui.Card))e.Graphics.FillRectangle(b,e.Bounds);TextRenderer.DrawText(e.Graphics,e.Header.Text,Ui.Small,new Rectangle(e.Bounds.X+14,e.Bounds.Y,e.Bounds.Width-18,e.Bounds.Height),Ui.Muted,TextFormatFlags.VerticalCenter|Ui.PaintFlags);using(var pen=new Pen(Ui.Border))e.Graphics.DrawLine(pen,e.Bounds.Left,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1);};
   list.DrawItem+=delegate(object sender,DrawListViewItemEventArgs e){};
   list.DrawSubItem+=delegate(object sender,DrawListViewSubItemEventArgs e){
    var bounds=e.Bounds;using(var b=new SolidBrush(e.Item.Selected?Ui.Selection:Ui.Card))e.Graphics.FillRectangle(b,bounds);
    using(var pen=new Pen(Color.FromArgb(237,240,246)))e.Graphics.DrawLine(pen,bounds.Left,bounds.Bottom-1,bounds.Right,bounds.Bottom-1);
    if(e.ColumnIndex==0&&e.Item.Selected)using(var b=new SolidBrush(Ui.Accent))e.Graphics.FillRectangle(b,bounds.Left,bounds.Top+7,3,bounds.Height-14);
    Color color=e.ColumnIndex==3?Ui.Accent:e.ColumnIndex==1?Ui.PhaseColor(e.SubItem.Text):Ui.Text;
    TextRenderer.DrawText(e.Graphics,e.SubItem.Text,Ui.Body,new Rectangle(bounds.X+14,bounds.Y,bounds.Width-20,bounds.Height),color,TextFormatFlags.VerticalCenter|Ui.PaintFlags|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
   };
   list.SelectedIndexChanged+=delegate{if(list.SelectedItems.Count>0){C.Settings.SelectedServer=(string)list.SelectedItems[0].Tag;C.Save();Tick();}};table.Controls.Add(list);
   C.Rounds.Changed+=Changed;Changed();
  }
  void Changed(){if(Page.IsDisposed)return;while(list.Items.Count<C.Rounds.Servers.Count){var s=C.Rounds.Servers[list.Items.Count];var item=new ListViewItem(new[]{s.ShortName,"—","—","--:--","—","—","—"}){Tag=s.Key};list.Items.Add(item);if(s.Key==C.Settings.SelectedServer)item.Selected=true;}Tick();}
  public override void Tick(){
   if(!Page.Visible)return;
   overlay.Text=C.Settings.TimerVisible?"关闭悬浮计时":"开启悬浮计时";refresh.Enabled=!C.Rounds.Busy;
   var s=C.Rounds.Selected;var r=s==null?null:s.Round;name.Text=s==null?"选择服务器":s.ShortName;time.Text=Ui.Time(r==null?-1:r.Remaining(C.Settings.OffsetSeconds));
   phase.Text=r==null?"等待同步":r.Stale?"数据过期":r.Data.round_phase;time.ForeColor=Ui.PhaseColor(phase.Text);rounds.Text="剩余回合  "+(r==null||r.Stale||r.Data.rounds_left<0?"—":r.Data.rounds_left.ToString());
   details.Text=s==null||s.Info==null?"":s.Info.Map+"\n"+s.Info.Players+" / "+s.Info.MaxPlayers+" 人";
   list.BeginUpdate();try{for(int i=0;i<list.Items.Count;i++){var e=C.Rounds.Servers[i];var q=e.Round;bool fresh=q!=null&&!q.Stale;string[] values={e.ShortName,q==null?"等待同步":q.Stale?"数据过期":q.Data.round_phase,fresh&&q.Data.rounds_left>=0?q.Data.rounds_left.ToString():"—",Ui.Time(q==null?-1:q.Remaining(C.Settings.OffsetSeconds)),e.Info==null?"—":e.Info.Players+"/"+e.Info.MaxPlayers,e.InfoError!=null?"—":e.Info==null?"—":e.Info.Latency+" ms",e.Info==null?"—":e.Info.Map};for(int k=0;k<values.Length;k++)if(list.Items[i].SubItems[k].Text!=values[k])list.Items[i].SubItems[k].Text=values[k];}}finally{list.EndUpdate();}
  }
  public override void Dispose(){C.Rounds.Changed-=Changed;base.Dispose();rowHeight.Dispose();}
 }
 internal sealed class CrosshairModule : ToolModule {
  public override string Title{get{return "屏幕准星";}}public override string Symbol{get{return "⊕";}}
  readonly Preview preview;readonly FlatButton toggle;readonly TextBox code;readonly Label source;readonly Panel editor;readonly SelectField presets;bool editing;
  public CrosshairModule(ToolContext c):base(c){
   Header("屏幕准星","你的准星，你的手感。");
   preview=new Preview{Location=new Point(0,124),Size=new Size(320,288),Profile=C.Settings.Crosshair,PreviewScale=C.Settings.CrosshairScale};Page.Controls.Add(preview);
   toggle=Ui.Button(Page,"开启准星",0,428,152,()=>C.Window.ToggleCrosshair(),true);Ui.Button(Page,"选择颜色",168,428,152,ChooseColor);
   source=Ui.Label(Page,"",0,471,320,24,Ui.Small,Ui.Muted);
   var display=new CardPanel{Location=new Point(0,508),Size=new Size(320,194)};Page.Controls.Add(display);
   Ui.Label(display,"显示器",20,20,80);var screen=Ui.Combo(display,106,22,192);foreach(var d in Screen.AllScreens)screen.Items.Add(d.DeviceName+(d.Primary?" · 主显示器":""));int selected=Array.FindIndex(Screen.AllScreens,s=>s.DeviceName==C.Settings.ScreenDevice);screen.SelectedIndex=selected<0?0:selected;screen.SelectedIndexChanged+=delegate{var screens=Screen.AllScreens;if(screen.SelectedIndex>=0&&screen.SelectedIndex<screens.Length){C.Settings.ScreenDevice=screens[screen.SelectedIndex].DeviceName;Apply();}};
   Ui.Label(display,"缩放",20,66,75);Ui.Numeric(display,106,68,(decimal)C.Settings.CrosshairScale,.25m,4,v=>{C.Settings.CrosshairScale=(float)v;Apply();},.05m);
   Ui.Label(display,"偏移 X / Y",20,111,84);Ui.Numeric(display,106,114,C.Settings.CrosshairX,-2000,2000,v=>{C.Settings.CrosshairX=(int)v;Apply();}).Width=88;Ui.Numeric(display,210,114,C.Settings.CrosshairY,-2000,2000,v=>{C.Settings.CrosshairY=(int)v;Apply();}).Width=88;
   Ui.Label(display,"相对于显示器中心 · 单位：像素",20,153,280,24,Ui.Small,Ui.Muted);
   var card=new CardPanel{Location=new Point(344,124),Size=new Size(592,152)};Page.Controls.Add(card);Ui.Label(card,"导入准星",20,14,300,28,Ui.Heading);
   code=Ui.Input(card,"",20,56,552);code.AccessibleName="准星码";Ui.Button(card,"导入",452,99,120,Import,true);
   editor=new CardPanel{Location=new Point(344,292),Size=new Size(592,236)};Page.Controls.Add(editor);BuildEditor();
   var saved=new CardPanel{Location=new Point(344,544),Size=new Size(592,158)};Page.Controls.Add(saved);
   Ui.Label(saved,"我的预设",20,13,300,28,Ui.Heading);presets=Ui.Combo(saved,20,58,348);Ui.Button(saved,"保存当前预设",384,54,188,SavePreset);
   Ui.Button(saved,"应用",20,106,104,LoadPreset);Ui.Button(saved,"删除",138,106,104,DeletePreset);Ui.Button(saved,"重置准星",452,106,120,delegate{C.Settings.Crosshair=new CrosshairProfile();BuildEditor();Apply();});
   FillPresets();Tick();
  }
  void BuildEditor(){editing=true;foreach(Control control in editor.Controls.Cast<Control>().ToArray())control.Dispose();var p=C.Settings.Crosshair;
   Ui.Label(editor,"形状",20,20,75);var shape=Ui.Combo(editor,98,22,154);shape.Items.AddRange(new object[]{"十字","圆环","方框","四点"});shape.SelectedIndex=p.Shape=="circle"?1:p.Shape=="square"?2:p.Shape=="quad"?3:0;shape.SelectedIndexChanged+=delegate{p.Shape=new[]{"cross","circle","square","quad"}[shape.SelectedIndex];Apply();};
   Ui.Label(editor,"长度",20,66,75);Ui.Numeric(editor,98,68,(decimal)p.Length,0,255,v=>{p.Length=(float)v;p.VerticalLength=(float)v;Apply();},.5m);
   Ui.Label(editor,"粗细",332,66,80);Ui.Numeric(editor,448,68,(decimal)p.Thickness,0,32,v=>{p.Thickness=(float)v;Apply();},.5m);
   Ui.Label(editor,"间距",20,110,75);Ui.Numeric(editor,98,112,(decimal)p.Gap,-3840,3840,v=>{p.Gap=(float)v;Apply();},.5m);
   Ui.Label(editor,"描边",332,110,80);Ui.Numeric(editor,448,112,(decimal)p.Outline,0,8,v=>{p.Outline=(float)v;Apply();},.5m);
   Ui.Check(editor,"中心点",20,157,p.Dot,v=>{p.Dot=v;Apply();}).Width=110;Ui.Check(editor,"T 形",176,157,p.TStyle,v=>{p.TStyle=v;Apply();}).Width=100;Ui.Check(editor,"外线",332,157,p.Outer,v=>{p.Outer=v;Apply();}).Width=100;
   Ui.Label(editor,"透明度",332,193,100);Ui.Numeric(editor,448,195,p.Alpha,0,255,v=>{p.Alpha=(int)v;Apply();});if(C.Window.UiScale!=1)foreach(Control child in editor.Controls)child.Scale(new SizeF(C.Window.UiScale,C.Window.UiScale));editing=false;
  }
  void Apply(){if(editing)return;C.Settings.Crosshair.Validate();preview.Profile=C.Settings.Crosshair;preview.PreviewScale=C.Settings.CrosshairScale;preview.Invalidate();C.UpdateCrosshair();C.Save();Tick();}
  void Import(){try{var p=CrosshairImport.Parse(code.Text);C.Settings.Crosshair=p;BuildEditor();Apply();C.Window.SetStatus("已导入 "+p.Source+" 静态准星");}catch(Exception ex){C.Window.SetStatus(ex.Message);}}
  void ChooseColor(){var p=C.Settings.Crosshair;using(var d=new ColorDialog{Color=Color.FromArgb(p.Red,p.Green,p.Blue),FullOpen=true})if(d.ShowDialog(C.Window)==DialogResult.OK){p.Red=d.Color.R;p.Green=d.Color.G;p.Blue=d.Color.B;Apply();}}
  void FillPresets(){presets.Items.Clear();foreach(var p in C.Settings.Presets)presets.Items.Add(p);if(presets.Items.Count>0)presets.SelectedIndex=presets.Items.Count-1;}
  void SavePreset(){if(C.Settings.Presets.Count>=100){C.Window.SetStatus("最多保存 100 个预设");return;}string name=Prompt.Ask(C.Window,"保存准星",C.Settings.Crosshair.Name);if(string.IsNullOrWhiteSpace(name))return;C.Settings.Presets.Add(new CrosshairPreset{Name=name.Trim(),Profile=C.Settings.Crosshair.Clone()});C.Save();FillPresets();}
  void LoadPreset(){var p=presets.SelectedItem as CrosshairPreset;if(p==null||p.Profile==null)return;C.Settings.Crosshair=p.Profile.Clone();BuildEditor();Apply();}
  void DeletePreset(){var p=presets.SelectedItem as CrosshairPreset;if(p==null)return;C.Settings.Presets.Remove(p);FillPresets();C.Save();}
  public override void Tick(){toggle.Text=C.Settings.CrosshairVisible?"关闭准星":"开启准星";source.Text=C.Settings.Crosshair.Source;}
 }
 internal static class Prompt {
  public static string Ask(Form owner,string title,string value){using(var f=new Form{Text=title,ClientSize=new Size(400,130),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=Ui.Bg}){var t=Ui.Input(f,value,20,22,360);t.MaxLength=60;var b=Ui.Button(f,"保存",260,75,120,()=>{f.DialogResult=DialogResult.OK;},true);f.AcceptButton=b;return f.ShowDialog(owner)==DialogResult.OK?t.Text:null;}}
 }
}
