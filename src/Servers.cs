using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxToolbox {
 public sealed class ApiEnvelope { public long server_time; public List<ApiServer> servers; }
 public sealed class ApiServer { public string server_name,ip,round_phase; public int port,rounds_left=-1,round_time_left=-1; public long updated_at; }
 public sealed class RoundSnapshot {
  public ApiServer Data; public double BaseAge; public long Stamp;
  public double Age { get { return BaseAge+(Stopwatch.GetTimestamp()-Stamp)/(double)Stopwatch.Frequency; } }
  public bool Stale { get { return Age>90; } }
  public int Remaining(int offset) { return Data.round_time_left<0 || Stale ? -1 : Math.Max(0,Data.round_time_left-(int)Age+offset); }
  public static RoundSnapshot Create(ApiServer s,long serverTime,DateTime now) {
   double local= (now.ToUniversalTime()-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds;
   return new RoundSnapshot {Data=s,Stamp=Stopwatch.GetTimestamp(),BaseAge=Math.Max(Math.Max(0,serverTime-s.updated_at),Math.Max(0,local-serverTime))};
  }
 }
 public sealed class ServerEntry {
  public string Host,Name; public int Port; public RoundSnapshot Round; public A2sInfo Info; public DateTime InfoAt;
  public string InfoError="尚未查询";
  public string Key {get {return Host+":"+Port;} }
  public string ShortName { get { var n=Name??Key; int bar=n.IndexOf('|'); if(bar>=0)n=n.Substring(0,bar); return n.Replace("[CN]Neo RXBreach ",""); } }
  public override string ToString() { return ShortName; }
 }
 public static class HttpData {
  public static Uri Https(string value) { Uri u; if(!Uri.TryCreate(value,UriKind.Absolute,out u)||u.Scheme!="https"||!string.IsNullOrEmpty(u.UserInfo)) throw new InvalidDataException("请输入有效的 HTTPS 地址。"); return u; }
  public static byte[] Get(string url,int limit,int timeout) {
   var request=(HttpWebRequest)WebRequest.Create(Https(url)); request.Timeout=timeout; request.ReadWriteTimeout=timeout;
   request.UserAgent="RxToolbox/1.0.0"; request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
   request.CachePolicy=new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
   using(var response=(HttpWebResponse)request.GetResponse()) {
    Https(response.ResponseUri.AbsoluteUri);
    if(response.ContentLength>limit) throw new InvalidDataException("响应超过大小限制。");
    using(var source=response.GetResponseStream()) using(var dest=new MemoryStream()) { var buffer=new byte[8192]; int n; while((n=source.Read(buffer,0,buffer.Length))>0) { if(dest.Length+n>limit) throw new InvalidDataException("响应超过大小限制。"); dest.Write(buffer,0,n); } return dest.ToArray(); }
   }
  }
  public static string Text(string url) { return Encoding.UTF8.GetString(Get(url,2*1024*1024,6000)); }
 }
 internal sealed class RoundService : IDisposable {
  readonly Settings settings; readonly SemaphoreSlim gate=new SemaphoreSlim(3); bool busy,disposed; DateTime lastA2s=DateTime.MinValue;
  public readonly List<ServerEntry> Servers=new List<ServerEntry>(); public string Error; public DateTime LastSuccess; public event Action Changed;
  public RoundService(Settings s) {settings=s;
   string[] hosts={"202.189.21.228","202.189.23.252","43.249.193.77"}; int[][] ports={new[]{27076,27265,27852,31284},new[]{27045,27235,27265},new[]{27076,27265,27852}};
   string[][] names={new[]{"娘娘测试服","白菜测试服","猫娘窝测试服","兑换服"},new[]{"④服","⑤服","⑥服"},new[]{"①服","②服","③服"}};
   for(int h=0;h<3;h++) for(int i=0;i<ports[h].Length;i++) Servers.Add(new ServerEntry{Host=hosts[h],Port=ports[h][i],Name=names[h][i]});
  }
  public bool Busy {get {return busy;} }
  public async void Refresh(bool force) {
   if(busy||disposed)return; busy=true;
   try {
    var apiTask=Task.Run(delegate { string u=settings.ApiUrl; return Json.Read<ApiEnvelope>(HttpData.Text(u+(u.Contains("?")?"&":"?")+"_="+DateTime.UtcNow.Ticks)); });
    Task infoTask=Task.FromResult(0);
    if(force||(DateTime.UtcNow-lastA2s).TotalSeconds>=30) {lastA2s=DateTime.UtcNow; infoTask=Task.WhenAll(Servers.ToArray().Select(Query));}
    try {
     var env=await apiTask;
     if(env==null||env.servers==null||env.server_time<=0) throw new InvalidDataException("回合接口返回格式无效");
     int accepted=0;
     foreach(var row in env.servers.Take(128)) { IPAddress ip; if(row==null||!IPAddress.TryParse(row.ip,out ip)||row.port<1||row.port>65535||row.updated_at<=0||row.round_time_left< -1||row.round_time_left>86400) continue;
      var server=Servers.FirstOrDefault(x=>x.Host==row.ip&&x.Port==row.port); if(server==null) {server=new ServerEntry{Host=row.ip,Port=row.port};Servers.Add(server);}
      server.Name=row.server_name??server.Name; server.Round=RoundSnapshot.Create(row,env.server_time,DateTime.UtcNow); accepted++;
     }
     if(accepted==0) throw new InvalidDataException("回合接口没有有效服务器数据");
     Error=null; LastSuccess=DateTime.UtcNow;
    } catch(Exception ex) {Error=ex is WebException?"回合接口连接失败":ex.Message;}
    if(!disposed&&Changed!=null) Changed();
    await infoTask;
   } catch(Exception ex) { Error=ex.Message; Log.Write(ex); }
   finally {busy=false; if(!disposed&&Changed!=null)Changed();}
  }
  async Task Query(ServerEntry s) {
   await gate.WaitAsync();
   try { if(disposed)return; var info=await Task.Run(()=>A2s.QueryInfo(s.Host,s.Port)); if(!disposed) {s.Info=info;s.InfoAt=DateTime.UtcNow;s.InfoError=null;} }
   catch(Exception) {if(!disposed)s.InfoError="A2S 无响应";}
   finally {gate.Release();}
  }
  public ServerEntry Selected {get {return Servers.FirstOrDefault(x=>x.Key==settings.SelectedServer);} }
  public ServerEntry TimerSelected {get {return string.IsNullOrEmpty(settings.TimerServer)?Selected:Servers.FirstOrDefault(x=>x.Key==settings.TimerServer)??Selected;} }
  public void Dispose() {disposed=true;}
 }
 public sealed class A2sInfo { public string Name,Map,Game; public int Players,MaxPlayers,Bots,Latency; }
 public static class A2s {
  public static A2sInfo QueryInfo(string host,int port) {
   var watch=Stopwatch.StartNew(); var body=Exchange(host,port,new byte[]{255,255,255,255,0x54}.Concat(Encoding.ASCII.GetBytes("Source Engine Query\0")).ToArray());
   var info=ParseInfo(body); info.Latency=(int)watch.ElapsedMilliseconds; return info;
  }
  public static Dictionary<string,string> QueryRules(string host,int port) {
   var b=Exchange(host,port,new byte[]{255,255,255,255,0x56,255,255,255,255});
   using(var r=new BinaryReader(new MemoryStream(b),Encoding.UTF8)) {
    Header(r,0x45); int count=r.ReadUInt16(); if(count>4096)throw new InvalidDataException("规则数过多"); var d=new Dictionary<string,string>();
    for(int i=0;i<count;i++) d[Z(r)]=Z(r); return d;
   }
  }
  static byte[] Exchange(string host,int port,byte[] query) {
   using(var udp=new UdpClient()) {
    udp.Client.ReceiveTimeout=1800;udp.Client.SendTimeout=1800;udp.Connect(host,port); udp.Send(query,query.Length);
    for(int challenge=0;challenge<3;challenge++) {
     var data=Receive(udp);
     if(data.Length>=9&&BitConverter.ToInt32(data,0)==-1&&data[4]==0x41) {
      byte[] q;
      if(query[4]==0x54){q=new byte[query.Length+4];Buffer.BlockCopy(query,0,q,0,query.Length);Buffer.BlockCopy(data,5,q,query.Length,4);}
      else {q=(byte[])query.Clone();Buffer.BlockCopy(data,5,q,5,4);}
      udp.Send(q,q.Length);continue;
     }
     return data;
    }
    throw new InvalidDataException("A2S challenge 次数超限");
   }
  }
  static byte[] Receive(UdpClient udp) {
   IPEndPoint ep=null; var first=udp.Receive(ref ep); if(first.Length<5)throw new InvalidDataException("A2S 数据不完整");
   if(BitConverter.ToInt32(first,0)!=-2)return first;
   if(first.Length<12)throw new InvalidDataException("A2S 分片头不完整");
   uint id=BitConverter.ToUInt32(first,4); if((id&0x80000000)!=0) throw new InvalidDataException("服务器返回压缩 A2S 分片，当前版本不支持");
   int count=first[8]; if(count<1||count>64)throw new InvalidDataException("A2S 分片数无效");
   byte[][] parts=new byte[count][]; var deadline=Stopwatch.StartNew(); byte[] b=first;int got=0,total=0,received=0;
   while(got<count) {
    if(++received>128||deadline.ElapsedMilliseconds>3500) throw new TimeoutException();
    if(b.Length<12||BitConverter.ToInt32(b,0)!=-2||BitConverter.ToUInt32(b,4)!=id||b[8]!=count||b[9]>=count)throw new InvalidDataException("A2S 分片不匹配");
    int n=b[9];if(parts[n]==null){parts[n]=new byte[b.Length-12];Buffer.BlockCopy(b,12,parts[n],0,parts[n].Length);total+=parts[n].Length;got++;if(total>256000)throw new InvalidDataException("A2S 分片过大");}
    if(got<count)b=udp.Receive(ref ep);
   }
   using(var m=new MemoryStream()){foreach(var part in parts)m.Write(part,0,part.Length);return m.ToArray();}
  }
  public static A2sInfo ParseInfo(byte[] b) {
   using(var r=new BinaryReader(new MemoryStream(b),Encoding.UTF8)) {
    Header(r,0x49);r.ReadByte();var i=new A2sInfo();i.Name=Z(r);i.Map=Z(r);Z(r);i.Game=Z(r);r.ReadUInt16();i.Players=r.ReadByte();i.MaxPlayers=r.ReadByte();i.Bots=r.ReadByte();return i;
   }
  }
  static void Header(BinaryReader r,int type){if(r.ReadInt32()!=-1||r.ReadByte()!=type)throw new InvalidDataException("A2S 响应类型无效");}
  static string Z(BinaryReader r){using(var m=new MemoryStream()){for(int i=0;i<8192;i++){byte c=r.ReadByte();if(c==0)return Encoding.UTF8.GetString(m.ToArray());m.WriteByte(c);}throw new InvalidDataException("A2S 字符串过长");}}
 }
}
