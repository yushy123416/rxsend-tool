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
  public string Key {get {return Privacy.ServerKey(Host,Port);} }
  public string ShortName { get { var n=string.IsNullOrWhiteSpace(Name)?"未命名服务器":Privacy.Redact(Name); int bar=n.IndexOf('|'); if(bar>=0)n=n.Substring(0,bar); return n.Replace("[CN]Neo RXBreach ",""); } }
  public override string ToString() { return ShortName; }
 }
 public static class HttpData {
  public static Uri Https(string value) { Uri u; if(!Uri.TryCreate(value,UriKind.Absolute,out u)||u.Scheme!="https"||!string.IsNullOrEmpty(u.UserInfo)) throw new InvalidDataException("请输入有效的 HTTPS 地址。"); return u; }
  internal static Uri RelayUri(string value){
   Uri url;
   if(!Uri.TryCreate(value,UriKind.Absolute,out url)||(url.Scheme!=Uri.UriSchemeHttp&&url.Scheme!=Uri.UriSchemeHttps)||!string.IsNullOrEmpty(url.UserInfo)||!string.IsNullOrEmpty(url.Fragment))
    throw new InvalidDataException("请输入有效的 HTTP 或 HTTPS 备用地址。");
   return url;
  }
  public static byte[] Get(string url,int limit,int timeout) {
   return Get(Https(url),limit,timeout);
  }
  static byte[] Get(Uri url,int limit,int timeout){
   var request=(HttpWebRequest)WebRequest.Create(url); request.Timeout=timeout; request.ReadWriteTimeout=timeout;
   request.UserAgent="RxToolbox/1.0.0"; request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
   request.CachePolicy=new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
   using(var response=(HttpWebResponse)request.GetResponse()) {
    if(url.Scheme==Uri.UriSchemeHttps)Https(response.ResponseUri.AbsoluteUri);
    else RelayUri(response.ResponseUri.AbsoluteUri);
    if(response.ContentLength>limit) throw new InvalidDataException("响应超过大小限制。");
    using(var source=response.GetResponseStream()) using(var dest=new MemoryStream()) { var buffer=new byte[8192]; int n; while((n=source.Read(buffer,0,buffer.Length))>0) { if(dest.Length+n>limit) throw new InvalidDataException("响应超过大小限制。"); dest.Write(buffer,0,n); } return dest.ToArray(); }
   }
  }
  public static string Text(string url) { return Encoding.UTF8.GetString(Get(url,2*1024*1024,6000)); }
  internal static string RelayText(string url){return Encoding.UTF8.GetString(Get(RelayUri(url),2*1024*1024,6000));}
 }
 internal sealed class RoundService : IDisposable {
  readonly Settings settings; readonly SemaphoreSlim gate=new SemaphoreSlim(3); bool busy,disposed; DateTime lastA2s=DateTime.MinValue;
  readonly RoundFeedSource feedSource=new RoundFeedSource((url,viaRelay)=>viaRelay?HttpData.RelayText(url):HttpData.Text(url));
  public readonly List<ServerEntry> Servers=new List<ServerEntry>(); public string Error; public DateTime LastSuccess; public event Action Changed;
  public RoundService(Settings s) {settings=s;
   Servers.AddRange(EndpointCatalog.Servers);
   settings.SelectedServer=MigrateKey(settings.SelectedServer,true);
   settings.TimerServer=MigrateKey(settings.TimerServer,false);
  }
  string MigrateKey(string value,bool primary){
   if(string.IsNullOrEmpty(value))return primary?Servers[0].Key:"";
   var match=Servers.FirstOrDefault(x=>x.Key==value||x.Host+":"+x.Port==value);
   return match!=null?match.Key:value.StartsWith("server-",StringComparison.Ordinal)?value:primary?Servers[0].Key:"";
  }
  public bool Busy {get {return busy;} }
  RoundFeed ReadRoundFeed(){
   string primary=null,relay=null;
   try{primary=settings.ApiUrl;}catch(InvalidDataException ex){Log.Write(ex);}
   try{relay=settings.RelayApiUrl;}catch(InvalidDataException ex){Log.Write(ex);}
   return feedSource.Read(primary,relay,DateTime.UtcNow);
  }
  public async void Refresh(bool force) {
   if(busy||disposed)return; busy=true;
   try {
    var apiTask=Task.Run(()=>ReadRoundFeed());
    Task infoTask=Task.FromResult(0);
    if(force||(DateTime.UtcNow-lastA2s).TotalSeconds>=30) {lastA2s=DateTime.UtcNow; infoTask=Task.WhenAll(Servers.ToArray().Select(Query));}
    try {
     var feed=await apiTask;
     foreach(var row in feed.Servers) {
      var server=Servers.FirstOrDefault(x=>x.Host==row.ip&&x.Port==row.port); if(server==null) {server=new ServerEntry{Host=row.ip,Port=row.port};Servers.Add(server);}
      server.Name=Privacy.Redact(row.server_name)??server.Name; server.Round=RoundSnapshot.Create(row,feed.ServerTime,DateTime.UtcNow);
     }
     Error=null; LastSuccess=DateTime.UtcNow;
    } catch(Exception ex) {Error=ex is WebException||ex is IOException?"回合接口连接失败":"回合接口数据无效，请稍后重试";}
    if(!disposed&&Changed!=null) Changed();
    await infoTask;
   } catch(Exception ex) { Error="服务器信息刷新失败，请稍后重试"; Log.Write(ex); }
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
 public sealed class A2sInfo { public string Name,Map,Game,Folder,Version,System; public int Players,MaxPlayers,Bots,Latency,AppId;public bool? Password,Vac; }
 public sealed class A2sPlayer {public byte Index;public string Name;public int Score;public float DurationSeconds;}
 public static class A2s {
  public static A2sPlayer[] QueryPlayers(string host,int port){return ParsePlayers(Exchange(host,port,new byte[]{255,255,255,255,0x55,255,255,255,255}));}
  public static A2sPlayer[] ParsePlayers(byte[] bytes){
   using(var reader=new BinaryReader(new MemoryStream(bytes),Encoding.UTF8)){
    Header(reader,0x44);int count=reader.ReadByte();var players=new A2sPlayer[count];
    for(int i=0;i<count;i++){
     var player=new A2sPlayer{Index=reader.ReadByte(),Name=Z(reader),Score=reader.ReadInt32(),DurationSeconds=reader.ReadSingle()};
     if(float.IsNaN(player.DurationSeconds)||float.IsInfinity(player.DurationSeconds)||player.DurationSeconds<0||player.DurationSeconds>int.MaxValue)throw new InvalidDataException("玩家在线时长无效");
     players[i]=player;
    }
    return players;
   }
  }
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
   IPAddress ip;using(var udp=new UdpClient(IPAddress.TryParse(host,out ip)?ip.AddressFamily:AddressFamily.InterNetwork)) {
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
    Header(r,0x49);r.ReadByte();var i=new A2sInfo();i.Name=Z(r);i.Map=Z(r);i.Folder=Z(r);i.Game=Z(r);i.AppId=r.ReadUInt16();i.Players=r.ReadByte();i.MaxPlayers=r.ReadByte();i.Bots=r.ReadByte();
    if(r.BaseStream.Position<r.BaseStream.Length){
     r.ReadByte();char environment=(char)r.ReadByte();i.System=environment=='l'?"Linux":environment=='w'?"Windows":environment=='m'||environment=='o'?"macOS":environment.ToString();
     i.Password=r.ReadByte()!=0;i.Vac=r.ReadByte()!=0;i.Version=Z(r);
     if(r.BaseStream.Position<r.BaseStream.Length){int flags=r.ReadByte();if((flags&0x80)!=0)r.ReadUInt16();if((flags&0x10)!=0)r.ReadUInt64();if((flags&0x40)!=0){r.ReadUInt16();Z(r);}if((flags&0x20)!=0)Z(r);if((flags&0x01)!=0)i.AppId=(int)(r.ReadUInt64()&0xFFFFFF);}
    }
    return i;
   }
  }
  static void Header(BinaryReader r,int type){if(r.ReadInt32()!=-1||r.ReadByte()!=type)throw new InvalidDataException("A2S 响应类型无效");}
  static string Z(BinaryReader r){using(var m=new MemoryStream()){for(int i=0;i<8192;i++){byte c=r.ReadByte();if(c==0)return Encoding.UTF8.GetString(m.ToArray());m.WriteByte(c);}throw new InvalidDataException("A2S 字符串过长");}}
 }
}
