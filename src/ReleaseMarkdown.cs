using System;
using System.Drawing;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace RxToolbox {
 // Native controls keep remote release Markdown inert while preserving headings,
 // lists, links and collapsible GitHub/Gitee details sections.
 internal sealed class ReleaseMarkdownPanel : Panel {
  bool arranging;
  internal ReleaseMarkdownPanel(string markdown){BackColor=Ui.Card;DoubleBuffered=true;
   string text=(markdown??"").Replace("\r","");int start=0;
   foreach(Match section in Regex.Matches(text,@"<details\b[^>]*>\s*<summary\b[^>]*>(.*?)</summary>(.*?)</details>",RegexOptions.Singleline|RegexOptions.IgnoreCase)){
    AddLines(text.Substring(start,section.Index-start));Controls.Add(new ReleaseSection(Plain(section.Groups[1].Value),section.Groups[2].Value));start=section.Index+section.Length;
   }AddLines(text.Substring(start));
  }
  internal static string Plain(string value){return WebUtility.HtmlDecode(Regex.Replace(value,@"<[^>]*>","")).Replace("**","").Replace("`","").Trim();}
  void AddLines(string text){
   foreach(string raw in text.Split('\n')){
    string line=raw.Trim();if(line.StartsWith("```"))continue;
    if(line.Length==0){Controls.Add(new Panel{Height=10,Tag="gap"});continue;}
    bool heading=Regex.IsMatch(line,@"^#{1,6}\s+")||(line.StartsWith("**")&&line.EndsWith("**"));
    line=Regex.Replace(line,@"^#{1,6}\s+","");line=Regex.Replace(line,@"^[-*]\s+","• ");
    line=Plain(line);if(line.Length==0)continue;
    var label=new LinkLabel{AutoSize=false,AutoEllipsis=false,BackColor=Ui.Card,ForeColor=Ui.Text,Font=heading?Ui.Heading:Ui.Body,TextAlign=ContentAlignment.TopLeft,LinkColor=Ui.Accent,ActiveLinkColor=Ui.Accent,VisitedLinkColor=Ui.Accent,UseMnemonic=false,Tag=heading?"heading":"text"};
    var links=Regex.Matches(line,@"\[([^\]]+)\]\((https://[^\s)]+)\)");int position=0;string display="";
    foreach(Match link in links){display+=line.Substring(position,link.Index-position);int at=display.Length;display+=link.Groups[1].Value;label.Links.Add(at,link.Groups[1].Value.Length,link.Groups[2].Value);position=link.Index+link.Length;}
    display+=line.Substring(position);label.Text=display;if(links.Count==0)label.LinkArea=new LinkArea(0,0);
    label.LinkClicked+=delegate(object sender,LinkLabelLinkClickedEventArgs e){UpdateAnnouncementDialog.OpenReleasePage((string)e.Link.LinkData);};Controls.Add(label);
   }
  }
  protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);if(arranging)return;arranging=true;try{int top=0;foreach(Control child in Controls){int width=Math.Max(100,ClientSize.Width-12),height;
    if(child is ReleaseSection){child.SetBounds(0,top,width,child.Height);child.PerformLayout();height=child.Height;}
    else{height=Equals(child.Tag,"gap")?10:TextRenderer.MeasureText(child.Text,child.Font,new Size(width,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl|TextFormatFlags.NoPrefix).Height+(Equals(child.Tag,"heading")?14:8);child.SetBounds(0,top,width,height);}
    top+=height;
   }Height=Math.Max(1,top);}finally{arranging=false;}}
 }
 internal sealed class ReleaseSection : Panel {
  readonly Label title;readonly ReleaseMarkdownPanel body;readonly string caption;bool expanded=true,arranging;
  internal ReleaseSection(string caption,string markdown){this.caption=caption;BackColor=Ui.Card;title=Ui.Label(this,"▾ "+caption,0,0,400,34,Ui.Heading);title.Cursor=Cursors.Hand;title.TabStop=true;body=new ReleaseMarkdownPanel(markdown);Controls.Add(body);title.Click+=delegate{Toggle();};title.KeyDown+=delegate(object s,KeyEventArgs e){if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter){Toggle();e.Handled=true;}};body.SizeChanged+=delegate{if(!arranging)PerformLayout();};}
  internal void Toggle(){expanded=!expanded;title.Text=(expanded?"▾ ":"▸ ")+caption;body.Visible=expanded;PerformLayout();if(Parent!=null)Parent.PerformLayout();}
  protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);if(arranging||title==null)return;arranging=true;try{title.Width=ClientSize.Width;body.SetBounds(6,38,Math.Max(100,ClientSize.Width-6),body.Height);body.PerformLayout();Height=expanded?38+body.Height+8:40;}finally{arranging=false;}}
 }
}
