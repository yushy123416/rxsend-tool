using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace RxToolbox {
 internal sealed class ReleaseDescription {public string tag_name=null,body=null;public bool draft=false;}
 internal static class ReleaseAnnouncements {
  internal static string GitHubPage(string repo,string version){return "https://github.com/"+repo+"/releases/tag/v"+Updates.ParseVersion(version).ToString(3);}
  internal static void Load(UpdateManifest manifest,string repo,string mirror,Func<string,string> fetch){
   var sources=new List<string[]>();string version=Updates.ParseVersion(manifest.version).ToString(3),tag="v"+version;
   Uri uri;
   if(Uri.TryCreate(mirror,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&uri.Host=="gitee.com"){
    var path=uri.AbsolutePath.Trim('/').Split('/');
    if(path.Length>=3&&Regex.IsMatch(path[0]+"/"+path[1],@"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")){
     string project=path[0]+"/"+path[1];sources.Add(new[]{"https://gitee.com/api/v5/repos/"+project+"/releases/tags/"+tag,"https://gitee.com/"+project+"/releases/tag/"+tag,"Gitee"});
    }
   }
   if(!string.IsNullOrWhiteSpace(repo)&&Regex.IsMatch(repo,@"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$"))sources.Add(new[]{"https://api.github.com/repos/"+repo+"/releases/tags/"+tag,GitHubPage(repo,version),"GitHub"});
   manifest.ReleaseUrl=sources.Count==0?null:sources[0][1];
   foreach(var source in sources){try{
    var release=Json.Read<ReleaseDescription>(fetch(source[0]));
    if(release==null||release.draft||Updates.ParseVersion(release.tag_name)!=Updates.ParseVersion(manifest.version)||string.IsNullOrWhiteSpace(release.body))throw new InvalidDataException("发布说明与更新版本不匹配或正文为空");
    manifest.ReleaseNotes=release.body;manifest.ReleaseUrl=source[1];manifest.SourceLabel=source[2];return;
   }catch(Exception){}
    try{manifest.ReleaseNotes=ReadPage(fetch(source[1]));manifest.ReleaseUrl=source[1];manifest.SourceLabel=source[2];return;}catch(Exception){}
   }
   // Keep the verified update available when release services are unavailable.
   // Bundled notes are an offline fallback, never a replacement for a live release body.
   manifest.ReleaseNotes=manifest.notes;
  }
  internal static string ReadPage(string html){
   // Extract only the release's rendered Markdown container, never navigation,
   // login prompts or the remainder of the hosting site's HTML.
   var container=Regex.Match(html,@"<div\b[^>]*class\s*=\s*[""'][^""']*\bmarkdown-body\b[^""']*[""'][^>]*>",RegexOptions.IgnoreCase);
   if(!container.Success)throw new InvalidDataException("发布页未包含正文");
   int start=container.Index+container.Length,end=-1,depth=1;
   foreach(Match div in Regex.Matches(html.Substring(start),@"</?div\b[^>]*>",RegexOptions.IgnoreCase)){depth+=div.Value.StartsWith("</")?-1:1;if(depth==0){end=start+div.Index;break;}}
   if(end<0)throw new InvalidDataException("发布页正文不完整");
   string body=html.Substring(start,end-start);
   body=Regex.Replace(body,@"<(script|style)\b[^>]*>.*?</\1>","",RegexOptions.IgnoreCase|RegexOptions.Singleline);
   body=Regex.Replace(body,@"<a\b[^>]*href=[""'](https://[^""']+)[""'][^>]*>(.*?)</a>",m=>"["+Regex.Replace(m.Groups[2].Value,@"<[^>]*>","")+"]("+m.Groups[1].Value+")",RegexOptions.Singleline|RegexOptions.IgnoreCase);
   body=Regex.Replace(body,@"<h[1-6]\b[^>]*>","\n### ",RegexOptions.IgnoreCase);
   body=Regex.Replace(body,@"<li\b[^>]*>","\n• ",RegexOptions.IgnoreCase);
   body=Regex.Replace(body,@"</(?:p|h[1-6]|li|ul|ol|pre)>|<br\s*/?>","\n",RegexOptions.IgnoreCase);
   body=WebUtility.HtmlDecode(Regex.Replace(body,@"<[^>]*>","")).Trim();
   if(string.IsNullOrWhiteSpace(body))throw new InvalidDataException("发布说明为空");return body;
  }
 }
}
