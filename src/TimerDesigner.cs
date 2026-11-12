using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace RxToolbox {
 internal sealed class TimerCanvas : Control {
  public TimerAppearance Appearance;public TimerContent Content=TimerContent.Sample;public int Selected;public event Action SelectionChanged,LayoutChanged;
  float zoom=1;PointF origin;Point dragStart;Rectangle original;bool dragging,resizing;
  public TimerCanvas(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);BackColor=Ui.Side;TabStop=true;AccessibleName="悬浮计时布局画布";}
  void Transform(){zoom=Math.Max(.1f,Math.Min(2f,Math.Min((Width-48f)/Appearance.Width,(Height-48f)/Appearance.Height)));origin=new PointF((Width-Appearance.Width*zoom)/2,(Height-Appearance.Height*zoom)/2);}
  RectangleF Display(Rectangle r){return new RectangleF(origin.X+r.X*zoom,origin.Y+r.Y*zoom,r.Width*zoom,r.Height*zoom);}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.Clear(Ui.Side);using(var brush=new SolidBrush(Color.FromArgb(225,231,241)))for(int y=0;y<Height;y+=20)for(int x=0;x<Width;x+=20)if((x/20+y/20)%2==0)g.FillRectangle(brush,x,y,20,20);if(Appearance==null)return;Transform();
   using(var bitmap=TimerRenderer.Render(Appearance,Content)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(bitmap,Rectangle.Round(Display(new Rectangle(0,0,Appearance.Width,Appearance.Height))),0,0,bitmap.Width,bitmap.Height,GraphicsUnit.Pixel);}
   for(int i=0;i<Appearance.Parts.Length;i++){var part=Appearance.Parts[i];if(!part.Visible)continue;var r=Display(part.Bounds);using(var pen=new Pen(i==Selected?Ui.Accent:Color.FromArgb(110,167,190,225),i==Selected?2:1)){pen.DashStyle=i==Selected?DashStyle.Solid:DashStyle.Dot;g.DrawRectangle(pen,r.X,r.Y,r.Width,r.Height);}if(i==Selected)using(var brush=new SolidBrush(Ui.Accent))g.FillRectangle(brush,r.Right-5,r.Bottom-5,10,10);}
  }
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left||Appearance==null)return;Focus();Transform();
   var current=Display(Appearance.Parts[Selected].Bounds);if(Appearance.Parts[Selected].Visible&&new RectangleF(current.Right-9,current.Bottom-9,18,18).Contains(e.Location)){resizing=true;}else{resizing=false;int hit=-1;for(int i=Appearance.Parts.Length-1;i>=0;i--)if(Appearance.Parts[i].Visible&&Display(Appearance.Parts[i].Bounds).Contains(e.Location)){hit=i;break;}if(hit<0)return;Selected=hit;}
   original=Appearance.Parts[Selected].Bounds;dragStart=e.Location;dragging=true;Capture=true;Invalidate();if(SelectionChanged!=null)SelectionChanged();
  }
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!dragging)return;var p=Appearance.Parts[Selected];int dx=(int)Math.Round((e.X-dragStart.X)/zoom),dy=(int)Math.Round((e.Y-dragStart.Y)/zoom);
   if(resizing){p.Width=Math.Max(24,Math.Min(Appearance.Width-p.X,original.Width+dx));p.Height=Math.Max(20,Math.Min(Appearance.Height-p.Y,original.Height+dy));}else{p.X=Math.Max(0,Math.Min(Appearance.Width-p.Width,original.X+dx));p.Y=Math.Max(0,Math.Min(Appearance.Height-p.Height,original.Y+dy));}Invalidate();if(LayoutChanged!=null)LayoutChanged();
  }
  protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=false;Capture=false;}
  protected override bool IsInputKey(Keys keyData){var k=keyData&Keys.KeyCode;return k==Keys.Left||k==Keys.Right||k==Keys.Up||k==Keys.Down||base.IsInputKey(keyData);}
  protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(Appearance==null)return;var p=Appearance.Parts[Selected];int step=e.Shift?10:1;if(e.KeyCode==Keys.Left)p.X-=step;else if(e.KeyCode==Keys.Right)p.X+=step;else if(e.KeyCode==Keys.Up)p.Y-=step;else if(e.KeyCode==Keys.Down)p.Y+=step;else return;Appearance.Validate();Invalidate();if(LayoutChanged!=null)LayoutChanged();e.Handled=true;}
 }
 internal sealed class TimerDesigner : Form {
  public TimerAppearance Appearance {get;private set;}
  readonly TimerCanvas canvas;readonly SelectField fonts,parts;readonly NumberField fontSize,opacity,x,y,w,h,width,height,radius;readonly FluentCheck visible,bold,phaseColor;bool syncing;
  public TimerDesigner(TimerAppearance source){
   Appearance=source.Clone();Appearance.Validate();Text="悬浮计时设置";ClientSize=new Size(1000,680);BackColor=Ui.Bg;ForeColor=Ui.Text;Font=Ui.Body;AutoScaleMode=AutoScaleMode.None;StartPosition=FormStartPosition.CenterParent;MinimizeBox=false;MaximizeBox=false;FormBorderStyle=FormBorderStyle.FixedDialog;AutoScroll=true;
   Ui.Label(this,"悬浮计时",24,14,500,38,Ui.Heading);Ui.Label(this,"拖动组件调整位置，拖动右下角调整尺寸",24,52,630,26,Ui.Small,Ui.Muted);
   canvas=new TimerCanvas{Location=new Point(24,88),Size=new Size(640,412),Appearance=Appearance};Controls.Add(canvas);
   var panel=new CardPanel{Location=new Point(688,24),Size=new Size(288,582)};Controls.Add(panel);
   Ui.Label(panel,"字体",16,12,250);fonts=Ui.Combo(panel,16,44,256);using(var installed=new InstalledFontCollection())foreach(string name in installed.Families.Select(f=>f.Name).OrderBy(n=>n))fonts.Items.Add(name);int fontIndex=fonts.Items.IndexOf(Appearance.FontFamily);fonts.SelectedIndex=fontIndex<0?0:fontIndex;fonts.SelectedIndexChanged+=delegate{if(syncing)return;Appearance.FontFamily=Convert.ToString(fonts.SelectedItem);RefreshCanvas();};
   Ui.Label(panel,"背景不透明度",16,91,124);opacity=Ui.Numeric(panel,146,88,Appearance.Opacity*100m/255m,0,100,v=>{Appearance.Opacity=(int)Math.Round(v*255/100);RefreshCanvas();});Ui.Label(panel,"%",262,92,24);
   Ui.Button(panel,"背景颜色",16,136,120,()=>ChooseColor(0));Ui.Button(panel,"文字颜色",152,136,120,()=>ChooseColor(1));
   Ui.Label(panel,"组件",16,188,250);parts=Ui.Combo(panel,16,220,256);foreach(var p in Appearance.Parts)parts.Items.Add(p);parts.SelectedIndex=0;parts.SelectedIndexChanged+=delegate{if(syncing)return;canvas.Selected=parts.SelectedIndex;SyncPart();canvas.Invalidate();};
   Ui.Label(panel,"字号",16,269,100);fontSize=Ui.Numeric(panel,146,266,36,8,80,v=>ChangePart(p=>p.FontSize=(float)v));
   visible=Ui.Check(panel,"显示",16,313,true,v=>ChangePart(p=>p.Visible=v));visible.Width=112;bold=Ui.Check(panel,"粗体",152,313,true,v=>ChangePart(p=>p.Bold=v));bold.Width=112;
   Ui.Label(panel,"位置 X",16,355,120);Ui.Label(panel,"位置 Y",152,355,120);x=Ui.Numeric(panel,16,384,0,0,1000,v=>ChangePart(p=>p.X=(int)v));y=Ui.Numeric(panel,152,384,0,0,600,v=>ChangePart(p=>p.Y=(int)v));
   Ui.Label(panel,"宽度",16,431,120);Ui.Label(panel,"高度",152,431,120);w=Ui.Numeric(panel,16,460,100,24,1000,v=>ChangePart(p=>p.Width=(int)v));h=Ui.Numeric(panel,152,460,40,20,600,v=>ChangePart(p=>p.Height=(int)v));
   Ui.Button(panel,"计时颜色",16,520,120,()=>ChooseColor(2));phaseColor=Ui.Check(panel,"核弹红色",152,523,Appearance.PhaseColor,v=>{Appearance.PhaseColor=v;RefreshCanvas();});phaseColor.Width=124;
   Ui.Label(this,"画布宽度",24,517,130);width=Ui.Numeric(this,24,548,Appearance.Width,240,1000,v=>{Appearance.Width=(int)v;ValidateLayout();});
   Ui.Label(this,"画布高度",176,517,130);height=Ui.Numeric(this,176,548,Appearance.Height,96,600,v=>{Appearance.Height=(int)v;ValidateLayout();});
   Ui.Label(this,"圆角",328,517,130);radius=Ui.Numeric(this,328,548,Appearance.CornerRadius,0,40,v=>{Appearance.CornerRadius=(int)v;RefreshCanvas();});
   Ui.Button(this,"恢复默认",24,622,128,Reset);var save=Ui.Button(this,"保存",704,622,128,delegate{ValidateChildren();Appearance.Validate();DialogResult=DialogResult.OK;},true);var cancel=Ui.Button(this,"取消",848,622,128,delegate{DialogResult=DialogResult.Cancel;});cancel.CausesValidation=false;CancelButton=cancel;
   canvas.SelectionChanged+=delegate{syncing=true;parts.SelectedIndex=canvas.Selected;syncing=false;SyncPart();};canvas.LayoutChanged+=SyncPart;SyncPart();
   using(var dpi=CreateGraphics()){float factor=dpi.DpiX/96f;if(factor!=1)Scale(new SizeF(factor,factor));}var work=Screen.FromControl(this).WorkingArea;Size=new Size(Math.Min(Width,work.Width),Math.Min(Height,work.Height));
  }
  void ChangePart(Action<TimerPart> change){if(syncing)return;change(Appearance.Parts[canvas.Selected]);ValidateLayout();}
  void ValidateLayout(){Appearance.Validate();SyncPart();RefreshCanvas();}
  void SyncPart(){if(fontSize==null)return;syncing=true;var p=Appearance.Parts[canvas.Selected];fontSize.Value=(decimal)p.FontSize;x.Value=p.X;y.Value=p.Y;w.Value=p.Width;h.Value=p.Height;visible.Checked=p.Visible;bold.Checked=p.Bold;syncing=false;}
  void RefreshCanvas(){if(!syncing)canvas.Invalidate();}
  void ChooseColor(int target){using(var picker=new ColorDialog{FullOpen=true,Color=Color.FromArgb(target==0?Appearance.Background:target==1?Appearance.Foreground:Appearance.Accent)})if(picker.ShowDialog(this)==DialogResult.OK){if(target==0)Appearance.Background=picker.Color.ToArgb();else if(target==1)Appearance.Foreground=picker.Color.ToArgb();else Appearance.Accent=picker.Color.ToArgb();RefreshCanvas();}}
  void Reset(){Appearance=new TimerAppearance();canvas.Appearance=Appearance;canvas.Selected=0;syncing=true;parts.Items.Clear();foreach(var p in Appearance.Parts)parts.Items.Add(p);parts.SelectedIndex=0;fonts.SelectedIndex=fonts.Items.IndexOf(Appearance.FontFamily);opacity.Value=Appearance.Opacity*100m/255m;width.Value=Appearance.Width;height.Value=Appearance.Height;radius.Value=Appearance.CornerRadius;phaseColor.Checked=Appearance.PhaseColor;syncing=false;SyncPart();canvas.Invalidate();}
 }
}
