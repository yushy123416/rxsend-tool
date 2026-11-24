using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
namespace RxToolbox {
 internal static class RenderUi {
  static void Capture(Control view,string file){view.CreateControl();using(var b=new Bitmap(view.Width,view.Height)){
   view.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));
   // DrawToBitmap does not preserve overlapping child z-order; composite the root overlay last.
   foreach(var toast in view.Controls.OfType<ToastNotification>().Where(t=>t.Visible))using(var layer=new Bitmap(toast.Width,toast.Height))using(var g=Graphics.FromImage(b)){
    toast.DrawToBitmap(layer,new Rectangle(Point.Empty,layer.Size));var origin=view is Form?new Point(view.PointToScreen(Point.Empty).X-view.Left,view.PointToScreen(Point.Empty).Y-view.Top):Point.Empty;
    g.SetClip(new Rectangle(origin,view.ClientSize));g.TranslateTransform(origin.X+toast.Left,origin.Y+toast.Top);if(toast.Region!=null)g.SetClip(toast.Region,System.Drawing.Drawing2D.CombineMode.Intersect);g.DrawImageUnscaled(layer,0,0);
   }
   b.Save(file,ImageFormat.Png);
  }}
  static void Prepare(Form form){form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-30000,-30000);form.Show();}
  static IEnumerable<Control> All(Control parent){foreach(Control child in parent.Controls){yield return child;foreach(var nested in All(child))yield return nested;}}
  static void HoverReview(Form form){
   var flags=BindingFlags.NonPublic|BindingFlags.Instance;var check=All(form).OfType<FluentCheck>().Single(c=>c.Text=="核弹红色");
   foreach(var button in All(form).OfType<FlatButton>().Where(b=>b.Text=="保存"||b.Text=="计时颜色")){
    typeof(FlatButton).GetMethod("OnMouseEnter",flags).Invoke(button,new object[]{EventArgs.Empty});button.Refresh();
    typeof(FlatButton).GetMethod("OnMouseLeave",flags).Invoke(button,new object[]{EventArgs.Empty});button.Refresh();
    typeof(Control).GetMethod("OnMouseEnter",flags).Invoke(check,new object[]{EventArgs.Empty});check.Refresh();
    typeof(Control).GetMethod("OnMouseLeave",flags).Invoke(check,new object[]{EventArgs.Empty});check.Refresh();
   }
   Capture(form,"build/ui-review/timer-designer-hover.png");
  }
  static void ToastReview(MainWindow form){
   var flags=BindingFlags.NonPublic|BindingFlags.Instance;
   var toast=(ToastNotification)typeof(MainWindow).GetField("toast",flags).GetValue(form);
   form.ShowToast("✓ 复制成功",Ui.Accent);
   if(toast.Top!=-toast.Height||toast.Parent!=form||form.Controls.GetChildIndex(toast)!=0)throw new InvalidOperationException("Toast must start above the application and float above its pages");
   toast.Advance(60);Capture(form,"build/ui-review/broadcast-toast-enter.png");
   toast.Advance(300);
   if(toast.Top!=(int)(20*form.UiScale)||!toast.Visible||toast.Region.IsVisible(0,0))throw new InvalidOperationException("Toast must settle below the top with rounded corners");
   Capture(form,"build/ui-review/broadcast-toast.png");
   int settled=toast.Top;form.ShowToast("✓ 复制成功",Ui.Accent);
   if(toast.Top!=settled)throw new InvalidOperationException("Repeated copy must keep the visible toast in place");
   typeof(MainWindow).GetMethod("SelectModule",flags).Invoke(form,new object[]{0});
   for(int step=0;step<10;step++)typeof(MainWindow).GetMethod("Animate",flags).Invoke(form,new object[]{null,EventArgs.Empty});
   if(!toast.Visible||form.Controls.GetChildIndex(toast)!=0)throw new InvalidOperationException("Toast must remain above the page after navigation");
   Capture(form,"build/ui-review/toast-over-page.png");
   toast.Advance(1990);Capture(form,"build/ui-review/broadcast-toast-exit.png");
   toast.Advance(2200);if(toast.Visible)throw new InvalidOperationException("Toast must disappear after sliding away");
  }
  static void FullscreenReview(){
   using(var dialog=new FullscreenRiskDialog()){
    Prepare(dialog);Capture(dialog,"build/ui-review/fullscreen-risk.png");
    var confirm=All(dialog).OfType<FlatButton>().Single(b=>b.Text=="了解风险并启用");
    var cancel=All(dialog).OfType<FlatButton>().Single(b=>b.Text=="取消");
    if(confirm.Enabled||dialog.AcceptButton!=cancel||dialog.CancelButton!=cancel)throw new Exception("Fullscreen consent must default to cancellation");
    confirm.PerformClick();if(dialog.DialogResult==DialogResult.OK)throw new Exception("Unacknowledged consent cannot enable fullscreen");
    cancel.PerformClick();if(dialog.DialogResult!=DialogResult.Cancel)throw new Exception("Cancel must reject consent");
   }
   using(var dialog=new FullscreenRiskDialog()){
    Prepare(dialog);All(dialog).OfType<FluentCheck>().Single().Checked=true;
    var confirm=All(dialog).OfType<FlatButton>().Single(b=>b.Text=="了解风险并启用");
    if(!confirm.Enabled)throw new Exception("Acknowledgement must enable explicit confirmation");
    confirm.PerformClick();if(dialog.DialogResult!=DialogResult.OK)throw new Exception("Explicit confirmation must succeed");
   }
  }
  static void DuplicateReview(){
   int stopped=0,acquired=0;
   using(var dialog=new DuplicateInstanceDialog(()=>{stopped++;return System.Threading.Tasks.Task.FromResult(0);},()=>{acquired++;return true;})){
    Prepare(dialog);Capture(dialog,"build/ui-review/duplicate-instance.png");
    All(dialog).OfType<FlatButton>().Single(b=>b.Text=="取消").PerformClick();
    if(stopped!=0||acquired!=0||dialog.DialogResult!=DialogResult.Cancel)throw new InvalidOperationException("Cancelling duplicate launch must leave the existing instance alone");
   }
   using(var dialog=new DuplicateInstanceDialog(()=>{stopped++;return System.Threading.Tasks.Task.FromResult(0);},()=>{acquired++;return true;})){
    Prepare(dialog);All(dialog).OfType<FlatButton>().Single(b=>b.Text=="关闭上一个程序").PerformClick();
    if(stopped!=1||acquired!=1||dialog.DialogResult!=DialogResult.OK)throw new InvalidOperationException("Replacement must close the previous instance and acquire ownership before continuing");
   }
   int attempts=0;
   using(var dialog=new DuplicateInstanceDialog(()=>{if(attempts++==0)throw new IOException("权限不足");return System.Threading.Tasks.Task.FromResult(0);},()=>true)){
    Prepare(dialog);var button=All(dialog).OfType<FlatButton>().Single(b=>b.Text=="关闭上一个程序");button.PerformClick();
    if(!button.Enabled||dialog.DialogResult==DialogResult.OK)throw new InvalidOperationException("Failed replacement must allow retry without starting another instance");
    Capture(dialog,"build/ui-review/duplicate-instance-error.png");button.PerformClick();
    if(dialog.DialogResult!=DialogResult.OK)throw new InvalidOperationException("Retry must be able to continue after the previous failure");
   }
  }
  static void SeedDetails(MainWindow form){
   var flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var module=((List<IToolModule>)typeof(MainWindow).GetField("modules",flags).GetValue(form)).OfType<ServerDetailsModule>().Single();
   var service=(ServerDetailsService)typeof(ServerDetailsModule).GetField("service",flags).GetValue(module);var data=service.Snapshot;
   data.Info=new A2sInfo{Name="服务器信息 · 离线渲染样例",Map="rx_site19_cn_gamma_final",Game="CN Gmod:SCP集合社区",Folder="garrysmod",AppId=4000,System="Linux",Version="2026.11.15",Password=false,Vac=true,Bots=0,Players=4,MaxPlayers=70,Latency=38};
   data.Players=new[]{new A2sPlayer{Name="短时在线玩家",DurationSeconds=30},new A2sPlayer{Name="中文玩家 ❤",DurationSeconds=3661},new A2sPlayer{Name="在线超过一天",DurationSeconds=90061},new A2sPlayer{Name="玩家 & 符号",DurationSeconds=3600}};
   data.AttemptedAt=data.InfoAt=data.PlayersAt=DateTime.UtcNow;
   typeof(ServerDetailsModule).GetMethod("Render",flags).Invoke(module,null);
   var list=All(module.View).OfType<ListView>().Single();
   if(list.Items[0].SubItems[1].Text!="在线超过一天"||list.Items[0].SubItems[2].Text!="25:01:01")throw new InvalidOperationException("Server players must be displayed by duration descending");
  }
  [STAThread]static int Main(){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Directory.CreateDirectory("build/ui-review");DuplicateReview();FullscreenReview();
   using(var form=new MainWindow()){form.AutomaticUpdatesEnabled=false;form.AutomaticTemplateUpdatesEnabled=false;form.AutomaticServerQueriesEnabled=false;var flags=BindingFlags.Instance|BindingFlags.NonPublic;var context=(ToolContext)typeof(MainWindow).GetField("context",flags).GetValue(form);context.Settings.ExperimentalFullscreen=false;context.Settings.TimerVisible=false;context.Settings.CrosshairVisible=false;typeof(MainWindow).GetField("nextPoll",flags).SetValue(form,DateTime.MaxValue);SeedDetails(form);Prepare(form);var select=typeof(MainWindow).GetMethod("SelectModule",flags);int moduleCount=((System.Collections.ICollection)typeof(MainWindow).GetField("modules",flags).GetValue(form)).Count;for(int i=0;i<moduleCount;i++){select.Invoke(form,new object[]{i});if(i==4){for(int pump=0;pump<20;pump++){Application.DoEvents();System.Threading.Thread.Sleep(25);}}for(int step=0;step<10;step++)typeof(MainWindow).GetMethod("Animate",flags).Invoke(form,new object[]{null,EventArgs.Empty});Capture(form,"build/ui-review/page-"+i+".png");}select.Invoke(form,new object[]{0});for(int cycle=0;cycle<8;cycle++){form.Size=form.MinimumSize;form.ClientSize=new Size(1200,820);form.Refresh();}for(int step=0;step<10;step++)typeof(MainWindow).GetMethod("Animate",flags).Invoke(form,new object[]{null,EventArgs.Empty});Capture(form,"build/ui-review/page-after-resize.png");form.Size=form.MinimumSize;select.Invoke(form,new object[]{3});for(int step=0;step<10;step++)typeof(MainWindow).GetMethod("Animate",flags).Invoke(form,new object[]{null,EventArgs.Empty});Capture(form,"build/ui-review/page-small.png");select.Invoke(form,new object[]{1});for(int step=0;step<10;step++)typeof(MainWindow).GetMethod("Animate",flags).Invoke(form,new object[]{null,EventArgs.Empty});Capture(form,"build/ui-review/server-info-small.png");var infoView=((List<IToolModule>)typeof(MainWindow).GetField("modules",flags).GetValue(form)).OfType<ServerDetailsModule>().Single().View;var infoScroll=All(infoView).OfType<SlimScroll>().Single();infoScroll.Value=infoScroll.Maximum;Capture(form,"build/ui-review/server-info-players.png");select.Invoke(form,new object[]{5});for(int step=0;step<10;step++)typeof(MainWindow).GetMethod("Animate",flags).Invoke(form,new object[]{null,EventArgs.Empty});Capture(form,"build/ui-review/broadcast-small.png");var broadcastView=(ScrollViewport)((List<IToolModule>)typeof(MainWindow).GetField("modules",flags).GetValue(form))[5].View;var scroll=All(broadcastView).OfType<SlimScroll>().Single();scroll.Value=scroll.Maximum;Capture(form,"build/ui-review/broadcast-scrolled.png");ToastReview(form);form.Hide();}
   using(var form=new TimerDesigner(new TimerAppearance())){Prepare(form);Capture(form,"build/ui-review/timer-designer.png");HoverReview(form);form.Hide();}
   using(var bitmap=TimerRenderer.Render(new TimerAppearance(),TimerContent.Sample))bitmap.Save("build/ui-review/timer.png",ImageFormat.Png);
   Console.WriteLine("Rendered all pages, broadcast layouts, timer editor and timer overlay");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 }
}
