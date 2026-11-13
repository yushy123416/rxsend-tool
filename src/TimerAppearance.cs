using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace RxToolbox {
 public sealed class TimerPart {
  public string Id;public bool Visible=true;public int X,Y,Width,Height;public float FontSize;public bool Bold;
  public TimerPart(){}
  public TimerPart(string id,int x,int y,int w,int h,float size,bool bold){Id=id;X=x;Y=y;Width=w;Height=h;FontSize=size;Bold=bold;}
  public Rectangle Bounds {get{return new Rectangle(X,Y,Width,Height);}}
  public override string ToString(){return Id=="name"?"服务器名称":Id=="time"?"倒计时":Id=="phase"?"回合阶段":"剩余回合";}
 }
 public sealed class TimerAppearance {
  public int Width=360,Height=148,Opacity=240,CornerRadius=12;
  public string FontFamily="Microsoft YaHei UI";
  public int Background=unchecked((int)0xff172033),Foreground=unchecked((int)0xffedf2fa),Accent=unchecked((int)0xff83b4ff);
  public bool PhaseColor=true;
  public TimerPart[] Parts={
   new TimerPart("name",16,28,328,26,11,false),
   new TimerPart("time",14,56,186,76,36,true),
   new TimerPart("phase",214,59,132,28,11,false),
   new TimerPart("rounds",214,93,132,28,11,false)
  };
  public TimerAppearance Clone(){return Json.Read<TimerAppearance>(Json.Write(this));}
  public void Validate(){
   Width=Math.Max(240,Math.Min(1000,Width));Height=Math.Max(96,Math.Min(600,Height));Opacity=Math.Max(0,Math.Min(255,Opacity));CornerRadius=Math.Max(0,Math.Min(40,CornerRadius));
   if(string.IsNullOrWhiteSpace(FontFamily)||FontFamily.Length>100)FontFamily="Microsoft YaHei UI";
   var defaults=new TimerAppearance();var parts=new TimerPart[4];
   for(int i=0;i<parts.Length;i++){TimerPart found=null;if(Parts!=null)foreach(var part in Parts)if(part!=null&&part.Id==defaults.Parts[i].Id){found=part;break;}var p=found??defaults.Parts[i];p.Width=Math.Max(24,Math.Min(Width,p.Width));p.Height=Math.Max(20,Math.Min(Height,p.Height));p.X=Math.Max(0,Math.Min(Width-p.Width,p.X));p.Y=Math.Max(0,Math.Min(Height-p.Height,p.Y));p.FontSize=float.IsNaN(p.FontSize)||float.IsInfinity(p.FontSize)?12:Math.Max(8,Math.Min(80,p.FontSize));parts[i]=p;}Parts=parts;
  }
 }
 internal sealed class TimerContent {
  public string Name,Time,Phase,Rounds;
  public string Text(string id){return id=="name"?Name:id=="time"?Time:id=="phase"?Phase:Rounds;}
  public static TimerContent From(ServerEntry server,int offset){var r=server==null?null:server.Round;return new TimerContent{Name=server==null?"选择服务器":server.ShortName,Time=Ui.Time(r==null?-1:r.Remaining(offset)),Phase=r==null?"等待同步":r.Stale?"数据过期":r.Data.round_phase,Rounds="剩余 "+(r==null||r.Stale||r.Data.rounds_left<0?"—":r.Data.rounds_left.ToString())+" 回合"};}
  public static TimerContent Sample {get{return new TimerContent{Name="娘娘测试服",Time="04:32",Phase="回合中",Rounds="剩余 12 回合"};}}
 }
 internal static class TimerRenderer {
  internal static Font FontFor(TimerAppearance a,TimerPart p){try{return new Font(a.FontFamily,p.FontSize,p.Bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Point);}catch{return new Font("Segoe UI",p.FontSize,p.Bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Point);}}
  internal static Bitmap Render(TimerAppearance a,TimerContent content){
   var bitmap=new Bitmap(a.Width,a.Height,PixelFormat.Format32bppPArgb);bitmap.SetResolution(96,96);
   using(var g=Graphics.FromImage(bitmap)){g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
    using(var brush=new SolidBrush(Color.FromArgb(a.Opacity,Color.FromArgb(a.Background)))){
     if(a.CornerRadius==0)g.FillRectangle(brush,0,0,a.Width,a.Height);else using(var path=Ui.Round(new RectangleF(0,0,a.Width,a.Height),a.CornerRadius))g.FillPath(brush,path);
    }
    foreach(var part in a.Parts){if(!part.Visible)continue;var state=g.Save();g.SetClip(part.Bounds);
     Color color=Color.FromArgb(255,Color.FromArgb(part.Id=="time"?a.Accent:a.Foreground));
     if(part.Id=="time"&&a.PhaseColor&&content.Phase!=null&&content.Phase.Contains("核弹"))color=Color.FromArgb(255,117,127);
     var bounds=part.Bounds;if(part.Id=="name")bounds.Width=Math.Max(1,bounds.Width-22);
     using(var font=FontFor(a,part))using(var brush=new SolidBrush(color))using(var format=new StringFormat(StringFormat.GenericTypographic){Alignment=StringAlignment.Near,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})g.DrawString(content.Text(part.Id)??"",font,brush,bounds,format);
     if(part.Id=="name"){float x=part.X+part.Width-12,y=part.Y+part.Height/2f;using(var pen=new Pen(color,1.5f))g.DrawLines(pen,new[]{new PointF(x-4,y-2),new PointF(x,y+2),new PointF(x+4,y-2)});}g.Restore(state);
    }
   }return bitmap;
  }
 }
}
