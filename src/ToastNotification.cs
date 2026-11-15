using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RxToolbox {
 internal sealed class ToastNotification : Control {
  const double EnterDuration=280,HoldDuration=1600,ExitDuration=220;
  readonly Timer animation=new Timer{Interval=15};readonly Stopwatch elapsed=new Stopwatch();
  float scale=1;double progress,startProgress;Rectangle area;Color textColor=Ui.Accent;bool disposed;

  public ToastNotification(){
   SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
   SetStyle(ControlStyles.Selectable,false);TabStop=false;Visible=false;Font=Ui.Heading;BackColor=Ui.Card;AccessibleRole=AccessibleRole.Alert;
   animation.Tick+=delegate{Advance(elapsed.Elapsed.TotalMilliseconds);};
  }
  internal void ShowMessage(string message,Color color,Rectangle contentArea,float uiScale){
   startProgress=Visible?progress:0;progress=startProgress;
   Text=message;textColor=color;Position(contentArea,uiScale);BringToFront();Show();Invalidate();
   elapsed.Restart();animation.Start();
  }
  internal void Position(Rectangle contentArea,float uiScale){
   area=contentArea;scale=uiScale;
   int width=Math.Max((int)(200*scale),TextRenderer.MeasureText(Text??"",Font).Width+(int)(48*scale));
   width=Math.Min(width,Math.Max(1,area.Width));int height=Math.Max(1,(int)(58*scale));
   int top=(int)Math.Round(-height+progress*(height+20*scale));
   SetBounds(area.Left+(area.Width-width)/2,top,width,height);
  }
  internal void Advance(double milliseconds){
   if(milliseconds<EnterDuration){double t=Math.Max(0,milliseconds)/EnterDuration;progress=startProgress+(1-startProgress)*(1-Math.Pow(1-t,3));}
   else if(milliseconds<EnterDuration+HoldDuration)progress=1;
   else if(milliseconds<EnterDuration+HoldDuration+ExitDuration){double t=(milliseconds-EnterDuration-HoldDuration)/ExitDuration;progress=1-t*t*t;}
   else{animation.Stop();elapsed.Stop();progress=0;Hide();return;}
   Position(area,scale);
  }
  protected override void OnSizeChanged(EventArgs e){
   base.OnSizeChanged(e);if(Width<1||Height<1)return;
   using(var path=Ui.Round(new RectangleF(0,0,Width,Height),12*scale)){
    var previous=Region;Region=new Region(path);if(previous!=null)previous.Dispose();
   }
  }
  protected override void OnPaint(PaintEventArgs e){
   var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
   using(var path=Ui.Round(new RectangleF(.5f,.5f,Width-1,Height-1),12*scale))
   using(var fill=new SolidBrush(Ui.Card))using(var border=new Pen(Ui.Border)){g.FillPath(fill,path);g.DrawPath(border,path);}
   TextRenderer.DrawText(g,Text,Font,new Rectangle((int)(12*scale),0,Width-(int)(24*scale),Height),textColor,Ui.PaintFlags|TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
  }
  protected override void Dispose(bool disposing){if(disposing&&!disposed){disposed=true;animation.Dispose();elapsed.Stop();}base.Dispose(disposing);}
 }
}
