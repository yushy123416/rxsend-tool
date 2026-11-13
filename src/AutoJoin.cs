using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace RxToolbox {
 internal static class SteamConnect {
  internal static string BuildUri(string host,int port){
   IPAddress address;if(!IPAddress.TryParse(host,out address)||port<1||port>65535)throw new ArgumentException("服务器地址无效");
   string endpoint=address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6?"["+address+"]":address.ToString();
   return "steam://connect/"+endpoint+":"+port;
  }
  internal static void Open(string uri){
   Process.Start(BuildStartInfo(uri));
  }
  internal static ProcessStartInfo BuildStartInfo(string uri){
   const string prefix="steam://connect/";
   if(uri==null||!uri.StartsWith(prefix,StringComparison.Ordinal))throw new ArgumentException("服务器地址无效");
   Uri parsed; if(!Uri.TryCreate("http://"+uri.Substring(prefix.Length),UriKind.Absolute,out parsed)||BuildUri(parsed.Host.Trim('[',']'),parsed.Port)!=uri)throw new ArgumentException("服务器地址无效");
   string target=uri.Substring(prefix.Length);
   return new ProcessStartInfo("steam://rungame/4000/76561202255233023/"+Uri.EscapeDataString("+connect "+target)){UseShellExecute=true};
  }
 }
 internal sealed class AutoJoinService : IDisposable {
  readonly Func<string,int,A2sInfo> query;readonly Action<string> launch;readonly Func<DateTime> now;
  readonly object claim=new object();int generation;bool busy,disposed;DateTime nextQuery;string host,name;int port;ServerEntry[] targets;
  public bool Running {get;private set;}public string Status {get;private set;}public string ServerKey {get;private set;}
  public int QueryCount {get;private set;}public int FailureCount {get;private set;}public DateTime StartedAt {get;private set;}public bool Querying {get{return busy;}}public int NextQueryMs {get{return Math.Max(0,(int)(nextQuery-now()).TotalMilliseconds);}}
  public readonly Queue<string> Activity=new Queue<string>();
  void Record(string message){lock(claim){Activity.Enqueue(now().ToLocalTime().ToString("HH:mm:ss")+"  "+message);while(Activity.Count>6)Activity.Dequeue();}}
  public event Action Changed;
  public AutoJoinService():this(A2s.QueryInfo,SteamConnect.Open,()=>DateTime.UtcNow){}
  internal AutoJoinService(Func<string,int,A2sInfo> queryServer,Action<string> open,Func<DateTime> clock){query=queryServer;launch=open;now=clock;Status="";}
  void Notify(){if(!disposed&&Changed!=null)Changed();}
  public void Start(IEnumerable<ServerEntry> servers){
   var items=servers.Where(s=>s!=null).GroupBy(s=>s.Key).Select(g=>g.First()).ToArray();if(items.Length==0)throw new ArgumentException("请至少选择一个服务器");foreach(var item in items)SteamConnect.BuildUri(item.Host,item.Port);Start(items[0]);targets=items;Status="正在监听 "+items.Length+" 个服务器";Notify();
  }
  public void Start(ServerEntry server){
   if(disposed)throw new ObjectDisposedException("AutoJoinService");if(server==null)throw new ArgumentException("请先选择服务器");
   SteamConnect.BuildUri(server.Host,server.Port);QueryCount=0;FailureCount=0;StartedAt=now();Activity.Clear();Record("任务启动，等待人数响应");targets=new[]{server};generation++;host=server.Host;port=server.Port;name=server.ShortName;ServerKey=server.Key;nextQuery=DateTime.MinValue;Running=true;Status="等待空位 · "+name;Notify();
  }
  public void Stop(){generation++;if(!Running)return;Running=false;Status="已取消自动进服 · "+name;Notify();}
  public async void Tick(){await PollAsync();}
  internal async Task PollAsync(){
   if(disposed||!Running||busy||now()<nextQuery)return;
   int ticket=generation;busy=true;nextQuery=now().AddSeconds(1);
   try{await Task.WhenAll(targets.Select(server=>PollServer(server,ticket)).ToArray());}
   finally{busy=false;if(ticket==generation)Notify();}
  }
  async Task PollServer(ServerEntry server,int ticket){
   var watch=Stopwatch.StartNew();
   try{
    var info=await Task.Run(()=>query(server.Host,server.Port));
    if(disposed||ticket!=generation||!Running)return;
    if(info==null||info.MaxPlayers<=0||info.Players<0)throw new InvalidDataException("人数响应无效");
    QueryCount++;Record(server.ShortName+"  "+info.Players+"/"+info.MaxPlayers+"  ·  "+watch.ElapsedMilliseconds+" ms");server.Info=info;server.InfoError=null;
    Status="等待空位 · "+server.ShortName+" · "+info.Players+"/"+info.MaxPlayers+" · 查询 "+watch.ElapsedMilliseconds+" ms";
    if(info.Players>=info.MaxPlayers)return;
    lock(claim){if(disposed||ticket!=generation||!Running)return;Running=false;generation++;ServerKey=server.Key;}
    try{launch(SteamConnect.BuildUri(server.Host,server.Port));Status="已发起连接 · "+server.ShortName+" · 其他目标已停止";}
    catch(Exception ex){Status="无法发起进服："+ex.Message;}
    Notify();
   }catch(Exception){if(!disposed&&ticket==generation&&Running){QueryCount++;FailureCount++;Record(server.ShortName+"  查询超时/失败，等待重试");server.InfoError="查询失败";Status="人数查询失败，将重试 · "+server.ShortName;}}
  }
  public void Dispose(){disposed=true;generation++;Running=false;}
 }
}
