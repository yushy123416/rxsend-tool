using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace RxToolbox {
 internal class FieldSurface : UserControl {
  public FieldSurface(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);AutoScaleMode=AutoScaleMode.None;BackColor=Ui.Card;ForeColor=Ui.Text;Font=Ui.Body;}
  protected float Factor {get{return DeviceDpi/96f;}}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Ui.Bg:Parent.BackColor);}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=Ui.Round(new RectangleF(1,1,Width-3,Height-3),6*Factor))using(var brush=new SolidBrush(Ui.Card))using(var pen=new Pen(ContainsFocus?Ui.Accent:Ui.Border,ContainsFocus?1.5f*Factor:Factor)){e.Graphics.FillPath(brush,p);e.Graphics.DrawPath(pen,p);}}
  protected override void OnEnter(EventArgs e){base.OnEnter(e);Invalidate();}
  protected override void OnLeave(EventArgs e){base.OnLeave(e);Invalidate();}
 }
 internal sealed class InputField : FieldSurface {
  public readonly TextBox Input;
  public InputField(){TabStop=false;Input=new TextBox{BorderStyle=BorderStyle.None,BackColor=Ui.Card,ForeColor=Ui.Text,Font=Font};Controls.Add(Input);Input.GotFocus+=delegate{Invalidate();};Input.LostFocus+=delegate{Invalidate();};}
  protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);if(Input!=null){int pad=(int)(12*Factor);Input.SetBounds(pad,Math.Max(3,(Height-Input.PreferredHeight)/2),Math.Max(1,Width-pad*2),Input.PreferredHeight);}}
 }
 internal sealed class FieldAction : Control {
  bool hover;
  public FieldAction(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Font=Ui.Body;BackColor=Ui.Card;ForeColor=Ui.Muted;Cursor=Cursors.Hand;TabStop=true;AccessibleRole=AccessibleRole.PushButton;}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Ui.Card);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;if(hover&&Enabled)using(var p=Ui.Round(new RectangleF(0,0,Width-1,Height-1),4))using(var b=new SolidBrush(Ui.Selection))e.Graphics.FillPath(b,p);TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?Ui.Accent:Ui.Border,TextFormatFlags.VerticalCenter|Ui.PaintFlags|TextFormatFlags.HorizontalCenter);if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(2,2,Width-4,Height-4));}
  protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter){OnClick(EventArgs.Empty);e.Handled=true;e.SuppressKeyPress=true;}base.OnKeyDown(e);}
 }
 internal sealed class NumberField : FieldSurface {
  readonly TextBox input;readonly FieldAction minus,plus;readonly decimal minimum,maximum,increment;decimal value;
  public event EventHandler ValueChanged;
  public decimal Value {get{return value;}set{decimal next=Math.Max(minimum,Math.Min(maximum,value));bool changed=this.value!=next;this.value=next;Sync();if(changed&&ValueChanged!=null)ValueChanged(this,EventArgs.Empty);}}
  public NumberField(decimal initial,decimal min,decimal max,decimal step){
   minimum=min;maximum=max;increment=step;TabStop=false;AccessibleRole=AccessibleRole.SpinButton;
   input=new TextBox{BorderStyle=BorderStyle.None,TextAlign=HorizontalAlignment.Center,BackColor=Ui.Card,ForeColor=Ui.Text,Font=Font,AccessibleName="数值"};
   minus=new FieldAction{Text="−",AccessibleName="减小数值"};plus=new FieldAction{Text="+",AccessibleName="增大数值"};Controls.Add(input);Controls.Add(minus);Controls.Add(plus);
   minus.Click+=delegate{Commit();Value-=increment;};plus.Click+=delegate{Commit();Value+=increment;};
   input.Validated+=delegate{Commit();};input.GotFocus+=delegate{Invalidate();};input.LostFocus+=delegate{Invalidate();};
   input.KeyDown+=delegate(object s,KeyEventArgs e){if(e.KeyCode==Keys.Up||e.KeyCode==Keys.Down){Commit();Value+=e.KeyCode==Keys.Up?increment:-increment;e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Enter){Commit();e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Escape){Sync();e.SuppressKeyPress=true;}};
   Value=initial;
  }
  internal void Commit(){decimal parsed;if(decimal.TryParse(input.Text,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.CurrentCulture,out parsed)){parsed=increment>=1?decimal.Truncate(parsed):decimal.Round(parsed,2);Value=parsed;}else Sync();}
  void Sync(){input.Text=value.ToString(increment<1?"0.##":"0",CultureInfo.CurrentCulture);minus.Enabled=value>minimum;plus.Enabled=value<maximum;}
  protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);if(input==null)return;int pad=Math.Max(3,(int)(4*Factor)),button=Math.Max(16,(int)(21*Factor));minus.SetBounds(pad,pad,button,Math.Max(1,Height-pad*2));plus.SetBounds(Width-pad-button,pad,button,Math.Max(1,Height-pad*2));input.SetBounds(pad+button,Math.Max(3,(Height-input.PreferredHeight)/2),Math.Max(1,Width-2*(pad+button)),input.PreferredHeight);}
 }
 internal sealed class SelectField : FieldSurface {
  readonly ListBox list;readonly ToolStripDropDown popup;readonly ToolStripControlHost host;
  public ListBox.ObjectCollection Items {get{return list.Items;}}
  public int SelectedIndex {get{return list.SelectedIndex;}set{list.SelectedIndex=value;}}
  public object SelectedItem {get{return list.SelectedItem;}}
  public event EventHandler SelectedIndexChanged;
  public SelectField(){
   TabStop=true;Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.ComboBox;
   list=new ListBox{BorderStyle=BorderStyle.None,IntegralHeight=false,DrawMode=DrawMode.OwnerDrawFixed,BackColor=Ui.Card,ForeColor=Ui.Text,Font=Ui.Body};
   host=new ToolStripControlHost(list){Margin=Padding.Empty,Padding=Padding.Empty,AutoSize=false};
   popup=new ToolStripDropDown{AutoSize=false,Padding=new Padding(1),BackColor=Ui.Card,DropShadowEnabled=true};popup.Items.Add(host);
   list.DrawItem+=delegate(object s,DrawItemEventArgs e){if(e.Index<0)return;bool selected=(e.State&DrawItemState.Selected)!=0;using(var b=new SolidBrush(selected?Ui.Selection:Ui.Card))e.Graphics.FillRectangle(b,e.Bounds);var r=e.Bounds;r.X+=(int)(12*Factor);r.Width-=(int)(24*Factor);TextRenderer.DrawText(e.Graphics,Convert.ToString(list.Items[e.Index]),Font,r,selected?Ui.Accent:Ui.Text,TextFormatFlags.VerticalCenter|Ui.PaintFlags|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);};
   list.SelectedIndexChanged+=delegate{Invalidate();if(SelectedIndexChanged!=null)SelectedIndexChanged(this,EventArgs.Empty);};
   list.MouseClick+=delegate{popup.Close();Focus();};list.KeyDown+=delegate(object s,KeyEventArgs e){if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Escape){popup.Close();Focus();e.SuppressKeyPress=true;}};
   popup.Closed+=delegate{Invalidate();};
  }
  void Open(){if(Items.Count==0)return;list.Font=Font;list.ItemHeight=(int)(34*Factor);int h=Math.Min(8,Items.Count)*list.ItemHeight+4;host.Size=new Size(Width-2,h);popup.Size=new Size(Width,h+2);list.Size=host.Size;popup.Show(this,new Point(0,Height+4));list.Focus();}
  protected override void OnClick(EventArgs e){base.OnClick(e);Focus();if(popup.Visible)popup.Close();else Open();}
  protected override bool IsInputKey(Keys keyData){Keys k=keyData&Keys.KeyCode;return k==Keys.Up||k==Keys.Down||k==Keys.Home||k==Keys.End||base.IsInputKey(keyData);}
  protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter||(e.Alt&&e.KeyCode==Keys.Down)){Open();e.SuppressKeyPress=true;}else if(Items.Count>0&&(e.KeyCode==Keys.Up||e.KeyCode==Keys.Down)){SelectedIndex=Math.Max(0,Math.Min(Items.Count-1,SelectedIndex+(e.KeyCode==Keys.Up?-1:1)));e.Handled=true;}else if(Items.Count>0&&(e.KeyCode==Keys.Home||e.KeyCode==Keys.End)){SelectedIndex=e.KeyCode==Keys.Home?0:Items.Count-1;e.Handled=true;}base.OnKeyDown(e);}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);int pad=(int)(12*Factor);TextRenderer.DrawText(e.Graphics,SelectedItem==null?"选择预设…":Convert.ToString(SelectedItem),Font,new Rectangle(pad,0,Math.Max(1,Width-pad*3),Height),SelectedItem==null?Ui.Muted:Ui.Text,TextFormatFlags.VerticalCenter|Ui.PaintFlags|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);float x=Width-18*Factor,y=Height/2f;using(var pen=new Pen(Ui.Muted,1.4f*Factor)){e.Graphics.DrawLine(pen,x-4*Factor,y-2*Factor,x,y+2*Factor);e.Graphics.DrawLine(pen,x,y+2*Factor,x+4*Factor,y-2*Factor);}}
  protected override void Dispose(bool disposing){if(disposing)popup.Dispose();base.Dispose(disposing);}
 }
 internal sealed class FluentCheck : Control {
  bool check;public event EventHandler CheckedChanged;
  public bool Checked {get{return check;}set{if(check==value)return;check=value;Invalidate();AccessibilityNotifyClients(AccessibleEvents.StateChange,-1);if(CheckedChanged!=null)CheckedChanged(this,EventArgs.Empty);}}
  public FluentCheck(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Opaque|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);BackColor=Ui.Card;Font=Ui.Body;Cursor=Cursors.Hand;TabStop=true;AccessibleRole=AccessibleRole.CheckButton;}
  protected override void OnMouseDown(MouseEventArgs e){Focus();base.OnMouseDown(e);}
  protected override void OnClick(EventArgs e){if(Enabled)Checked=!Checked;base.OnClick(e);}
  protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space){OnClick(EventArgs.Empty);e.Handled=true;e.SuppressKeyPress=true;}base.OnKeyDown(e);}
  protected override AccessibleObject CreateAccessibilityInstance(){return new CheckAccessibility(this);}
  sealed class CheckAccessibility : ControlAccessibleObject {
   readonly FluentCheck owner;public CheckAccessibility(FluentCheck control):base(control){owner=control;}
   public override string DefaultAction {get{return owner.Checked?"取消选中":"选中";}}
   public override AccessibleStates State {get{return base.State|(owner.Checked?AccessibleStates.Checked:AccessibleStates.None);}}
   public override void DoDefaultAction(){if(owner.Enabled)owner.OnClick(EventArgs.Empty);}
  }
  protected override void OnPaint(PaintEventArgs e){
   float s=DeviceDpi/96f;var g=e.Graphics;
   using(var background=new SolidBrush(BackColor))g.FillRectangle(background,ClientRectangle);
   g.SmoothingMode=SmoothingMode.AntiAlias;var box=new RectangleF(s,(Height-17*s)/2,17*s,17*s);
   using(var path=Ui.Round(box,4*s))using(var brush=new SolidBrush(Checked?Ui.Accent:Ui.Card))using(var pen=new Pen(Checked?Ui.Accent:Ui.Border,1.2f*s)){g.FillPath(brush,path);g.DrawPath(pen,path);}
   if(Checked)using(var pen=new Pen(Color.White,1.8f*s))g.DrawLines(pen,new[]{new PointF(box.X+4*s,box.Y+8*s),new PointF(box.X+7*s,box.Y+11*s),new PointF(box.X+13*s,box.Y+5*s)});
   using(var brush=new SolidBrush(Enabled?Ui.Text:Ui.Muted))using(var format=new StringFormat{LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})g.DrawString(Text,Font,brush,new RectangleF(28*s,0,Math.Max(1,Width-28*s),Height),format);
   if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(1,1,Width-2,Height-2));
  }
 }
}
