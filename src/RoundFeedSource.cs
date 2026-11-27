using System;
using System.IO;
using System.Linq;
using System.Net;

namespace RxToolbox {
 internal sealed class RoundFeed {
  internal long ServerTime;
  internal ApiServer[] Servers;
 }
 internal sealed class RoundFeedSource {
  readonly Func<string,bool,string> getText;
  string preferredRelay;
  DateTime retryPrimaryAt;

  internal RoundFeedSource(Func<string,bool,string> getText){this.getText=getText;}

  internal RoundFeed Read(string primary,string relay,DateTime utcNow){
   if(string.IsNullOrEmpty(relay)||string.Equals(primary,relay,StringComparison.Ordinal))relay=null;
   bool preferRelay=relay!=null&&relay==preferredRelay&&utcNow<retryPrimaryAt;
   bool viaRelay=preferRelay;
   RoundFeed result;
   try{result=Fetch(preferRelay?relay:primary,viaRelay,utcNow);}
   catch(Exception ex){
    if(relay==null||!(ex is WebException||ex is InvalidDataException||ex is IOException))throw;
    viaRelay=!preferRelay;
    result=Fetch(viaRelay?relay:primary,viaRelay,utcNow);
   }
   if(!viaRelay)preferredRelay=null;
   else if(!preferRelay){
    preferredRelay=relay;
    retryPrimaryAt=utcNow.AddMinutes(1);
   }
   return result;
  }

  RoundFeed Fetch(string endpoint,bool viaRelay,DateTime utcNow){
   var url=new UriBuilder(viaRelay?HttpData.RelayUri(endpoint):HttpData.Https(endpoint));
   string query=url.Query.TrimStart('?');
   url.Query=(query.Length==0?"":query+"&")+"_="+utcNow.Ticks;
   string text=getText(url.Uri.AbsoluteUri,viaRelay);
   ApiEnvelope env;
   try{env=Json.Read<ApiEnvelope>(text);}
   catch(ArgumentException ex){throw new InvalidDataException("回合接口返回格式无效",ex);}
   catch(InvalidOperationException ex){throw new InvalidDataException("回合接口返回格式无效",ex);}
   if(env==null||env.servers==null||env.server_time<=0)throw new InvalidDataException("回合接口返回格式无效");
   var rows=env.servers.Take(128).Where(ValidRow).ToArray();
   if(rows.Length==0)throw new InvalidDataException("回合接口没有有效服务器数据");
   return new RoundFeed{ServerTime=env.server_time,Servers=rows};
  }

  static bool ValidRow(ApiServer row){
   IPAddress ip;
   return row!=null&&IPAddress.TryParse(row.ip,out ip)&&row.port>=1&&row.port<=65535&&row.updated_at>0&&row.round_time_left>=-1&&row.round_time_left<=86400;
  }
 }
}
