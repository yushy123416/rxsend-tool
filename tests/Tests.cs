using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RxToolbox {
 internal static class Tests {
  static int count;
  static async Task AutoJoinChecks(){
   var clock=new DateTime(2026,11,12,0,0,0,DateTimeKind.Utc);var server=new ServerEntry{Host="127.0.0.1",Port=27015,Name="测试服"};int queries=0,launches=0;string launched=null;var response=new A2sInfo{Players=70,MaxPlayers=70};
   using(var join=new AutoJoinService((h,p)=>{queries++;return response;},uri=>{launches++;launched=uri;},()=>clock)){
    join.Start(server);await join.PollAsync();Check(join.Running&&launches==0&&queries==1,"auto join waits while server is full");
    await join.PollAsync();Check(queries==1,"auto join enforces five-second query interval");
    clock=clock.AddSeconds(5);response=new A2sInfo{Players=69,MaxPlayers=70};await join.PollAsync();await join.PollAsync();Check(!join.Running&&launches==1&&launched=="steam://connect/127.0.0.1:27015","auto join launches exactly once when a fresh query finds a slot");
   }
   queries=0;launches=0;
   using(var join=new AutoJoinService((h,p)=>{queries++;if(queries==1)throw new IOException("timeout");return new A2sInfo{Players=1,MaxPlayers=2};},uri=>launches++,()=>clock)){
    join.Start(server);await join.PollAsync();Check(join.Running&&launches==0,"query failures keep waiting without launching");
    clock=clock.AddSeconds(5);await join.PollAsync();Check(launches==1&&!join.Running,"auto join recovers on a later valid query");
   }
   launches=0;
   using(var join=new AutoJoinService((h,p)=>new A2sInfo{Players=0,MaxPlayers=0},uri=>launches++,()=>clock)){join.Start(server);await join.PollAsync();Check(join.Running&&launches==0,"unknown capacity never triggers auto join");join.Stop();}
   using(var release=new System.Threading.ManualResetEventSlim(false)){
    var entered=new TaskCompletionSource<bool>();launches=0;
    using(var join=new AutoJoinService((h,p)=>{entered.TrySetResult(true);if(!release.Wait(5000))throw new TimeoutException();return new A2sInfo{Players=1,MaxPlayers=2};},uri=>launches++,()=>clock)){
     join.Start(server);var pending=join.PollAsync();await entered.Task;join.Stop();release.Set();await pending;Check(!join.Running&&launches==0,"cancelling an in-flight query prevents later launch");
    }
   }
   using(var release=new System.Threading.ManualResetEventSlim(false)){
    var entered=new TaskCompletionSource<bool>();launches=0;queries=0;launched=null;
    using(var join=new AutoJoinService((h,p)=>{if(++queries==1){entered.TrySetResult(true);if(!release.Wait(5000))throw new TimeoutException();}return new A2sInfo{Players=1,MaxPlayers=2};},uri=>{launches++;launched=uri;},()=>clock)){
     join.Start(server);var pending=join.PollAsync();await entered.Task;join.Start(new ServerEntry{Host="127.0.0.2",Port=27016,Name="新目标"});await join.PollAsync();Check(queries==1,"restarting cannot create overlapping queries");release.Set();await pending;Check(launches==0&&join.Running,"previous target result cannot launch after restart");await join.PollAsync();Check(launches==1&&launched=="steam://connect/127.0.0.2:27016","restarted queue launches only the new target");
    }
   }
   launches=0;
   using(var join=new AutoJoinService((h,p)=>new A2sInfo{Players=1,MaxPlayers=2},uri=>{launches++;throw new IOException("Steam missing");},()=>clock)){join.Start(server);await join.PollAsync();clock=clock.AddSeconds(10);await join.PollAsync();Check(!join.Running&&launches==1&&join.Status.Contains("无法发起进服"),"Steam launch failure stops rather than repeatedly launching");}
   Reject(()=>SteamConnect.BuildUri("127.0.0.1;+quit",27015),"reject command injection in connection target");
   Reject(()=>SteamConnect.BuildUri("127.0.0.1",0),"reject invalid game port");
   Check(SteamConnect.BuildUri("::1",27015)=="steam://connect/[::1]:27015","IPv6 connection target is bracketed");
  }
  static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
  static void Reject(Action action,string name){try{action();}catch{Check(true,name);return;}throw new Exception("Expected rejection: "+name);}
  static string Encode(byte[] b,bool legacy){b[0]=(byte)(b.Skip(1).Sum(x=>(int)x)&255);BigInteger n=0;foreach(byte x in b)n=(n<<8)+x;const string a="ABCDEFGHJKLMNOPQRSTUVWXYZabcdefhijkmnopqrstuvwxyz23456789";var s=new StringBuilder();int len=legacy?25:44;for(int i=0;i<len;i++){s.Append(a[(int)(n%57)]);n/=57;}return legacy?"CSGO-"+string.Join("-",Enumerable.Range(0,5).Select(i=>s.ToString().Substring(i*5,5))):"CS"+s;}
  static byte[] InfoPacket(){using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){w.Write(-1);w.Write((byte)0x49);w.Write((byte)17);foreach(string s in new[]{"测试服务器","rx_site","garrysmod","RXBreach"}){w.Write(Encoding.UTF8.GetBytes(s));w.Write((byte)0);}w.Write((ushort)4000);w.Write((byte)12);w.Write((byte)64);w.Write((byte)2);return m.ToArray();}}
  [STAThread]public static int Main(){try{
   var cs=CrosshairImport.Parse("CSvbPubOq37zTGqtsPTP5QTrp5CB4xFXiKRLfzJsm49ZRe");Check(cs.Red==255&&cs.Green==0&&cs.Blue==0&&cs.Length==5&&cs.Thickness==1&&cs.Dot&&cs.ReferenceHeight==768,"current CS2 reference vector");
   var old=CrosshairImport.Parse("CSGO-Cn37R-YE7vo-pLCAL-aURmZ-z6zkG");Check(old.Source.Contains("旧版"),"legacy CSGO reference vector");
   foreach(int version in new[]{3,4}){byte[] b=new byte[18];b[1]=(byte)version;b[2]=68;b[3]=20;b[4]=200;b[7]=3;b[8]=7;b[12]=128;b[14]=56;b[15]=4;var p=CrosshairImport.Parse(Encode(b,true));Check(p.Length==7&&p.Thickness==1&&p.ReferenceHeight==1080&&p.Dot,"legacy pixel v"+version);}
   byte[] signed=new byte[32];signed[1]=1;signed[4]=4;signed[13]=2;signed[14]=254;signed[15]=255;signed[16]=6;Check(CrosshairImport.Parse(Encode(signed,false)).Gap==-2,"signed CS2 gap");
   Reject(()=>CrosshairImport.Parse("CSvbPubOq37zTGqtsPTP5QTrp5CB4xFXiKRLfzJsm49ZRf"),"checksum corruption");Reject(()=>CrosshairImport.Parse("CSGO-AAAAA-AAAAA-AAAAA-AAAAA-AAAAg"),"invalid base57 alphabet");
   var val=CrosshairImport.Parse("0;s;1;P;c;5;h;0;0l;4;0o;2;0a;1;0f;0;1b;0;A;c;7;0l;20;S;c;4;o;1");Check(val.Red==0&&val.Green==255&&val.Blue==255&&val.Length==4&&val.Gap==2&&!val.Outer&&val.Outline==0,"VALORANT P section isolated from ADS and scope");
   var custom=CrosshairImport.Parse("0;P;c;8;u;12AB34FF;0l;7;0v;3;0g;1");Check(custom.Red==18&&custom.Green==171&&custom.VerticalLength==3,"VALORANT custom color and unlinked length");
   Reject(()=>CrosshairImport.Parse("0;P;0l;NaN"),"nonfinite values");Reject(()=>CrosshairImport.Parse("0;P;0l"),"truncated VALORANT pair");Reject(()=>CrosshairImport.Parse("0;P;0l;3;0l;4"),"duplicate VALORANT fields");
   long now=(long)(DateTime.UtcNow-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds;
   var round=RoundSnapshot.Create(new ApiServer{round_time_left=923,rounds_left=0,updated_at=now-3},now,DateTime.UtcNow);Check(round.Remaining(0)<=920&&round.Remaining(0)>=919&&!round.Stale,"countdown compensates sample age");Check(round.Data.rounds_left==0,"zero remaining rounds preserved");
   round=RoundSnapshot.Create(new ApiServer{round_time_left=923,updated_at=now-100},now,DateTime.UtcNow);Check(round.Stale&&round.Remaining(0)==-1,"stale snapshot never displays live time");
   round=RoundSnapshot.Create(new ApiServer{round_time_left=-1,updated_at=now},now,DateTime.UtcNow);Check(round.Remaining(30)==-1,"idle sentinel preserved");
   var env=Json.Read<ApiEnvelope>("{\"server_time\":1794471201,\"servers\":[{\"ip\":\"202.189.21.228\",\"port\":27076,\"rounds_left\":1,\"round_time_left\":923,\"round_phase\":\"回合中\",\"updated_at\":1794471201}]}");Check(env.servers[0].round_time_left==923,"actual API schema");
   var info=A2s.ParseInfo(InfoPacket());Check(info.Name=="测试服务器"&&info.Players==12&&info.MaxPlayers==64&&info.Map=="rx_site","A2S UTF-8 response parser");Reject(()=>A2s.ParseInfo(new byte[]{255,255,255,255,0x49}),"truncated A2S packet");
   UdpFixture();
   Reject(()=>Updates.Validate(new UpdateManifest{version="1.1.0",url="https://example.com/app.exe",sha256="wrong"}),"update checksum mandatory");Reject(()=>HttpData.Https("http://example.com"),"reject insecure update source");
   Check(Updates.ParseVersion("v1.10.0")>Updates.ParseVersion("1.9.9"),"semantic version comparison");Check(Updates.Hash(Encoding.ASCII.GetBytes("abc"))=="ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad","SHA256 known vector");
   byte[] exe=File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"RxToolbox.exe"));var manifest=new UpdateManifest{version=Updates.Current.ToString(3),url="https://example.com/primary.exe",mirrors=new[]{"https://mirror.example.com/app.exe"},sha256=Updates.Hash(exe)};
   int requests=0;var checkedManifest=Updates.CheckCore("owner/repo","https://mirror.example.com/update.json",url=>{requests++;if(requests==1)throw new IOException("offline");return Json.Write(manifest);});Check(requests==2&&checkedManifest.version==Updates.Current.ToString(3),"manifest fallback from mirror to GitHub");
   requests=0;string staged=Updates.DownloadCore(manifest,url=>{requests++;return requests==1?new byte[]{0}:exe;});Check(requests==2&&File.ReadAllBytes(staged).SequenceEqual(exe),"download fallback after corrupt primary and assembly identity check");File.Delete(staged);Directory.Delete(Path.GetDirectoryName(staged));
   manifest.version=new Version(Updates.Current.Major+1,0,0).ToString(3);Reject(()=>Updates.DownloadCore(manifest,url=>exe),"assembly version mismatch rejected");
   using(var bitmap=new Bitmap(200,200))using(var g=Graphics.FromImage(bitmap)){CrosshairPainter.Draw(g,new CrosshairProfile(),100,100,1);Check(bitmap.GetPixel(105,100).A>0&&bitmap.GetPixel(100,100).A==0,"crosshair raster and center gap");}
   using(var field=new NumberField(0,-300,300,1)){
    int changes=0;field.ValueChanged+=delegate{changes++;};var text=field.Controls.OfType<System.Windows.Forms.TextBox>().Single();
    text.Text="-301";field.Commit();Check(field.Value==-300&&changes==1,"numeric input clamps negative compensation to minimum");
    text.Text="invalid";field.Commit();Check(field.Value==-300&&text.Text=="-300"&&changes==1,"invalid numeric input restores last valid value");
    text.Text="23";field.Commit();Check(field.Value==23&&changes==2,"typed compensation commits once");
    field.Value=999;Check(field.Value==300,"numeric upper limit enforced");
   }
   using(var field=new NumberField(1,.25m,4,.05m)){var text=field.Controls.OfType<System.Windows.Forms.TextBox>().Single();text.Text=1.235m.ToString(System.Globalization.CultureInfo.CurrentCulture);field.Commit();Check(field.Value==1.24m,"fractional scale rounds to two decimal places");}
   var appearance=new TimerAppearance{Width=-1,Height=9000,Opacity=0,Parts=null};appearance.Validate();Check(appearance.Width==240&&appearance.Height==600&&appearance.Opacity==0&&appearance.Parts.Length==4,"timer layout validates sizes and restores missing components");
   appearance=new TimerAppearance();var draft=appearance.Clone();draft.Parts[1].X=48;Check(appearance.Parts[1].X==14,"timer editor draft does not mutate saved layout");
   draft.Parts[1].X=9999;draft.Parts[1].Y=-20;draft.Parts[1].FontSize=float.NaN;draft.Validate();Check(draft.Parts[1].X+draft.Parts[1].Width<=draft.Width&&draft.Parts[1].Y==0&&draft.Parts[1].FontSize==12,"timer components stay within canvas and reject invalid font size");
   appearance=new TimerAppearance{Background=System.Drawing.Color.Black.ToArgb(),Accent=System.Drawing.Color.White.ToArgb(),PhaseColor=false};
   foreach(var part in appearance.Parts)part.Visible=part.Id=="time";
   using(var bitmap=TimerRenderer.Render(appearance,TimerContent.Sample)){var bounds=appearance.Parts[1].Bounds;int top=bitmap.Height,bottom=0;for(int by=bounds.Top;by<bounds.Bottom;by++)for(int bx=bounds.Left;bx<bounds.Right;bx++){var pixel=bitmap.GetPixel(bx,by);if(pixel.A>240&&pixel.R>200){top=Math.Min(top,by);bottom=Math.Max(bottom,by);}}Check(top<bottom&&Math.Abs((top+bottom)/2.0-(bounds.Top+bounds.Height/2.0))<7,"countdown glyphs are vertically centered within component");}
   using(var timerBitmap=TimerRenderer.Render(new TimerAppearance(),TimerContent.Sample))Check(Math.Abs(timerBitmap.HorizontalResolution-96)<.1&&Math.Abs(timerBitmap.VerticalResolution-96)<.1,"timer rendering uses fixed DPI so high-DPI displays cannot clip digits");
   var timerSettings=new Settings();using(var service=new RoundService(timerSettings)){timerSettings.SelectedServer=service.Servers[0].Key;timerSettings.TimerServer=service.Servers[1].Key;Check(service.TimerSelected==service.Servers[1]&&service.Selected==service.Servers[0],"overlay server selection is independent of main list");timerSettings.TimerServer="";Check(service.TimerSelected==service.Selected,"overlay can follow the main server selection");timerSettings.TimerServer="missing";Check(service.TimerSelected==service.Selected,"missing overlay server falls back to current main selection");}
   var transparentTimer=new TimerAppearance{Opacity=0};using(var transparent=TimerRenderer.Render(transparentTimer,TimerContent.Sample)){
    Check(transparent.GetPixel(transparent.Width-20,transparent.Height-10).A==0,"zero background opacity leaves empty areas fully transparent");
    var area=transparentTimer.Parts[1].Bounds;int opaqueText=0;for(int ty=area.Top;ty<area.Bottom;ty++)for(int tx=area.Left;tx<area.Right;tx++)if(transparent.GetPixel(tx,ty).A==255)opaqueText++;
    Check(opaqueText>100,"zero background opacity preserves opaque countdown glyphs");
   }
   transparentTimer.Opacity=128;using(var translucent=TimerRenderer.Render(transparentTimer,TimerContent.Sample))Check(translucent.GetPixel(transparentTimer.Width-20,transparentTimer.Height-10).A==128,"background pixels use configured alpha independently of text");
   Task.Run((Func<Task>)AutoJoinChecks).GetAwaiter().GetResult();
   using(var checkbox=new FluentCheck{Text="核弹红色",Size=new Size(180,36),Checked=true})using(var bitmap=new Bitmap(180,36)){
    using(var g=Graphics.FromImage(bitmap))g.Clear(Color.Magenta);
    checkbox.DrawToBitmap(bitmap,new Rectangle(0,0,180,36));
    Check(bitmap.GetPixel(179,18).ToArgb()==Ui.Card.ToArgb(),"checkbox repaints entire surface instead of retaining hovered button pixels");
    int changes=0;checkbox.CheckedChanged+=delegate{changes++;};checkbox.AccessibilityObject.DoDefaultAction();Check(!checkbox.Checked&&changes==1,"custom checkbox accessibility action toggles state exactly once");
   }
   var oldSettings=new Settings{GithubRepo="",MirrorUrl=""};Updates.UseDefaultSource(oldSettings);Check(oldSettings.GithubRepo=="yushy123416/rxsend-tool"&&oldSettings.MirrorUrl==Updates.DefaultMirrorUrl,"legacy empty update source migrates to Gitee primary and GitHub fallback");
   var githubSettings=new Settings{GithubRepo=Updates.DefaultRepository,MirrorUrl=""};Updates.UseDefaultSource(githubSettings);Check(githubSettings.MirrorUrl==Updates.DefaultMirrorUrl,"existing default GitHub settings gain Gitee primary mirror");
   var mirrorSettings=new Settings{GithubRepo="",MirrorUrl="https://example.com/update.json"};Updates.UseDefaultSource(mirrorSettings);Check(mirrorSettings.GithubRepo==""&&mirrorSettings.MirrorUrl=="https://example.com/update.json","custom mirror configuration is preserved");
   var window=new MainWindow();window.Dispose();window.Dispose();Check(window.IsDisposed,"main window can be disposed twice without tray null reference");
   Console.WriteLine(count+" checks passed");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
  static void UdpFixture(){using(var server=new UdpClient(new IPEndPoint(IPAddress.Loopback,0))){server.Client.ReceiveTimeout=4000;int port=((IPEndPoint)server.Client.LocalEndPoint).Port;var task=Task.Run(delegate{IPEndPoint remote=null;byte[] first=server.Receive(ref remote);if(first.Length!=25||first[4]!=0x54)throw new Exception("wrong info request");byte[] challenge={255,255,255,255,65,11,22,33,44};server.Send(challenge,challenge.Length,remote);byte[] second=server.Receive(ref remote);if(second.Length!=29||second[25]!=11)throw new Exception("missing challenge");var packet=InfoPacket();int half=packet.Length/2;for(int part=1;part>=0;part--){using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){w.Write(-2);w.Write(123);w.Write((byte)2);w.Write((byte)part);w.Write((ushort)1248);int start=part==0?0:half;w.Write(packet,start,part==0?half:packet.Length-half);var bytes=m.ToArray();server.Send(bytes,bytes.Length,remote);}}});var info=A2s.QueryInfo("127.0.0.1",port);task.GetAwaiter().GetResult();Check(info.Players==12,"A2S challenge and out-of-order split packets");}}
 }
}
