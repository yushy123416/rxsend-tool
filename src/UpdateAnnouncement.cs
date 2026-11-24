using System;
using System.Drawing;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace RxToolbox {
 internal sealed class UpdateAnnouncementDialog : Form {
  internal UpdateAnnouncementDialog(UpdateManifest manifest,bool ready){
   SuspendLayout();
   Text="更新公告 · RXSend Tool";ClientSize=new Size(800,640);StartPosition=FormStartPosition.CenterParent;
   FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;BackColor=Ui.Card;ForeColor=Ui.Text;Font=Ui.Body;
   Ui.Label(this,"有新版本 v"+Updates.ParseVersion(manifest.version).ToString(3),28,20,530,48,Ui.Title);
   var releasePage=Ui.Button(this,"前往发布页",618,26,154,()=>OpenReleasePage(manifest.ReleaseUrl));releasePage.Enabled=!string.IsNullOrEmpty(manifest.ReleaseUrl);
   Ui.Line(this,28,84,744);
   string notes=manifest.ReleaseNotes??manifest.notes;
   if(string.IsNullOrWhiteSpace(notes))notes="此版本暂未提供详细更新说明。你可以暂不更新，稍后再查看。";
   var body=new ReleaseMarkdownPanel(notes);
   var scroll=new ScrollViewport(body){Bounds=new Rectangle(28,104,744,426),BackColor=Ui.Card};Controls.Add(scroll);
   var footer=new Panel{Bounds=new Rectangle(0,550,800,90),BackColor=Ui.Bg};Controls.Add(footer);
   Ui.Line(footer,0,0,800);
   Ui.Label(footer,ready?"确认后安装并重启":"确认后下载、校验并重启",28,25,400,38,Ui.Small,Ui.Muted);
   var later=Ui.Button(footer,"暂不更新",484,25,128,()=>{DialogResult=DialogResult.Cancel;Close();});
   Ui.Button(footer,ready?"安装并重启":"立即更新",626,25,146,()=>{DialogResult=DialogResult.OK;Close();},true);
   AcceptButton=later;CancelButton=later;ActiveControl=later;
   AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;ResumeLayout(true);
  }
  internal static void OpenReleasePage(string url){try{Process.Start(new ProcessStartInfo(HttpData.Https(url).AbsoluteUri){UseShellExecute=true});}catch(Exception ex){MessageBox.Show("无法打开发布页："+ex.Message,"更新公告",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
 }
 internal sealed class UpdateBadge : Control {
  bool hover;
  internal UpdateBadge(){Text="有新版本";AccessibleName="有新版本，查看更新公告";AccessibleRole=AccessibleRole.PushButton;TabStop=true;Cursor=Cursors.Hand;Font=Ui.Small;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true);}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space){OnClick(EventArgs.Empty);e.Handled=true;e.SuppressKeyPress=true;}base.OnKeyDown(e);}
  protected override AccessibleObject CreateAccessibilityInstance(){return new BadgeAccessibility(this);}
  sealed class BadgeAccessibility : ControlAccessibleObject {
   readonly UpdateBadge badge;internal BadgeAccessibility(UpdateBadge owner):base(owner){badge=owner;}
   public override string DefaultAction{get{return "查看更新公告";}}
   public override void DoDefaultAction(){if(badge.Enabled&&badge.Visible)badge.OnClick(EventArgs.Empty);}
  }
  protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent==null?Ui.Side:Parent.BackColor);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using(var path=Ui.Round(new RectangleF(0,0,Width-1,Height-1),5*DeviceDpi/96f))using(var brush=new SolidBrush(hover?Color.FromArgb(180,36,47):Color.FromArgb(210,48,60)))e.Graphics.FillPath(brush,path);TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|Ui.PaintFlags);if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(3,3,Width-6,Height-6),Color.White,Color.FromArgb(210,48,60));}
 }
}
