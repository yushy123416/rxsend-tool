using System;
using System.Drawing;
using System.Windows.Forms;
namespace RxToolbox {
 internal sealed class ScrollViewport : Panel {
  public readonly Panel Content;readonly SlimScroll bar;int offset;bool laying;
  public ScrollViewport(Panel content){DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);BackColor=Ui.Bg;Content=content;Content.AutoScrollMinSize=Size.Empty;Content.AutoScroll=false;Controls.Add(Content);bar=new SlimScroll();Controls.Add(bar);bar.Changed+=delegate{offset=bar.Value;Arrange();};MouseWheel+=Wheel;Content.MouseWheel+=Wheel;}
  void Wheel(object s,MouseEventArgs e){bar.Value=Math.Max(0,Math.Min(bar.Maximum,bar.Value-e.Delta/120*60));}
  protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);Arrange();}
  protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);Arrange();}
  void Arrange(){if(laying||bar==null)return;laying=true;try{int height=0;foreach(Control child in Content.Controls)height=Math.Max(height,child.Bottom+12);bar.Maximum=Math.Max(0,height-ClientSize.Height);bar.Viewport=ClientSize.Height;bar.Visible=bar.Maximum>0;offset=Math.Min(offset,bar.Maximum);Content.Bounds=new Rectangle(0,-offset,Math.Max(1,ClientSize.Width-(bar.Maximum>0?18:0)),Math.Max(height,ClientSize.Height));bar.Bounds=new Rectangle(ClientSize.Width-14,4,10,Math.Max(1,ClientSize.Height-8));bar.BringToFront();bar.Invalidate();Content.Invalidate(true);Invalidate();}finally{laying=false;}}
 }
 internal sealed class SlimScroll : Control {
  public int Maximum,Viewport;int value,grab;bool dragging,hover;public event Action Changed;
  public int Value {get{return value;}set{int n=Math.Max(0,Math.Min(Maximum,value));if(n==this.value)return;this.value=n;Invalidate();if(Changed!=null)Changed();}}
  public SlimScroll(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);BackColor=Ui.Bg;Cursor=Cursors.Hand;TabStop=true;AccessibleName="页面滚动条";}
  Rectangle Thumb {get{int h=Math.Min(Height,Math.Max(36,Height*Viewport/Math.Max(1,Viewport+Maximum)));return new Rectangle(1,(Height-h)*Value/Math.Max(1,Maximum),Math.Max(2,Width-2),h);}}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using(var p=Ui.Round(Thumb,4))using(var b=new SolidBrush(dragging?Ui.Accent:hover?Color.FromArgb(133,153,183):Color.FromArgb(188,200,218)))e.Graphics.FillPath(b,p);}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;var t=Thumb;if(!t.Contains(e.Location))Value+=(e.Y<t.Top?-1:1)*Math.Max(60,Viewport-40);else{dragging=true;grab=e.Y-t.Top;Capture=true;}Invalidate();}
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)Value=(e.Y-grab)*Maximum/Math.Max(1,Height-Thumb.Height);}
  protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=false;Capture=false;Invalidate();}
  protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Down)Value+=40;else if(e.KeyCode==Keys.Up)Value-=40;else if(e.KeyCode==Keys.PageDown)Value+=Viewport;else if(e.KeyCode==Keys.PageUp)Value-=Viewport;else if(e.KeyCode==Keys.Home)Value=0;else if(e.KeyCode==Keys.End)Value=Maximum;else{base.OnKeyDown(e);return;}e.Handled=true;}
 }
}
