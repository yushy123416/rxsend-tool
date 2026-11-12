using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace RxToolbox {
 public sealed class CrosshairPreset {public string Name;public CrosshairProfile Profile; public override string ToString(){return Name;} }
 public sealed class CrosshairProfile {
  public string Name="薄荷十字", Source="自定义", Code="", Shape="cross";
  public int Red=77,Green=239,Blue=189,Alpha=255,OutlineRed=0,OutlineGreen=0,OutlineBlue=0,OutlineAlpha=200,ReferenceHeight=0;
  public float Length=7,VerticalLength=7,Thickness=2,Gap=4,Outline=1,DotSize=2,DotAlpha=1,InnerAlpha=1,OuterLength=2,OuterVerticalLength=2,OuterThickness=2,OuterGap=10,OuterAlpha=.35f;
  public bool Dot=false,TStyle=false,Inner=true,Outer=false;
  public void Validate() {
   foreach(var v in new[]{Length,VerticalLength,Thickness,Gap,Outline,DotSize,DotAlpha,InnerAlpha,OuterLength,OuterVerticalLength,OuterThickness,OuterGap,OuterAlpha}) if(float.IsNaN(v)||float.IsInfinity(v))throw new InvalidDataException("准星参数无效");
   Red=Clamp(Red,0,255);Green=Clamp(Green,0,255);Blue=Clamp(Blue,0,255);Alpha=Clamp(Alpha,0,255);
   OutlineRed=Clamp(OutlineRed,0,255);OutlineGreen=Clamp(OutlineGreen,0,255);OutlineBlue=Clamp(OutlineBlue,0,255);OutlineAlpha=Clamp(OutlineAlpha,0,255);
   Length=Limit(Length,0,255);VerticalLength=Limit(VerticalLength,0,255);Thickness=Limit(Thickness,0,32);Gap=Limit(Gap,-3840,3840);Outline=Limit(Outline,0,8);DotSize=Limit(DotSize,0,32);DotAlpha=Limit(DotAlpha,0,1);
   OuterLength=Limit(OuterLength,0,255);OuterVerticalLength=Limit(OuterVerticalLength,0,255);OuterThickness=Limit(OuterThickness,0,32);OuterGap=Limit(OuterGap,0,255);OuterAlpha=Limit(OuterAlpha,0,1);
   ReferenceHeight=Clamp(ReferenceHeight,0,16384);InnerAlpha=Limit(InnerAlpha,0,1);
  }
  static int Clamp(int n,int lo,int hi){return Math.Max(lo,Math.Min(hi,n));} static float Limit(float n,float lo,float hi){return Math.Max(lo,Math.Min(hi,n));}
  public CrosshairProfile Clone(){return Json.Read<CrosshairProfile>(Json.Write(this));}
 }
 public interface ICrosshairImporter {bool Accepts(string code);CrosshairProfile Parse(string code);}
 public static class CrosshairImport {
  static readonly ICrosshairImporter[] Importers={new CounterStrikeImporter(),new ValorantImporter()};
  public static CrosshairProfile Parse(string code) {code=(code??"").Trim();if(code.Length>4096)throw new InvalidDataException("准星码过长");foreach(var i in Importers)if(i.Accepts(code)){var p=i.Parse(code);p.Code=code;p.Validate();return p;}throw new InvalidDataException("请输入 CS2 或无畏契约准星码。");}
 }
 public sealed class CounterStrikeImporter : ICrosshairImporter {
  const string Alphabet="ABCDEFGHJKLMNOPQRSTUVWXYZabcdefhijkmnopqrstuvwxyz23456789";
  public bool Accepts(string s){return s.StartsWith("CS",StringComparison.Ordinal);}
  public CrosshairProfile Parse(string code) {
   bool legacy=code.StartsWith("CSGO");string text;
   if(legacy){if(!Regex.IsMatch(code,@"^CSGO(?:-?[A-Za-z2-9]{5}){5}$"))throw new InvalidDataException("CSGO 准星码格式无效");text=code.Substring(4).Replace("-","");}
   else {if(code.Length!=46)throw new InvalidDataException("CS2 准星码长度无效");text=code.Substring(2);}
   BigInteger n=BigInteger.Zero;
   for(int i=text.Length-1;i>=0;i--){int v=Alphabet.IndexOf(text[i]);if(v<0)throw new InvalidDataException("准星码包含无效字符");n=n*57+v;}
   int size=legacy?18:32;byte[] b=new byte[size];for(int i=size-1;i>=0;i--){b[i]=(byte)(n&255);n>>=8;}
   if(n!=0||b[0]!=(b.Skip(1).Sum(x=>(int)x)&255))throw new InvalidDataException("准星码校验失败，请完整复制");
   var p=new CrosshairProfile{Source="CS2",Name="CS2 导入",Outer=false};int style;
   if(!legacy){
    if(b[1]!=1)throw new InvalidDataException("暂不支持此 CS2 准星码版本");
    p.ReferenceHeight=b[2]|b[3]<<8;style=b[4]&31;p.Dot=(b[4]&64)!=0;p.TStyle=(b[4]&128)!=0;
    p.Red=b[5];p.Green=b[6];p.Blue=b[7];p.Alpha=b[8];p.OutlineRed=b[9];p.OutlineGreen=b[10];p.OutlineBlue=b[11];p.OutlineAlpha=b[12];
    p.Thickness=b[13]&63;int outline=b[13]>>6;p.Outline=outline==0?0:outline==2?.5f:1;
    p.Gap=(short)(b[14]|b[15]<<8);p.Length=b[16];
   } else if(b[1]==1){
    p.Source="CSGO / CS2 旧版";p.Gap=Math.Max(0,4+(sbyte)b[2]/10f);p.Length=b[14]/10f;p.Thickness=b[12]/10f;p.ReferenceHeight=480;
    p.Outline=(b[10]&8)!=0?b[3]/2f:0;p.Red=b[4];p.Green=b[5];p.Blue=b[6];p.Alpha=(b[13]&64)!=0?b[7]:255;
    int color=b[10]&7;Color[] palette={Color.Red,Color.Lime,Color.Yellow,Color.Blue,Color.Cyan};if(color<5){p.Red=palette[color].R;p.Green=palette[color].G;p.Blue=palette[color].B;}
    p.Dot=(b[13]&16)!=0;p.TStyle=(b[13]&128)!=0;style=(b[13]&15)>>1;
   } else if(b[1]==3||b[1]==4){
    p.ReferenceHeight=b[14]|b[15]<<8;style=b[2]&15;p.Dot=(b[2]&64)!=0;p.TStyle=(b[2]&128)!=0;p.Red=b[3];p.Green=b[4];p.Blue=b[5];p.Alpha=b[6];p.Gap=b[7];p.Length=b[8];
    uint bits=BitConverter.ToUInt32(b,10);p.Thickness=(bits>>23)&31;int outline=b[1]==3?((b[2]&32)!=0?1:0):(int)((bits>>28)&3);p.Outline=outline==0?0:outline==2?.5f:1;
   } else throw new InvalidDataException("暂不支持此旧版准星码版本");
   if(!(legacy&&b[1]==1)) {if(style==1||style==3)p.Shape="circle";else if(style==8)p.Shape="square";else if(style==7||style==9)p.Shape="quad";else if(style==6){p.Inner=false;p.Dot=true;}}
   p.VerticalLength=p.Length;p.DotSize=p.Thickness;return p;
  }
 }
 public sealed class ValorantImporter : ICrosshairImporter {
  public bool Accepts(string s){return s.StartsWith("0;");}
  public CrosshairProfile Parse(string code) {
   var parts=code.Split(';');var fields=new Dictionary<string,string>();bool primary=false,found=false;
   for(int i=1;i<parts.Length;){string k=parts[i++];if(k=="P"||k=="A"||k=="S"){primary=k=="P";found|=primary;continue;}if(i>=parts.Length)throw new InvalidDataException("无畏契约准星码不完整");string value=parts[i++];if(primary){if(fields.ContainsKey(k))throw new InvalidDataException("准星码包含重复参数");fields[k]=value;}}
   if(!found)throw new InvalidDataException("准星码缺少主准星 P 段");
   var p=new CrosshairProfile{Source="无畏契约",Name="无畏契约导入",Red=255,Green=255,Blue=255,Alpha=255,Outline=1,OutlineAlpha=128,DotSize=2,Length=6,VerticalLength=6,Thickness=2,Gap=3,Outer=true};
   int c=(int)N(fields,"c",0,0,8);Color[] colors={Color.White,Color.Lime,Color.FromArgb(127,255,0),Color.FromArgb(223,255,0),Color.Yellow,Color.Cyan,Color.Magenta,Color.Red};
   Color color=c<8?colors[c]:Color.White;
   if(c==8){string hex;if(!fields.TryGetValue("u",out hex)||!Regex.IsMatch(hex,"^[A-Fa-f0-9]{8}$"))throw new InvalidDataException("自定义颜色格式无效");color=Color.FromArgb(Convert.ToInt32(hex.Substring(6,2),16),Convert.ToInt32(hex.Substring(0,2),16),Convert.ToInt32(hex.Substring(2,2),16),Convert.ToInt32(hex.Substring(4,2),16));}
   p.Red=color.R;p.Green=color.G;p.Blue=color.B;p.Alpha=color.A;
   p.Outline=B(fields,"h",true)?N(fields,"t",1,0,6):0;p.OutlineAlpha=(int)(N(fields,"o",.5f,0,1)*255);
   p.Dot=B(fields,"d",false);p.DotSize=N(fields,"z",2,0,10);p.DotAlpha=N(fields,"a",1,0,1);
   p.Inner=B(fields,"0b",true);p.Length=N(fields,"0l",6,0,20);p.VerticalLength=B(fields,"0g",false)?N(fields,"0v",6,0,20):p.Length;p.Thickness=N(fields,"0t",2,0,10);p.Gap=N(fields,"0o",3,0,40);
   p.InnerAlpha=N(fields,"0a",.8f,0,1);
   p.Outer=B(fields,"1b",true);p.OuterLength=N(fields,"1l",2,0,10);p.OuterVerticalLength=B(fields,"1g",false)?N(fields,"1v",2,0,10):p.OuterLength;p.OuterThickness=N(fields,"1t",2,0,10);p.OuterGap=N(fields,"1o",10,0,40);p.OuterAlpha=N(fields,"1a",.35f,0,1);
   return p;
  }
  static bool B(Dictionary<string,string>d,string k,bool fallback){return N(d,k,fallback?1:0,0,1)==1;}
  static float N(Dictionary<string,string>d,string k,float fallback,float lo,float hi){string s;if(!d.TryGetValue(k,out s))return fallback;float n;if(!float.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out n)||float.IsNaN(n)||float.IsInfinity(n)||n<lo||n>hi)throw new InvalidDataException("准星参数无效："+k);return n;}
 }
 internal static class CrosshairPainter {
  public static void Draw(Graphics g,CrosshairProfile p,float x,float y,float scale) {
   g.SmoothingMode=SmoothingMode.None;float thick=Math.Max(1,p.Thickness*scale),gap=p.Gap*scale,len=p.Length*scale,vertical=p.VerticalLength*scale;
   using(var fill=new SolidBrush(Color.FromArgb((int)(p.Alpha*p.InnerAlpha),p.Red,p.Green,p.Blue)))using(var outline=new SolidBrush(Color.FromArgb(p.OutlineAlpha,p.OutlineRed,p.OutlineGreen,p.OutlineBlue))) {
    float o=p.Outline*scale;
    if(p.Inner&&p.Thickness>0) {
     if(p.Shape=="circle"||p.Shape=="square") {
      float radius=Math.Max(1,Math.Abs(gap)+len/2);var rect=new RectangleF(x-radius,y-radius,2*radius,2*radius);
      g.SmoothingMode=SmoothingMode.AntiAlias;
      using(var edge=new Pen(outline,thick+2*o))using(var pen=new Pen(fill,thick)){if(o>0){if(p.Shape=="circle")g.DrawEllipse(edge,rect);else g.DrawRectangle(edge,rect.X,rect.Y,rect.Width,rect.Height);}if(p.Shape=="circle")g.DrawEllipse(pen,rect);else g.DrawRectangle(pen,rect.X,rect.Y,rect.Width,rect.Height);}
      g.SmoothingMode=SmoothingMode.None;
     } else if(p.Shape=="quad") {
      for(int sx=-1;sx<=1;sx+=2)for(int sy=-1;sy<=1;sy+=2) Rect(g,fill,outline,x+sx*(gap+len/2)-thick/2,y+sy*(gap+len/2)-thick/2,thick,thick,o);
     } else Lines(g,fill,outline,x,y,len,vertical,thick,gap,o,p.TStyle);
    }
    if(p.Outer&&p.OuterThickness>0)using(var outer=new SolidBrush(Color.FromArgb((int)(p.Alpha*p.OuterAlpha),p.Red,p.Green,p.Blue)))Lines(g,outer,outline,x,y,p.OuterLength*scale,p.OuterVerticalLength*scale,Math.Max(1,p.OuterThickness*scale),p.OuterGap*scale,o,p.TStyle);
    if(p.Dot&&p.DotSize>0)using(var dot=new SolidBrush(Color.FromArgb((int)(p.Alpha*p.DotAlpha),p.Red,p.Green,p.Blue))){float d=Math.Max(1,p.DotSize*scale);Rect(g,dot,outline,x-d/2,y-d/2,d,d,o);}
   }
  }
  static void Lines(Graphics g,Brush fill,Brush border,float x,float y,float len,float vertical,float t,float gap,float o,bool tee){if(len>0){Rect(g,fill,border,x+gap,y-t/2,len,t,o);Rect(g,fill,border,x-gap-len,y-t/2,len,t,o);}if(vertical>0){Rect(g,fill,border,x-t/2,y+gap,t,vertical,o);if(!tee)Rect(g,fill,border,x-t/2,y-gap-vertical,t,vertical,o);}}
  static void Rect(Graphics g,Brush fill,Brush border,float x,float y,float w,float h,float o){x=(float)Math.Round(x);y=(float)Math.Round(y);w=Math.Max(1,(float)Math.Round(w));h=Math.Max(1,(float)Math.Round(h));if(o>0)g.FillRectangle(border,x-o,y-o,w+o*2,h+o*2);g.FillRectangle(fill,x,y,w,h);}
 }
}
