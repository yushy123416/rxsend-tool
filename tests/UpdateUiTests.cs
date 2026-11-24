using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace RxToolbox {
 internal static class UpdateUiTests {
  static int count;static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  static void Check(bool value,string message){if(!value)throw new Exception(message);count++;}
  static IEnumerable<Control> All(Control parent){foreach(Control child in parent.Controls){yield return child;foreach(var nested in All(child))yield return nested;}}
  static void Capture(Form form,string name){
   using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save("build/ui-review/"+name+".png",ImageFormat.Png);}
  }
  static UpdateManifest Release(string notes){return new UpdateManifest{version="1.1.0",url="https://example.com/app.exe",sha256=new string('a',64),notes=notes,SourceLabel="Gitee",ReleaseUrl=ReleaseAnnouncements.GitHubPage(Updates.DefaultRepository,"1.1.0")};}
  static void Sources(){
   var requests=new List<string>();var release=Release("本次更新公告");
   var result=Updates.CheckCore(Updates.DefaultRepository,Updates.DefaultMirrorUrl,url=>{requests.Add(url);return Json.Write(release);});
   Check(requests.Count==1&&requests[0]==Updates.DefaultMirrorUrl&&result.notes==release.notes&&result.SourceLabel=="Gitee","Gitee announcement must be preferred");
   requests.Clear();result=Updates.CheckCore(Updates.DefaultRepository,Updates.DefaultMirrorUrl,url=>{requests.Add(url);if(requests.Count==1)throw new IOException("offline");return Json.Write(release);});
   Check(requests.Count==2&&requests[1].Contains("github.com/")&&result.SourceLabel=="GitHub"&&result.notes==release.notes,"Offline Gitee must fall back to GitHub announcement");
   int calls=0;result=Updates.CheckCore(Updates.DefaultRepository,Updates.DefaultMirrorUrl,url=>++calls==1?"<html>unavailable</html>":Json.Write(release));Check(calls==2&&result.SourceLabel=="GitHub","Invalid Gitee content must fall back");
   calls=0;result=Updates.CheckCore(Updates.DefaultRepository,Updates.DefaultMirrorUrl,url=>Json.Write(++calls==1?Release(" "):release));Check(calls==2&&result.notes==release.notes&&result.SourceLabel=="GitHub","Missing Gitee announcement must fall back");
   calls=0;result=Updates.CheckCore(Updates.DefaultRepository,Updates.DefaultMirrorUrl,url=>{calls++;if(calls==2)throw new IOException("offline");return Json.Write(Release(null));});Check(calls==2&&result.version==release.version&&result.notes==null,"Valid release survives both announcements being unavailable");
   calls=0;result=Updates.CheckCore(Updates.DefaultRepository,Updates.DefaultMirrorUrl,url=>{var m=++calls==1?Release(null):release;if(calls==1)m.version="2.0.0";return Json.Write(m);});Check(result.version=="2.0.0"&&result.notes==null,"Older GitHub announcement cannot downgrade newer Gitee release");
   calls=0;result=Updates.CheckCore(Updates.DefaultRepository,Updates.DefaultMirrorUrl,url=>{var m=++calls==1?Release(null):Release("other binary");if(calls==2)m.sha256=new string('b',64);return Json.Write(m);});Check(result.notes==null,"Different binary announcement must not replace primary release");
   calls=0;result=Updates.CheckCore(Updates.DefaultRepository,Updates.DefaultMirrorUrl,url=>{var m=Release(null);if(++calls==2)m.version="2.0.0";return Json.Write(m);});Check(result.version=="2.0.0","Newer fallback release must survive both sources lacking notes");
   bool failed=false;try{Updates.CheckCore(Updates.DefaultRepository,Updates.DefaultMirrorUrl,url=>{throw new IOException("offline");});}catch(IOException){failed=true;}Check(failed,"Both update sources failing must report an error");
  }
  static DialogResult Decide(MainWindow owner,UpdateManifest release,bool ready,string capture,Action<UpdateAnnouncementDialog> action){
   using(var dialog=new UpdateAnnouncementDialog(release,ready)){
    var later=All(dialog).OfType<FlatButton>().Single(b=>b.Text=="暂不更新");
    Check(dialog.AcceptButton==later&&dialog.CancelButton==later,"Enter and Esc must default to defer");
    Check(dialog.BackColor==Ui.Card&&All(dialog).OfType<Label>().Any(l=>l.Text=="有新版本 v1.1.0"),"Light dialog must show new version and number on one line");
    dialog.Shown+=delegate{dialog.BeginInvoke((Action)delegate{if(capture!=null)Capture(dialog,capture);action(dialog);});};
    return dialog.ShowDialog(owner);
   }
  }
  [STAThread]static int Main(){try{
   Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Directory.CreateDirectory("build/ui-review");Sources();ReleaseSources();
   Control.CheckForIllegalCrossThreadCalls=true;int result=0;
   using(var form=new MainWindow()){
    form.AutomaticUpdatesEnabled=false;form.AutomaticTemplateUpdatesEnabled=false;form.AutomaticServerQueriesEnabled=false;
    var context=(ToolContext)typeof(MainWindow).GetField("context",Private).GetValue(form);context.Settings.ExperimentalFullscreen=false;context.Settings.CrosshairVisible=false;context.Settings.TimerVisible=false;
    typeof(MainWindow).GetField("nextPoll",Private).SetValue(form,DateTime.MaxValue);
    form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-30000,-30000);form.ShowInTaskbar=false;
    form.Shown+=async delegate{try{await RunChecks(form);}catch(Exception ex){Console.Error.WriteLine(ex);result=1;}finally{form.Dispose();Application.ExitThread();}};
    Application.Run(form);
   }
   if(result==0)Console.WriteLine("PASS "+count+" update source, consent, reminder, retry and layout checks; screenshots: build/ui-review/update-*.png");return result;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
  static void ReleaseSources(){
   var manifest=Release("bundled fallback");var requested=new List<string>();string json=Json.Write(new ReleaseDescription{tag_name="v1.1.0",body="### Release 正文\n- 来源于发布页"});
   ReleaseAnnouncements.Load(manifest,Updates.DefaultRepository,Updates.DefaultMirrorUrl,url=>{requested.Add(url);return json;});
   Check(requested.Count==1&&requested[0].Contains("gitee.com/api/v5/")&&manifest.ReleaseNotes.Contains("Release 正文")&&manifest.ReleaseNotes!=manifest.notes,"Announcement must fetch version-specific Gitee release body instead of manifest notes");
   requested.Clear();ReleaseAnnouncements.Load(manifest,Updates.DefaultRepository,Updates.DefaultMirrorUrl,url=>{requested.Add(url);if(url.Contains("gitee.com"))throw new IOException("offline");return json;});
   Check(manifest.SourceLabel=="GitHub"&&manifest.ReleaseUrl==ReleaseAnnouncements.GitHubPage(Updates.DefaultRepository,"1.1.0")&&requested.Last().Contains("api.github.com"),"Gitee release failure must fall back to matching GitHub release");
   string page="<header>ignore navigation</header><div class=\"markdown-body\"><h2>实际公告</h2><div><p>修复 &amp; 改进</p></div><ul><li>正文</li></ul></div><footer>ignore footer</footer>";
   ReleaseAnnouncements.Load(manifest,Updates.DefaultRepository,Updates.DefaultMirrorUrl,url=>{if(url.Contains("/api/")||url.Contains("api.github.com"))throw new IOException("rate limited");return page;});
   Check(manifest.ReleaseNotes.Contains("实际公告")&&manifest.ReleaseNotes.Contains("修复 & 改进")&&!manifest.ReleaseNotes.Contains("navigation")&&!manifest.ReleaseNotes.Contains("footer"),"Public release page must survive API limits and exclude surrounding page content");
   ReleaseAnnouncements.Load(manifest,Updates.DefaultRepository,Updates.DefaultMirrorUrl,url=>url.Contains("/tags/")?Json.Write(new ReleaseDescription{tag_name="v1.0.0",body="wrong release"}):"<html>login</html>");
   Check(manifest.ReleaseNotes==manifest.notes,"Mismatched release and login pages must fall back to bundled notes");
   ReleaseAnnouncements.Load(manifest,Updates.DefaultRepository,Updates.DefaultMirrorUrl,url=>{throw new IOException("offline");});Check(manifest.ReleaseNotes==manifest.notes,"All release sources failing must keep update choice available");
  }
  static async Task RunChecks(MainWindow form){
    var module=((List<IToolModule>)typeof(MainWindow).GetField("modules",Private).GetValue(form)).OfType<SettingsModule>().Single();
    var badge=All(form).OfType<UpdateBadge>().Single();int fetched=0,decisions=0,downloaded=0,installed=0;
    var release=Release("### 更新体验，由你掌控\n\n- 新增更新公告，升级前先了解本次变化。\n- 支持暂不更新，随时从左上角重新查看。\n- 公告优先从 Gitee 获取，失败自动切换 GitHub。\n\n### 稳定性改进\n\n- 保留下载校验与失败重试，保护当前版本。");
    module.CheckUpdate=(repo,mirror)=>{fetched++;var same=Release("No changes");same.version=Updates.Current.ToString(3);return same;};
    module.ConfirmUpdate=(m,ready)=>{decisions++;return DialogResult.Cancel;};
    module.DownloadUpdate=m=>{downloaded++;return "test-payload";};module.ApplyUpdate=(file,m)=>{installed++;};
    await module.CheckForUpdates();Check(!badge.Visible&&decisions==0&&downloaded==0,"Latest version must not prompt or download");
    module.CheckUpdate=(repo,mirror)=>{fetched++;return release;};
    module.ConfirmUpdate=(m,ready)=>{decisions++;return Decide(form,m,ready,"update-announcement",d=>All(d).OfType<FlatButton>().Single(b=>b.Text=="暂不更新").PerformClick());};
    await module.CheckForUpdates();Check(badge.Visible&&decisions==1&&downloaded==0&&installed==0,"Deferring startup update must preserve reminder without downloading");
    typeof(MainWindow).GetMethod("SelectModule",Private).Invoke(form,new object[]{6});for(int i=0;i<10;i++)typeof(MainWindow).GetMethod("Animate",Private).Invoke(form,new object[]{null,EventArgs.Empty});
    Capture(form,"update-reminder");
    module.ConfirmUpdate=(m,ready)=>{decisions++;return Decide(form,m,ready,null,d=>d.Close());};
    badge.AccessibilityObject.DoDefaultAction();
    Check(decisions==2&&fetched==2&&badge.Visible&&downloaded==0,"Badge must reopen same announcement; closing must not download or re-fetch");
    string longNotes=string.Join("\n",Enumerable.Range(1,60).Select(i=>"• 第 "+i+" 条较长的更新说明，可滚动阅读全部内容。"));
    var longRelease=Release(longNotes);Decide(form,longRelease,false,"update-announcement-long",d=>{var scroll=All(d).OfType<SlimScroll>().Single();Check(scroll.Maximum>0,"Long announcements need scrolling");scroll.Value=scroll.Maximum;Capture(d,"update-announcement-scrolled");All(d).OfType<FlatButton>().Single(b=>b.Text=="暂不更新").PerformClick();});
    var detailed=Release("unused manifest notes");detailed.ReleaseNotes="<details open><summary>🐞 修复问题</summary>\n\n- 修复更新失败后的重试问题。\n- 保留当前运行版本。\n\n</details>\n\n## ✨ 新增功能\n\n- 支持查看 Release 发布说明。\n\n[查看完整发布页](https://github.com/yushy123416/rxsend-tool/releases)";
    Decide(form,detailed,false,"update-announcement-release",d=>{var section=All(d).OfType<ReleaseSection>().Single();int expanded=section.Height;section.Toggle();Check(section.Height<expanded,"Release details must collapse");section.Toggle();Check(section.Height==expanded,"Release details must expand again");Check(All(d).OfType<LinkLabel>().Any(l=>l.Text.Contains("支持查看 Release"))&&!All(d).OfType<LinkLabel>().Any(l=>l.Text.Contains("unused manifest")),"Dialog must display live release body");d.Close();});
    Decide(form,Release(null),false,"update-announcement-missing",d=>{Check(All(d).OfType<Label>().Any(l=>l.Text.Contains("暂未提供详细")),"Missing notes must be explained");d.Close();});
    module.ConfirmUpdate=(m,ready)=>DialogResult.OK;module.DownloadUpdate=m=>{downloaded++;throw new IOException("test offline");};await module.ShowUpdateAnnouncement();Check(badge.Visible&&downloaded==1&&installed==0,"Download failure must retain reminder and current version");
    module.ConfirmUpdate=(m,ready)=>DialogResult.Cancel;await module.ShowUpdateAnnouncement();Check(downloaded==1&&installed==0,"Deferring after failure must not retry downloads");
    module.DownloadUpdate=m=>{downloaded++;return "verified-test-payload";};await module.ShowUpdateAnnouncement();Check(downloaded==1,"Explicit consent is required for retry");
    module.ConfirmUpdate=(m,ready)=>Decide(form,m,ready,null,d=>All(d).OfType<FlatButton>().Single(b=>b.Text=="立即更新").PerformClick());
    module.ApplyUpdate=(file,m)=>{installed++;throw new IOException("test locked");};await module.ShowUpdateAnnouncement();Check(downloaded==2&&installed==1&&badge.Visible,"Installation failure must remain retryable");
    module.ConfirmUpdate=(m,ready)=>{Check(ready,"Verified payload must be reused");return Decide(form,m,ready,"update-announcement-ready",d=>All(d).OfType<FlatButton>().Single(b=>b.Text=="安装并重启").PerformClick());};
    module.ApplyUpdate=(file,m)=>{installed++;};await module.ShowUpdateAnnouncement();Check(downloaded==2&&installed==2,"Confirmed retry must install verified payload without downloading again");
    module.CheckUpdate=(repo,mirror)=>{throw new IOException("both sources offline");};await module.CheckForUpdates();Check(badge.Visible,"A failed recheck must not remove existing reminder");
  }
 }
}
