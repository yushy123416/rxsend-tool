using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RxToolbox {
 internal static class Ui {
  public const TextFormatFlags PaintFlags=TextFormatFlags.PreserveGraphicsClipping|TextFormatFlags.PreserveGraphicsTranslateTransform;
  public static Image BrandImage(){using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("RxToolbox.Brand")){if(stream!=null)using(var source=Image.FromStream(stream))return new Bitmap(source);}return SystemIcons.Application.ToBitmap();}
  public static readonly Color Bg=Color.FromArgb(245,247,251),Side=Color.FromArgb(237,241,248),Card=Color.White,Border=Color.FromArgb(221,227,237),Text=Color.FromArgb(30,40,57),Muted=Color.FromArgb(104,116,136),Accent=Color.FromArgb(39,99,235),Selection=Color.FromArgb(226,236,255);
  public static readonly Font Body=new Font("Microsoft YaHei UI",10),Small=new Font("Microsoft YaHei UI",9),Title=new Font("Microsoft YaHei UI",23,FontStyle.Bold),Heading=new Font("Microsoft YaHei UI",12,FontStyle.Bold),Number=new Font("Segoe UI",44,FontStyle.Bold);
  public static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
  public static Label Label(Control parent,string text,int x,int y,int w=300,int h=28,Font font=null,Color? color=null){var l=new Label{Text=text,Location=new Point(x,y),Size=new Size(w,h),Font=font??Body,ForeColor=color??Text,BackColor=parent.BackColor,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft};parent.Controls.Add(l);return l;}
  public static TextBox Input(Control parent,string text,int x,int y,int w){var frame=new InputField{Location=new Point(x,y),Size=new Size(w,36)};frame.Input.Text=text??"";parent.Controls.Add(frame);return frame.Input;}
  public static FlatButton Button(Control parent,string text,int x,int y,int w,Action click,bool accent=false){var b=new FlatButton{Text=text,AccessibleName=text,Location=new Point(x,y),Size=new Size(w,38),Accent=accent};b.Click+=delegate{click();};parent.Controls.Add(b);return b;}
  public static FluentCheck Check(Control parent,string text,int x,int y,bool value,Action<bool> change){var c=new FluentCheck{Text=text,Location=new Point(x,y),Size=new Size(240,30),Checked=value,ForeColor=Text,BackColor=parent.BackColor,Font=Body};c.CheckedChanged+=delegate{change(c.Checked);};parent.Controls.Add(c);return c;}
  public static NumberField Numeric(Control parent,int x,int y,decimal value,decimal min,decimal max,Action<decimal> change,decimal step=1){var n=new NumberField(value,min,max,step){Location=new Point(x,y),Size=new Size(110,36)};n.ValueChanged+=delegate{change(n.Value);};parent.Controls.Add(n);return n;}
  public static SelectField Combo(Control parent,int x,int y,int w){var c=new SelectField{Location=new Point(x,y),Size=new Size(w,36)};parent.Controls.Add(c);return c;}
  public static void Line(Control parent,int x,int y,int w,int h=1){parent.Controls.Add(new Panel{Location=new Point(x,y),Size=new Size(w,h),BackColor=Border});}
  public static string Time(int seconds){return seconds<0?"--:--":(seconds/60).ToString("00")+":"+(seconds%60).ToString("00");}
  public static Color PhaseColor(string phase){if(phase==null)return Muted;if(phase.Contains("核弹"))return Color.FromArgb(194,60,70);if(phase.Contains("准备"))return Color.FromArgb(125,94,185);return phase=="回合中"?Accent:Muted;}
 }
 internal sealed class FlatButton : Control, IButtonControl {
  public bool Accent,Selected,Navigation;public string Glyph;bool hover;
  public DialogResult DialogResult {get;set;}
  public FlatButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable|ControlStyles.ResizeRedraw,true);Font=Ui.Body;Cursor=Cursors.Hand;TabStop=true;BackColor=Ui.Bg;AccessibleRole=AccessibleRole.PushButton;}
  public void NotifyDefault(bool value){Invalidate();}
  public void PerformClick(){if(Enabled&&Visible)OnClick(EventArgs.Empty);}
  protected override void OnClick(EventArgs e){base.OnClick(e);var form=FindForm();if(form!=null&&DialogResult!=System.Windows.Forms.DialogResult.None)form.DialogResult=DialogResult;}
  protected override void OnMouseDown(MouseEventArgs e){Focus();base.OnMouseDown(e);}
  protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter){PerformClick();e.Handled=true;e.SuppressKeyPress=true;}base.OnKeyDown(e);}
  protected override AccessibleObject CreateAccessibilityInstance(){return new ButtonAccessibility(this);}
  sealed class ButtonAccessibility : ControlAccessibleObject {
   readonly FlatButton button;
   public ButtonAccessibility(FlatButton owner):base(owner){button=owner;}
   public override string DefaultAction {get{return "按下";}}
   public override void DoDefaultAction(){button.PerformClick();}
  }
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Ui.Bg:Parent.BackColor);}
  protected override void OnPaint(PaintEventArgs e){
   var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float scale=DeviceDpi/96f;
   var fill=!Enabled?Color.FromArgb(237,240,245):Accent?(hover?Color.FromArgb(27,79,204):Ui.Accent):Selected?Ui.Selection:hover?Color.FromArgb(231,236,245):Navigation?Ui.Side:Ui.Card;
   using(var p=Ui.Round(new RectangleF(1,1,Width-3,Height-3),6*scale))using(var b=new SolidBrush(fill)){g.FillPath(b,p);if(!Navigation&&!Accent)using(var pen=new Pen(Ui.Border))g.DrawPath(pen,p);}
   var color=!Enabled?Color.FromArgb(143,153,168):Accent?Color.White:Selected?Ui.Accent:Ui.Text;
   if(Navigation&&Selected)using(var b=new SolidBrush(Ui.Accent))using(var p=Ui.Round(new RectangleF(2,Height/2-10*scale,3*scale,20*scale),1.5f*scale))g.FillPath(b,p);
   var text=ClientRectangle;var flags=TextFormatFlags.VerticalCenter|Ui.PaintFlags|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|Ui.PaintFlags;
   if(!string.IsNullOrEmpty(Glyph)){using(var font=new Font("Segoe MDL2 Assets",12))TextRenderer.DrawText(g,Glyph,font,new Rectangle((int)(17*scale),0,(int)(24*scale),Height),color,TextFormatFlags.VerticalCenter|Ui.PaintFlags|TextFormatFlags.HorizontalCenter);text.X+=(int)(52*scale);text.Width-=(int)(58*scale);flags|=TextFormatFlags.Left;}else flags|=TextFormatFlags.HorizontalCenter;
   TextRenderer.DrawText(g,Text,Font,text,color,flags);if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(5,5,Width-10,Height-10),color,fill);
  }
 }
 internal sealed class BufferedPanel : Panel {
  [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)]static extern int SetWindowTheme(IntPtr window,string app,string id);
  protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);SetWindowTheme(Handle,"Explorer",null);}
  public BufferedPanel(){DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);}
 }
 internal sealed class ServerListView : ListView {
  public ServerListView(){DoubleBuffered=true;SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);}
  protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);Native.SendMessage(Handle,0x1036,(IntPtr)0x10000,(IntPtr)0x10000);}
 }
 internal class CardPanel : Panel {
  public CardPanel(){DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);BackColor=Ui.Card;Padding=new Padding(1);}
  protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);Invalidate(true);}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Ui.Bg:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float s=DeviceDpi/96f;using(var p=Ui.Round(new RectangleF(0,0,Width-1,Height-1),8*s))using(var b=new SolidBrush(Ui.Card))using(var pen=new Pen(Ui.Border)){e.Graphics.FillPath(b,p);e.Graphics.DrawPath(pen,p);}}
 }
 internal sealed class Preview : CardPanel {
  public CrosshairProfile Profile;public float PreviewScale=1;
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;float s=DeviceDpi/96f;int step=(int)(24*s),pad=(int)(20*s);using(var pen=new Pen(Color.FromArgb(233,237,244))){for(int x=pad;x<Width-pad;x+=step)g.DrawLine(pen,x,48*s,x,Height-40*s);for(int y=(int)(48*s);y<Height-40*s;y+=step)g.DrawLine(pen,pad,y,Width-pad,y);}TextRenderer.DrawText(g,"实时预览",Ui.Small,new Point(pad,(int)(16*s)),Ui.Muted);TextRenderer.DrawText(g,"静态主准星 · 实时调整",Ui.Small,new Rectangle(pad,Height-(int)(36*s),Width-pad*2,(int)(24*s)),Ui.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|Ui.PaintFlags);if(Profile!=null)CrosshairPainter.Draw(g,Profile,Width/2,Height/2,PreviewScale*(Profile.ReferenceHeight>0?Screen.PrimaryScreen.Bounds.Height/(float)Profile.ReferenceHeight:1));}
 }
}
