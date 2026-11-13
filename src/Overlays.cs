using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RxToolbox {
 internal static class Native {
  [StructLayout(LayoutKind.Sequential)] internal struct Point {public int X,Y; public Point(int x,int y){X=x;Y=y;} }
  [StructLayout(LayoutKind.Sequential)] internal struct Size {public int X,Y; public Size(int x,int y){X=x;Y=y;} }
  [StructLayout(LayoutKind.Sequential,Pack=1)] internal struct Blend {public byte Op,Flags,Alpha,Format;}
  [DllImport("user32.dll")]internal static extern IntPtr GetDC(IntPtr hwnd);
  [DllImport("user32.dll")]internal static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
  [DllImport("gdi32.dll")]internal static extern IntPtr CreateCompatibleDC(IntPtr dc);
  [DllImport("gdi32.dll")]internal static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
  [DllImport("gdi32.dll")]internal static extern bool DeleteObject(IntPtr obj);
  [DllImport("gdi32.dll")]internal static extern bool DeleteDC(IntPtr dc);
  [DllImport("user32.dll",SetLastError=true)]internal static extern bool UpdateLayeredWindow(IntPtr hwnd,IntPtr dst,ref Point pos,ref Size size,IntPtr src,ref Point origin,int key,ref Blend blend,int flags);
  [DllImport("user32.dll")]internal static extern bool RegisterHotKey(IntPtr hwnd,int id,uint mods,uint key);
  [DllImport("user32.dll")]internal static extern bool UnregisterHotKey(IntPtr hwnd,int id);
  [DllImport("dwmapi.dll")]internal static extern int DwmSetWindowAttribute(IntPtr hwnd,int attr,ref int value,int size);
  [DllImport("user32.dll")]internal static extern IntPtr SendMessage(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam);
 }
 internal class LayeredOverlay : Form {
  readonly bool passThrough;
  public LayeredOverlay(bool through) {passThrough=through;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;}
  protected override bool ShowWithoutActivation {get{return true;}}
  protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x80000|0x80|0x08000000;if(passThrough)p.ExStyle|=0x20;return p;}}
  protected override void WndProc(ref Message m) {if(m.Msg==0x21){m.Result=new IntPtr(3);return;}if(m.Msg==0x84){m.Result=new IntPtr(passThrough?-1:2);return;}base.WndProc(ref m);}
  protected void Present(Bitmap b,int x,int y,byte opacity=255) {
   IntPtr screen=Native.GetDC(IntPtr.Zero),dc=Native.CreateCompatibleDC(screen),bitmap=IntPtr.Zero,old=IntPtr.Zero;
   try {bitmap=b.GetHbitmap(Color.FromArgb(0));old=Native.SelectObject(dc,bitmap);var pos=new Native.Point(x,y);var size=new Native.Size(b.Width,b.Height);var src=new Native.Point(0,0);var blend=new Native.Blend{Alpha=opacity,Format=1};
    if(!Native.UpdateLayeredWindow(Handle,screen,ref pos,ref size,dc,ref src,0,ref blend,2))throw new System.ComponentModel.Win32Exception();
   }finally{if(old!=IntPtr.Zero)Native.SelectObject(dc,old);if(bitmap!=IntPtr.Zero)Native.DeleteObject(bitmap);Native.DeleteDC(dc);Native.ReleaseDC(IntPtr.Zero,screen);}
  }
 }
 internal sealed class CrosshairOverlay : LayeredOverlay {
  public CrosshairOverlay():base(true){}
  public void Update(CrosshairProfile p,Rectangle screen,float scale,int offsetX,int offsetY) {
   float factor=scale*(p.ReferenceHeight>0?screen.Height/(float)p.ReferenceHeight:1);
   float extent=Math.Max(Math.Abs(p.Gap)+Math.Max(p.Length,p.VerticalLength),p.Outer?p.OuterGap+Math.Max(p.OuterLength,p.OuterVerticalLength):0)+p.Outline+p.Thickness+p.DotSize+4;
   int side=Math.Min(2048,Math.Max(64,(int)(extent*factor*2+8)));side+=(side%2);
   using(var bitmap=new Bitmap(side,side,PixelFormat.Format32bppPArgb)) {bitmap.SetResolution(96,96);using(var g=Graphics.FromImage(bitmap))CrosshairPainter.Draw(g,p,side/2,side/2,factor);Present(bitmap,screen.Left+screen.Width/2-side/2+offsetX,screen.Top+screen.Height/2-side/2+offsetY);}
  }
 }
 internal sealed class TimerOverlay : LayeredOverlay {
  readonly Settings settings;readonly RoundService service;readonly ContextMenuStrip serverMenu=new ContextMenuStrip();string last="";public event Action Moved,ConfigureRequested,SelectionChanged;
  public TimerOverlay(Settings s,RoundService rounds):base(false){settings=s;service=rounds;Location=new Point(s.TimerX,s.TimerY);}
  public void Update(ServerEntry server,string error){
   var appearance=settings.TimerAppearance;var content=TimerContent.From(server,settings.OffsetSeconds);
   string key=content.Name+content.Time+content.Phase+content.Rounds+Json.Write(appearance);if(key==last)return;last=key;
   using(var bitmap=TimerRenderer.Render(appearance,content)){using(var g=Graphics.FromImage(bitmap))using(var brush=new SolidBrush(Color.FromArgb(1,0,0,0)))g.FillRectangle(brush,0,0,bitmap.Width,20);var p=Location;if(!Screen.AllScreens.AnyIntersect(new Rectangle(p,bitmap.Size)))p=new Point(Screen.PrimaryScreen.WorkingArea.Left+32,Screen.PrimaryScreen.WorkingArea.Top+80);Present(bitmap,p.X,p.Y);}
  }
  bool NameHit(Point point){var p=settings.TimerAppearance.Parts[0];return p.Visible&&new Rectangle(p.X+p.Width-30,p.Y+p.Height/2-18,36,36).Contains(point);}
  void ShowServers(){
   for(int i=serverMenu.Items.Count-1;i>=0;i--)serverMenu.Items[i].Dispose();serverMenu.Items.Clear();var follow=serverMenu.Items.Add("跟随主界面选择");((ToolStripMenuItem)follow).Checked=string.IsNullOrEmpty(settings.TimerServer);follow.Click+=delegate{SelectServer("");};serverMenu.Items.Add(new ToolStripSeparator());
   foreach(var entry in service.Servers){string key=entry.Key;var item=new ToolStripMenuItem(entry.ShortName){Checked=key==settings.TimerServer};item.Click+=delegate{SelectServer(key);};serverMenu.Items.Add(item);}
   serverMenu.Items.Add(new ToolStripSeparator());serverMenu.Items.Add("外观与布局…",null,delegate{if(ConfigureRequested!=null)ConfigureRequested();});serverMenu.Show(Cursor.Position);
  }
  void SelectServer(string key){settings.TimerServer=key;if(SelectionChanged!=null)SelectionChanged();Update(service.TimerSelected,service.Error);}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left&&NameHit(e.Location))ShowServers();}
  protected override void WndProc(ref Message m){if(m.Msg==0x84){long packed=m.LParam.ToInt64();var point=PointToClient(new Point((short)(packed&0xffff),(short)((packed>>16)&0xffff)));if(NameHit(point)){m.Result=(IntPtr)1;return;}}if(m.Msg==0x7b){ShowServers();return;}base.WndProc(ref m);if(m.Msg==0x232){settings.TimerX=Left;settings.TimerY=Top;if(Moved!=null)Moved();}}
  protected override void Dispose(bool disposing){if(disposing)serverMenu.Dispose();base.Dispose(disposing);}
 }
 internal static class ScreenHelpers {internal static bool AnyIntersect(this Screen[] screens,Rectangle r){foreach(var s in screens)if(s.WorkingArea.IntersectsWith(r))return true;return false;}}
}
