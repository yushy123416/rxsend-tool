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
  static void Capture(Control view,string file){view.CreateControl();using(var b=new Bitmap(view.Width,view.Height)){view.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(file,ImageFormat.Png);}}
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
  [STAThread]static int Main(){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Directory.CreateDirectory("build/ui-review");
   using(var form=new MainWindow()){var flags=BindingFlags.Instance|BindingFlags.NonPublic;var context=(ToolContext)typeof(MainWindow).GetField("context",flags).GetValue(form);context.Settings.TimerVisible=false;context.Settings.CrosshairVisible=false;typeof(MainWindow).GetField("nextPoll",flags).SetValue(form,DateTime.MaxValue);Prepare(form);var select=typeof(MainWindow).GetMethod("SelectModule",flags);for(int i=0;i<4;i++){select.Invoke(form,new object[]{i});Capture(form,"build/ui-review/page-"+i+".png");}form.Hide();}
   using(var form=new TimerDesigner(new TimerAppearance())){Prepare(form);Capture(form,"build/ui-review/timer-designer.png");HoverReview(form);form.Hide();}
   using(var bitmap=TimerRenderer.Render(new TimerAppearance(),TimerContent.Sample))bitmap.Save("build/ui-review/timer.png",ImageFormat.Png);
   Console.WriteLine("Rendered four pages, timer editor and timer overlay");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 }
}
