using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace RxToolbox {
 internal static class SteamConnect {
  internal static string BuildUri(string host,int port){
   IPAddress address;if(!IPAddress.TryParse(host,out address)||port<1||port>65535)throw new ArgumentException("服务器地址无效");
   string endpoint=address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6?"["+address+"]":address.ToString();
   return "steam://connect/"+endpoint+":"+port;
  }
  internal static void Open(string uri){Process.Start(new ProcessStartInfo(uri){UseShellExecute=true});}
 }
 internal sealed class AutoJoinService : IDisposable {
  readonly Func<string,int,A2sInfo> query;readonly Action<string> launch;readonly Func<DateTime> now;
  int generation;bool busy,disposed;DateTime nextQuery;string host,name;int port;
  public bool Running {get;private set;}public string Status {get;private set;}public string ServerKey {get;private set;}
  public event Action Changed;
  public AutoJoinService():this(A2s.QueryInfo,SteamConnect.Open,()=>DateTime.UtcNow){}
  internal AutoJoinService(Func<string,int,A2sInfo> queryServer,Action<string> open,Func<DateTime> clock){query=queryServer;launch=open;now=clock;Status="";}
  void Notify(){if(!disposed&&Changed!=null)Changed();}
  public void Start(ServerEntry server){
   if(disposed)throw new ObjectDisposedException("AutoJoinService");if(server==null)throw new ArgumentException("请先选择服务器");
   SteamConnect.BuildUri(server.Host,server.Port);generation++;host=server.Host;port=server.Port;name=server.ShortName;ServerKey=server.Key;nextQuery=DateTime.MinValue;Running=true;Status="等待空位 · "+name;Notify();
  }
  public void Stop(){generation++;if(!Running)return;Running=false;Status="已取消自动进服 · "+name;Notify();}
  public async void Tick(){await PollAsync();}
  internal async Task PollAsync(){
   if(disposed||!Running||busy||now()<nextQuery)return;
   int ticket=generation;string target=host;int targetPort=port;bool sending=false;busy=true;nextQuery=now().AddSeconds(5);
   try{
    var info=await Task.Run(()=>query(target,targetPort));
    if(disposed||ticket!=generation||!Running)return;
    if(info==null||info.MaxPlayers<=0||info.Players<0)throw new InvalidDataException("人数响应无效");
    if(info.Players>=info.MaxPlayers){Status="等待空位 · "+name+" · "+info.Players+"/"+info.MaxPlayers;return;}
    Running=false;sending=true;Status="正在发起进服 · "+name;
    launch(SteamConnect.BuildUri(target,targetPort));Status="已向 Steam 发送进服请求 · "+name;
   }catch(Exception ex){
    if(disposed||ticket!=generation)return;
    if(sending){Running=false;Status="无法发起进服："+ex.Message;}
    else Status="人数查询失败，将重试 · "+name;
   }finally{busy=false;if(ticket==generation)Notify();}
  }
  public void Dispose(){disposed=true;generation++;Running=false;}
 }
}
