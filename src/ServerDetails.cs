using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace RxToolbox {
 internal sealed class ServerDetailsSnapshot {
  internal A2sInfo Info;internal A2sPlayer[] Players;
  internal string InfoError,PlayersError;internal DateTime AttemptedAt,InfoAt,PlayersAt;
 }
 internal sealed class ServerDetailsService : IDisposable {
  internal const int RefreshSeconds=30;
  readonly Func<string,int,A2sInfo> queryInfo;readonly Func<string,int,A2sPlayer[]> queryPlayers;readonly Func<DateTime> now;
  readonly Dictionary<string,ServerDetailsSnapshot> snapshots=new Dictionary<string,ServerDetailsSnapshot>();volatile bool disposed;
  internal ServerEntry Selected{get;private set;}internal bool Busy{get;private set;}
  internal event Action Changed;
  internal ServerDetailsSnapshot Snapshot {get{return Selected==null?null:snapshots[Selected.Key];}}
  internal int NextRefreshSeconds {get{var snapshot=Snapshot;return snapshot==null||snapshot.AttemptedAt==DateTime.MinValue?0:Math.Max(0,(int)Math.Ceiling((snapshot.AttemptedAt.AddSeconds(RefreshSeconds)-now()).TotalSeconds));}}
  internal ServerDetailsService():this(A2s.QueryInfo,A2s.QueryPlayers,()=>DateTime.UtcNow){}
  internal ServerDetailsService(Func<string,int,A2sInfo> info,Func<string,int,A2sPlayer[]> players,Func<DateTime> clock){queryInfo=info;queryPlayers=players;now=clock;}
  internal static ServerEntry ParseEndpoint(string text){
   Uri endpoint;IPAddress ip;
   if(string.IsNullOrWhiteSpace(text)||!Uri.TryCreate("udp://"+text.Trim(),UriKind.Absolute,out endpoint)||endpoint.Scheme!="udp"||!string.IsNullOrEmpty(endpoint.UserInfo)||!string.IsNullOrEmpty(endpoint.Query)||!string.IsNullOrEmpty(endpoint.Fragment)||(endpoint.AbsolutePath!=""&&endpoint.AbsolutePath!="/")||!IPAddress.TryParse(endpoint.Host.Trim('[',']'),out ip)||endpoint.Port<1||endpoint.Port>65535)throw new ArgumentException("请输入 IP:端口，例如 127.0.0.1:27015，IPv6 请使用 [地址]:端口。");
   return new ServerEntry{Host=ip.ToString(),Port=endpoint.Port,Name="自定义服务器"};
  }
  internal void Select(ServerEntry server){
   if(disposed)return;if(server==null)throw new ArgumentNullException("server");SteamConnect.BuildUri(server.Host,server.Port);Selected=server;
   if(!snapshots.ContainsKey(server.Key))snapshots.Add(server.Key,new ServerDetailsSnapshot());
  }
  internal async Task PollAsync(){
   if(disposed||Busy||Selected==null||NextRefreshSeconds>0)return;
   Busy=true;var server=Selected;var snapshot=Snapshot;snapshot.AttemptedAt=now();Notify();
   try{
    var result=await Task.Run(()=>{
     var data=new ServerDetailsSnapshot();
     try{data.Info=queryInfo(server.Host,server.Port);if(data.Info==null)throw new InvalidDataException();data.InfoAt=now();}catch(Exception){data.InfoError="基本信息查询失败，30 秒后重试";}
     if(disposed)return data;
     try{data.Players=queryPlayers(server.Host,server.Port);if(data.Players==null)throw new InvalidDataException();data.PlayersAt=now();}catch(Exception){data.PlayersError="玩家列表查询失败或未开放，30 秒后重试";}
     return data;
    });
    if(disposed)return;
    if(result.Info!=null){snapshot.Info=result.Info;snapshot.InfoAt=result.InfoAt;}
    if(result.Players!=null){snapshot.Players=result.Players;snapshot.PlayersAt=result.PlayersAt;}
    snapshot.InfoError=result.InfoError;snapshot.PlayersError=result.PlayersError;
   }finally{Busy=false;Notify();}
  }
  void Notify(){if(!disposed&&Changed!=null)Changed();}
  public void Dispose(){disposed=true;}
 }
}
