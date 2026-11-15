using System;
using System.IO;
using System.Text;

namespace RxToolbox {
 internal sealed class BroadcastSyncResult {
  public string[] Templates;public bool Cached;
 }
 internal static class BroadcastTemplates {
  internal const string MirrorUrl="https://gitee.com/ysyyyy/rxsend-tool/raw/main/broadcasts/templates.json";
  internal const string GithubUrl="https://raw.githubusercontent.com/yushy123416/rxsend-tool/main/broadcasts/templates.json";
  internal static string CachePath {get{return Path.Combine(Settings.Folder,"broadcast-templates.json");}}
  internal static readonly string[] Defaults={
   "防化服持枪直接击毙，不建议普通科研持枪，MTF有权命令你丢弃。优先疏散科研，允许逗留轻收容。若刷新外部未知单位，所有基金会武装单位不能继续逗留，安保部门人员必须换装，MTF也要保护内部安全代理安全。",
   "科研持枪警告喵~❤防化服持危险品击毙喵~❤保安换装不许怯战喵~❤mtf可以逗留喵~❤",
   "912优先疏散科研，屠杀DD，鸭脖持刀警告，老兵持枪击毙，811禁止随地大小便，LPY不准进广播",
   "奉天承运红帽诏曰：可留轻收先遣科研。防化科研挟危物斩立决！特科许佩兵、守卫易服不得退怯",
   "优先疏散杂鱼科研，科研防化持危险品打飞喵♥，笨蛋保安换装去喵♥，允许逗留喵♥",
   "我说实话喵～防化持枪击毙喵～就在这么一个小设施里当mtf喵～允许逗留清收，把战专牛的不行了喵～",
   "杂鱼老登快来广播室叫亲卫队喵~保安不许下班喵必须换装加班喵~严查防化服喵 安保换装后巡逻办公喵，防化科研不准拿危险品和枪喵，拿枪的优先警告喵，不听的直接草似喵，老登你怎么来了喵？不要碰那里喵，不要喵，错了喵，饶命喵，齁齁哦哦哦哦哦哦，不要再来了喵，要似了喵！",
   "根据美国宪法第二修正案，site19的所有公民均享有合法持枪的权利。"
  };
  internal static string[] Parse(string json){
   if(json==null||json.Length>65536)throw new InvalidDataException("广播模板文件过大");
   var values=Json.Read<object>(json) as object[];
   if(values==null||values.Length>100)throw new InvalidDataException("广播模板应为最多 100 条正文的 JSON 数组");
   var templates=new string[values.Length];
   for(int i=0;i<values.Length;i++){
    var text=values[i] as string;
    if(string.IsNullOrWhiteSpace(text)||text.Length>4000||text.IndexOf('\0')>=0)throw new InvalidDataException("广播正文为空或超出长度限制");
    templates[i]=text;
   }
   return templates;
  }
  internal static string[] Load(string path){
   try{if(!File.Exists(path))return Defaults;if(new FileInfo(path).Length>262144)throw new InvalidDataException("本地模板缓存过大");return Parse(File.ReadAllText(path,new UTF8Encoding(false,true)));}
   catch(Exception ex){Log.Write(ex);return Defaults;}
  }
  internal static BroadcastSyncResult Refresh(string path,Func<string,string> fetch){
   Exception last=null;
   foreach(string url in new[]{MirrorUrl,GithubUrl}){
    string[] templates;
    try{templates=Parse(fetch(url));}catch(Exception ex){last=ex;continue;}
    bool cached=false;string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
    try{
     Directory.CreateDirectory(Path.GetDirectoryName(path));
     File.WriteAllText(temporary,Json.Write(templates),new UTF8Encoding(false));
     if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
     cached=true;
    }catch(Exception ex){Log.Write(ex);}
    finally{try{if(File.Exists(temporary))File.Delete(temporary);}catch{}}
    return new BroadcastSyncResult{Templates=templates,Cached=cached};
   }
   throw new IOException("广播模板源暂不可用",last);
  }
  internal static BroadcastSyncResult Refresh(){return Refresh(CachePath,url=>new UTF8Encoding(false,true).GetString(HttpData.Get(url,262144,8000)).TrimStart('\uFEFF'));}
 }
}