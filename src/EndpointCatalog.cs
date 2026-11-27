using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace RxToolbox {
 // Embedded keys only obstruct static string extraction; they are not client-side secrets.
 internal static class EndpointCatalog {
  internal sealed class Catalog { public string Api=null, RelayApi=null; public List<ServerEntry> Servers=null; }
  static readonly Catalog data=Read();
  internal static string Api {get{return data.Api;}}
  internal static string RelayApi {get{return data.RelayApi;}}
  internal static IEnumerable<ServerEntry> Servers {get{return data.Servers.Select(s=>new ServerEntry{Host=s.Host,Port=s.Port,Name=s.Name});}}
  static Catalog Read(){
   try {
    var key=Convert.FromBase64String(EndpointPayload.PartA);var mask=Convert.FromBase64String(EndpointPayload.PartB);
    for(int i=0;i<key.Length;i++)key[i]^=mask[i];
    using(var aes=Aes.Create()){
     aes.Key=key;aes.IV=Convert.FromBase64String(EndpointPayload.IV);
     var bytes=Convert.FromBase64String(EndpointPayload.Data);
     using(var decrypt=aes.CreateDecryptor()){
      var result=Json.Read<Catalog>(Encoding.UTF8.GetString(decrypt.TransformFinalBlock(bytes,0,bytes.Length)));
      HttpData.Https(result.Api);
      if(!string.IsNullOrEmpty(result.RelayApi))HttpData.RelayUri(result.RelayApi);
      if(result.Servers==null||result.Servers.Count==0)throw new InvalidDataException();
      foreach(var server in result.Servers)SteamConnect.BuildUri(server.Host,server.Port);
      return result;
     }
    }
   }catch{throw new InvalidDataException("内置连接配置无效，请重新下载程序。");}
  }
 }
 internal static class Privacy {
  static readonly Regex urls=new Regex(@"https?://[^\s\""<>]+",RegexOptions.IgnoreCase);
  static readonly Regex ipv4=new Regex(@"(?<![\w.])(?:\d{1,3}\.){3}\d{1,3}(?::\d{1,5})?(?![\w.])");
  static readonly Regex ipv6=new Regex(@"(?<![\w:])\[?(?:[0-9a-fA-F]{0,4}:){2,7}[0-9a-fA-F]{0,4}\]?(?::\d{1,5})?(?![\w:])");
  internal static string Redact(string value){if(string.IsNullOrEmpty(value))return value;return ipv6.Replace(ipv4.Replace(urls.Replace(value,"[地址已隐藏]"),"[地址已隐藏]"),"[地址已隐藏]");}
  internal static string ServerKey(string host,int port){using(var hash=SHA256.Create())return "server-"+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(host+":"+port))).Replace("-","").ToLowerInvariant();}
 }
}
