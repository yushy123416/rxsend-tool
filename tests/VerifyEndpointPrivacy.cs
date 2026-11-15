using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;

internal static class VerifyEndpointPrivacy {
 static int Main(string[] args){
  try{
   string path=Path.GetFullPath(args[0]);var assembly=Assembly.LoadFile(path);
   var catalog=assembly.GetType("RxToolbox.EndpointCatalog",true);
   var flags=BindingFlags.Static|BindingFlags.NonPublic;
   string api=(string)catalog.GetProperty("Api",flags).GetValue(null,null);
   byte[] bytes=File.ReadAllBytes(path);
   RejectLiteral(bytes,api);RejectLiteral(bytes,new Uri(api).Host);
   int count=0;
   foreach(object server in (IEnumerable)catalog.GetProperty("Servers",flags).GetValue(null,null)){
    RejectLiteral(bytes,(string)server.GetType().GetField("Host").GetValue(server));count++;
   }
   if(count==0)throw new InvalidDataException();
   Console.WriteLine("PASS release artifact has a working encrypted catalog and no literal configured endpoints");return 0;
  }catch{Console.Error.WriteLine("Endpoint privacy verification failed. Rebuild and prepare a protected payload for the new version before publishing.");return 1;}
 }
 static void RejectLiteral(byte[] bytes,string value){
  foreach(var encoding in new[]{Encoding.UTF8,Encoding.Unicode}){
   byte[] needle=encoding.GetBytes(value);
   for(int i=0;i<=bytes.Length-needle.Length;i++){
    int j=0;while(j<needle.Length&&bytes[i+j]==needle[j])j++;
    if(j==needle.Length)throw new InvalidDataException();
   }
  }
 }
}
